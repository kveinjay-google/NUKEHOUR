#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software under the GNU General Public License.
 */
#endregion

namespace OpenRA
{
	public static class TouchMouseEventFilter
	{
		// SDL marks mouse events synthesized from touch with this reserved device id.
		public const uint TouchMouseId = uint.MaxValue;

		public static bool ShouldIgnore(bool isIOS, uint mouseId)
		{
			return isIOS && mouseId == TouchMouseId;
		}
	}
}
