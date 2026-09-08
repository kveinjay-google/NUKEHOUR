#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software under the GNU General Public License.
 */
#endregion

using System.IO;
using System.Reflection;
using NUnit.Framework;
using OpenRA.Mods.Common.Widgets.Logic;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class PerfDebugTextFormatterTest
	{
		static string RepositoryRoot()
		{
			var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, ".git")))
				directory = directory.Parent;

			Assert.That(directory, Is.Not.Null, "Unable to locate the repository root.");
			return directory!.FullName;
		}

		[Test]
		public void PerformancePanelUsesChineseLabelsAndNumericValues()
		{
			var formatter = typeof(PerfDebugLogic).GetMethod("FormatPerformanceText",
				BindingFlags.Public | BindingFlags.Static);
			Assert.That(formatter, Is.Not.Null, "Performance text formatter is missing.");

			var text = (string)formatter!.Invoke(null, new object[]
			{
				60d, 5.1d, "1192 MB", "419.4 MB", 7536, 3.8d, 17959, 15d, 2048, 1536
			});

			Assert.That(text, Is.EqualTo(
				"帧率：60 FPS   处理器：5.1%\n" +
				"内存：1192 MB 常驻 / 419.4 MB 托管\n" +
				"逻辑：第 7536 帧 / 3.8 ms\n" +
				"渲染：第 17959 帧 / 15.0 ms\n" +
				"输出分辨率：2048 × 1536"));
			StringAssert.DoesNotContain("批次数", text);
			StringAssert.DoesNotContain("视口", text);
			StringAssert.DoesNotContain("世界缓冲区", text);
		}

		[Test]
		public void UnavailableResourceValuesUseChineseFallback()
		{
			var formatter = typeof(PerfDebugLogic).GetMethod("FormatPerformanceText",
				BindingFlags.Public | BindingFlags.Static);
			Assert.That(formatter, Is.Not.Null, "Performance text formatter is missing.");

			var text = (string)formatter!.Invoke(null, new object[]
			{
				0d, null, "N/A", "N/A", 0, 0d, 0, 0d, 0, 0
			});

			StringAssert.Contains("处理器：暂无", text);
			StringAssert.Contains("内存：暂无 常驻 / 暂无 托管", text);
			StringAssert.DoesNotContain("N/A", text);
		}

		[Test]
		public void IngamePerformanceTextIsAnchoredAtTopLeft()
		{
			var layout = File.ReadAllText(Path.Combine(RepositoryRoot(),
				"engine", "mods", "common", "chrome", "ingame-perf.yaml"));

			StringAssert.Contains("X: 24", layout);
			StringAssert.Contains("Y: 24", layout);
			StringAssert.DoesNotContain("X: WINDOW_WIDTH - 340", layout);
			StringAssert.DoesNotContain("Y: WINDOW_HEIGHT - 145", layout);
		}
	}
}
