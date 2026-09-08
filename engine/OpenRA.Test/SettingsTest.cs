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
using System.Globalization;
using System.IO;
using NUnit.Framework;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class SettingsTest
	{
		string temporaryDirectory;
		string settingsPath;

		[SetUp]
		public void SetUp()
		{
			temporaryDirectory = Path.Combine(Path.GetTempPath(), $"openra-settings-{Guid.NewGuid():N}");
			Directory.CreateDirectory(temporaryDirectory);
			settingsPath = Path.Combine(temporaryDirectory, "settings.yaml");
		}

		[TearDown]
		public void TearDown()
		{
			if (Directory.Exists(temporaryDirectory))
				Directory.Delete(temporaryDirectory, true);
		}

		[Test]
		public void CommandLineCanEnableClassicMouseStyle()
		{
			var settings = new Settings(settingsPath,
				new Arguments("Game.UseClassicMouseStyle=true"));

			Assert.That(settings.Game.UseClassicMouseStyle, Is.True);
		}

		[Test]
		public void MissingSettingsUseSystemLanguagePreference()
		{
			var settings = new Settings(settingsPath, new Arguments());

			Assert.That(settings.Game.Language, Is.EqualTo(LanguageSelectionPolicy.SystemPreference));
			Assert.That(settings.SystemLanguageTag, Is.EqualTo(CultureInfo.CurrentUICulture.Name));
		}

		[Test]
		public void InvalidStoredLanguageNormalizesToSystemAndUnknownYamlSurvivesSave()
		{
			File.WriteAllText(settingsPath,
				"Game:\n" +
				"\tLanguage: broken\n" +
				"\tFutureSetting: keep-me\n");

			var settings = new Settings(settingsPath, new Arguments());
			Assert.That(settings.Game.Language, Is.EqualTo(LanguageSelectionPolicy.SystemPreference));

			settings.Save();
			var saved = File.ReadAllText(settingsPath);
			StringAssert.DoesNotContain("Language:", saved);
			StringAssert.Contains("FutureSetting: keep-me", saved);
		}

		[TestCase("en")]
		[TestCase("zh-CN")]
		public void ManualStoredLanguagePreferencesArePreserved(string preference)
		{
			File.WriteAllText(settingsPath, $"Game:\n\tLanguage: {preference}\n");

			var settings = new Settings(settingsPath, new Arguments());

			Assert.That(settings.Game.Language, Is.EqualTo(preference));
		}

		[TestCase("broken", "en", "System")]
		[TestCase("ZH_cn", "broken", "zh-CN")]
		[TestCase("EN", "zh-CN", "en")]
		public void CommandLineLanguageIsCanonicalizedAfterFileLoading(
			string commandLinePreference, string storedPreference, string expected)
		{
			File.WriteAllText(settingsPath, $"Game:\n\tLanguage: {storedPreference}\n");

			var settings = new Settings(settingsPath,
				new Arguments($"Game.Language={commandLinePreference}"));

			Assert.That(settings.Game.Language, Is.EqualTo(expected));
		}

		[Test]
		public void ExplicitSystemLanguageTagIsReadOnlyStartupState()
		{
			var settings = new Settings(settingsPath,
				new Arguments("Engine.SystemLanguage=zh-Hans"));

			Assert.That(settings.SystemLanguageTag, Is.EqualTo("zh-Hans"));
			Assert.That(settings.Sections.ContainsKey("SystemLanguageTag"), Is.False);

			settings.Save();
			StringAssert.DoesNotContain("SystemLanguageTag", File.ReadAllText(settingsPath));
		}

		[TestCase("")]
		[TestCase("   ")]
		public void BlankSystemLanguageTagFallsBackToCurrentUiCulture(string systemLanguageTag)
		{
			var settings = new Settings(settingsPath,
				new Arguments($"Engine.SystemLanguage={systemLanguageTag}"));

			Assert.That(settings.SystemLanguageTag, Is.EqualTo(CultureInfo.CurrentUICulture.Name));
		}

		[TestCase("en", "en-US")]
		[TestCase("zh-CN", "zh-Hans")]
		public void ManualLanguageAlwaysRemainsSerialized(string preference, string systemLanguageTag)
		{
			var settings = new Settings(settingsPath, new Arguments(
				$"Game.Language={preference}",
				$"Engine.SystemLanguage={systemLanguageTag}"));

			settings.Save();

			StringAssert.Contains($"Language: {preference}", File.ReadAllText(settingsPath));
		}

		[Test]
		public void SystemLanguagePreferenceIsOmittedAsTheDefault()
		{
			var settings = new Settings(settingsPath, new Arguments(
				"Game.Language=System",
				"Engine.SystemLanguage=zh-Hans"));

			settings.Save();

			StringAssert.DoesNotContain("Language:", File.ReadAllText(settingsPath));
		}

		[Test]
		public void FluentInitializationIsSerializedAndInstallsBundlesTransactionally()
		{
			var source = File.ReadAllText(Path.Combine(RepositoryRoot(),
				"engine", "OpenRA.Game", "FluentProvider.cs"));

			StringAssert.Contains("static readonly object InitializeSyncObject = new();", source);
			StringAssert.Contains(
				"static string currentLanguage = LanguageSelectionPolicy.English;", source);
			Assert.That(CountOccurrences(source, "LanguageSelectionPolicy.Resolve("), Is.EqualTo(1));
			StringAssert.Contains("Game.Settings?.Game?.Language", source);
			StringAssert.Contains("Game.Settings?.SystemLanguageTag", source);
			StringAssert.Contains("var culture = resolvedLanguage;", source);
			StringAssert.Contains(
				"WithLanguageOverrides(modData.Manifest.FluentMessages, resolvedLanguage, fileSystem)", source);

			var currentLanguageProperty = IndexOf(source, "public static string CurrentLanguage");
			var currentLanguageLock = IndexOf(source, "lock (SyncObject)", currentLanguageProperty);
			var currentLanguageReturn = IndexOf(source, "return currentLanguage;", currentLanguageLock);
			var currentLanguagePropertyEnd = MatchingBraceEnd(source,
				IndexOf(source, "{", currentLanguageProperty));
			Assert.That(currentLanguageLock, Is.LessThan(currentLanguageReturn));
			Assert.That(currentLanguageReturn, Is.LessThan(currentLanguagePropertyEnd));

			var initialize = IndexOf(source, "public static void Initialize(");
			var initializeSync = IndexOf(source, "lock (InitializeSyncObject)", initialize);
			var initializeSyncEnd = MatchingBraceEnd(source, IndexOf(source, "{", initializeSync));
			var resolve = IndexOf(source, "var resolvedLanguage = LanguageSelectionPolicy.Resolve(", initializeSync);
			var buildMod = IndexOf(source, "var nextModFluentBundle = new FluentBundle(");
			var buildMap = IndexOf(source, "nextMapFluentBundle = new FluentBundle(");
			var installLock = IndexOf(source, "lock (SyncObject)", buildMap);
			var installMod = IndexOf(source, "modFluentBundle = nextModFluentBundle;", installLock);
			var installMap = IndexOf(source, "mapFluentBundle = nextMapFluentBundle;", installMod);
			var installLanguage = IndexOf(source, "currentLanguage = resolvedLanguage;", installMap);
			var installLockEnd = MatchingBraceEnd(source, IndexOf(source, "{", installLock));

			Assert.That(initializeSync, Is.LessThan(resolve));
			Assert.That(resolve, Is.LessThan(buildMod));
			Assert.That(buildMod, Is.LessThan(buildMap));
			Assert.That(buildMap, Is.LessThan(installLock));
			Assert.That(installLock, Is.LessThan(installMod));
			Assert.That(installMod, Is.LessThan(installMap));
			Assert.That(installMap, Is.LessThan(installLanguage));
			Assert.That(installLanguage, Is.LessThan(installLockEnd));
			Assert.That(installLockEnd, Is.LessThan(initializeSyncEnd));
		}

		static int CountOccurrences(string source, string value)
		{
			var count = 0;
			var index = 0;
			while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
			{
				count++;
				index += value.Length;
			}

			return count;
		}

		static int IndexOf(string source, string value, int startIndex = 0)
		{
			var index = source.IndexOf(value, startIndex, StringComparison.Ordinal);
			Assert.That(index, Is.GreaterThanOrEqualTo(0), $"Missing source contract: {value}");
			return index;
		}

		static int MatchingBraceEnd(string source, int openingBrace)
		{
			var depth = 0;
			for (var index = openingBrace; index < source.Length; index++)
			{
				if (source[index] == '{')
					depth++;
				else if (source[index] == '}' && --depth == 0)
					return index;
			}

			Assert.Fail($"No matching closing brace for source index {openingBrace}.");
			return -1;
		}

		static string RepositoryRoot()
		{
			var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (directory != null)
			{
				var gitPath = Path.Combine(directory.FullName, ".git");
				if ((Directory.Exists(gitPath) || File.Exists(gitPath)) &&
					Directory.Exists(Path.Combine(directory.FullName, "engine", "OpenRA.Game")))
					return directory.FullName;

				directory = directory.Parent;
			}

			throw new InvalidOperationException("Could not locate the repository root.");
		}
	}
}
