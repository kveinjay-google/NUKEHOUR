using CoreGraphics;
using Foundation;
using OpenRA;
using OpenRA.Mods.RA2.Content;
using UIKit;

namespace OpenRA.iOS;

public sealed class IosNativeStrings
{
	static readonly IosNativeStrings English = new()
	{
		Running = "NUKE HOUR is running",
		StartupFailureTitle = "NUKE HOUR could not start",
		StartupFailureMessage = "An unexpected error prevented the game from starting. Restart the app. If the problem continues, share the diagnostic log.",
		ChooseFiles = "Choose Game Files",
		ImportBase = "Import Base Game",
		ImportYuri = "Import Yuri's Revenge",
		ViewLicense = "View Licenses",
		Retry = "Retry",
		ContinueGame = "Enter Game",
		Close = "Close",
		NoticeTitle = "Important Notice",
		OfficialWebsite = "Official Website · nukehour.com",
		WebsiteAccessibilityHint = "Opens the official NUKE HOUR website in your browser.",
		CheckingTitle = "Content Check",
		NeedsBaseTitle = "Import Game Content",
		ImportingTitle = "Importing",
		NeedsYuriTitle = "Base Game Ready",
		ReadyTitle = "Ready",
		ErrorTitle = "Import Incomplete",
		CheckingMessage = "Checking legally owned game content…",
		NeedsBaseMessage = "Choose files from your legally owned Red Alert 2 copy. The app does not download or include official game content.",
		NeedsYuriMessage = "The base game is ready. You can enter the game now or import Yuri's Revenge files.",
		ReadyMessage = "The legally owned game content check is complete.",
		ImportingMessage = "Validating and copying legally owned game content. Keep the app open…",
		CancelledMessage = "File selection was cancelled.",
		StatusFormat = "Base game: {0}   Yuri's Revenge: {1}   Audio: {2}   Maps: {3}",
		CheckingState = "Checking",
		ReadyState = "Ready",
		IncompleteState = "Incomplete",
		UnsupportedState = "Unsupported",
		MissingState = "Not imported",
		GenericImportError = "The selected files could not be imported.",
		CancelledError = "File selection was cancelled.",
		UnsupportedFileError = "The selected files include unsupported game content.",
		ExecutableContentError = "Executable files cannot be imported.",
		InsufficientSpaceError = "There is not enough free space to import these files.",
		IncompleteBaseContentError = "The selected files do not contain a complete Red Alert 2 base installation.",
		DuplicateFileError = "Multiple selected files have the same name.",
		IoFailureError = "The files could not be read or copied. Check access and try again.",
		LicenseTitle = "License and content notice",
		NoticeBody = "NUKE HOUR is an independent, free and open-source community project. " +
			"EA has not endorsed and does not support this product. NUKE HOUR is not affiliated with, sponsored by, authorized by, or officially partnered with Electronic Arts Inc., Apple Inc., or Google LLC.\n\n" +
			"NUKE HOUR contains no third-party retail game files. You must manually import the required files from a legally purchased and owned copy. Command & Conquer: Red Alert 2 and Yuri's Revenge are named only to describe compatibility and identify the files to import.\n\n" +
			"NUKE HOUR is completely free and open-source. Any claim that a separately sold copy is an officially authorized NUKE HOUR edition is false. If you paid for one, report the seller and request a refund.\n\n" +
			"Download only from the official NUKE HOUR website to reduce the risk of tampering or malicious code.\n\n" +
			"Command & Conquer, Red Alert 2, and Yuri's Revenge and related marks belong to Electronic Arts Inc. or their respective owners. Apple, macOS, iOS, and iPadOS are trademarks of Apple Inc. Android and Google are trademarks of Google LLC.\n\n" +
			"The game engine is released under GNU GPL v3 or later. Source code and build instructions are provided with the project.",
	};

