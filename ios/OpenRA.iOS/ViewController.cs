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
		CheckingTitle = "Content Check",
		NeedsBaseTitle = "Import Game Content",
		ImportingTitle = "Importing",
		NeedsYuriTitle = "Optional Music Not Imported",
		ReadyTitle = "Ready",
		ErrorTitle = "Import Incomplete",
		CheckingMessage = "Checking legally owned game content…",
		NeedsBaseMessage = "Choose files from your legally owned Red Alert 2 copy. The app does not download or include official game content.",
		NeedsYuriMessage = "The game can start now. Import theme.mix to enable the current soundtrack. thememd.mix is also recognized and preserved as the optional Yuri's Revenge music archive.",
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
		LicenseBody = "NUKE HOUR is an independent open-source project and is not affiliated with, endorsed by, or sponsored by Electronic Arts. The app includes no Electronic Arts game assets; players must import them from a legally owned copy.\n\nThe game engine is released under GNU GPL v3 or later. Source code and build instructions are provided with the project.",
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
		CheckingTitle = "资源检查",
		NeedsBaseTitle = "导入正版游戏资源",
		ImportingTitle = "正在导入",
		NeedsYuriTitle = "背景音乐尚未导入",
		ReadyTitle = "准备完成",
		ErrorTitle = "导入未完成",
		CheckingMessage = "正在检查正版游戏资源…",
		NeedsBaseMessage = "请选择您合法拥有的《红色警戒 2》游戏文件。应用不会下载或附带官方资源。",
		NeedsYuriMessage = "游戏已经可以启动。请导入 theme.mix 以启用当前背景音乐；thememd.mix 也会作为可选的 YR 音乐包被识别并保留。也可暂时无背景音乐进入游戏。",
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
		LicenseBody = "NUKE HOUR 是独立的开源项目，与 Electronic Arts 无隶属、认可或赞助关系。应用不包含任何 Electronic Arts 游戏资源；玩家必须从合法拥有的正版副本自行导入。\n\n游戏引擎依据 GNU GPL v3 或更高版本发布。源代码与构建说明随项目提供。",
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
	public string LicenseBody { get; private init; } = string.Empty;

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

