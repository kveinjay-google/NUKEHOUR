using System.Runtime.InteropServices;

namespace OpenRA.iOS;

static class IosMetricKitDiagnostics
{
	[DllImport("__Internal")]
	static extern void OpenRAStartMetricKitDiagnostics(string path);

	[DllImport("__Internal")]
	static extern void OpenRAStopMetricKitDiagnostics();

	public static void Start(string supportPath) =>
		OpenRAStartMetricKitDiagnostics(Path.Combine(supportPath, "Diagnostics", "Pending", "MetricKit"));

	public static void Stop() => OpenRAStopMetricKitDiagnostics();
}