	static readonly IosNativeStrings Chinese = new()
	{
		Running = "NUKE HOUR 正在运行",
		StartupFailureTitle = "NUKE HOUR 无法启动",
		StartupFailureMessage = "发生意外错误，游戏无法启动。请重新启动应用；如果问题仍然存在，请提供诊断日志。",
		ChooseFiles = "选择正版游戏文件",
		ImportBase = "导入基础游戏",
		ImportYuri = "导入尤里的复仇",
		ViewLicense = "查看许可证",
		Retry = "重试",
		ContinueGame = "进入游戏",
		Close = "关闭",
		NoticeTitle = "重要声明",
		OfficialWebsite = "官方网站 · nukehour.com",
		WebsiteAccessibilityHint = "在浏览器中打开 NUKE HOUR 官方网站。",
		CheckingTitle = "资源检查",
		NeedsBaseTitle = "导入正版游戏资源",
		ImportingTitle = "正在导入",
		NeedsYuriTitle = "基础游戏已就绪",
		ReadyTitle = "准备完成",
		ErrorTitle = "导入未完成",
		CheckingMessage = "正在检查正版游戏资源…",
		NeedsBaseMessage = "请选择您合法拥有的《红色警戒 2》游戏文件。应用不会下载或附带官方资源。",
		NeedsYuriMessage = "基础游戏已就绪。您可以继续进入游戏，或导入《尤里的复仇》文件。",
		ReadyMessage = "正版游戏资源检查完成。",
		ImportingMessage = "正在校验并复制正版游戏资源，请勿关闭应用…",
		CancelledMessage = "已取消选择。",
		StatusFormat = "基础游戏：{0}　尤里的复仇：{1}　音频：{2}　地图：{3}",
		CheckingState = "检查中",
		ReadyState = "就绪",
		IncompleteState = "不完整",
		UnsupportedState = "不支持",
		MissingState = "未导入",
		GenericImportError = "无法导入所选文件。",
		CancelledError = "已取消文件选择。",
		UnsupportedFileError = "所选文件包含不支持的游戏资源。",
		ExecutableContentError = "不能导入可执行文件。",
		InsufficientSpaceError = "可用空间不足，无法导入这些文件。",
		IncompleteBaseContentError = "所选文件不包含完整的《红色警戒 2》基础游戏内容。",
		DuplicateFileError = "多个所选文件使用了相同的文件名。",
		IoFailureError = "无法读取或复制文件，请检查访问权限后重试。",
		LicenseTitle = "许可证与资源说明",
		NoticeBody = "NUKE HOUR 是由社区独立开发的完全免费开源项目。EA 未认可且不支持本产品。NUKE HOUR 与 Electronic Arts Inc.、Apple Inc. 或 Google LLC 不存在隶属、赞助、授权或官方合作关系。\n\n" +
			"NUKE HOUR 不包含任何第三方零售游戏文件。用户必须从自己合法购买并拥有的正版游戏副本中手动导入所需文件。《Command & Conquer: Red Alert 2》和《Yuri's Revenge》的名称仅用于说明兼容性，以及帮助用户识别需要导入的文件。\n\n" +
			"NUKE HOUR 完全免费开源。任何声称所售副本是 NUKE HOUR 官方授权版本的行为均属虚假宣传；如已付款，请向销售平台举报并申请退款。\n\n" +
			"请仅从 NUKE HOUR 官方网站下载安装，以降低软件被篡改或植入恶意代码的风险。\n\n" +
			"《Command & Conquer》《Red Alert 2》和《Yuri's Revenge》及相关商标归 Electronic Arts Inc. 或相应权利人所有。Apple、macOS、iOS 和 iPadOS 是 Apple Inc. 的商标。Android 和 Google 是 Google LLC 的商标。\n\n" +
			"游戏引擎依据 GNU GPL v3 或更高版本发布。源代码与构建说明随项目提供。",
	};

