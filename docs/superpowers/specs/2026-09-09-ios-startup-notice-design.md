# iOS Startup Notice and Content Import Design

Date: 2026-09-09
Status: Approved

## Goal

Turn the existing iOS retail-content startup gate into a clear two-column launch screen that always presents NUKE HOUR's independent-project, clean-distribution, free/open-source, anti-resale, and safe-download notices before entering the game.

## Scope

This change is limited to the native iOS/iPadOS startup content-import screen. It does not change retail-content validation, imported-content storage, multiplayer compatibility, gameplay, macOS, Android, or the deferred in-game About page.

## Layout

The startup panel remains inside the landscape safe area and uses two responsive columns:

- The left column contains the current content-check title, explanation, readiness status, and import/retry/enter-game actions.
- The right column is an `Important Notice` card containing the legal and safety copy.
- Only the notice body scrolls when the text does not fit on a compact landscape iPhone. The website action, version label, and launch/import actions remain visible.
- The official website is a prominent system button opening `https://nukehour.com` in the external browser.
- The version appears at the panel's bottom-right and is read at runtime from `CFBundleShortVersionString` and `CFBundleVersion`. The visible format is `NUKE HOUR <version> (Build <build>)`; it never contains `RA2`.
- Dynamic Type, multi-line labels, safe areas, and at least 48-point action heights remain supported.

## Copy and Trademark Boundary

English and Simplified Chinese copy live in the existing immutable `IosNativeStrings` tables.

The notice states:

1. NUKE HOUR is an independent, free, open-source community project.
2. `EA has not endorsed and does not support this product.`
3. The project is not affiliated with, sponsored by, authorized by, or officially partnered with Electronic Arts Inc., Apple Inc., or Google LLC.
4. The app contains no third-party retail game files. Users must import files from a legally purchased and owned copy.
5. `Command & Conquer: Red Alert 2` and `Yuri's Revenge` are named only to describe compatibility and identify the files to import.
6. Claims that an independently sold copy is an officially authorized NUKE HOUR edition are false; purchasers should report the seller and request a refund.
7. Users should download only from the official NUKE HOUR website to reduce malicious-code and tampering risk.
8. A compact attribution line identifies the respective EA, Apple, and Google word marks.

No EA, Command & Conquer, Red Alert, Apple, or Google logo, official artwork, trade dress, or promotional branding is added. Third-party names remain plain-text functional references and never become part of the NUKE HOUR app name.

## Startup Behavior

The content coordinator always shows the native startup screen after readiness inspection. A fully ready installation no longer auto-enters the game. Both `Base Game Ready` and `Ready` states expose `Enter Game`, so the user makes an explicit transition after the notice has been presented.

Checking and importing disable actions that would conflict with the active operation. Missing-content and error states retain file selection and retry recovery. The existing licenses action remains available and shows the full notice text in its current modal form.

## Link and Error Handling

The website URL is a fixed HTTPS constant, never localization or user input. The controller opens it through the iOS system URL handler on the main thread. If the system cannot open it, the app remains on the startup screen without crashing or bypassing the content gate.

Missing bundle version values fall back independently to `0.0.0` and `0`, producing a stable non-empty label without exposing internal exceptions.

## Tests

Source-contract tests are added before implementation and must prove:

- the fixed HTTPS official website exists and is wired to the system URL opener;
- the English and Chinese tables contain the independent-project, EA non-endorsement, clean-content, lawful-import, free/open-source, anti-resale, and safe-download statements;
- the version label is bundle-derived, bottom-right constrained, and excludes `RA2`;
- the notice body uses a scroll view while the website and version remain outside it;
- both ready states expose `Enter Game` and the coordinator no longer automatically starts after readiness inspection;
- existing PublicClean, localization, importer-safety, and iOS build audits continue to pass.

Final verification runs the focused iOS content-import tests, the complete managed test suite, and an iOS ARM64 Release build. Installation on a connected device is outside this change unless explicitly requested after the build.
