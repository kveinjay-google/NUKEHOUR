# iOS Positional Audio Distance Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Apply the desktop OpenAL inverse-distance-clamped gain curve to positional sounds mixed by the iOS PCM backend.

**Architecture:** Store listener-relative state and source position on each `PcmPlayback`, and keep the listener position in `PcmAudioMixer`. Calculate one gain per playback per mix buffer under the existing mixer lock, then wire the iOS SDL backend's listener and sound-position methods to that state.

**Tech Stack:** C# 12, .NET 8, NUnit, SDL2 queued audio, OpenRA `WPos`/`WVec` primitives.

---

### Task 1: Specify the desktop attenuation curve in mixer tests

**Files:**
- Modify: `engine/OpenRA.Test/PcmAudioMixerTest.cs`

- [ ] **Step 1: Add a reusable full-scale looping sample helper**

Add a helper that starts a mono 16-bit looping playback and mixes one stereo frame so the same playback can be sampled after listener/source movement.

- [ ] **Step 2: Add failing distance tests**

Cover full gain inside 6826 units, approximately 0.5 gain at 13652 units, clamped gain beyond 136533 units, and no attenuation for a listener-relative source. Use a listener at `WPos.Zero` and sources with a Z value of 2133 so the tests isolate horizontal distance while matching the desktop listener-height offset.

- [ ] **Step 3: Add failing live-update tests**

Start a positional looping playback, mix it, move the listener, mix it again, then move the source and mix again. Assert that the existing playback changes gain without restarting.

- [ ] **Step 4: Run only the mixer fixture and verify failure**

Run:

```bash
dotnet test engine/OpenRA.Test/OpenRA.Test.csproj --filter FullyQualifiedName~PcmAudioMixerTest --no-restore
```

Expected: compile failures because the spatial `Play`, `SetListenerPosition`, and `SetPlaybackPosition` APIs do not exist yet.

### Task 2: Implement allocation-free positional gain in the PCM mixer

**Files:**
- Modify: `engine/OpenRA.Game/Sound/PcmAudioMixer.cs`
- Test: `engine/OpenRA.Test/PcmAudioMixerTest.cs`

- [ ] **Step 1: Add desktop-compatible constants and spatial state**

Add constants for listener height 2133, reference distance 6826, maximum distance 136533, and rolloff 1. Store the listener position on the mixer. Store `Relative` and `Position` on `PcmPlayback`.

- [ ] **Step 2: Preserve the existing non-spatial API**

Keep the current six-argument `Play` method and delegate it to a new overload using `relative: true` and `WPos.Zero`. Add a spatial overload:

```csharp
public PcmPlayback Play(byte[] data, int channels, int sampleBits, int sampleRate,
	bool loop, float volume, bool relative, WPos position)
```

- [ ] **Step 3: Add synchronized position updates**

Add `SetListenerPosition(WPos position)` that stores `position + new WVec(0, 0, 2133)`, and `SetPlaybackPosition(PcmPlayback playback, WPos position)`. Both methods lock the existing `sync` object and ignore null playbacks.

- [ ] **Step 4: Apply one gain per mix buffer**

At the start of `MixSound`, calculate 1 for relative sources. For positional sources, calculate the `WVec.Length`, clamp it to the desktop reference/maximum range, and use:

```csharp
var gain = ReferenceDistance /
	(float)(ReferenceDistance + RolloffFactor * (distance - ReferenceDistance));
```

Multiply samples by this gain alongside the existing playback and master volumes. Do not allocate, log, or acquire another lock inside the sample loop.

- [ ] **Step 5: Run the mixer fixture**

Run the Task 1 test command. Expected: all `PcmAudioMixerTest` cases pass, including the pre-existing conversion, looping, stopping, and volume cases.

### Task 3: Wire iOS sound objects to the spatial mixer

**Files:**
- Modify: `ios/OpenRA.Platforms.iOS/Sdl2SoundEngine.cs`

- [ ] **Step 1: Make `Sdl2Sound` update its active playback**

Give `Sdl2Sound` a `PcmAudioMixer` reference and implement `SetPosition` by calling `SetPlaybackPosition`.

- [ ] **Step 2: Forward spatial arguments for buffered and streamed playback**

Extend the private `Add` method with `relative` and `WPos pos`, pass them to the spatial mixer overload, and update both `Play2D` and `Play2DStream` callers. Keep diagnostics, cleanup, and queued-audio thread behavior unchanged.

- [ ] **Step 3: Forward live listener and source updates**

Implement:

```csharp
public void SetListenerPosition(WPos position) => mixer.SetListenerPosition(position);
public void SetSoundPosition(ISound sound, WPos position)
{
	if (sound is Sdl2Sound s)
		s.SetPosition(position);
}
```

- [ ] **Step 4: Build the iOS platform project**

Run:

```bash
dotnet build ios/OpenRA.Platforms.iOS/OpenRA.Platforms.iOS.csproj -c Release -p:RuntimeIdentifier=ios-arm64 --no-restore
```

Expected: build succeeds with no new warnings or errors from the spatial-audio changes.

### Task 4: Regression verification

**Files:**
- Verify: `engine/OpenRA.Game/Sound/PcmAudioMixer.cs`
- Verify: `ios/OpenRA.Platforms.iOS/Sdl2SoundEngine.cs`
- Verify: `engine/OpenRA.Test/PcmAudioMixerTest.cs`

- [ ] **Step 1: Run the complete OpenRA test project**

Run:

```bash
dotnet test engine/OpenRA.Test/OpenRA.Test.csproj --no-restore
```

Expected: all tests pass, or any unrelated pre-existing failure is recorded with its exact test name and output.

- [ ] **Step 2: Check formatting and diff scope**

Run `git diff --check` on the three implementation files and confirm the pre-existing queued-audio recovery changes remain present.

- [ ] **Step 3: Commit only positional-audio files**

Commit the mixer, tests, iOS wiring, and this plan without staging unrelated worktree changes. Use commit message `Fix iOS positional audio attenuation`.