	public string Running { get; private init; } = string.Empty;
	public string StartupFailureTitle { get; private init; } = string.Empty;
	public string StartupFailureMessage { get; private init; } = string.Empty;
	public string ChooseFiles { get; private init; } = string.Empty;
	public string ImportBase { get; private init; } = string.Empty;
	public string ImportYuri { get; private init; } = string.Empty;
	public string ViewLicense { get; private init; } = string.Empty;
	public string Retry { get; private init; } = string.Empty;
	public string ContinueGame { get; private init; } = string.Empty;
	public string Close { get; private init; } = string.Empty;
	public string NoticeTitle { get; private init; } = string.Empty;
	public string NoticeBody { get; private init; } = string.Empty;
	public string OfficialWebsite { get; private init; } = string.Empty;
	public string WebsiteAccessibilityHint { get; private init; } = string.Empty;
	public string CheckingTitle { get; private init; } = string.Empty;
	public string NeedsBaseTitle { get; private init; } = string.Empty;
	public string ImportingTitle { get; private init; } = string.Empty;
	public string NeedsYuriTitle { get; private init; } = string.Empty;
	public string ReadyTitle { get; private init; } = string.Empty;
	public string ErrorTitle { get; private init; } = string.Empty;
	public string CheckingMessage { get; private init; } = string.Empty;
	public string NeedsBaseMessage { get; private init; } = string.Empty;
	public string NeedsYuriMessage { get; private init; } = string.Empty;
	public string ReadyMessage { get; private init; } = string.Empty;
	public string ImportingMessage { get; private init; } = string.Empty;
	public string CancelledMessage { get; private init; } = string.Empty;
	public string StatusFormat { get; private init; } = string.Empty;
	public string CheckingState { get; private init; } = string.Empty;
	public string ReadyState { get; private init; } = string.Empty;
	public string IncompleteState { get; private init; } = string.Empty;
	public string UnsupportedState { get; private init; } = string.Empty;
	public string MissingState { get; private init; } = string.Empty;
	public string GenericImportError { get; private init; } = string.Empty;
	public string CancelledError { get; private init; } = string.Empty;
	public string UnsupportedFileError { get; private init; } = string.Empty;
	public string ExecutableContentError { get; private init; } = string.Empty;
	public string InsufficientSpaceError { get; private init; } = string.Empty;
	public string IncompleteBaseContentError { get; private init; } = string.Empty;
	public string DuplicateFileError { get; private init; } = string.Empty;
	public string IoFailureError { get; private init; } = string.Empty;
	public string LicenseTitle { get; private init; } = string.Empty;
	public string LicenseBody => NoticeBody;

	IosNativeStrings() { }

	public static IosNativeStrings ForLanguage(string language)
		=> language == LanguageSelectionPolicy.SimplifiedChinese ? Chinese : English;

	public string WizardTitle(IosContentImportState state) => state switch
	{
		IosContentImportState.Checking => CheckingTitle,
		IosContentImportState.NeedsBase => NeedsBaseTitle,
		IosContentImportState.Importing => ImportingTitle,
		IosContentImportState.NeedsYuriOptional => NeedsYuriTitle,
		IosContentImportState.Ready => ReadyTitle,
		_ => ErrorTitle,
	};

	public string ImportError(RetailImportError error) => error switch
	{
		RetailImportError.None => GenericImportError,
		RetailImportError.Cancelled => CancelledError,
		RetailImportError.UnsupportedFile => UnsupportedFileError,
		RetailImportError.ExecutableContent => ExecutableContentError,
		RetailImportError.InsufficientSpace => InsufficientSpaceError,
		RetailImportError.IncompleteBaseContent => IncompleteBaseContentError,
		RetailImportError.DuplicateFile => DuplicateFileError,
		RetailImportError.IoFailure => IoFailureError,
		_ => GenericImportError,
	};

	public string ContentStatus(RetailContentStatus? status)
	{
		if (status == null)
			return string.Format(StatusFormat, CheckingState, CheckingState, CheckingState, CheckingState);

		return string.Format(StatusFormat,
			StateText(status.Base), StateText(status.Yuri), StateText(status.Audio), StateText(status.Maps));
	}

	string StateText(ContentState state) => state switch
	{
		ContentState.Ready => ReadyState,
		ContentState.Incomplete => IncompleteState,
		ContentState.Unsupported => UnsupportedState,
		_ => MissingState,
	};
}

sealed class IosContentWizardView : UIView
{
	static readonly nfloat OuterPadding = 18;
	static readonly nfloat CardPadding = 18;
	static readonly nfloat Gap = 12;
	readonly bool isPad;
	readonly UIScrollView rootScroll = new UIScrollView();
	readonly UIView summaryCard = Card();
	readonly UIView noticeCard = Card();
	readonly UILabel title;
	readonly UILabel message;
	readonly UILabel categoryStatus;
	readonly UIButton primaryButton;
	readonly List<UIButton> secondaryButtons = new();
	readonly UILabel noticeTitle;
	readonly UIScrollView noticeScroll = new UIScrollView();
	readonly UILabel noticeBody;
	readonly UIButton website;
	readonly UILabel version;

