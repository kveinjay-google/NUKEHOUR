#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is
 * made available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version.
 * For more information, see COPYING.
 */
#endregion

using OpenRA.GameRules;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Warheads
{
	[Desc("Triggers platform haptic feedback using the world-audio distance and volume curve.")]
	public sealed class HapticFeedbackWarhead : Warhead
	{
		public readonly HapticEffect Effect = HapticEffect.LightningStrike;
		public readonly float Intensity = 1f;

		public override void DoImpact(in Target target, WarheadArgs args)
		{
			var strength = Game.Sound?.WorldHapticStrength(target.CenterPosition, Intensity) ?? 0f;
			if (strength > 0f)
				Game.Haptics?.Play(Effect, strength);
		}
	}
}
