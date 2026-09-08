#!/usr/bin/env python3
"""Generate deterministic NUKE HOUR macOS installer artwork."""

from pathlib import Path

from PIL import Image, ImageDraw, ImageEnhance, ImageFont, ImageOps


ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "mods" / "ra2" / "uibits" / "NUCLEAR-CRISIS-BG-06.png"
FONT = ROOT / "mods" / "ra2" / "fonts" / "NotoSansCJKsc-Bold.otf"
BRAND_TITLE = "NUKE HOUR"
OUTPUTS = (
	((600, 450), ROOT / "packaging" / "artwork" / "macos-background.png"),
	((1200, 900), ROOT / "packaging" / "artwork" / "macos-background-2x.png"),
)


def render_dmg_background(source: Image.Image, size: tuple[int, int]) -> Image.Image:
	"""Render one branded 4:3 DMG background from the canonical wallpaper."""
	canvas = ImageOps.fit(
		source.convert("RGB"),
		size,
		method=Image.Resampling.LANCZOS,
		centering=(0.5, 0.5),
	)
	canvas = ImageEnhance.Brightness(canvas).enhance(0.58).convert("RGBA")

	width, height = size
	draw = ImageDraw.Draw(canvas, "RGBA")
	font = ImageFont.truetype(str(FONT), max(24, round(width * 0.062)))
	title = BRAND_TITLE
	box = draw.textbbox((0, 0), title, font=font, stroke_width=max(1, width // 600))
	text_width = box[2] - box[0]
	text_height = box[3] - box[1]
	x = (width - text_width) // 2
	y = max(round(height * 0.07), 18)
	padding_x = round(width * 0.035)
	padding_y = round(height * 0.018)
	draw.rounded_rectangle(
		(
			x - padding_x,
			y - padding_y,
			x + text_width + padding_x,
			y + text_height + padding_y,
		),
		radius=max(8, width // 50),
		fill=(5, 8, 12, 174),
		outline=(180, 38, 28, 210),
		width=max(1, width // 600),
	)
	draw.text(
		(x, y - box[1]),
		title,
		font=font,
		fill=(246, 214, 121, 255),
		stroke_width=max(1, width // 600),
		stroke_fill=(0, 0, 0, 230),
	)

	# Preserve the standard drag-to-Applications affordance between the two
	# Finder icon positions configured in mod.config.
	arrow_y = round(height * 0.51)
	arrow_start = round(width * 0.43)
	arrow_end = round(width * 0.57)
	arrow_width = max(3, round(width * 0.008))
	arrow_head = max(10, round(width * 0.022))
	draw.line(
		(arrow_start, arrow_y, arrow_end, arrow_y),
		fill=(246, 214, 121, 220),
		width=arrow_width,
	)
	draw.polygon(
		(
			(arrow_end, arrow_y),
			(arrow_end - arrow_head, arrow_y - arrow_head),
			(arrow_end - arrow_head, arrow_y + arrow_head),
		),
		fill=(246, 214, 121, 220),
	)
	return canvas


def main() -> None:
	with Image.open(SOURCE) as source:
		for size, output in OUTPUTS:
			output.parent.mkdir(parents=True, exist_ok=True)
			render_dmg_background(source, size).save(output, "PNG", optimize=True)


if __name__ == "__main__":
	main()