	public IosContentWizardView(
		IosContentWizardModel model,
		IosNativeStrings strings,
		string displayVersion,
		Action chooseFiles,
		Action showLicense,
		Action retry,
		Action continueGame,
		Action openWebsite)
	{
		isPad = UIDevice.CurrentDevice.UserInterfaceIdiom == UIUserInterfaceIdiom.Pad;
		BackgroundColor = UIColor.FromRGB(17, 22, 28);
		Layer.BorderColor = UIColor.FromRGB(190, 57, 39).CGColor;
		Layer.BorderWidth = 2;
		Layer.CornerRadius = 14;
		ClipsToBounds = true;

		title = Label(model.Title, UIFontTextStyle.Title1, UIColor.FromRGB(246, 202, 92));
		message = Label(model.Message, UIFontTextStyle.Body, UIColor.White);
		categoryStatus = Label(strings.ContentStatus(model.Status), UIFontTextStyle.Headline,
			UIColor.FromRGB(196, 205, 214));

		var actionsEnabled = model.State is not IosContentImportState.Checking and not IosContentImportState.Importing;
		primaryButton = Button(strings.ChooseFiles, chooseFiles, actionsEnabled);
		secondaryButtons.Add(Button(strings.ViewLicense, showLicense));
		secondaryButtons.Add(Button(strings.Retry, retry, actionsEnabled));
		if (model.State is IosContentImportState.NeedsYuriOptional or IosContentImportState.Ready)
			secondaryButtons.Add(Button(strings.ContinueGame, continueGame));

		noticeTitle = Label(strings.NoticeTitle, UIFontTextStyle.Title2, UIColor.FromRGB(246, 202, 92));
		noticeBody = Label(strings.NoticeBody, UIFontTextStyle.Footnote, UIColor.FromRGB(224, 229, 234));
		website = Button(strings.OfficialWebsite, openWebsite);
		website.AccessibilityHint = strings.WebsiteAccessibilityHint;
		version = Label(displayVersion, UIFontTextStyle.Footnote, UIColor.FromRGB(154, 166, 178));
		version.TextAlignment = UITextAlignment.Right;

		rootScroll.AlwaysBounceVertical = !isPad;
		rootScroll.ShowsVerticalScrollIndicator = !isPad;
		noticeScroll.AlwaysBounceVertical = true;
		noticeScroll.ShowsVerticalScrollIndicator = true;

		summaryCard.AddSubviews(title, message, categoryStatus, primaryButton);
		foreach (var button in secondaryButtons)
			summaryCard.AddSubview(button);

		noticeScroll.AddSubview(noticeBody);
		noticeCard.AddSubviews(noticeTitle, noticeScroll, website);
		rootScroll.AddSubviews(summaryCard, noticeCard);
		AddSubviews(rootScroll, version);
	}

	public override void LayoutSubviews()
	{
		base.LayoutSubviews();
		var width = Max(1, Bounds.Width - 2 * OuterPadding);
		nfloat versionHeight = 24;
		var scrollHeight = Max(1, Bounds.Height - 2 * OuterPadding - versionHeight - 6);
		rootScroll.Frame = new CGRect(OuterPadding, OuterPadding, width, scrollHeight);
		version.Frame = new CGRect(OuterPadding, Bounds.Height - OuterPadding - versionHeight, width, versionHeight);

		if (isPad)
		{
			var summaryWidth = (width - Gap) * (nfloat).52;
			summaryCard.Frame = new CGRect(0, 0, summaryWidth, scrollHeight);
			noticeCard.Frame = new CGRect(summaryWidth + Gap, 0, width - summaryWidth - Gap, scrollHeight);
			rootScroll.ContentSize = rootScroll.Bounds.Size;
		}
		else
		{
			var summaryHeight = SummaryRequiredHeight(width);
			var noticeHeight = Max(280, Min(360, scrollHeight * (nfloat).82));
			summaryCard.Frame = new CGRect(0, 0, width, summaryHeight);
			noticeCard.Frame = new CGRect(0, summaryHeight + Gap, width, noticeHeight);
			rootScroll.ContentSize = new CGSize(width, summaryHeight + Gap + noticeHeight);
		}

		LayoutSummaryCard();
		LayoutNoticeCard();
	}

