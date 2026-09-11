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
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using OpenRA.Mods.RA2.Content;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class RetailContentImporterTest
	{
		string root;
		string source;
		string content;

		[SetUp]
		public void SetUp()
		{
			root = Path.Combine(Path.GetTempPath(), "openra-retail-import-" + Guid.NewGuid().ToString("N"));
			source = Path.Combine(root, "source");
			content = Path.Combine(root, "Content");
			Directory.CreateDirectory(source);
		}

		[TearDown]
		public void TearDown()
		{
			if (Directory.Exists(root))
				Directory.Delete(root, true);
		}

		string WriteSource(string name, string value = "data")
		{
			var path = Path.Combine(source, name);
			File.WriteAllText(path, value);
			return path;
		}

		RetailImportRequest BaseRequest(params string[] extras)
		{
			var files = new[] { WriteSource("ra2.mix"), WriteSource("language.mix") }.Concat(extras);
			return new RetailImportRequest(files, content, long.MaxValue);
		}

		[Test]
		public async Task CompleteBaseContentIsPublishedWithHashes()
		{
			var result = await new RetailContentImporter().ImportAsync(BaseRequest(), CancellationToken.None);

			Assert.That(result.Published, Is.True);
			Assert.That(result.Error, Is.EqualTo(RetailImportError.None));
			Assert.That(File.Exists(Path.Combine(content, "ra2", "ra2.mix")), Is.True);
			using var sha = SHA256.Create();
			var expected = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes("data"))).Replace("-", "").ToLowerInvariant();
			Assert.That(result.Hashes["ra2.mix"], Is.EqualTo(expected));
		}

		[TestCase("payload.dylib")]
		[TestCase("payload.exe")]
		[TestCase("payload.dll")]
		public async Task ExecutableExtensionsAreRejected(string name)
		{
			var result = await new RetailContentImporter().ImportAsync(
				BaseRequest(WriteSource(name)), CancellationToken.None);

			Assert.That(result.Error, Is.EqualTo(RetailImportError.ExecutableContent));
			Assert.That(result.Published, Is.False);
		}

		[Test]
		public async Task ExecutableMagicIsRejectedEvenWithMixExtension()
		{
			var executable = Path.Combine(source, "payload.mix");
			File.WriteAllBytes(executable, new byte[] { 0xCF, 0xFA, 0xED, 0xFE, 1, 2, 3, 4 });

			var result = await new RetailContentImporter().ImportAsync(
				BaseRequest(executable), CancellationToken.None);

			Assert.That(result.Error, Is.EqualTo(RetailImportError.ExecutableContent));
		}

		[Test]
		public async Task UnsupportedExtensionsAreRejected()
		{
			var result = await new RetailContentImporter().ImportAsync(
				BaseRequest(WriteSource("notes.txt")), CancellationToken.None);

			Assert.That(result.Error, Is.EqualTo(RetailImportError.UnsupportedFile));
		}

		[Test]
		public async Task CancellationDoesNotPublishContent()
		{
			using var cancellation = new CancellationTokenSource();
			cancellation.Cancel();

			var result = await new RetailContentImporter().ImportAsync(BaseRequest(), cancellation.Token);

			Assert.That(result.Error, Is.EqualTo(RetailImportError.Cancelled));
			Assert.That(Directory.Exists(Path.Combine(content, "ra2")), Is.False);
		}

		[Test]
		public async Task InsufficientSpaceIsRejectedBeforeCopy()
		{
			var result = await new RetailContentImporter().ImportAsync(
				new RetailImportRequest(BaseRequest().SourceFiles, content, 1), CancellationToken.None);

			Assert.That(result.Error, Is.EqualTo(RetailImportError.InsufficientSpace));
			Assert.That(Directory.Exists(Path.Combine(content, ".staging")), Is.False);
		}

		[Test]
		public async Task DuplicateNamesAreRejectedCaseInsensitively()
		{
			var duplicateDirectory = Path.Combine(source, "duplicate");
			Directory.CreateDirectory(duplicateDirectory);
			var duplicate = Path.Combine(duplicateDirectory, "RA2.MIX");
			File.WriteAllText(duplicate, "duplicate");

			var result = await new RetailContentImporter().ImportAsync(
				BaseRequest(duplicate), CancellationToken.None);

			Assert.That(result.Error, Is.EqualTo(RetailImportError.DuplicateFile));
		}

		[Test]
		public async Task StaleStagingIsCleanedBeforeSuccessfulImport()
		{
			var stale = Path.Combine(content, ".staging", "interrupted");
			Directory.CreateDirectory(stale);
			File.WriteAllText(Path.Combine(stale, "partial.mix"), "partial");

			var result = await new RetailContentImporter().ImportAsync(BaseRequest(), CancellationToken.None);

			Assert.That(result.Published, Is.True);
			Assert.That(Directory.EnumerateFileSystemEntries(Path.Combine(content, ".staging")), Is.Empty);
		}

		[Test]
		public async Task FailedImportPreservesPreviousReadyDirectory()
		{
			var importer = new RetailContentImporter();
			Assert.That((await importer.ImportAsync(BaseRequest(), CancellationToken.None)).Published, Is.True);
			File.WriteAllText(Path.Combine(content, "ra2", "marker.dat"), "previous");

			var failed = await importer.ImportAsync(
				new RetailImportRequest(new[] { WriteSource("replacement.mix") }, content, long.MaxValue),
				CancellationToken.None);

			Assert.That(failed.Error, Is.EqualTo(RetailImportError.IncompleteBaseContent));
			Assert.That(File.ReadAllText(Path.Combine(content, "ra2", "marker.dat")), Is.EqualTo("previous"));
		}
	}
}
