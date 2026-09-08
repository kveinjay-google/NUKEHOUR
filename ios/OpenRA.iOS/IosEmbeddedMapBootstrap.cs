using System.Collections.Immutable;
using OpenRA.Mods.RA2.Content;

namespace OpenRA.iOS;

public sealed record IosEmbeddedMapBootstrapResult(bool Ready, int PlayableMaps, string Message);

public static class IosEmbeddedMapBootstrap
{
	const string ReadyMarker = ".embedded-maps-v1-ready";

	public static IosEmbeddedMapBootstrapResult EnsurePlayableMaps(
		string enginePath,
		string supportPath,
		CancellationToken cancellationToken)
	{
		var importedRoot = Path.Combine(supportPath, "Content", "ra2");
		var mapsPath = Path.Combine(importedRoot, "maps");
		var markerPath = Path.Combine(mapsPath, ReadyMarker);
		if (File.Exists(markerPath) && Directory.Exists(mapsPath) &&
			Directory.EnumerateFiles(mapsPath, "*.oramap", SearchOption.TopDirectoryOnly).Any())
			return new IosEmbeddedMapBootstrapResult(true,
				Directory.EnumerateFiles(mapsPath, "*.oramap", SearchOption.TopDirectoryOnly).Count(),
				string.Empty);

		var scanner = new RetailCompatibleMapSourceScanner();
		var sources = scanner.Scan(importedRoot,
			Path.Combine(enginePath, "global mix database.dat"), cancellationToken);
		if (sources.Length == 0)
			return new IosEmbeddedMapBootstrapResult(false, 0, "导入的游戏文件中没有找到可转换的官方地图。");

		var profile = File.Exists(Path.Combine(importedRoot, "ra2md.mix")) &&
			File.Exists(Path.Combine(importedRoot, "langmd.mix"))
			? Ra2RuntimeProfileId.Yuri : Ra2RuntimeProfileId.Base;
		var arguments = new Arguments(new[]
		{
			$"Engine.EngineDir={enginePath}",
			$"Engine.SupportDir={supportPath}",
			"Engine.SystemLanguage=zh-CN",
			"Game.Mod=ra2",
		});
		var modSearchPaths = ImmutableArray.Create(
			Path.Combine(enginePath, "mods"), Path.Combine(supportPath, "mods"));
		var configuration = new EngineBootstrapConfiguration(
			enginePath, supportPath, modSearchPaths, ImmutableArray<string>.Empty, arguments);

		using var preGame = PreGameEngineContext.Open(configuration);
		using var services = new ModDataServiceCollection();
		using var options = services.Seal(loader => loader, preGame.Settings,
			FluentInitializationMode.Isolated, ModDataRenderingMode.NoRender);
		using var scope = preGame.OpenModData(profile.ManifestSelection(), options);
		var converter = new Ra2MapConversionService();
		Directory.CreateDirectory(mapsPath);

		var playable = 0;
		try
		{
			foreach (var source in sources.Take(IosImportEnvelopePolicyV1.RuntimeMaps))
			{
				cancellationToken.ThrowIfCancellationRequested();
				var filename = $"retail-{playable:D3}-{source.SourceSha256[..16]}.oramap";
				var destination = Path.Combine(mapsPath, filename);
				var partial = destination + ".partial";
				try
				{
					var players = converter.Convert(scope.ModData, source, partial);
					if (players < 2)
						throw new InvalidDataException("The converted map is not a valid two-player map.");

					File.Move(partial, destination, true);
					playable++;
				}
				catch (Exception exception) when (exception is InvalidDataException ||
					exception is FormatException || exception is IOException ||
					exception is ArgumentException || exception is OverflowException ||
					exception is IndexOutOfRangeException)
				{
					Console.Error.WriteLine($"Skipping embedded map {source.SafeName}: {exception.GetType().Name}");
					if (File.Exists(partial))
						File.Delete(partial);
				}
			}
		}
		finally
		{
			foreach (var source in sources)
				source.Dispose();
		}

		if (playable == 0)
			return new IosEmbeddedMapBootstrapResult(false, 0, "未能从导入文件生成可用的双人地图。");

		File.WriteAllText(markerPath, $"schema=1\nplayable={playable}\n");
		return new IosEmbeddedMapBootstrapResult(true, playable, string.Empty);
	}
}
