using Foundation;
using OpenRA;

namespace OpenRA.iOS;

public sealed record IosNativeLanguageSelection(
	string Preference,
	string SystemLanguageTag,
	string EffectiveLanguage);

public static class IosNativeLanguage
{
	public static IosNativeLanguageSelection Resolve(string settingsPath)
	{
		var preference = ReadPreference(settingsPath);
		var systemLanguageTag = PreferredSystemLanguage();
		return new IosNativeLanguageSelection(
			preference,
			systemLanguageTag,
			LanguageSelectionPolicy.Resolve(preference, systemLanguageTag));
	}

	static string ReadPreference(string settingsPath)
	{
		if (!File.Exists(settingsPath))
			return LanguageSelectionPolicy.SystemPreference;

		try
		{
			var root = new MiniYaml(string.Empty, MiniYaml.FromFile(settingsPath));
			var value = root.NodeWithKeyOrDefault("Game")?
				.Value.NodeWithKeyOrDefault("Language")?.Value.Value;
			return LanguageSelectionPolicy.NormalizePreference(value);
		}
		catch (Exception e) when (e is InvalidDataException || e is IOException ||
			e is UnauthorizedAccessException || e is YamlException)
		{
			Console.Error.WriteLine($"Ignoring unreadable iOS language preference: {e.Message}");
			return LanguageSelectionPolicy.SystemPreference;
		}
	}

	static string PreferredSystemLanguage()
	{
		try
		{
			var preferredLanguages = NSLocale.PreferredLanguages;
			if (preferredLanguages is { Length: > 0 } &&
				!string.IsNullOrWhiteSpace(preferredLanguages[0]))
				return preferredLanguages[0];
		}
		catch (Exception e)
		{
			Console.Error.WriteLine($"Unable to read the preferred iOS language: {e.Message}");
		}

		return LanguageSelectionPolicy.English;
	}
}
