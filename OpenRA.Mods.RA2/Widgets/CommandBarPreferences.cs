#region Copyright & License Information
/*
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenRA.Primitives;

namespace OpenRA.Mods.RA2.Widgets
{
	public static class CommandBarCatalog
	{
		public sealed class Slot
		{
			public readonly string Id;
			public readonly int Width;
			public readonly bool DefaultVisible;

			public Slot(string id, int width, bool defaultVisible)
			{
				Id = id;
				Width = width;
				DefaultVisible = defaultVisible;
			}
		}

		public static readonly Slot[] All =
		{
			// Selection / groups
			new("GROUP_01", 48, true),
			new("GROUP_02", 48, true),
			new("GROUP_03", 48, true),
			new("GROUP_04", 48, true),
			new("GROUP_05", 48, true),
			new("PRODUCTION_X5", 48, true),
			new("GROUP_06", 48, true),
			new("GROUP_07", 48, true),
			new("GROUP_08", 48, true),
			new("GROUP_09", 48, true),
			new("GROUP_10", 48, true),
			new("SELECT_ALL", 48, true),
			new("SELECT_BY_TYPE", 48, true),
			new("CYCLE_BASE", 48, false),
			new("TO_SELECTION", 48, false),
			new("TO_LAST_EVENT", 48, false),
			new("CYCLE_HARVESTERS", 48, false),
			new("REMOVE_FROM_GROUP", 48, false),

			// Unit commands
			new("ATTACK_MOVE", 48, true),
			new("FORCE_MOVE", 48, true),
			new("FORCE_ATTACK", 48, true),
			new("GUARD", 48, true),
			new("DEPLOY", 48, true),
			new("SCATTER", 48, true),
			new("STOP", 48, true),
			new("QUEUE_ORDERS", 48, true),

			// Stances
			new("STANCE_ATTACKANYTHING", 48, true),
			new("STANCE_DEFEND", 48, true),
			new("STANCE_RETURNFIRE", 48, true),
			new("STANCE_HOLDFIRE", 48, true),

			// Orders
			new("SELL", 48, false),
			new("REPAIR", 48, false),
			new("BEACON", 48, false),
		};

		public static readonly HashSet<string> AllIds = new(All.Select(s => s.Id));

		public static IEnumerable<string> DefaultVisibleIds()
		{
			return All.Where(s => s.DefaultVisible).Select(s => s.Id);
		}

		public static Slot Get(string id)
		{
			return All.First(s => s.Id == id);
		}
	}

	public static class CommandBarLayoutPolicy
	{
		public const int CurrentVersion = 4;
		const int ProductionMultiplierMigrationVersion = 3;

		static readonly string[] RequiredTouchIds = { "STOP", "CYCLE_BASE" };

		static readonly HashSet<string> TouchSuppressedIds = new()
		{
			"GROUP_06", "GROUP_07", "GROUP_08", "GROUP_09", "GROUP_10",
			"SELECT_BY_TYPE", "FORCE_ATTACK", "DEPLOY",
			"FORCE_MOVE", "GUARD", "STANCE_ATTACKANYTHING", "STANCE_DEFEND", "STANCE_RETURNFIRE"
		};

		public static bool IsRequiredTouchId(string id, bool touchLayout) =>
			touchLayout && RequiredTouchIds.Contains(id);

		public static bool CanHide(string id, bool touchLayout) =>
			!IsRequiredTouchId(id, touchLayout);

		public static IEnumerable<string> AvailableIds(IEnumerable<string> ids, bool touchLayout)
		{
			return ids.Where(id => CommandBarCatalog.AllIds.Contains(id) &&
				(touchLayout ? !TouchSuppressedIds.Contains(id) : id != "PRODUCTION_X5"));
		}

		public static IEnumerable<string> NormalizeVisibleOrder(
			IEnumerable<string> ids, bool touchLayout, int savedVersion)
		{
			var normalized = AvailableIds(ids, touchLayout).Distinct().ToList();
			if (touchLayout && savedVersion < ProductionMultiplierMigrationVersion &&
				!normalized.Contains("PRODUCTION_X5"))
			{
				var groupFive = normalized.IndexOf("GROUP_05");
				normalized.Insert(groupFive < 0 ? normalized.Count : groupFive + 1, "PRODUCTION_X5");
			}

			if (touchLayout)
				foreach (var id in RequiredTouchIds)
					if (!normalized.Contains(id))
						normalized.Add(id);

			return normalized;
		}
	}

	public readonly struct TouchCommandBarSlotLayout
	{
		public readonly Size ButtonSize;
		public readonly Rectangle IconBounds;
		public readonly Rectangle LabelBounds;
		public readonly string Font;
		public readonly bool ShowLabel;

		public TouchCommandBarSlotLayout(
			Size buttonSize, Rectangle iconBounds, Rectangle labelBounds,
			string font, bool showLabel)
		{
			ButtonSize = buttonSize;
			IconBounds = iconBounds;
			LabelBounds = labelBounds;
			Font = font;
			ShowLabel = showLabel;
		}
	}

	public static class TouchCommandBarSlotPolicy
	{
		public static TouchCommandBarSlotLayout Create(
			IosScreenSnapshot snapshot, string id, bool expanded)
		{
			var required = CommandBarLayoutPolicy.IsRequiredTouchId(id, true);
			var height = snapshot.LogicalPoints(expanded ? 52 : 44);
			var width = required ? snapshot.LogicalPoints(56) :
				expanded ? CommandBarCatalog.Get(id).Width : 44;
			var showLabel = expanded || required;
			var labelHeight = snapshot.LogicalPoints(14);
			var bottomInset = snapshot.LogicalPoints(2);
			var iconSize = required ? snapshot.LogicalPoints(26) : 26;
			var label = showLabel
				? new Rectangle(0, height - bottomInset - labelHeight, width, labelHeight)
				: Rectangle.Empty;
			var iconBottom = showLabel ? label.Top : height;
			var icon = new Rectangle(
				(width - iconSize) / 2,
				Math.Max(0, (iconBottom - iconSize) / 2),
				iconSize,
				iconSize);
			return new TouchCommandBarSlotLayout(
				new Size(width, height), icon, label,
				required ? "IosTouchLabel" : "TinyBold", showLabel);
		}

		public static Rectangle PlaceBar(IosScreenSnapshot snapshot, Size size)
		{
			var safe = snapshot.SafeBounds;
			var margin = snapshot.LogicalPoints(12);
			return new Rectangle(
				safe.Left + Math.Max(0, (safe.Width - size.Width) / 2),
				Math.Max(safe.Top, safe.Bottom - margin - size.Height),
				Math.Min(size.Width, safe.Width),
				Math.Min(size.Height, safe.Height));
		}
	}

	public sealed class CommandBarPrefs
	{
		public List<string> VisibleOrder = new();
		public bool Expanded = false;
		public int Version = CommandBarLayoutPolicy.CurrentVersion;
	}

	public static class CommandBarPreferences
	{
		const string FileName = "ra2-commandbar.yaml";

		static string FilePath => Path.Combine(Platform.SupportDir, FileName);

		public static CommandBarPrefs Load()
		{
			var prefs = new CommandBarPrefs
			{
				VisibleOrder = CommandBarCatalog.DefaultVisibleIds().ToList(),
				Expanded = false
			};

			try
			{
				if (!File.Exists(FilePath))
					return prefs;

				prefs.Version = 1;

				var root = MiniYaml.FromFile(FilePath);
				var version = root.FirstOrDefault(n => n.Key == "Version");
				if (version != null && int.TryParse(version.Value.Value, out var savedVersion))
					prefs.Version = savedVersion;

				var visible = root.FirstOrDefault(n => n.Key == "Visible");
				if (visible != null && !string.IsNullOrWhiteSpace(visible.Value.Value))
				{
					var ids = visible.Value.Value.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
						.Select(s => s.Trim())
						.Where(id => CommandBarCatalog.AllIds.Contains(id))
						.Distinct()
						.ToList();
					if (ids.Count > 0)
						prefs.VisibleOrder = ids;
				}

				var expanded = root.FirstOrDefault(n => n.Key == "Expanded");
				if (expanded != null && bool.TryParse(expanded.Value.Value, out var exp))
					prefs.Expanded = exp;
			}
			catch (Exception e)
			{
				Log.Write("debug", $"Failed to load command bar preferences: {e}");
			}

			return prefs;
		}

		public static List<string> LoadVisibleOrder()
		{
			return Load().VisibleOrder;
		}

		public static void SaveVisibleOrder(IEnumerable<string> visibleOrder)
		{
			Save(visibleOrder, Load().Expanded);
		}

		public static void Save(IEnumerable<string> visibleOrder, bool expanded)
		{
			try
			{
				var ids = visibleOrder.Where(id => CommandBarCatalog.AllIds.Contains(id)).Distinct().ToList();
				var yaml = $"Version: {CommandBarLayoutPolicy.CurrentVersion}\n" +
					$"Visible: {string.Join(", ", ids)}\nExpanded: {expanded}\n";
				File.WriteAllText(FilePath, yaml);
			}
			catch (Exception e)
			{
				Log.Write("debug", $"Failed to save command bar preferences: {e}");
			}
		}
	}
}
