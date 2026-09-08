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
using System.Runtime.InteropServices;
using System.Text;
using OpenRA.Widgets;
using SDL2;

namespace OpenRA.Platforms.Default
{
	sealed class Sdl2Input
	{
		MouseButton lastButtonBits = MouseButton.None;
		MouseButton touchButtonBits = MouseButton.None;
		readonly TouchGestureAdapter touchGestures = new TouchGestureAdapter();
		TouchPointerButtonState touchPointerButtons;
		bool touchDown;
		bool twoFingerTouch;
		bool threeFingerTouch;
		bool ignoringRemainingTouch;
		long touchFingerId;
		long secondTouchFingerId;
		long thirdTouchFingerId;
		long ignoredTouchFingerId;
		int ignoredTouchReleaseCount;

		public static string GetClipboardText() { return SDL.SDL_GetClipboardText(); }
		public static bool SetClipboardText(string text) { return SDL.SDL_SetClipboardText(text) == 0; }

		static MouseButton MakeButton(byte b)
		{
			return b == SDL.SDL_BUTTON_LEFT ? MouseButton.Left
				: b == SDL.SDL_BUTTON_RIGHT ? MouseButton.Right
				: b == SDL.SDL_BUTTON_MIDDLE ? MouseButton.Middle
				: 0;
		}

		static Modifiers MakeModifiers(int raw)
		{
			return ((raw & (int)SDL.SDL_Keymod.KMOD_ALT) != 0 ? Modifiers.Alt : 0)
				 | ((raw & (int)SDL.SDL_Keymod.KMOD_CTRL) != 0 ? Modifiers.Ctrl : 0)
				 | ((raw & (int)SDL.SDL_Keymod.KMOD_LGUI) != 0 ? Modifiers.Meta : 0)
				 | ((raw & (int)SDL.SDL_Keymod.KMOD_RGUI) != 0 ? Modifiers.Meta : 0)
				 | ((raw & (int)SDL.SDL_Keymod.KMOD_SHIFT) != 0 ? Modifiers.Shift : 0);
		}

		static int2 EventPosition(Sdl2PlatformWindow device, int x, int y)
		{
			// On Windows and Linux (X11) events are given in surface coordinates
			// These must be scaled to our effective window coordinates
			// Round fractional components up to avoid rounding small deltas to 0
			if (Platform.CurrentPlatform != PlatformType.OSX && device.EffectiveWindowSize != device.SurfaceSize)
			{
				var s = 1 / device.EffectiveWindowScale;
				return new int2((int)(Math.Sign(x) / 2f + x * s), (int)(Math.Sign(x) / 2f + y * s));
			}

			// On macOS we must still account for the user-requested scale modifier
			if (Platform.CurrentPlatform == PlatformType.OSX && device.EffectiveWindowScale != device.NativeWindowScale)
			{
				var s = device.NativeWindowScale / device.EffectiveWindowScale;
				return new int2((int)(Math.Sign(x) / 2f + x * s), (int)(Math.Sign(x) / 2f + y * s));
			}

			return new int2(x, y);
		}

