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

using System;

namespace OpenRA.Mods.RA2.Content
{
	public enum Ra2RuntimeProfileId
	{
		Base = 1,
		Yuri = 2,
	}

	[Flags]
	public enum Ra2MapCompatibleProfiles
	{
		None = 0,
		Base = 1,
		Yuri = 2,
	}

	public static class Ra2RuntimeProfiles
	{
		public static TrustedManifestSelection ManifestSelection(this Ra2RuntimeProfileId profile) =>
			profile switch
			{
				Ra2RuntimeProfileId.Base => new TrustedManifestSelection("ra2", "mod.ios-base.yaml"),
				Ra2RuntimeProfileId.Yuri => new TrustedManifestSelection("ra2", "mod.ios-yuri.yaml"),
				_ => throw new ArgumentOutOfRangeException(nameof(profile)),
			};
	}
}
