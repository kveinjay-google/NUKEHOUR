#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is
 * made available under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.IO;

namespace OpenRA.Mods.Common.Widgets
{
	public static class IosFloatingControlsPreferences
	{
		const string FileName = "ios-floating-controls.txt";

		static bool loaded;
		static bool visible = true;

		public static bool Visible
		{
			get
			{
				EnsureLoaded();
				return visible;
			}
			set
			{
				EnsureLoaded();
				if (visible == value)
					return;

				visible = value;
				Save();
			}
		}

		static string FilePath => Path.Combine(Platform.SupportDir, FileName);

		static void EnsureLoaded()
		{
			if (loaded)
				return;

			loaded = true;
			try
			{
				if (File.Exists(FilePath) && bool.TryParse(File.ReadAllText(FilePath), out var saved))
					visible = saved;
			}
			catch (Exception e)
			{
				Log.Write("debug", $"Failed to load iOS floating control preference: {e}");
			}
		}

		static void Save()
		{
			try
			{
				File.WriteAllText(FilePath, visible.ToString());
			}
			catch (Exception e)
			{
				Log.Write("debug", $"Failed to save iOS floating control preference: {e}");
			}
		}
	}
}
