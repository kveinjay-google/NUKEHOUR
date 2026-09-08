# iOS Positional Audio Distance Design

## Goal

Make world sounds on iOS become quieter as the camera moves away from their source, matching the desktop OpenAL backend. Music, video, UI sounds, notifications, and other listener-relative audio must keep their current volume behavior.

## Root Cause

The engine already passes each world sound's `WPos`, its `relative` flag, and the current viewport center to `ISoundEngine`. The iOS `Sdl2SoundEngine` currently discards all three pieces of spatial state:

- `Play2D` does not pass `relative` or `pos` into `PcmAudioMixer`.
- `SetListenerPosition` is empty.
- `SetSoundPosition` and `Sdl2Sound.SetPosition` are empty.

`PcmAudioMixer` therefore multiplies every playback only by its source and master volumes. A world sound is mixed at the same gain regardless of its distance from the camera.

## Reference Behavior

The desktop `OpenAlSoundEngine` is the compatibility reference:

- Listener position: viewport center with a Z offset of 2133 world units.
- Reference distance: 6826 world units.
- Maximum distance: 136533 world units.
- Rolloff factor: OpenAL default of 1.
- Distance model: inverse-distance clamped.

For a non-relative source, iOS will compute:

```text
distance = length(sourcePosition - listenerPositionWithZOffset)
clampedDistance = clamp(distance, referenceDistance, maximumDistance)
distanceGain = referenceDistance /
    (referenceDistance + rolloff * (clampedDistance - referenceDistance))
```

Sources within the reference distance remain at full gain. Beyond it, gain falls smoothly using the same curve as desktop OpenAL and remains clamped at the desktop maximum-distance result.

## Architecture

### Playback spatial state

Extend `PcmPlayback` with:

- Source world position.
- Whether the source is listener-relative.

The existing non-spatial `Play` API remains available for current callers and defaults to listener-relative behavior. A spatial overload or optional arguments will be used by the iOS sound backend.

### Mixer listener state

`PcmAudioMixer` stores the current listener position and exposes thread-safe methods to update the listener and an active playback's source position. The existing mixer synchronization protects these updates from the dedicated iOS audio mixing thread.

During each mix buffer, the mixer calculates one distance gain per playback, not once per sample. This keeps the hot audio loop allocation-free and makes already-playing and looping sounds respond to camera movement.

### iOS backend wiring

`Sdl2SoundEngine.Play2D` forwards `relative` and `pos` to the mixer. `SetListenerPosition` forwards the viewport center. `SetSoundPosition` and `Sdl2Sound.SetPosition` update the active playback position so moving actors remain spatially correct.

The current queued-audio thread and audio recovery changes are preserved; this work only adds spatial state and gain calculation.

## Audio Categories

- Non-relative world sounds use distance attenuation.
- Relative sounds remain at full configured gain regardless of camera position.
- Music and video streams remain non-spatial.
- UI and speech notifications that are intentionally relative remain non-spatial.
- Master volume and per-sound volume continue to multiply the distance gain.

Stereo panning is outside this change. Existing left/right channel content is preserved, and the same distance gain is applied to both output channels.

## Threading and Failure Behavior

- Listener and source position updates must not race with the queued SDL mixing thread.
- The mixer performs no allocation, logging, or file access in the audio hot path.
- Invalid or completed sound handles are ignored using the existing null/completion behavior.
- A relative sound never reads spatial state, preventing UI audio from changing unexpectedly.

## Tests

Automated mixer tests will verify:

1. A non-relative source within the desktop reference distance plays at full gain.
2. A non-relative source at twice the reference distance uses the desktop inverse-distance gain.
3. A source beyond the desktop maximum distance uses the clamped maximum-distance gain.
4. A relative source is unaffected by listener distance.
5. Moving the listener changes the gain of an already-playing looped source.
6. Moving an active source changes its gain.
7. Existing mono/stereo conversion, looping, stopping, and volume tests continue to pass.

## Acceptance Criteria

- World sounds near the viewport play normally and fade smoothly as the camera moves away.
- Returning the camera toward an active looping source raises its volume again.
- Moving world sources track their actor position.
- Music, UI sounds, and relative voice notifications do not fade with map distance.
- The iOS curve matches the constants and inverse-distance-clamped formula used by desktop OpenAL.
- The iOS audio thread remains stable with no new per-buffer allocations.

## Non-Goals

- Adding stereo positional panning or HRTF.
- Changing RA2 sound rule data or global sound volume settings.
- Replacing SDL queued audio or the recovered iOS audio service.
- Changing desktop OpenAL behavior.