public sealed class ViewController : UIViewController
{
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
		InstallMenuBackdrop();
        status.Frame = new CGRect(24, 24, View.Bounds.Width - 48, 80);
        status.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleBottomMargin;
        View.AddSubview(status);
    }

	void InstallMenuBackdrop()
	{
		var resourcePath = NSBundle.MainBundle.ResourcePath;
		if (string.IsNullOrEmpty(resourcePath))
			return;

		var wallpaper = UIImage.FromFile(Path.Combine(resourcePath, "mods", "ra2", "uibits", "NUCLEAR-CRISIS-BG-02.png"));
		if (wallpaper == null)
			return;

		var imageView = new UIImageView(wallpaper)
		{
			Frame = View!.Bounds,
			AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight,
			ContentMode = UIViewContentMode.ScaleAspectFill,
			ClipsToBounds = true,
		};
		View.InsertSubview(imageView, 0);
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

		TryStartInLandscape();
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
		Action chooseBase,
		Action? continueGame)
	{
		contentWizard?.RemoveFromSuperview();
		status.Hidden = true;

		var panel = new UIView { TranslatesAutoresizingMaskIntoConstraints = false };
		panel.BackgroundColor = UIColor.FromRGB(17, 22, 28);
		panel.Layer.BorderColor = UIColor.FromRGB(190, 57, 39).CGColor;
		panel.Layer.BorderWidth = 2;
		panel.Layer.CornerRadius = 14;

		var title = Label(model.Title, UIFontTextStyle.Title1, UIColor.FromRGB(246, 202, 92));
		var message = Label(model.Message, UIFontTextStyle.Body, UIColor.White);
		message.Lines = 0;
		var requiredFiles = new UIStackView(new UIView[]
		{
			RequiredFileStatus("ra2.mix", model.Ra2Imported),
			RequiredFileStatus("language.mix", model.LanguageImported),
			RequiredFileStatus("theme.mix（RA2 背景音乐）", model.Ra2MusicImported),
		})
		{
			Axis = UILayoutConstraintAxis.Vertical,
			Alignment = UIStackViewAlignment.Fill,
			Spacing = 4,
		};
		var yuriTitle = Label("尤里的复仇（可选）", UIFontTextStyle.Subheadline, UIColor.FromRGB(196, 205, 214));
		var yuriFiles = new UIStackView(new UIView[]
		{
			RequiredFileStatus("ra2md.mix（YR）", model.Ra2MdImported),
			RequiredFileStatus("langmd.mix（YR）", model.LangMdImported),
			RequiredFileStatus("thememd.mix（YR 音乐包，可选）", model.YuriMusicImported),
		})
		{
			Axis = UILayoutConstraintAxis.Vertical,
			Alignment = UIStackViewAlignment.Fill,
			Spacing = 4,
		};

		var buttons = new UIStackView
		{
			Axis = UILayoutConstraintAxis.Horizontal,
			Alignment = UIStackViewAlignment.Fill,
			Distribution = UIStackViewDistribution.FillEqually,
			Spacing = 12,
			TranslatesAutoresizingMaskIntoConstraints = false
		};

		// One explicit entry point keeps the legal-content flow clear on iPad.
		if (model.State != IosContentImportState.Importing)
			buttons.AddArrangedSubview(Button(strings.ChooseFiles, chooseBase));
		if (continueGame != null)
			buttons.AddArrangedSubview(Button(strings.ContinueGame, continueGame));

		var stack = new UIStackView(new UIView[] { title, message, requiredFiles, yuriTitle, yuriFiles, buttons })
		{
			Axis = UILayoutConstraintAxis.Vertical,
			Alignment = UIStackViewAlignment.Fill,
			Distribution = UIStackViewDistribution.Fill,
			Spacing = 18,
			TranslatesAutoresizingMaskIntoConstraints = false
		};

		var scroll = new UIScrollView
		{
			TranslatesAutoresizingMaskIntoConstraints = false,
			AlwaysBounceVertical = false,
		};
		panel.AddSubview(scroll);
		scroll.AddSubview(stack);
		View!.AddSubview(panel);
		contentWizard = panel;
		var constraints = new List<NSLayoutConstraint>
		{
			panel.LeadingAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.LeadingAnchor, 24),
			panel.TrailingAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.TrailingAnchor, -24),
			panel.TopAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.TopAnchor, 12),
			panel.BottomAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.BottomAnchor, -12),
			panel.TopAnchor.ConstraintGreaterThanOrEqualTo(View.SafeAreaLayoutGuide.TopAnchor, 12),
			panel.BottomAnchor.ConstraintLessThanOrEqualTo(View.SafeAreaLayoutGuide.BottomAnchor, -12),
			scroll.LeadingAnchor.ConstraintEqualTo(panel.LeadingAnchor, 24),
			scroll.TrailingAnchor.ConstraintEqualTo(panel.TrailingAnchor, -24),
			scroll.TopAnchor.ConstraintEqualTo(panel.TopAnchor, 24),
			scroll.BottomAnchor.ConstraintEqualTo(panel.BottomAnchor, -24),
			stack.LeadingAnchor.ConstraintEqualTo(scroll.ContentLayoutGuide.LeadingAnchor),
			stack.TrailingAnchor.ConstraintEqualTo(scroll.ContentLayoutGuide.TrailingAnchor),
			stack.TopAnchor.ConstraintEqualTo(scroll.ContentLayoutGuide.TopAnchor),
			stack.BottomAnchor.ConstraintEqualTo(scroll.ContentLayoutGuide.BottomAnchor),
			stack.WidthAnchor.ConstraintEqualTo(scroll.FrameLayoutGuide.WidthAnchor),
		};
		if (model.State != IosContentImportState.Importing)
			constraints.Add(buttons.HeightAnchor.ConstraintGreaterThanOrEqualTo(58));
		NSLayoutConstraint.ActivateConstraints(constraints.ToArray());
	}

	public void DismissContentImportWizard()
	{
		contentWizard?.RemoveFromSuperview();
		contentWizard = null;
	}

	static UILabel RequiredFileStatus(string filename, bool imported)
	{
		var color = imported ? UIColor.FromRGB(92, 214, 115) : UIColor.FromRGB(231, 92, 92);
		var state = imported ? "已导入" : "未导入";
		return Label($"● {filename}  {state}", UIFontTextStyle.Headline, color);
	}

	static UILabel Label(string text, UIFontTextStyle style, UIColor color)
	{
		return new UILabel
		{
			Text = text,
			TextColor = color,
			Font = UIFont.GetPreferredFontForTextStyle(style),
			AdjustsFontForContentSizeCategory = true,
		};
	}

	static UIButton Button(string title, Action action)
	{
		var button = new UIButton(UIButtonType.System);
		button.SetTitle(title, UIControlState.Normal);
		button.SetTitleColor(UIColor.White, UIControlState.Normal);
		button.BackgroundColor = UIColor.FromRGB(49, 58, 68);
		button.Layer.BorderColor = UIColor.FromRGB(190, 57, 39).CGColor;
		button.Layer.BorderWidth = 1;
		button.Layer.CornerRadius = 8;
		button.TitleLabel!.Font = UIFont.GetPreferredFontForTextStyle(UIFontTextStyle.Headline);
		button.TitleLabel.AdjustsFontForContentSizeCategory = true;
		button.TitleLabel.Lines = 2;
		button.TitleLabel.TextAlignment = UITextAlignment.Center;
		button.TitleLabel.LineBreakMode = UILineBreakMode.WordWrap;
		button.TouchUpInside += (_, _) => action();
		return button;
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
		View.BringSubviewToFront(status);
    }
}
