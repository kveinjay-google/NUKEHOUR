# NUKE HOUR Icon Provenance

Adopted: 2026-08-28

## Source

- Repository source: `branding/NUKE-HOUR-source-1254.png`
- User-supplied attachment path at adoption: `/var/folders/7n/f7n6bv290dj9lbz5yr2wk4pw0000gn/T/codex-clipboard-7e1dbe1f-07b3-4295-84f4-182c0e0297c8.png`
- Format: PNG, 1254×1254 pixels, opaque RGB
- Source SHA-256: `af3a739efcd5eefd02c0a622b60dcb6a88cd52b224813f53df8284b60a19e99b`
- Canonical 1024×1024 master SHA-256: `2ec7920648f7bf7ec10f19b15a72040c5931a04c51a20dd79ba473565391fa72`

The user supplied this artwork and explicitly authorized its use and redistribution as the NUKE HOUR game and application icon. This record does not claim that the artwork is an Electronic Arts retail asset or that it was originally created by this project.

## Deterministic conversion

Run from the repository root with Python 3.11 and exactly Pillow 11.3.0:

```sh
python3 packaging/generate_nuke_hour_icons.py \
  --root /absolute/path/to/ra2-mac \
  --source /absolute/path/to/ra2-mac/branding/NUKE-HOUR-source-1254.png
python3 packaging/generate_nuke_hour_icons.py \
  --root /absolute/path/to/ra2-mac \
  --check
```

The generator verifies the immutable source hash, dimensions, format, and RGB mode before writing. It removes inherited metadata, declares sRGB, downsamples the complete uncropped image to a 1024×1024 RGB master with Pillow `Image.Resampling.LANCZOS`, and derives every PNG from that master with fixed PNG settings.

The generator creates one temporary Apple-standard ten-slot macOS iconset, invokes `/usr/bin/iconutil` once to create a single ICNS file, and fails closed if the tool reports any error. The resulting ICNS bytes are copied unchanged to all three tracked consumers. Generation and verification must run with normal macOS Mach/XPC access; a restricted process sandbox can prevent `iconutil` from validating an otherwise valid iconset.

Use `--list-outputs` for the exact sorted derivative allowlist and `--list-mutations` for the exact sorted union of desired outputs and removed legacy catalog images. The immutable source itself is intentionally absent from the mutation list.
