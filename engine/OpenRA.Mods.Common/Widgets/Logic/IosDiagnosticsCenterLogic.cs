using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	public sealed class IosDiagnosticsCenterLogic : ChromeLogic
	{
		[ObjectCreator.UseCtor]
		public IosDiagnosticsCenterLogic(Widget widget)
		{
			var button = widget.Get<ButtonWidget>("SAVE_DIAGNOSTICS");
			button.IsVisible = () => Platform.IsIOS;
			button.OnClick = () => HangDiagnostics.SaveSnapshot("user-marked-problem");
		}
	}
}
