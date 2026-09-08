# Projectile Impact Overlay Asset Provenance

## Classification

The three files listed below are **Project original** clean-room assets generated
inside this repository. They are distributed with the project under the GNU
General Public License version 3 or any later version described in
`docs/legal/PUBLIC_DISTRIBUTION.md`.

- `mods/ra2/bits/animations/nc-impact-core.png`
- `mods/ra2/bits/animations/nc-impact-ring.png`
- `mods/ra2/bits/animations/nc-impact-debris.png`

This classification applies only to these three generated overlays. It does not
reclassify any existing game sequence, sound, sprite, model, palette, or YAML
file, and it does not grant permission to redistribute player-imported content.

## Clean-room method

`packaging/generate_projectile_impact_assets.py` constructs every pixel from
fixed project colors, mathematical ellipses, deterministic local particle
coordinates, alpha compositing, and a fixed pseudo-random seed. The generator
has no external raster input and does not read retail files, reference images,
fonts, logos, characters, game palettes, or extracted animation frames.

The warm frames depict an abstract white-hot flash, shock ring, and smoke/debris
layer. The alternate core and ring frames use an original cyan/violet electrical
color treatment. None of the shapes trace or reproduce an existing game sprite.

## Reproduction

From the repository root, regenerate the canonical files with:

```sh
python3 packaging/generate_projectile_impact_assets.py
```

The generator writes RGBA PNG sheets with uncompressed PNG `tEXt` metadata for
`FrameSize` and `FrameAmount`. Repeated isolated runs produce byte-identical
outputs. The asset test verifies exact output names, deterministic bytes, frame
geometry, transparent gutters, non-empty frames, and absence of file-reading or
download dependencies.

## Review boundary

The generated overlays are designed to be layered at runtime with main
explosions, sounds, and water splashes imported by each player from a legally
owned game copy. Those imported resources are not inputs to this generator and
must not be copied into a public App, IPA, installer, or source archive.