		void DispatchTouch(TouchGestureResult result, IInputHandler inputHandler, Modifiers mods)
		{
			for (var i = 0; i < result.Count; i++)
			{
				var action = i == 0 ? result.First : i == 1 ? result.Second : result.Third;
				var button = touchPointerButtons.Resolve(action.Type,
					Game.Settings.Game.UseClassicMouseStyle, Game.Settings.Game.UseAlternateScrollButton);
				var inputEvent = MouseInputEvent.Move;
				var multiTapCount = 0;

				switch (action.Type)
				{
					case TouchPointerActionType.LeftDown:
						inputEvent = MouseInputEvent.Down;
						inputHandler.OnMouseInput(new MouseInput(MouseInputEvent.Move, MouseButton.None,
							action.Position, int2.Zero, mods, 0));
						touchButtonBits |= button;
						multiTapCount = MultiTapDetection.DetectFromMouse((byte)SDL.SDL_BUTTON_LEFT, action.Position);
						break;
					case TouchPointerActionType.LeftMove:
						break;
					case TouchPointerActionType.LeftUp:
						inputEvent = MouseInputEvent.Up;
						multiTapCount = MultiTapDetection.InfoFromMouse((byte)SDL.SDL_BUTTON_LEFT);
						touchButtonBits &= ~button;
						break;
					case TouchPointerActionType.LeftCancel:
						inputEvent = MouseInputEvent.Cancel;
						MultiTapDetection.CancelFromMouse((byte)SDL.SDL_BUTTON_LEFT);
						touchButtonBits &= ~button;
						break;
					case TouchPointerActionType.RightDown:
						inputEvent = MouseInputEvent.Down;
						inputHandler.OnMouseInput(new MouseInput(MouseInputEvent.Move, MouseButton.None,
							action.Position, int2.Zero, mods, 0));
						touchButtonBits |= button;
						break;
					case TouchPointerActionType.RightUp:
						inputEvent = MouseInputEvent.Up;
						touchButtonBits &= ~button;
						break;
					case TouchPointerActionType.MiddleDown:
						inputEvent = MouseInputEvent.Down;
						inputHandler.OnMouseInput(new MouseInput(MouseInputEvent.Move, MouseButton.None,
							action.Position, int2.Zero, mods, 0));
						touchButtonBits |= button;
						break;
					case TouchPointerActionType.MiddleMove:
						break;
					case TouchPointerActionType.MiddleUp:
						inputEvent = MouseInputEvent.Up;
						touchButtonBits &= ~button;
						break;
					case TouchPointerActionType.Scroll:
						inputEvent = MouseInputEvent.Scroll;
						break;
					default:
						continue;
				}

				var actionModifiers = action.Type == TouchPointerActionType.Scroll ?
					mods | Game.Settings.Game.ZoomModifier : mods;
				inputHandler.OnMouseInput(new MouseInput(inputEvent, button,
					action.Position, action.Delta, actionModifiers, multiTapCount));
			}
		}

		void ResetTouchInput(IInputHandler inputHandler, Modifiers mods, string reason)
		{
			var hadTouchState = touchDown || twoFingerTouch || threeFingerTouch || ignoringRemainingTouch;
			TouchPressFeedback.End();
			DispatchTouch(touchGestures.Cancel(), inputHandler, mods);
			if (hadTouchState)
				inputHandler.OnMouseInput(new MouseInput(MouseInputEvent.Cancel, MouseButton.None,
					int2.Zero, int2.Zero, mods, 0));

			touchDown = false;
			twoFingerTouch = false;
			threeFingerTouch = false;
			ignoringRemainingTouch = false;
			touchFingerId = 0;
			secondTouchFingerId = 0;
			thirdTouchFingerId = 0;
			ignoredTouchFingerId = 0;
			ignoredTouchReleaseCount = 0;
			touchButtonBits = MouseButton.None;
			touchPointerButtons.Reset();

			if (hadTouchState && OperatingSystem.IsIOS())
				Log.Write("debug", $"Reset interrupted iOS touch input: {reason}");
		}

