#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is
 * made available under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version.
 */
#endregion

namespace OpenRA
{
	public static class DebugVisualizationPolicy
	{
		public static bool IsEnabled(bool requested, bool isIos)
		{
			return requested && !isIos;
		}
	}
}
