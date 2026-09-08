using OpenRA.Graphics;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets
{
	public sealed class VirtualViewportJoystickWidget : Widget, ITouchFactionSkinTarget
	{
		public readonly int Radius = 50;
		public readonly float DeadZone = 0.15f;
		public readonly float MaxSpeed = 18f;

		readonly WorldRenderer worldRenderer;
		readonly VirtualViewportJoystickState state;
		Sprite baseSprite;
		Sprite thumbSprite;
		Sprite activeThumbSprite;
		string imageCollection = "ios-touch-joystick-allies";
		int thumbDiameter = 48;

		[ObjectCreator.UseCtor]
		public VirtualViewportJoystickWidget(WorldRenderer worldRenderer)
		{
			this.worldRenderer = worldRenderer;
			state = new VirtualViewportJoystickState(Radius, DeadZone);
			CacheSprites();
			IsVisible = ControlsVisible;
		}

		public void ApplySkin(string skin)
		{
			var collection = TouchFactionSkin.JoystickCollection(skin);
			if (collection == imageCollection)
				return;

			imageCollection = collection;
			CacheSprites();
		}

		void CacheSprites()
		{
			baseSprite = ChromeProvider.GetImage(imageCollection, "base");
			thumbSprite = ChromeProvider.GetImage(imageCollection, "thumb");
			activeThumbSprite = ChromeProvider.GetImage(imageCollection, "thumb-active");
		}

		bool ControlsVisible()
		{
			var visible = Platform.IsIOS && IosFloatingControlsPreferences.Visible;
			if (!visible)
				CancelActiveInput();

			return visible;
		}

		public void ApplyLayout(Rectangle bounds, int thumbDiameter, int movementRadius)
		{
			var parentOrigin = Parent == null ? int2.Zero : Parent.ChildOrigin;
			var newBounds = new WidgetBounds(
				bounds.X - parentOrigin.X, bounds.Y - parentOrigin.Y, bounds.Width, bounds.Height);
			var normalizedThumbDiameter = System.Math.Max(1, thumbDiameter);
			var normalizedMovementRadius = System.Math.Max(1, movementRadius);
			if (SameBounds(Bounds, newBounds) && this.thumbDiameter == normalizedThumbDiameter &&
				state.Radius == normalizedMovementRadius)
				return;

			CancelActiveInput();
			state.SetRadius(normalizedMovementRadius);
			Bounds = newBounds;
			this.thumbDiameter = normalizedThumbDiameter;
		}

		public void CancelActiveInput()
		{
			state.End();
			if (HasMouseFocus)
				base.YieldMouseFocus(default);
		}

		static bool SameBounds(WidgetBounds a, WidgetBounds b) =>
			a.X == b.X && a.Y == b.Y && a.Width == b.Width && a.Height == b.Height;

		static bool InscribedCircleContains(Rectangle bounds, int2 location)
		{
			if (bounds.Width <= 0 || bounds.Height <= 0)
				return false;

			var diameter = (double)System.Math.Min(bounds.Width, bounds.Height);
			var dx = 2.0 * location.X - (2.0 * bounds.X + bounds.Width);
			var dy = 2.0 * location.Y - (2.0 * bounds.Y + bounds.Height);
			return dx * dx + dy * dy <= diameter * diameter;
		}

		public override bool EventBoundsContains(int2 location)
		{
			if (InscribedCircleContains(RenderBounds, location))
				return true;

			foreach (var child in Children)
				if (child.IsVisible() && child.EventBoundsContains(location))
					return true;

			return false;
		}

		public override bool HandleMouseInput(MouseInput mi)
		{
			if (!ControlsVisible())
				return false;

			if (mi.Event == MouseInputEvent.Down && mi.Button == MouseButton.Left)
			{
				if (!TakeMouseFocus(mi))
					return false;

				var bounds = RenderBounds;
				state.Begin(new int2(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2));
				state.Move(mi.Location);
				return true;
			}

			if (HasMouseFocus && mi.Event == MouseInputEvent.Move)
			{
				state.Move(mi.Location);
				return true;
			}

			if (HasMouseFocus && mi.Event == MouseInputEvent.Up)
			{
				state.End();
				YieldMouseFocus(mi);
				return true;
			}

			return state.Active;
		}

		public override void Draw()
		{
			if (!ControlsVisible())
				return;

			var bounds = RenderBounds;
			WidgetUtils.DrawSprite(baseSprite, new float2(bounds.X, bounds.Y), bounds.Size);
			var center = new int2(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2) + state.ThumbOffset;
			var currentThumbSprite = state.Active ? activeThumbSprite : thumbSprite;
			WidgetUtils.DrawSprite(currentThumbSprite,
				new float2(center.X - thumbDiameter / 2f, center.Y - thumbDiameter / 2f),
				new Size(thumbDiameter, thumbDiameter));

			if (state.Speed > 0)
				worldRenderer.Viewport.Scroll(new float2(state.Direction.X, state.Direction.Y) * state.Speed * MaxSpeed, false);

			base.Draw();
		}

		public override bool YieldMouseFocus(MouseInput mi)
		{
			state.End();
			return base.YieldMouseFocus(mi);
		}
	}
}
