// Copyright (c) The OpenRA Developers and Contributors
// This file is part of OpenRA, which is free software. It is made
// available to you under the terms of the GNU General Public License
// as published by the Free Software Foundation, either version 3 of
// the License, or (at your option) any later version.

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using NUnit.Framework;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class OrderFeedbackEffectsTest
	{
		static bool IsRepositoryRoot(DirectoryInfo directory)
		{
			var gitMetadata = Path.Combine(directory.FullName, ".git");
			return (Directory.Exists(gitMetadata) || File.Exists(gitMetadata)) &&
				Directory.Exists(Path.Combine(directory.FullName, "mods", "ra2"));
		}

		static string RepositoryRoot()
		{
			var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (directory != null && !IsRepositoryRoot(directory))
				directory = directory.Parent;

			return directory?.FullName ?? throw new InvalidOperationException("Could not locate the repository root.");
		}

		static OrderEffectsInfo Ra2OrderEffectsInfo()
		{
			var world = MiniYaml.FromFile(Path.Combine(RepositoryRoot(), "mods", "ra2", "rules", "world.yaml"))
				.Single(node => node.Key == "World");
			var trait = world.Value.Nodes.Single(node => node.Key == "OrderEffects");
			var info = new OrderEffectsInfo();
			FieldLoader.Load(info, trait.Value);
			return info;
		}

		static MiniYamlNode Sequence(string image, string sequence)
		{
			var images = MiniYaml.FromFile(Path.Combine(RepositoryRoot(), "mods", "ra2", "sequences", "misc.yaml"));
			return images.Single(node => node.Key == image).Value.Nodes.Single(node => node.Key == sequence);
		}

		[Test]
		public void RepositoryRootRecognitionAcceptsLinkedWorktreeMetadataFile()
		{
			var root = Path.Combine(Path.GetTempPath(), $"order-feedback-worktree-{Guid.NewGuid():N}");
			Directory.CreateDirectory(Path.Combine(root, "mods", "ra2"));
			File.WriteAllText(Path.Combine(root, ".git"), "gitdir: /tmp/example\n");

			try
			{
				Assert.That(IsRepositoryRoot(new DirectoryInfo(root)), Is.True);
			}
			finally
			{
				Directory.Delete(root, true);
			}
		}

		[TestCase("Move", "move")]
		[TestCase("Attack", "attack")]
		[TestCase("ForceAttack", "attack")]
		public void Ra2MapsSupportedOrdersForEveryVisualTargetType(string order, string expectedSequence)
		{
			var info = Ra2OrderEffectsInfo();

			Assert.Multiple(() =>
			{
				Assert.That(info.OrderFeedbackImage, Is.EqualTo("order-feedback"));
				Assert.That(info.OrderFeedbackPalette, Is.EqualTo("mouse"));
				Assert.That(info.GetOrderFeedbackSequence(order, TargetType.Terrain), Is.EqualTo(expectedSequence));
				Assert.That(info.GetOrderFeedbackSequence(order, TargetType.Actor), Is.EqualTo(expectedSequence));
				Assert.That(info.GetOrderFeedbackSequence(order, TargetType.FrozenActor), Is.EqualTo(expectedSequence));
			});
		}

		[TestCase("Move", "move")]
		[TestCase("Attack", "attack")]
		public void MappedOrdersScheduleExactlyOneSpriteAnnotationThroughOrderIssued(
			string order, string expectedSequence)
		{
			var info = Ra2OrderEffectsInfo();
			var expectedPosition = new WPos(123, 456, 789);
			var scheduledCount = 0;
			World scheduledWorld = null;
			var scheduledPosition = WPos.Zero;
			string scheduledImage = null;
			string scheduledSequence = null;
			string scheduledPalette = null;
			var effects = new OrderEffects(info, (world, position, image, sequence, palette) =>
			{
				scheduledCount++;
				scheduledWorld = world;
				scheduledPosition = position;
				scheduledImage = image;
				scheduledSequence = sequence;
				scheduledPalette = palette;
			});

			var handled = ((INotifyOrderIssued)effects).OrderIssued(
				null, order, Target.FromPos(expectedPosition));

			Assert.Multiple(() =>
			{
				Assert.That(handled, Is.True);
				Assert.That(scheduledCount, Is.EqualTo(1));
				Assert.That(scheduledWorld, Is.Null);
				Assert.That(scheduledPosition, Is.EqualTo(expectedPosition));
				Assert.That(scheduledImage, Is.EqualTo("order-feedback"));
				Assert.That(scheduledSequence, Is.EqualTo(expectedSequence));
				Assert.That(scheduledPalette, Is.EqualTo("mouse"));
			});
		}

		[TestCase("AttackMove")]
		[TestCase("AssaultMove")]
		[TestCase("CreateGroup")]
		[TestCase("UnknownOrder")]
		public void UnsupportedOrdersUseTheExistingFallback(string order)
		{
			var info = Ra2OrderEffectsInfo();

			Assert.That(info.GetOrderFeedbackSequence(order, TargetType.Terrain), Is.Null);
		}

		[Test]
		public void InvalidTargetsAndUnconfiguredModsUseTheExistingFallback()
		{
			var info = Ra2OrderEffectsInfo();

			Assert.Multiple(() =>
			{
				Assert.That(info.GetOrderFeedbackSequence("Move", TargetType.Invalid), Is.Null);
				Assert.That(new OrderEffectsInfo().GetOrderFeedbackSequence("Move", TargetType.Terrain), Is.Null);
			});
		}

		[Test]
		public void EmptyOrderFeedbackSequenceUsesTheExistingFallback()
		{
			var info = Ra2OrderEffectsInfo();
			info.OrderFeedbackSequences["Move"] = "";

			Assert.That(info.GetOrderFeedbackSequence("Move", TargetType.Terrain), Is.Null);
		}

		[TestCase("move", "31", "10", "40")]
		[TestCase("attack", "58", "5", "80")]
		public void Ra2OrderFeedbackSequencesUseTheRetailMouseFramesForFourHundredMilliseconds(
			string sequenceName, string start, string length, string tick)
		{
			var sequence = Sequence("order-feedback", sequenceName);
			var fields = sequence.Value.Nodes.ToDictionary(node => node.Key, node => node.Value.Value);

			Assert.Multiple(() =>
			{
				Assert.That(fields["Filename"], Is.EqualTo("conquer|mouse.shp"));
				Assert.That(fields["Start"], Is.EqualTo(start));
				Assert.That(fields["Length"], Is.EqualTo(length));
				Assert.That(fields["Tick"], Is.EqualTo(tick));
				Assert.That(fields["ZOffset"], Is.EqualTo("2047"));
				Assert.That(int.Parse(length, CultureInfo.InvariantCulture) *
					int.Parse(tick, CultureInfo.InvariantCulture), Is.EqualTo(400));
			});
		}

		[Test]
		public void ExistingTerrainFallbackSequenceRemainsConfigured()
		{
			var info = Ra2OrderEffectsInfo();
			var moveFlash = Sequence("moveflsh", "idle");

			Assert.Multiple(() =>
			{
				Assert.That(info.TerrainFlashImage, Is.EqualTo("moveflsh"));
				Assert.That(info.TerrainFlashSequence, Is.EqualTo("idle"));
				Assert.That(info.TerrainFlashPalette, Is.EqualTo("moveflash"));
				Assert.That(moveFlash.Value.Nodes, Has.Some.Matches<MiniYamlNode>(node => node.Key == "Filename" && node.Value.Value == "ring.shp"));
			});
		}
	}
}
