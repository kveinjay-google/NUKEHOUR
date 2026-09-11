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

using NUnit.Framework;
using OpenRA.Mods.RA2.Content;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class RetailContentCatalogTest
	{
		readonly RetailContentCatalog catalog = new();

		[Test]
		public void EmptyDirectoryReportsEveryContentGroupMissing()
		{
			var status = catalog.Inspect(System.Array.Empty<string>());

			Assert.That(status.Base, Is.EqualTo(ContentState.Missing));
			Assert.That(status.Yuri, Is.EqualTo(ContentState.Missing));
			Assert.That(status.Audio, Is.EqualTo(ContentState.Missing));
			Assert.That(status.Maps, Is.EqualTo(ContentState.Missing));
			Assert.That(status.CanLaunchBaseGame, Is.False);
			Assert.That(status.CanLaunchYuriGame, Is.False);
		}

		[Test]
		public void BaseGameRequirementsAreCaseInsensitive()
		{
			var status = catalog.Inspect(new[] { "disc/RA2.MIX", "Language.mix" });

			Assert.That(status.Base, Is.EqualTo(ContentState.Ready));
			Assert.That(status.CanLaunchBaseGame, Is.True);
			Assert.That(status.Yuri, Is.EqualTo(ContentState.Missing));
		}

		[Test]
		public void LanguageArchivesProvideAudioWithoutLooseBagFiles()
		{
			var baseOnly = catalog.Inspect(new[] { "ra2.mix", "language.mix" });
			var yuri = catalog.Inspect(new[] { "ra2.mix", "language.mix", "ra2md.mix", "langmd.mix" });

			Assert.That(baseOnly.Audio, Is.EqualTo(ContentState.Ready));
			Assert.That(baseOnly.CanLaunchBaseGame, Is.True);
			Assert.That(yuri.Audio, Is.EqualTo(ContentState.Ready));
		}

		[Test]
		public void YuriRequiresBothExpansionArchives()
		{
			var incomplete = catalog.Inspect(new[] { "ra2.mix", "language.mix", "ra2md.mix" });
			var ready = catalog.Inspect(new[] { "ra2.mix", "language.mix", "ra2md.mix", "langmd.mix" });

			Assert.That(incomplete.Yuri, Is.EqualTo(ContentState.Incomplete));
			Assert.That(incomplete.CanLaunchYuriGame, Is.False);
			Assert.That(ready.Yuri, Is.EqualTo(ContentState.Ready));
			Assert.That(ready.CanLaunchYuriGame, Is.True);
		}

		[Test]
		public void LegacyMapsAreNotReportedReadyBeforeConversion()
		{
			var status = catalog.Inspect(new[] { "ra2.mix", "language.mix", "Maps/Custom.MPR" });

			Assert.That(status.Maps, Is.EqualTo(ContentState.Missing));
			Assert.That(status.CanLaunchBaseGame, Is.True);
		}

		[Test]
		public void OpenRaMapPackagesAreReportedReady()
		{
			var status = catalog.Inspect(new[] { "ra2.mix", "language.mix", "Maps/Custom.oramap" });

			Assert.That(status.Maps, Is.EqualTo(ContentState.Ready));
		}
	}
}
