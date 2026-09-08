#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;

namespace OpenRA.Mods.Common
{
	public static class ProductionBatchPolicy
	{
		public static int ResolveStartCount(Modifiers modifiers, int touchMultiplier)
		{
			return modifiers.HasModifier(Modifiers.Shift) ? 5 : Math.Max(1, touchMultiplier);
		}

		public static int ResolveAcceptedCount(uint requestedCount, int queueCount, int sameItemCount,
			int queueLimit, int itemLimit, int buildLimit, int ownedCount)
		{
			var accepted = requestedCount > int.MaxValue ? int.MaxValue : (int)requestedCount;
			if (queueLimit > 0)
				accepted = Math.Min(accepted, queueLimit - queueCount);

			if (itemLimit > 0)
				accepted = Math.Min(accepted, itemLimit - sameItemCount);

			if (buildLimit > 0)
				accepted = Math.Min(accepted, buildLimit - sameItemCount - ownedCount);

			return Math.Max(0, accepted);
		}
	}
}