		public void PumpInput(Sdl2PlatformWindow device, IInputHandler inputHandler, int2? lockedMousePosition)
		{
			var mods = MakeModifiers((int)SDL.SDL_GetModState());
			inputHandler.ModifierKeys(mods);
			var acceptTouchInput = inputHandler is not NullInputHandler;
			if (OperatingSystem.IsIOS() && !acceptTouchInput)
				ResetTouchInput(inputHandler, mods, "input temporarily disabled");

			MouseInput? pendingMotion = null;

			while (SDL.SDL_PollEvent(out var e) != 0)
			{
				switch (e.type)
				{
					case SDL.SDL_EventType.SDL_FINGERDOWN:
					{
						if (!OperatingSystem.IsIOS() || !acceptTouchInput)
							break;
						if (ignoringRemainingTouch)
							ResetTouchInput(inputHandler, mods, "new finger after incomplete multi-touch release");

						// SDL reports iOS touch coordinates normalized to [0, 1].
						// Translate the first finger into OpenRA's existing left-mouse path.
						if (!touchDown)
						{
							touchDown = true;
							touchFingerId = e.tfinger.fingerId;
							var pos = TouchGestureAdapter.MapPosition(e.tfinger.x, e.tfinger.y, device.EffectiveWindowSize);
							touchGestures.Begin(pos, SDL.SDL_GetTicks64());
							inputHandler.OnMouseInput(new MouseInput(MouseInputEvent.Move, MouseButton.None,
								pos, int2.Zero, mods, 0));
							TouchPressFeedback.Begin(pos, Ui.MouseOverWidget);
						}
						else if (!twoFingerTouch && e.tfinger.fingerId != touchFingerId)
						{
							TouchPressFeedback.End();
							twoFingerTouch = true;
							secondTouchFingerId = e.tfinger.fingerId;
							var pos = TouchGestureAdapter.MapPosition(e.tfinger.x, e.tfinger.y, device.EffectiveWindowSize);
							DispatchTouch(touchGestures.BeginSecond(pos, SDL.SDL_GetTicks64()), inputHandler, mods);
						}
						else if (twoFingerTouch && !threeFingerTouch && e.tfinger.fingerId != touchFingerId &&
							e.tfinger.fingerId != secondTouchFingerId)
						{
							TouchPressFeedback.End();
							threeFingerTouch = true;
							thirdTouchFingerId = e.tfinger.fingerId;
							var pos = TouchGestureAdapter.MapPosition(e.tfinger.x, e.tfinger.y, device.EffectiveWindowSize);
							DispatchTouch(touchGestures.BeginThird(pos), inputHandler, mods);
						}

						break;
					}

					case SDL.SDL_EventType.SDL_FINGERMOTION:
					{
						if (!OperatingSystem.IsIOS() || !acceptTouchInput)
							break;
						if (ignoringRemainingTouch)
							break;

						if (touchDown && threeFingerTouch)
						{
							var pos = TouchGestureAdapter.MapPosition(e.tfinger.x, e.tfinger.y, device.EffectiveWindowSize);
							if (e.tfinger.fingerId == touchFingerId)
								DispatchTouch(touchGestures.MoveThreeFingerPrimary(pos), inputHandler, mods);
							else if (e.tfinger.fingerId == secondTouchFingerId)
								DispatchTouch(touchGestures.MoveThreeFingerSecond(pos), inputHandler, mods);
							else if (e.tfinger.fingerId == thirdTouchFingerId)
								DispatchTouch(touchGestures.MoveThird(pos), inputHandler, mods);
						}
						else if (touchDown && twoFingerTouch)
						{
							var pos = TouchGestureAdapter.MapPosition(e.tfinger.x, e.tfinger.y, device.EffectiveWindowSize);
							var timestamp = SDL.SDL_GetTicks64();
							if (e.tfinger.fingerId == touchFingerId)
								DispatchTouch(touchGestures.MovePrimary(pos, timestamp), inputHandler, mods);
							else if (e.tfinger.fingerId == secondTouchFingerId)
								DispatchTouch(touchGestures.MoveSecond(pos, timestamp), inputHandler, mods);
						}
						else if (touchDown && e.tfinger.fingerId == touchFingerId)
						{
							var pos = TouchGestureAdapter.MapPosition(e.tfinger.x, e.tfinger.y, device.EffectiveWindowSize);
							TouchPressFeedback.Move(pos);
							inputHandler.OnMouseInput(new MouseInput(MouseInputEvent.Move, MouseButton.None,
								pos, int2.Zero, mods, 0));
							DispatchTouch(touchGestures.Move(pos), inputHandler, mods);
						}

						break;
					}

					case SDL.SDL_EventType.SDL_FINGERUP:
					{
						if (!OperatingSystem.IsIOS() || !acceptTouchInput)
							break;
						if (ignoringRemainingTouch)
						{
							if (ignoredTouchReleaseCount > 0)
							{
								ignoredTouchReleaseCount--;
								if (ignoredTouchReleaseCount == 0)
									ignoringRemainingTouch = false;
							}
							else if (e.tfinger.fingerId == ignoredTouchFingerId)
								ignoringRemainingTouch = false;
							break;
						}

						if (touchDown && threeFingerTouch &&
							(e.tfinger.fingerId == touchFingerId || e.tfinger.fingerId == secondTouchFingerId ||
							e.tfinger.fingerId == thirdTouchFingerId))
						{
							DispatchTouch(touchGestures.EndThreeFinger(), inputHandler, mods);
							ignoringRemainingTouch = true;
							ignoredTouchFingerId = 0;
							ignoredTouchReleaseCount = 2;
							threeFingerTouch = false;
							twoFingerTouch = false;
							touchDown = false;
						}
						else if (touchDown && twoFingerTouch &&
							(e.tfinger.fingerId == touchFingerId || e.tfinger.fingerId == secondTouchFingerId))
						{
							DispatchTouch(touchGestures.EndTwoFinger(), inputHandler, mods);
							ignoringRemainingTouch = true;
							ignoredTouchFingerId = e.tfinger.fingerId == touchFingerId ?
								secondTouchFingerId : touchFingerId;
							twoFingerTouch = false;
							touchDown = false;
						}
						else if (touchDown && e.tfinger.fingerId == touchFingerId)
						{
							var pos = TouchGestureAdapter.MapPosition(e.tfinger.x, e.tfinger.y, device.EffectiveWindowSize);
							TouchPressFeedback.End();
							DispatchTouch(touchGestures.End(pos), inputHandler, mods);
							touchDown = false;
						}

						break;
					}

					case SDL.SDL_EventType.SDL_QUIT:
						// SDL's UIKit backend posts a quit event while it transfers the
						// initial application window to UIKit. The UIApplication lifecycle
						// owns termination on iOS, so treating this as a game exit closes
						// OpenRA immediately after it starts.
						if (OperatingSystem.IsIOS())
							break;

						// On macOS, we'd like to restrict Cmd + Q from suddenly exiting the game.
						if (Platform.CurrentPlatform != PlatformType.OSX || !mods.HasModifier(Modifiers.Meta))
							Game.Exit();

						break;

					case SDL.SDL_EventType.SDL_WINDOWEVENT:
					{
						switch (e.window.windowEvent)
						{
							case SDL.SDL_WindowEventID.SDL_WINDOWEVENT_FOCUS_LOST:
								ResetTouchInput(inputHandler, mods, "window focus lost");
								lastButtonBits = MouseButton.None;
								device.HasInputFocus = false;
								break;

							case SDL.SDL_WindowEventID.SDL_WINDOWEVENT_FOCUS_GAINED:
								device.HasInputFocus = true;
								break;

							// Triggered when moving between displays with different DPI settings
							case SDL.SDL_WindowEventID.SDL_WINDOWEVENT_RESIZED:
							case SDL.SDL_WindowEventID.SDL_WINDOWEVENT_SIZE_CHANGED:
								device.WindowSizeChanged();
								break;

							case SDL.SDL_WindowEventID.SDL_WINDOWEVENT_HIDDEN:
							case SDL.SDL_WindowEventID.SDL_WINDOWEVENT_MINIMIZED:
								if (OperatingSystem.IsIOS())
									ResetTouchInput(inputHandler, mods, "window hidden or minimized");
								lastButtonBits = MouseButton.None;
								device.IsSuspended = true;
								break;

							case SDL.SDL_WindowEventID.SDL_WINDOWEVENT_EXPOSED:
							case SDL.SDL_WindowEventID.SDL_WINDOWEVENT_SHOWN:
							case SDL.SDL_WindowEventID.SDL_WINDOWEVENT_MAXIMIZED:
							case SDL.SDL_WindowEventID.SDL_WINDOWEVENT_RESTORED:
								device.IsSuspended = false;
								break;
						}

						break;
					}

					case SDL.SDL_EventType.SDL_MOUSEBUTTONDOWN:
					case SDL.SDL_EventType.SDL_MOUSEBUTTONUP:
					{
						if (TouchMouseEventFilter.ShouldIgnore(OperatingSystem.IsIOS(), e.button.which))
							break;

						// Mouse 1, Mouse 2 and Mouse 3 are handled as mouse inputs
						// Mouse 4 and Mouse 5 are treated as (pseudo) keyboard inputs
						if (e.button.button == SDL.SDL_BUTTON_LEFT ||
							e.button.button == SDL.SDL_BUTTON_MIDDLE ||
							e.button.button == SDL.SDL_BUTTON_RIGHT)
						{
							if (pendingMotion != null)
							{
								inputHandler.OnMouseInput(pendingMotion.Value);
								pendingMotion = null;
							}

							var button = MakeButton(e.button.button);

							if (e.type == SDL.SDL_EventType.SDL_MOUSEBUTTONDOWN)
								lastButtonBits |= button;
							else
								lastButtonBits &= ~button;

							var input = lockedMousePosition ?? new int2(e.button.x, e.button.y);
							var pos = EventPosition(device, input.X, input.Y);

							if (e.type == SDL.SDL_EventType.SDL_MOUSEBUTTONDOWN)
								inputHandler.OnMouseInput(new MouseInput(
									MouseInputEvent.Down, button, pos, int2.Zero, mods,
									MultiTapDetection.DetectFromMouse(e.button.button, pos)));
							else
								inputHandler.OnMouseInput(new MouseInput(
									MouseInputEvent.Up, button, pos, int2.Zero, mods,
									MultiTapDetection.InfoFromMouse(e.button.button)));
						}

						if (e.button.button == SDL.SDL_BUTTON_X1 ||
							e.button.button == SDL.SDL_BUTTON_X2)
						{
							Keycode keyCode;

							if (e.button.button == SDL.SDL_BUTTON_X1)
								keyCode = Keycode.MOUSE4;
							else
								keyCode = Keycode.MOUSE5;

							var type = e.type == SDL.SDL_EventType.SDL_MOUSEBUTTONDOWN ?
								KeyInputEvent.Down : KeyInputEvent.Up;

							var tapCount = e.type == SDL.SDL_EventType.SDL_MOUSEBUTTONDOWN ?
								MultiTapDetection.DetectFromKeyboard(keyCode, mods) :
								MultiTapDetection.InfoFromKeyboard(keyCode, mods);

							var keyEvent = new KeyInput
							{
								Event = type,
								Key = keyCode,
								Modifiers = mods,
								UnicodeChar = '?',
								MultiTapCount = tapCount,
								IsRepeat = e.key.repeat != 0
							};
							inputHandler.OnKeyInput(keyEvent);
						}

						break;
					}

					case SDL.SDL_EventType.SDL_MOUSEMOTION:
					{
						if (TouchMouseEventFilter.ShouldIgnore(OperatingSystem.IsIOS(), e.motion.which))
							break;

						var mousePos = new int2(e.motion.x, e.motion.y);
						var input = lockedMousePosition ?? mousePos;
						var pos = EventPosition(device, input.X, input.Y);

						var delta = lockedMousePosition == null
							? EventPosition(device, e.motion.xrel, e.motion.yrel)
							: mousePos - lockedMousePosition.Value;

						pendingMotion = new MouseInput(
							MouseInputEvent.Move, lastButtonBits | touchButtonBits, pos, delta, mods, 0);

						break;
					}

					case SDL.SDL_EventType.SDL_MOUSEWHEEL:
					{
						if (TouchMouseEventFilter.ShouldIgnore(OperatingSystem.IsIOS(), e.wheel.which))
							break;

						SDL.SDL_GetMouseState(out var x, out var y);

						var pos = EventPosition(device, x, y);
						inputHandler.OnMouseInput(new MouseInput(MouseInputEvent.Scroll, MouseButton.None, pos, new int2(0, e.wheel.y), mods, 0));

						break;
					}

					case SDL.SDL_EventType.SDL_TEXTINPUT:
					{
						var rawBytes = new byte[SDL.SDL_TEXTINPUTEVENT_TEXT_SIZE];
						unsafe { Marshal.Copy((IntPtr)e.text.text, rawBytes, 0, SDL.SDL_TEXTINPUTEVENT_TEXT_SIZE); }
						inputHandler.OnTextInput(Encoding.UTF8.GetString(rawBytes, 0, rawBytes.IndexOf((byte)0)));
						break;
					}

					case SDL.SDL_EventType.SDL_KEYDOWN:
					case SDL.SDL_EventType.SDL_KEYUP:
					{
						var keyCode = (Keycode)e.key.keysym.sym;
						var type = e.type == SDL.SDL_EventType.SDL_KEYDOWN ?
							KeyInputEvent.Down : KeyInputEvent.Up;

						var tapCount = e.type == SDL.SDL_EventType.SDL_KEYDOWN ?
							MultiTapDetection.DetectFromKeyboard(keyCode, mods) :
							MultiTapDetection.InfoFromKeyboard(keyCode, mods);

						var keyEvent = new KeyInput
						{
							Event = type,
							Key = keyCode,
							Modifiers = mods,
							UnicodeChar = (char)e.key.keysym.sym,
							MultiTapCount = tapCount,
							IsRepeat = e.key.repeat != 0
						};

						// Special case workaround for windows users
						if (e.key.keysym.sym == SDL.SDL_Keycode.SDLK_F4 && mods.HasModifier(Modifiers.Alt) &&
							Platform.CurrentPlatform == PlatformType.Windows)
							Game.Exit();
						else
							inputHandler.OnKeyInput(keyEvent);

						break;
					}
				}
			}

			if (OperatingSystem.IsIOS())
				DispatchTouch(touchGestures.Poll(SDL.SDL_GetTicks64()), inputHandler, mods);

			if (pendingMotion != null)
				inputHandler.OnMouseInput(pendingMotion.Value);
		}
	}
}
