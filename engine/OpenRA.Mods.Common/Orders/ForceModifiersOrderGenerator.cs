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

using System.Collections.Generic;
using System.Linq;

namespace OpenRA.Mods.Common.Orders
{
	public class ForceModifiersOrderGenerator : UnitOrderGenerator
	{
		public readonly Modifiers Modifiers;
		readonly bool cancelOnFirstUse;
		public bool IsForceAttackTargeting => Modifiers == Modifiers.Ctrl && cancelOnFirstUse;

		public ForceModifiersOrderGenerator(Modifiers modifiers, bool cancelOnFirstUse)
		{
			Modifiers = modifiers;
			this.cancelOnFirstUse = cancelOnFirstUse;
		}

		public override IEnumerable<Order> Order(World world, CPos cell, int2 worldPixel, MouseInput mi)
		{
			mi.Modifiers |= Modifiers;
			if (cancelOnFirstUse && IsExplicitCancel(mi.Button, Game.Settings.Game.MouseButtonPreference.Cancel))
			{
				world.CancelInputMode();
				return System.Array.Empty<Order>();
			}

			var orders = base.Order(world, cell, worldPixel, mi).ToArray();

			if (ShouldCancelAfterOrders(Modifiers, cancelOnFirstUse, orders))
				world.CancelInputMode();

			return orders;
		}

		public static bool ShouldCancelAfterOrders(Modifiers modifiers, bool cancelOnFirstUse, IEnumerable<Order> orders)
		{
			if (!cancelOnFirstUse)
				return false;

			// Ctrl is the one-shot force-attack mode. Keep it armed for empty,
			// selection-only, or fallback move results, and clear it only after
			// a real world target accepted a force-attack order.
			if (modifiers == Modifiers.Ctrl)
				return orders.Any(o => o != null && o.OrderString != "CreateGroup" && o.OrderString != "Move");

			// Preserve the existing one-shot behavior for forced move and any
			// future modifier modes that use this generic generator.
			return true;
		}

		public static bool IsExplicitCancel(MouseButton button, MouseButton cancelButton)
		{
			return button == cancelButton;
		}

		public override string GetCursor(World world, CPos cell, int2 worldPixel, MouseInput mi)
		{
			mi.Modifiers |= Modifiers;
			return base.GetCursor(world, cell, worldPixel, mi);
		}

		public override bool InputOverridesSelection(World world, int2 xy, MouseInput mi)
		{
			mi.Modifiers |= Modifiers;
			if (Modifiers == Modifiers.Ctrl)
				return true;

			return base.InputOverridesSelection(world, xy, mi);
		}
	}
}
