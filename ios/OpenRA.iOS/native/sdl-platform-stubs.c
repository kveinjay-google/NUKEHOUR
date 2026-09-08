// iOS does not implement SDL's Android, GDK, or Direct3D entry points.
// SDL2-CS exposes these symbols for all platforms, so static linking needs
// harmless definitions even though OpenRA never calls them on iOS.
void SDL_AndroidBackButton(void) {}
void SDL_AndroidGetActivity(void) {}
void SDL_AndroidGetExternalStoragePath(void) {}
void SDL_AndroidGetExternalStorageState(void) {}
void SDL_AndroidGetInternalStoragePath(void) {}
void SDL_AndroidGetJNIEnv(void) {}
void SDL_AndroidRequestPermission(void) {}
void SDL_AndroidShowToast(void) {}
void SDL_GDKRunApp(void) {}
void SDL_GetAndroidSDKVersion(void) {}
void SDL_IsAndroidTV(void) {}
void SDL_IsChromebook(void) {}
void SDL_IsDeXMode(void) {}
void SDL_RenderGetD3D11Device(void) {}
void SDL_RenderGetD3D9Device(void) {}
void SDL_SetWindowsMessageHook(void) {}
void SDL_WinRTGetDeviceFamily(void) {}
void SDL_WinRTRunApp(void) {}

// Keep OpenRA's SDL UIKit framebuffer accessor linked into the final app.
// The managed runtime resolves this bridge from __Internal, while this C-level
// reference ensures the symbol is pulled from libSDL2.a.
extern unsigned int OpenRA_SDL_GetDrawableFramebuffer(void);
extern void OpenRA_SDL_GetDrawableSize(int *width, int *height);
unsigned int OpenRA_iOS_GetDrawableFramebuffer(void)
{
	return OpenRA_SDL_GetDrawableFramebuffer();
}

void OpenRA_iOS_GetDrawableSize(int *width, int *height)
{
	OpenRA_SDL_GetDrawableSize(width, height);
}
