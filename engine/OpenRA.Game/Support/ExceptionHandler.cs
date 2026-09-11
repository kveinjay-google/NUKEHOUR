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

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace OpenRA
{
	public static class ExceptionHandler
	{
		public static void HandleFatalError(Exception ex)
		{
			HandleFatalError(ex, null);
		}

		public static void HandleFatalError(object exceptionObject, string supportDirectory)
		{
			var exception = exceptionObject as Exception;
			if (exception == null)
			{
				string payload;
				try
				{
					payload = exceptionObject?.ToString() ?? "<null>";
				}
				catch
				{
					payload = "<ToString failed>";
				}

				var payloadType = exceptionObject?.GetType().FullName ?? "<null>";
				exception = new InvalidOperationException(
					$"Unhandled exception payload was not a System.Exception. Type={payloadType}; Value={payload}");
			}

			HandleFatalError(exception, supportDirectory);
		}

		static void HandleFatalError(Exception ex, string supportDirectory)
		{
			var report = BuildFatalErrorReportSafely(ex);

			try
			{
				var timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHHmmss.fffZ", CultureInfo.InvariantCulture);
				var exceptionName = $"exception-{timestamp}-{Guid.NewGuid():N}.log";
				var rootDirectory = string.IsNullOrEmpty(supportDirectory) ? Platform.SupportDir : supportDirectory;
				var directory = Path.Combine(rootDirectory, "Logs");
				Directory.CreateDirectory(directory);
				var path = Path.Combine(directory, exceptionName);
				WriteFatalErrorReport(path, report);
			}
			catch (Exception persistenceError)
			{
				try
				{
					Console.Error.WriteLine($"Failed to persist fatal exception report: {persistenceError}");
				}
				catch
				{
					// Fatal reporting must never hide the original game-loop exception.
				}
			}

			try
			{
				Console.Error.WriteLine(report);
			}
			catch
			{
				// Console output is best effort after the durable report attempt.
			}
		}

		static void WriteFatalErrorReport(string path, string report)
		{
			using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read,
				4096, FileOptions.WriteThrough);
			using var writer = new StreamWriter(stream, new UTF8Encoding(false), 4096, leaveOpen: true);
			writer.Write(report);
			writer.Flush();
			stream.Flush(true);
		}

		static StringBuilder BuildFatalErrorReport(Exception ex)
		{
			var report = new StringBuilder();

			AppendReportSection(report, "engine", () =>
			{
				var engineVersion = Game.EngineVersion;
				if (engineVersion != null)
					report.AppendLine($"OpenRA engine version {engineVersion}");
			});

			AppendReportSection(report, "mod", () =>
			{
				var modData = Game.ModData;
				var manifest = modData?.Manifest;
				if (manifest != null)
				{
					report.AppendLine($"{manifest.Id} product version {manifest.Metadata.DisplayVersionOrVersion}");
					report.AppendLine($"{manifest.Id} compatibility {manifest.Metadata.CompatibilityOrVersion}");
				}
			});

			AppendReportSection(report, "world", () =>
			{
				var orderManager = Game.OrderManager;
				var world = orderManager?.World;
				var map = world?.Map;
				if (map != null)
					report.AppendLine($"on map {map.Uid} ({map.Title} by {map.Author}).");
			});

			AppendReportSection(report, "date", () =>
				report.AppendLine($"Date: {DateTime.UtcNow:u}"));
			AppendReportSection(report, "operating-system", () =>
				report.AppendLine(
					$"Operating System: {Platform.CurrentPlatform} ({Platform.CurrentArchitecture}, {Environment.OSVersion})"));
			AppendReportSection(report, "runtime", () =>
				report.AppendLine($"Runtime Version: {Platform.RuntimeVersion}"));
			AppendReportSection(report, "language", () =>
				report.AppendLine(
					"Installed Language: " +
					$"{CultureInfo.InstalledUICulture.TwoLetterISOLanguageName} (Installed) " +
					$"{CultureInfo.CurrentCulture.TwoLetterISOLanguageName} (Current) " +
					$"{CultureInfo.CurrentUICulture.TwoLetterISOLanguageName} (Current UI)"));

			return BuildExceptionReport(ex, report, 0);
		}

		static void AppendReportSection(StringBuilder report, string section, Action append)
		{
			try
			{
				append();
			}
			catch
			{
				report.AppendLine($"Diagnostic metadata section `{section}` unavailable.");
			}
		}

		static string BuildFatalErrorReportSafely(Exception ex)
		{
			try
			{
				return BuildFatalErrorReport(ex).ToString();
			}
			catch
			{
				var report = new StringBuilder("Detailed fatal exception report construction failed.\n");
				string exceptionType;
				try
				{
					exceptionType = ex?.GetType().FullName ?? "<type unavailable>";
				}
				catch
				{
					exceptionType = "<type unavailable>";
				}

				string message;
				try
				{
					message = ex?.Message ?? "<message unavailable>";
				}
				catch
				{
					message = "<message unavailable>";
				}

				report.AppendLine($"Exception of type `{exceptionType}`: {message}");
				try
				{
					report.AppendLine(ex?.StackTrace ?? "<stack trace unavailable>");
				}
				catch
				{
					report.AppendLine("<stack trace unavailable>");
				}

				return report.ToString();
			}
		}

		static StringBuilder AppendIndentedLine(this StringBuilder sb, int indent, string message)
		{
			return sb.Append(new string(' ', indent * 2)).Append(message).AppendLine();
		}

		static StringBuilder BuildExceptionReport(Exception ex, StringBuilder sb, int indent)
		{
			if (ex == null)
				return sb;

			sb.AppendIndentedLine(indent, $"Exception of type `{ex.GetType().FullName}`: {ex.Message}");

			if (ex is TypeLoadException tle)
			{
				sb.AppendIndentedLine(indent, $"TypeName=`{tle.TypeName}`");
			}
			else if (ex is OutOfMemoryException)
			{
				var gcMemoryBeforeCollect = GC.GetTotalMemory(false);
				if (LoadMemoryPolicy.ShouldForceCollection(OperatingSystem.IsIOS()))
				{
					GC.Collect();
					GC.WaitForPendingFinalizers();
					GC.Collect();
					sb.AppendIndentedLine(indent, $"GC Memory (post-collect)={GC.GetTotalMemory(false):N0}");
				}
				else
					sb.AppendIndentedLine(indent, "GC collection suppressed on iOS fatal-report path.");

				sb.AppendIndentedLine(indent, $"GC Memory (pre-collect)={gcMemoryBeforeCollect:N0}");

				using (var p = Process.GetCurrentProcess())
				{
					sb.AppendIndentedLine(indent, $"Working Set={p.WorkingSet64:N0}");
					sb.AppendIndentedLine(indent, $"Private Memory={p.PrivateMemorySize64:N0}");
					sb.AppendIndentedLine(indent, $"Virtual Memory={p.VirtualMemorySize64:N0}");
				}
			}
			else
			{
				// TODO: more exception types
			}

			if (ex.InnerException != null)
			{
				sb.AppendIndentedLine(indent, "Inner");
				BuildExceptionReport(ex.InnerException, sb, indent + 1);
			}

			sb.AppendIndentedLine(indent, ex.StackTrace);

			return sb;
		}
	}
}