	nfloat SummaryRequiredHeight(nfloat width)
	{
		var textWidth = Max(1, width - 2 * CardPadding);
		var textHeight = MeasuredHeight(title, textWidth) + MeasuredHeight(message, textWidth) +
			MeasuredHeight(categoryStatus, textWidth) + 2 * Gap;
		nfloat actionsHeight = (isPad ? 54 : 50) + Gap + (isPad ? 52 : 48);
		return Max(isPad ? 320 : 300, 2 * CardPadding + textHeight + Gap + actionsHeight);
	}

	void LayoutSummaryCard()
	{
		var width = Max(1, summaryCard.Bounds.Width - 2 * CardPadding);
		var y = CardPadding;
		foreach (var label in new[] { title, message, categoryStatus })
		{
			var height = MeasuredHeight(label, width);
			label.Frame = new CGRect(CardPadding, y, width, height);
			y += height + Gap;
		}

		nfloat primaryHeight = isPad ? 54 : 50;
		nfloat secondaryHeight = isPad ? 52 : 48;
		var secondaryY = summaryCard.Bounds.Height - CardPadding - secondaryHeight;
		var primaryY = secondaryY - Gap - primaryHeight;
		primaryButton.Frame = new CGRect(CardPadding, primaryY, width, primaryHeight);
		LayoutButtonRow(secondaryButtons, CardPadding, secondaryY, width, secondaryHeight);
	}

	void LayoutNoticeCard()
	{
		var width = Max(1, noticeCard.Bounds.Width - 2 * CardPadding);
		var titleHeight = MeasuredHeight(noticeTitle, width);
		noticeTitle.Frame = new CGRect(CardPadding, CardPadding, width, titleHeight);
		nfloat buttonHeight = isPad ? 52 : 48;
		website.Frame = new CGRect(CardPadding, noticeCard.Bounds.Height - CardPadding - buttonHeight, width, buttonHeight);
		var scrollY = CardPadding + titleHeight + 10;
		var scrollBottom = website.Frame.Top - 10;
		noticeScroll.Frame = new CGRect(CardPadding, scrollY, width, Max(1, scrollBottom - scrollY));
		var bodyHeight = MeasuredHeight(noticeBody, width);
		noticeBody.Frame = new CGRect(0, 0, width, bodyHeight);
		noticeScroll.ContentSize = new CGSize(width, bodyHeight);
	}

	static void LayoutButtonRow(IReadOnlyList<UIButton> buttons, nfloat x, nfloat y, nfloat width, nfloat height)
	{
		if (buttons.Count == 0)
			return;

		var buttonWidth = (width - Gap * (buttons.Count - 1)) / buttons.Count;
		for (var i = 0; i < buttons.Count; i++)
			buttons[i].Frame = new CGRect(x + i * (buttonWidth + Gap), y, buttonWidth, height);
	}

	static nfloat MeasuredHeight(UILabel label, nfloat width)
		=> (nfloat)Math.Ceiling((double)label.SizeThatFits(new CGSize(width, 10_000)).Height);

	static nfloat Max(nfloat left, nfloat right) => left > right ? left : right;

	static nfloat Min(nfloat left, nfloat right) => left < right ? left : right;

	static UIView Card()
	{
		var card = new UIView { BackgroundColor = UIColor.FromRGB(27, 33, 40) };
		card.Layer.CornerRadius = 10;
		card.Layer.BorderColor = UIColor.FromRGB(87, 99, 111).CGColor;
		card.Layer.BorderWidth = 1;
		card.ClipsToBounds = true;
		return card;
	}

	static UILabel Label(string text, UIFontTextStyle style, UIColor color)
	{
		return new UILabel
		{
			Text = text,
			TextColor = color,
			Font = UIFont.GetPreferredFontForTextStyle(style),
			AdjustsFontForContentSizeCategory = true,
			Lines = 0,
			LineBreakMode = UILineBreakMode.WordWrap,
		};
	}

