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

namespace OpenRA.Network
{
	public sealed class ClientMapReadinessPublisher
	{
		const long ProgressPublishIntervalMilliseconds = 250;

		readonly Func<long> elapsedMilliseconds;
		string lastMapUid;
		ClientMapReadinessReport? lastPublishedReport;
		long lastPublishedAt;

		public ClientMapReadinessPublisher(Func<long> elapsedMilliseconds)
		{
			this.elapsedMilliseconds = elapsedMilliseconds ?? throw new ArgumentNullException(nameof(elapsedMilliseconds));
		}

		public ClientMapReadinessReport? Update(string mapUid, MapStatus status, int percentage)
		{
			var report = status switch
			{
				MapStatus.Searching => new ClientMapReadinessReport(mapUid, Session.ClientMapPhase.Searching, -1),
				MapStatus.DownloadAvailable => new ClientMapReadinessReport(mapUid, Session.ClientMapPhase.WaitingForDownload, -1),
				MapStatus.Downloading when percentage == 100 =>
					new ClientMapReadinessReport(mapUid, Session.ClientMapPhase.InstallingOrVerifying, -1),
				MapStatus.Downloading when percentage is >= 1 and <= 99 =>
					new ClientMapReadinessReport(mapUid, Session.ClientMapPhase.Downloading, percentage),
				MapStatus.Downloading => new ClientMapReadinessReport(mapUid, Session.ClientMapPhase.Downloading, -1),
				MapStatus.Available => new ClientMapReadinessReport(mapUid, Session.ClientMapPhase.Ready, 100),
				MapStatus.Unavailable => new ClientMapReadinessReport(mapUid, Session.ClientMapPhase.Unavailable, -1),
				MapStatus.DownloadError => new ClientMapReadinessReport(mapUid, Session.ClientMapPhase.Error, -1),
				_ => new ClientMapReadinessReport(mapUid, Session.ClientMapPhase.Unknown, -1)
			};

			return Publish(report);
		}

		public ClientMapReadinessReport? UpdateUnknown(string mapUid)
		{
			return Publish(new ClientMapReadinessReport(mapUid, Session.ClientMapPhase.Unknown, -1));
		}

		ClientMapReadinessReport? Publish(ClientMapReadinessReport report)
		{
			if (!report.IsValidFor(report.MapUid))
				return null;

			if (!string.Equals(lastMapUid, report.MapUid, StringComparison.Ordinal))
			{
				lastMapUid = report.MapUid;
				lastPublishedReport = null;
			}

			if (lastPublishedReport == report)
				return null;

			var now = elapsedMilliseconds();
			if (lastPublishedReport.HasValue &&
				lastPublishedReport.Value.Phase == Session.ClientMapPhase.Downloading &&
				report.Phase == Session.ClientMapPhase.Downloading &&
				now - lastPublishedAt < ProgressPublishIntervalMilliseconds)
				return null;

			lastPublishedReport = report;
			lastPublishedAt = now;
			return report;
		}
	}
}
