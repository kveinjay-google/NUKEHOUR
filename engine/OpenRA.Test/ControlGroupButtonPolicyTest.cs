#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is
 * made available under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version.
 * For more information, see COPYING.
 */
#endregion

using NUnit.Framework;
using OpenRA.Mods.RA2.Widgets.Logic;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class ControlGroupButtonPolicyTest
	{
		[TestCase(false, Modifiers.None, true, true, TouchControlGroupAction.UseDesktopRules)]
		[TestCase(true, Modifiers.Ctrl, true, true, TouchControlGroupAction.UseDesktopRules)]
		[TestCase(true, Modifiers.Shift, true, true, TouchControlGroupAction.UseDesktopRules)]
		[TestCase(true, Modifiers.None, true, true, TouchControlGroupAction.Create)]
		[TestCase(true, Modifiers.None, true, false, TouchControlGroupAction.Ignore)]
		[TestCase(true, Modifiers.None, false, true, TouchControlGroupAction.Recall)]
		[TestCase(true, Modifiers.None, false, false, TouchControlGroupAction.Recall)]
		public void TouchTapCreatesAnEmptyGroupWithoutBreakingDesktopShortcuts(
			bool touchPlatform, Modifiers modifiers, bool groupEmpty, bool hasOwnedSelection,
			TouchControlGroupAction expected)
		{
			Assert.That(ControlGroupButtonPolicy.Resolve(
				touchPlatform, modifiers, groupEmpty, hasOwnedSelection), Is.EqualTo(expected));
		}
	}
}
