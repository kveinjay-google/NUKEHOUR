#region Copyright & License Information
/*
 * Copyright 2007-2020 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System.Collections.Generic;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.RA2.Traits
{
	[RequireExplicitImplementation]
	public interface IRemoveInfector
	{
		void RemoveInfector(Actor self, bool kill, AttackInfo e = null);
	}

	[RequireExplicitImplementation]
	public interface INotifyPassengersDamage
	{
		void DamagePassengers(
			int damage, Actor attacker, int amount, Dictionary<string, int> versus, BitSet<DamageType> damageTypes, IEnumerable<int> damageModifiers);
	}

	[RequireExplicitImplementation]
	public interface INotifyEnteredGarrison { void OnEnteredGarrison(Actor self, Actor garrison); }

	[RequireExplicitImplementation]
	public interface INotifyExitedGarrison { void OnExitedGarrison(Actor self, Actor garrison); }

	[RequireExplicitImplementation]
	public interface INotifyGarrisonerEntered { void OnGarrisonerEntered(Actor self, Actor garrisoner); }

	[RequireExplicitImplementation]
	public interface INotifyGarrisonerExited { void OnGarrisonerExited(Actor self, Actor garrisoner); }
}