	static UIButton Button(string title, Action action, bool enabled = true)
	{
		var button = new UIButton(UIButtonType.System);
		button.SetTitle(title, UIControlState.Normal);
		button.SetTitleColor(UIColor.White, UIControlState.Normal);
		button.BackgroundColor = enabled ? UIColor.FromRGB(49, 58, 68) : UIColor.FromRGB(35, 41, 48);
		button.Layer.BorderColor = UIColor.FromRGB(190, 57, 39).CGColor;
		button.Layer.BorderWidth = 1;
		button.Layer.CornerRadius = 8;
		button.TitleLabel!.Font = UIFont.GetPreferredFontForTextStyle(UIFontTextStyle.Headline);
		button.TitleLabel.AdjustsFontForContentSizeCategory = true;
		button.TitleLabel.Lines = 2;
		button.TitleLabel.TextAlignment = UITextAlignment.Center;
		button.TitleLabel.LineBreakMode = UILineBreakMode.WordWrap;
		button.Enabled = enabled;
		button.TouchUpInside += (_, _) => action();
		return button;
	}
}

sealed class IosLicenseViewController : UIViewController
{
	readonly bool isPad = UIDevice.CurrentDevice.UserInterfaceIdiom == UIUserInterfaceIdiom.Pad;
	readonly UIView panel = new();
	readonly UILabel title = new();
	readonly UIScrollView licenseScroll = new UIScrollView();
	readonly UILabel body = new();
	readonly UIButton close = new(UIButtonType.System);

	public IosLicenseViewController(IosNativeStrings strings)
	{
		title.Text = strings.LicenseTitle;
		title.TextColor = UIColor.FromRGB(246, 202, 92);
		title.Font = UIFont.GetPreferredFontForTextStyle(UIFontTextStyle.Title1);
		title.AdjustsFontForContentSizeCategory = true;
		title.Lines = 1;

		body.Text = strings.LicenseBody;
		body.TextColor = UIColor.FromRGB(224, 229, 234);
		body.Font = UIFont.GetPreferredFontForTextStyle(UIFontTextStyle.Body);
		body.AdjustsFontForContentSizeCategory = true;
		body.Lines = 0;
		body.LineBreakMode = UILineBreakMode.WordWrap;

		close.SetTitle(strings.Close, UIControlState.Normal);
		close.SetTitleColor(UIColor.White, UIControlState.Normal);
		close.TitleLabel!.Font = UIFont.GetPreferredFontForTextStyle(UIFontTextStyle.Headline);
		close.TitleLabel.AdjustsFontForContentSizeCategory = true;
		close.BackgroundColor = UIColor.FromRGB(49, 58, 68);
		close.Layer.BorderColor = UIColor.FromRGB(190, 57, 39).CGColor;
		close.Layer.BorderWidth = 1;
		close.Layer.CornerRadius = 8;
		close.TouchUpInside += (_, _) => DismissViewController(true, null);
	}

	public override bool PrefersStatusBarHidden() => true;

	public override UIInterfaceOrientationMask GetSupportedInterfaceOrientations()
		=> UIInterfaceOrientationMask.Landscape;

	public override void ViewDidLoad()
	{
		base.ViewDidLoad();
		View!.BackgroundColor = UIColor.FromRGBA(0, 0, 0, 210);
		panel.BackgroundColor = UIColor.FromRGB(17, 22, 28);
		panel.Layer.BorderColor = UIColor.FromRGB(190, 57, 39).CGColor;
		panel.Layer.BorderWidth = 2;
		panel.Layer.CornerRadius = 14;
		panel.ClipsToBounds = true;
		licenseScroll.AlwaysBounceVertical = true;
		licenseScroll.ShowsVerticalScrollIndicator = true;
		licenseScroll.AddSubview(body);
		panel.AddSubviews(title, licenseScroll, close);
		View.AddSubview(panel);
	}

