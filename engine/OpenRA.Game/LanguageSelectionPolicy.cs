#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is
 * made available under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version.
 */
#endregion

using System;
using System.Collections.Generic;

namespace OpenRA
{
	public static class LanguageSelectionPolicy
	{
		public const string SystemPreference = "System";
		public const string SimplifiedChinese = "zh-CN";
		public const string English = "en";

		public static IReadOnlyList<string> SupportedPreferences { get; } = Array.AsReadOnly(new[]
		{
			SystemPreference,
			SimplifiedChinese,
			English,
		});

		public static string NormalizePreference(string preference)
		{
			var normalized = preference?.Trim().Replace('_', '-');
			if (string.Equals(normalized, SimplifiedChinese, StringComparison.OrdinalIgnoreCase))
				return SimplifiedChinese;

			if (string.Equals(normalized, English, StringComparison.OrdinalIgnoreCase))
				return English;

			return SystemPreference;
		}

		public static string Resolve(string preference, string systemLanguageTag)
		{
			var normalizedPreference = NormalizePreference(preference);
			if (normalizedPreference != SystemPreference)
				return normalizedPreference;

			var normalizedSystemLanguage = systemLanguageTag?.Trim().Replace('_', '-');
			if (string.IsNullOrEmpty(normalizedSystemLanguage) ||
				!IsValidLanguageTag(normalizedSystemLanguage))
				return English;

			var separator = normalizedSystemLanguage.IndexOf('-');
			var language = separator < 0 ?
				normalizedSystemLanguage : normalizedSystemLanguage[..separator];
			return string.Equals(language, "zh", StringComparison.OrdinalIgnoreCase) ?
				SimplifiedChinese : English;
		}

		static bool IsValidLanguageTag(string languageTag)
		{
			var subtags = languageTag.Split('-');
			if (subtags[0].Length < 2 || subtags[0].Length > 8 ||
				!ContainsOnlyAsciiLetters(subtags[0]))
				return false;

			for (var i = 1; i < subtags.Length; i++)
			{
				if (subtags[i].Length < 1 || subtags[i].Length > 8)
					return false;

				foreach (var character in subtags[i])
					if (!IsAsciiLetter(character) && (character < '0' || character > '9'))
						return false;
			}

			return true;
		}

		static bool ContainsOnlyAsciiLetters(string value)
		{
			foreach (var character in value)
				if (!IsAsciiLetter(character))
					return false;

			return true;
		}

		static bool IsAsciiLetter(char character)
		{
			return (character >= 'A' && character <= 'Z') ||
				(character >= 'a' && character <= 'z');
		}

		public static string GetDisplayName(string preference, string uiLanguage)
		{
			var normalizedPreference = NormalizePreference(preference);
			if (normalizedPreference == SimplifiedChinese)
				return "简体中文";

			if (normalizedPreference == English)
				return "English";

			return Resolve(SystemPreference, uiLanguage) == SimplifiedChinese ?
				"自动（跟随系统）" : "Auto (System)";
		}

		public static bool RequiresReload(
			string appliedLanguage,
			string preference,
			string systemLanguageTag)
		{
			var currentLanguage = Resolve(SystemPreference, appliedLanguage);
			var targetLanguage = Resolve(preference, systemLanguageTag);
			return currentLanguage != targetLanguage;
		}
	}
}
