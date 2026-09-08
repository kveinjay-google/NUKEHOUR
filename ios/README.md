# OpenRA iPad host

This directory contains the native iOS host for the existing OpenRA/RA2 engine. The game rules, resource parsers, renderer, and mod are reused from the Mac project; the iOS-specific code supplies the host lifecycle, static platform registration, native SDL2/FreeType libraries, sandbox paths, and basic touch-to-mouse input.

Build an Apple Silicon simulator target with:

```sh
DOTNET_ROOT="$HOME/.dotnet" PATH="$HOME/.dotnet:$PATH" \
  dotnet build ios/OpenRA.iOS/OpenRA.iOS.csproj \
  -f net8.0-ios -p:RuntimeIdentifier=iossimulator-arm64 \
  -p:CodesignDisable=true -p:TargetPlatform=ios
```

For a physical arm64 iPad, use `RuntimeIdentifier=ios-arm64` with a development provisioning profile configured in Xcode. The current machine has the iOS SDK and Apple Development certificate, but no installed Simulator runtime or provisioning profile, so local verification is currently limited to the AOT/link/build stage.

The native archives under `OpenRA.iOS/native` are the device and Apple Silicon simulator builds of SDL2 and FreeType. `libSDL2-platform-stubs.a` only resolves unused cross-platform SDL entry points that are exposed by SDL2-CS; it does not provide game functionality.
