namespace OpenRA.iOS;

public static class IosLaunchPreflight
{
	static readonly string[] RequiredPresentationFiles =
	{
		"VERSION",
		"glsl/combined.vert",
		"glsl/combined.frag",
		"mods/ra2/fonts/NotoSansCJKsc-Regular.otf",
		"mods/ra2/fonts/NotoSansCJKsc-Bold.otf",
		"mods/ra2/uibits/menubuttons.png",
		"mods/ra2/uibits/menupanel.png",
		"mods/ra2/uibits/chrome.png",
		"mods/ra2/uibits/ios-commandbar-icons-v2.png",
		"mods/ra2/uibits/NUCLEAR-CRISIS-BG-06.png",
	};

	public static void EnsureComplete(string bundlePath)
	{
		if (string.IsNullOrWhiteSpace(bundlePath))
			throw new ArgumentException("The iOS bundle path is unavailable.", nameof(bundlePath));

		var missing = RequiredPresentationFiles
			.Where(relativePath => !File.Exists(Path.Combine(
				bundlePath, relativePath.Replace('/', Path.DirectorySeparatorChar))))
			.ToArray();
		if (missing.Length != 0)
			throw new InvalidDataException(
				"The iOS application bundle is missing required presentation files: " +
				string.Join(", ", missing));
	}
}
