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
using NUnit.Framework;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class LanguageSelectionPolicyTest
	{
		[Test]
		public void SupportedPreferencesAreImmutableAndInUiOrder()
		{
			CollectionAssert.AreEqual(new[] { "System", "zh-CN", "en" },
				LanguageSelectionPolicy.SupportedPreferences);

			if (LanguageSelectionPolicy.SupportedPreferences is IList<string> mutableView)
			{
				Assert.That(mutableView.IsReadOnly, Is.True);
				Assert.Throws<NotSupportedException>(() => mutableView[0] = "en");
			}
		}

		[TestCase(null, "System")]
		[TestCase("", "System")]
		[TestCase("   ", "System")]
		[TestCase("System", "System")]
		[TestCase("system", "System")]
		[TestCase(" SYSTEM ", "System")]
		[TestCase("zh-CN", "zh-CN")]
		[TestCase("ZH-cn", "zh-CN")]
		[TestCase(" zh_CN ", "zh-CN")]
		[TestCase("en", "en")]
		[TestCase("EN", "en")]
		[TestCase(" en ", "en")]
		[TestCase("zh", "System")]
		[TestCase("fr", "System")]
		[TestCase("unknown", "System")]
		public void PreferencesNormalizeToCanonicalTokens(string preference, string expected)
		{
			Assert.That(LanguageSelectionPolicy.NormalizePreference(preference), Is.EqualTo(expected));
		}

		[TestCase(null, null, "en")]
		[TestCase("", "zh", "zh-CN")]
		[TestCase("   ", "zh-Hans", "zh-CN")]
		[TestCase("System", "zh-Hant", "zh-CN")]
		[TestCase("system", "zh_CN", "zh-CN")]
		[TestCase("System", " ZH-hans-CN ", "zh-CN")]
		[TestCase("System", "en-US", "en")]
		[TestCase("System", "fr-FR", "en")]
		[TestCase("System", "not-a-language", "en")]
		[TestCase("System", "zh-", "en")]
		[TestCase("System", "zh--CN", "en")]
		[TestCase("System", "zh-中文", "en")]
		[TestCase("System", "zh-123456789", "en")]
		[TestCase("en", "zh-Hans", "en")]
		[TestCase("EN", "zh-Hant", "en")]
		[TestCase("zh-CN", "en-US", "zh-CN")]
		[TestCase("ZH_cn", "fr-FR", "zh-CN")]
		[TestCase("broken", "zh-Hant", "zh-CN")]
		public void ResolvesPreferenceAgainstSystemLanguage(
			string preference, string systemLanguageTag, string expected)
		{
			Assert.That(LanguageSelectionPolicy.Resolve(preference, systemLanguageTag), Is.EqualTo(expected));
		}

		[TestCase("System", "en", "Auto (System)")]
		[TestCase("system", "en-US", "Auto (System)")]
		[TestCase("System", "zh", "自动（跟随系统）")]
		[TestCase("System", "zh-Hant", "自动（跟随系统）")]
		[TestCase("zh-CN", "en", "简体中文")]
		[TestCase("zh_cn", "zh-CN", "简体中文")]
		[TestCase("en", "zh-CN", "English")]
		[TestCase("EN", "en", "English")]
		[TestCase("broken", "zh-Hans", "自动（跟随系统）")]
		public void ReturnsExactLocalizedDisplayNames(string preference, string uiLanguage, string expected)
		{
			Assert.That(LanguageSelectionPolicy.GetDisplayName(preference, uiLanguage), Is.EqualTo(expected));
		}

		[TestCase("en", "System", "en-US", false)]
		[TestCase("zh-CN", "System", "zh-Hant", false)]
		[TestCase("zh-Hans", "System", "zh_CN", false)]
		[TestCase("EN", "en", "zh-Hans", false)]
		[TestCase("zh-CN", "zh_cn", "en-US", false)]
		[TestCase("en", "System", "zh-", false)]
		[TestCase("en", "zh-CN", "en-US", true)]
		[TestCase("zh-CN", "en", "zh-Hans", true)]
		public void ReloadsOnlyWhenTheEffectiveLanguageChanges(
			string appliedLanguage, string preference, string systemLanguageTag, bool expected)
		{
			Assert.That(LanguageSelectionPolicy.RequiresReload(
				appliedLanguage, preference, systemLanguageTag), Is.EqualTo(expected));
		}
	}
}
