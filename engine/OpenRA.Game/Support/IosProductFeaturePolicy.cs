namespace OpenRA
{
	public static class IosProductFeaturePolicy
	{
		public static bool ShowMapEditor(bool isIOS) { return !isIOS; }
		public static bool ShowAssetBrowser(bool isIOS) { return !isIOS; }
	}
}