	public override void ViewDidLayoutSubviews()
	{
		base.ViewDidLayoutSubviews();
		var bounds = View!.Bounds;
		var safe = View.SafeAreaInsets;
		nfloat outer = isPad ? 34 : 12;
		var availableWidth = Max(1, bounds.Width - safe.Left - safe.Right - 2 * outer);
		var availableHeight = Max(1, bounds.Height - safe.Top - safe.Bottom - 2 * outer);
		var panelWidth = isPad ? Min(availableWidth, bounds.Width * (nfloat).82) : availableWidth;
		var panelHeight = isPad ? Min(availableHeight, bounds.Height * (nfloat).88) : availableHeight;
		panel.Frame = new CGRect(
			(bounds.Width - panelWidth) / 2,
			(bounds.Height - panelHeight) / 2,
			panelWidth,
			panelHeight);

		nfloat padding = isPad ? 24 : 16;
		var contentWidth = Max(1, panelWidth - 2 * padding);
		var titleHeight = (nfloat)Math.Ceiling((double)title.SizeThatFits(new CGSize(contentWidth, 100)).Height);
		title.Frame = new CGRect(padding, padding, contentWidth, titleHeight);
		nfloat buttonHeight = isPad ? 54 : 48;
		close.Frame = new CGRect(padding, panelHeight - padding - buttonHeight, contentWidth, buttonHeight);
		var scrollY = padding + titleHeight + 12;
		var scrollBottom = close.Frame.Y - 12;
		licenseScroll.Frame = new CGRect(padding, scrollY, contentWidth, Max(1, scrollBottom - scrollY));
		var bodyHeight = (nfloat)Math.Ceiling((double)body.SizeThatFits(new CGSize(contentWidth, 20_000)).Height);
		body.Frame = new CGRect(0, 0, contentWidth, bodyHeight);
		licenseScroll.ContentSize = new CGSize(contentWidth, bodyHeight);
	}

	static nfloat Max(nfloat left, nfloat right) => left > right ? left : right;

	static nfloat Min(nfloat left, nfloat right) => left < right ? left : right;
}

public sealed class ViewController : UIViewController
{
	static readonly NSUrl OfficialWebsiteUrl = new("https://nukehour.com");
	readonly IosNativeStrings strings;
	Action? startWhenLandscape;
	bool didStart;
	UIView? contentWizard;

	public override bool PrefersStatusBarHidden() => true;

	public override UIInterfaceOrientation PreferredInterfaceOrientationForPresentation()
		=> UIInterfaceOrientation.LandscapeRight;

	public override UIInterfaceOrientationMask GetSupportedInterfaceOrientations()
		=> UIInterfaceOrientationMask.Landscape;

	readonly UILabel status = new UILabel
    {
        TextColor = UIColor.White,
        TextAlignment = UITextAlignment.Center,
        Lines = 0
    };

	public ViewController(IosNativeStrings strings)
	{
		this.strings = strings ?? throw new ArgumentNullException(nameof(strings));
		status.Text = strings.Running;
	}

    public override void ViewDidLoad()
    {
        base.ViewDidLoad();
        View!.BackgroundColor = UIColor.Black;
        status.Frame = new CGRect(24, 24, View.Bounds.Width - 48, 80);
        status.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleBottomMargin;
        View.AddSubview(status);
    }

	public override void ViewDidAppear(bool animated)
	{
		base.ViewDidAppear(animated);

		if (OperatingSystem.IsIOSVersionAtLeast(16))
		{
			SetNeedsUpdateOfSupportedInterfaceOrientations();

			if (View?.Window?.WindowScene is not UIWindowScene scene)
				return;

			var preferences = new UIWindowSceneGeometryPreferencesIOS(UIInterfaceOrientationMask.Landscape);
			scene.RequestGeometryUpdate(preferences, error =>
				Console.WriteLine($"OpenRA landscape request failed: {error.LocalizedDescription}"));
		}
	}

	public override void ViewDidLayoutSubviews()
	{
		base.ViewDidLayoutSubviews();
		if (View != null)
		{
			var bounds = View.Bounds;
			var insets = View.SafeAreaInsets;
			IosScreenMetrics.Publish(
				new OpenRA.Primitives.Size(
					Math.Max(1, (int)Math.Round(bounds.Width)),
					Math.Max(1, (int)Math.Round(bounds.Height))),
				new IosSafeAreaInsets(insets.Left, insets.Top, insets.Right, insets.Bottom));
		}

		if (contentWizard != null)
			contentWizard.Frame = WizardFrame();

		TryStartInLandscape();
	}

