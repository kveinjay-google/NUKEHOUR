#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is
 * made available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version.
 */
#endregion

using System;
using NUnit.Framework;
using OpenRA.Mods.Common.Orders;
using OpenRA.Widgets;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class ForceModifiersOrderGeneratorTest
	{
		[Test]
		public void OneShotForceAttackStaysActiveUntilAForceAttackOrderExists()
		{
			Assert.That(ForceModifiersOrderGenerator.ShouldCancelAfterOrders(
				Modifiers.Ctrl, true, Array.Empty<Order>()), Is.False);
			Assert.That(ForceModifiersOrderGenerator.ShouldCancelAfterOrders(
				Modifiers.Ctrl, true, new[] { new Order("CreateGroup", null, false) }), Is.False);
			Assert.That(ForceModifiersOrderGenerator.ShouldCancelAfterOrders(
				Modifiers.Ctrl, true, new[] { new Order("Move", null, false) }), Is.False);
			Assert.That(ForceModifiersOrderGenerator.ShouldCancelAfterOrders(
				Modifiers.Ctrl, true, new[] { new Order("CreateGroup", null, false), new Order("ForceAttack", null, false) }), Is.True);
			Assert.That(ForceModifiersOrderGenerator.ShouldCancelAfterOrders(
				Modifiers.Ctrl, true, new[] { new Order("C4", null, false) }), Is.True);
			Assert.That(ForceModifiersOrderGenerator.ShouldCancelAfterOrders(
				Modifiers.Ctrl, true, new[] { new Order("BeginMinefield", null, false) }), Is.True);
		}

		[Test]
		public void ExistingModifierModesKeepTheirPreviousCancellationPolicy()
		{
			Assert.That(ForceModifiersOrderGenerator.ShouldCancelAfterOrders(
				Modifiers.Alt, true, Array.Empty<Order>()), Is.True);
			Assert.That(ForceModifiersOrderGenerator.ShouldCancelAfterOrders(
				Modifiers.Ctrl, false, new[] { new Order("ForceAttack", null, false) }), Is.False);
			Assert.That(ForceModifiersOrderGenerator.IsExplicitCancel(MouseButton.Right, MouseButton.Right), Is.True);
			Assert.That(ForceModifiersOrderGenerator.IsExplicitCancel(MouseButton.Left, MouseButton.Right), Is.False);
		}
	}
}
