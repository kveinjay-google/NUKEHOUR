#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is
 * made available under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

namespace OpenRA.Support
{
	public static class PerformanceBenchmarkPolicy
	{
		public static bool IsActive { get; set; }
		public static bool RecordFrameDiagnostics => !IsActive;
		public static bool RunWatchdog => true;
		public static int MinimumWorldDownscaleFactor =>
			IsActive && System.Environment.GetEnvironmentVariable("OPENRA_IOS_PERF_RENDER_SCALE") == "0.5" ? 2 : 1;
	}
}