	CGRect WizardFrame()
	{
		var bounds = View!.Bounds;
		var insets = View.SafeAreaInsets;
		nfloat inset = 12;
		return new CGRect(
			bounds.X + insets.Left + inset,
			bounds.Y + insets.Top + inset,
			Math.Max(1, bounds.Width - insets.Left - insets.Right - 2 * inset),
			Math.Max(1, bounds.Height - insets.Top - insets.Bottom - 2 * inset));
	}

	public void StartInLandscape(Action start)
	{
		startWhenLandscape = start;
		TryStartInLandscape();
	}

	void TryStartInLandscape()
	{
		if (didStart || startWhenLandscape == null || View == null || View.Bounds.Width <= View.Bounds.Height)
			return;

		didStart = true;
		var start = startWhenLandscape;
		startWhenLandscape = null;
		BeginInvokeOnMainThread(start);
	}

	public void ShowContentImportWizard(
		IosContentWizardModel model,
		Action chooseFiles,
		Action showLicense,
		Action retry,
		Action continueGame)
	{
		contentWizard?.RemoveFromSuperview();
		status.Hidden = true;

		contentWizard = new IosContentWizardView(
			model,
			strings,
			BundleDisplayVersion(),
			chooseFiles,
			showLicense,
			retry,
			continueGame,
			OpenOfficialWebsite);
		contentWizard.Frame = WizardFrame();
		contentWizard.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight;
		View!.AddSubview(contentWizard);
	}

	public void BeginGameLaunch(Action startGame)
	{
		if (startGame == null)
			throw new ArgumentNullException(nameof(startGame));

		void CompleteNativeHandoff()
		{
			contentWizard?.RemoveFromSuperview();
			contentWizard = null;
			status.Hidden = true;
			View!.SetNeedsLayout();
			View.LayoutIfNeeded();
			BeginInvokeOnMainThread(startGame);
		}

		// A document picker or notice may still be completing its dismissal when the
		// user enters the game. SDL must not replace UIKit's presentation while that
		// transition is in flight, otherwise the renderer can remain behind the
		// launch wallpaper even though audio and hit testing are already active.
		if (PresentedViewController != null)
			DismissViewController(true, CompleteNativeHandoff);
		else
			CompleteNativeHandoff();
	}

	static string BundleDisplayVersion()
	{
		var bundle = NSBundle.MainBundle;
		var version = bundle.ObjectForInfoDictionary("CFBundleShortVersionString")?.ToString();
		var build = bundle.ObjectForInfoDictionary("CFBundleVersion")?.ToString();
		return $"NUKE HOUR {version ?? "0.0.0"} (Build {build ?? "0"})";
	}

	void OpenOfficialWebsite()
	{
		BeginInvokeOnMainThread(() =>
			UIApplication.SharedApplication.OpenUrl(
				OfficialWebsiteUrl,
				new UIApplicationOpenUrlOptions(),
				success =>
				{
					if (!success)
						Console.Error.WriteLine("The official NUKE HOUR website could not be opened.");
				}));
	}

	public void ShowStartupFailure(string message)
	{
		// The raw exception is already written to the diagnostic handlers by AppDelegate.
		// Do not render it here because it can contain private paths and stack frames.
		_ = message;
		contentWizard?.RemoveFromSuperview();
		contentWizard = null;
		status.Hidden = false;
		status.Text = $"{strings.StartupFailureTitle}\n\n{strings.StartupFailureMessage}";
		status.Font = UIFont.FromName("Menlo", 12) ?? UIFont.SystemFontOfSize(12);
		status.TextAlignment = UITextAlignment.Left;
		status.LineBreakMode = UILineBreakMode.WordWrap;
		status.Frame = new CGRect(24, 48, View!.Bounds.Width - 48, View.Bounds.Height - 72);
		status.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight;
		View.SetNeedsLayout();
		View.LayoutIfNeeded();
		View.BringSubviewToFront(status);
	}
}
