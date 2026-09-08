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
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	public sealed class IosServerCreationLayout
	{
		public int Gap { get; }
		public WidgetBounds Window { get; }
		public WidgetBounds Header { get; }
		public WidgetBounds Form { get; }
		public WidgetBounds MapPreview { get; }
		public WidgetBounds ChangeMap { get; }
		public WidgetBounds Footer { get; }
		public WidgetBounds CreateButton { get; }
		public WidgetBounds Back { get; }
		public WidgetBounds ServerNameRow { get; }
		public WidgetBounds PasswordRow { get; }
		public WidgetBounds PortRow { get; }
		public WidgetBounds Notices { get; }
		public WidgetBounds NoticesHeader { get; }
		public WidgetBounds NoticesBody { get; }

		IosServerCreationLayout(IosMenuLayoutPolicy policy)
		{
			var width = Math.Max(1, policy.ContentBounds.Width);
			var height = Math.Max(1, policy.ContentBounds.Height);
			Gap = policy.Gap;
			Window = new WidgetBounds(0, 0, width, height);

			var headerHeight = Math.Min(height, policy.HeaderHeight);
			Header = new WidgetBounds(0, 0, width, headerHeight);
			var footerHeight = Math.Min(policy.FooterHeight, height);
			var footerY = Math.Max(Header.Bottom + Gap, height - footerHeight);
			Footer = new WidgetBounds(0, footerY, width, Math.Max(0, height - footerY));

			var bodyY = Math.Min(Footer.Y, Header.Bottom + Gap);
			var bodyBottom = Math.Max(bodyY, Footer.Y - Gap);
			var bodyHeight = bodyBottom - bodyY;
			var mapWidth = Math.Min(width / 3, Math.Max(2 * policy.MinimumTarget, width / 4));
			var formWidth = Math.Max(0, width - mapWidth - Gap);
			Form = new WidgetBounds(0, bodyY, formWidth, bodyHeight);

			var changeHeight = Math.Min(policy.MinimumTarget, bodyHeight);
			var previewHeight = Math.Max(0, bodyHeight - changeHeight - (changeHeight > 0 ? Gap : 0));
			MapPreview = new WidgetBounds(Form.Right + Gap, bodyY, mapWidth, previewHeight);
			ChangeMap = new WidgetBounds(MapPreview.X, MapPreview.Bottom + (changeHeight > 0 ? Gap : 0),
				mapWidth, changeHeight);

			var desiredRowHeight = policy.IsPhone
				? policy.MinimumReadableTextHeight + Gap + policy.MinimumTarget
				: policy.MinimumTarget;
			var rowHeight = Math.Min(desiredRowHeight, bodyHeight);
			if (policy.IsPhone)
			{
				var availableWidth = Math.Max(0, Form.Width - 2 * Gap);
				var firstWidth = availableWidth / 3;
				var secondWidth = availableWidth / 3;
				var thirdWidth = Math.Max(0, availableWidth - firstWidth - secondWidth);
				ServerNameRow = new WidgetBounds(Form.X, Form.Y, firstWidth, rowHeight);
				PasswordRow = new WidgetBounds(ServerNameRow.Right + Gap, Form.Y, secondWidth, rowHeight);
				PortRow = new WidgetBounds(PasswordRow.Right + Gap, Form.Y, thirdWidth, rowHeight);
			}
			else
			{
				ServerNameRow = new WidgetBounds(Form.X, Form.Y, Form.Width, rowHeight);
				PasswordRow = NextRow(ServerNameRow, Form, rowHeight, Gap);
				PortRow = NextRow(PasswordRow, Form, rowHeight, Gap);
			}

			var noticesY = Math.Min(Form.Bottom, PortRow.Bottom + Gap);
			Notices = new WidgetBounds(Form.X, noticesY, Form.Width, Math.Max(0, Form.Bottom - noticesY));
			var noticesHeaderHeight = Math.Min(policy.MinimumReadableTextHeight, Notices.Height);
			NoticesHeader = new WidgetBounds(Notices.X, Notices.Y, Notices.Width, noticesHeaderHeight);
			NoticesBody = new WidgetBounds(Notices.X, NoticesHeader.Bottom, Notices.Width,
				Math.Max(0, Notices.Bottom - NoticesHeader.Bottom));

			var buttonWidth = Math.Max(0, (Footer.Width - Gap) / 2);
			CreateButton = new WidgetBounds(Footer.X, Footer.Y, buttonWidth, Footer.Height);
			Back = new WidgetBounds(CreateButton.Right + Gap, Footer.Y,
				Math.Max(0, Footer.Right - CreateButton.Right - Gap), Footer.Height);
		}

		public static IosServerCreationLayout Create(IosMenuLayoutPolicy policy)
		{
			return new IosServerCreationLayout(policy);
		}

		public static WidgetBounds[] HeaderSegments(int width, params int[] preferredWidths)
		{
			width = Math.Max(0, width);
			var segments = new WidgetBounds[preferredWidths.Length];
			if (preferredWidths.Length == 0)
				return segments;

			var preferred = new int[preferredWidths.Length];
			var preferredTotal = 0L;
			for (var i = 0; i < preferred.Length; i++)
			{
				preferred[i] = Math.Max(0, preferredWidths[i]);
				preferredTotal += preferred[i];
			}

			var x = 0;
			var accumulated = 0L;
			for (var i = 0; i < segments.Length; i++)
			{
				accumulated += preferred[i];
				var right = i == segments.Length - 1 ? width :
					preferredTotal > width && preferredTotal > 0
						? (int)(accumulated * width / preferredTotal)
						: Math.Min(width, x + preferred[i]);
				segments[i] = new WidgetBounds(x, 0, Math.Max(0, right - x), 1);
				x = right;
			}

			return segments;
		}

		static WidgetBounds NextRow(WidgetBounds previous, WidgetBounds container, int height, int gap)
		{
			var y = Math.Min(container.Bottom, previous.Bottom + gap);
			return new WidgetBounds(container.X, y, container.Width, Math.Min(height, container.Bottom - y));
		}
	}
}
