# iOS Duplicate Drag Selection Design

## Problem

One finger drag-selection produces an initially offset selection rectangle, then a correctly positioned rectangle, and both flicker. SDL emits both native finger events and compatibility mouse events for the same iOS touch. OpenRA currently translates the native events through `TouchGestureAdapter` while also accepting the synthesized mouse events, creating two competing drag streams with different coordinate conversion.

## Solution

On iOS only, ignore mouse button, motion, and wheel events whose device id equals SDL's reserved touch-mouse id (`uint.MaxValue`). Continue processing native finger events through `TouchGestureAdapter`, and continue accepting real external mouse/trackpad events with ordinary device ids. Desktop behavior remains unchanged.

The platform-neutral predicate lives in `OpenRA.Game` so it can be unit tested without loading SDL native libraries. `Sdl2Input` applies it at each mouse event entry point before changing button state or queuing motion.

## Verification

Tests cover iOS synthesized-event rejection, iOS physical-mouse preservation, and desktop compatibility. Device testing must confirm one stable selection rectangle whose origin remains under the starting finger position.
