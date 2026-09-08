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

using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets
{
	sealed class TouchScrollHandoff
	{
		ScrollPanelWidget scrollPanel;
		bool takingOver;

		public bool Begin(Widget owner, MouseInput mi)
		{
			Cancel();
			if (mi.Button != MouseButton.Left)
				return false;

			for (var ancestor = owner.Parent; ancestor != null; ancestor = ancestor.Parent)
			{
				if (ancestor is not ScrollPanelWidget panel || !panel.EnableContentDragging)
					continue;

				if (!panel.BeginContentDragFromChild(mi))
					return false;

				scrollPanel = panel;
				return true;
			}

			return false;
		}

		public bool TryTake(MouseInput mi, bool release = false)
		{
			var panel = scrollPanel;
			if (panel == null)
				return false;

			takingOver = true;
			bool taken;
			try
			{
				taken = panel.TryTakeContentDragFromChild(mi);
			}
			finally
			{
				takingOver = false;
			}

			if (!taken)
				return false;

			scrollPanel = null;
			if (release)
				panel.YieldMouseFocus(mi);

			return true;
		}

		public void Cancel()
		{
			var panel = scrollPanel;
			scrollPanel = null;
			panel?.CancelContentDragFromChild();
		}

		public void OwnerYielded()
		{
			var panel = scrollPanel;
			scrollPanel = null;
			if (!takingOver)
				panel?.CancelContentDragFromChild();
		}
	}
}
