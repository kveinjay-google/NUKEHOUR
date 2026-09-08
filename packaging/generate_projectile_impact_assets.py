#!/usr/bin/env python3
"""Generate the deterministic project-original projectile impact overlays."""

import argparse
import math
import random
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter, PngImagePlugin


ROOT = Path(__file__).resolve().parents[1]
DEFAULT_OUTPUT_DIR = ROOT / "mods" / "ra2" / "bits" / "animations"

FRAME_SIZE = 128
FRAME_COUNT = 8
FRAMES_PER_ROW = 8
GUTTER = 6
DRAW_SCALE = 2
SEED = 0x4E55434C454152

WARM = {
    "glow": (255, 72, 12),
    "mid": (255, 156, 28),
    "hot": (255, 246, 202),
    "edge": (255, 190, 72),
}
TESLA = {
    "glow": (48, 92, 255),
    "mid": (70, 214, 255),
    "hot": (226, 251, 255),
    "edge": (146, 118, 255),
}


def rgba(color, alpha):
    return (*color, max(0, min(255, round(alpha))))


def scaled(value):
    return round(value * DRAW_SCALE)


def transparent_frame():
    return Image.new("RGBA", (FRAME_SIZE * DRAW_SCALE, FRAME_SIZE * DRAW_SCALE), (0, 0, 0, 0))


def composite_disc(frame, center, radius, color, alpha, blur):
    layer = transparent_frame()
    draw = ImageDraw.Draw(layer)
    cx, cy = center
    bounds = (
        scaled(cx - radius),
        scaled(cy - radius),
        scaled(cx + radius),
        scaled(cy + radius),
    )
    draw.ellipse(bounds, fill=rgba(color, alpha))
    if blur > 0:
        layer = layer.filter(ImageFilter.GaussianBlur(scaled(blur)))
    return Image.alpha_composite(frame, layer)


def finish_frame(frame):
    frame = frame.resize((FRAME_SIZE, FRAME_SIZE), Image.Resampling.LANCZOS)
    draw = ImageDraw.Draw(frame)
    draw.rectangle((0, 0, FRAME_SIZE - 1, GUTTER - 1), fill=(0, 0, 0, 0))
    draw.rectangle((0, FRAME_SIZE - GUTTER, FRAME_SIZE - 1, FRAME_SIZE - 1), fill=(0, 0, 0, 0))
    draw.rectangle((0, 0, GUTTER - 1, FRAME_SIZE - 1), fill=(0, 0, 0, 0))
    draw.rectangle((FRAME_SIZE - GUTTER, 0, FRAME_SIZE - 1, FRAME_SIZE - 1), fill=(0, 0, 0, 0))
    return frame


def core_frame(index, colors, variant_seed):
    progress = index / (FRAME_COUNT - 1)
    envelope = 1 - progress * 0.82
    pulse = math.sin(progress * math.pi)
    rng = random.Random(SEED + variant_seed + index)
    center = (64 + rng.uniform(-0.8, 0.8), 65 + rng.uniform(-0.5, 0.5))

    frame = transparent_frame()
    frame = composite_disc(
        frame,
        center,
        17 + 31 * progress,
        colors["glow"],
        154 * envelope,
        6 + 2 * progress,
    )
    frame = composite_disc(
        frame,
        center,
        9 + 22 * pulse,
        colors["mid"],
        225 * envelope,
        2.8,
    )
    frame = composite_disc(
        frame,
        center,
        5 + 13 * pulse,
        colors["hot"],
        255 * envelope,
        1.2,
    )

    spark_layer = transparent_frame()
    draw = ImageDraw.Draw(spark_layer)
    for spark in range(7):
        angle = rng.uniform(0, math.tau)
        distance = (8 + 28 * progress) * rng.uniform(0.7, 1.15)
        length = rng.uniform(2.5, 7.0) * (1 - progress * 0.45)
        x0 = center[0] + math.cos(angle) * distance
        y0 = center[1] + math.sin(angle) * distance * 0.72
        x1 = x0 + math.cos(angle) * length
        y1 = y0 + math.sin(angle) * length * 0.72
        draw.line(
            (scaled(x0), scaled(y0), scaled(x1), scaled(y1)),
            fill=rgba(colors["edge"], 210 * envelope),
            width=scaled(1.3),
        )

    frame = Image.alpha_composite(frame, spark_layer)
    return finish_frame(frame)


def ring_frame(index, colors):
    progress = index / (FRAME_COUNT - 1)
    radius_x = 13 + 43 * progress
    radius_y = 5 + 20 * progress
    center_y = 72 + 3 * progress
    envelope = 1 - progress * 0.78

    glow = transparent_frame()
    glow_draw = ImageDraw.Draw(glow)
    bounds = (
        scaled(64 - radius_x),
        scaled(center_y - radius_y),
        scaled(64 + radius_x),
        scaled(center_y + radius_y),
    )
    glow_draw.ellipse(bounds, outline=rgba(colors["glow"], 205 * envelope), width=scaled(5.5))
    glow = glow.filter(ImageFilter.GaussianBlur(scaled(3.2)))

    ring = transparent_frame()
    ring_draw = ImageDraw.Draw(ring)
    ring_draw.ellipse(bounds, outline=rgba(colors["hot"], 245 * envelope), width=scaled(1.8))
    inset = scaled(2.2)
    inner_bounds = (bounds[0] + inset, bounds[1] + inset, bounds[2] - inset, bounds[3] - inset)
    ring_draw.ellipse(inner_bounds, outline=rgba(colors["edge"], 180 * envelope), width=scaled(1.2))

    return finish_frame(Image.alpha_composite(glow, ring))


def particle_parameters(particle):
    rng = random.Random(SEED + 1000 + particle * 97)
    return {
        "angle": rng.uniform(0, math.tau),
        "distance": rng.uniform(13, 42),
        "lift": rng.uniform(15, 36),
        "radius": rng.uniform(4.5, 10.5),
        "delay": rng.uniform(0, 0.28),
        "tone": rng.randrange(3),
    }


def debris_frame(index):
    progress = index / (FRAME_COUNT - 1)
    frame = transparent_frame()
    smoke = transparent_frame()
    draw = ImageDraw.Draw(smoke)
    smoke_colors = ((89, 73, 64), (123, 91, 61), (62, 64, 68))

    for particle in range(16):
        parameters = particle_parameters(particle)
        local = max(0.0, min(1.0, (progress - parameters["delay"]) / (1 - parameters["delay"])))
        if local <= 0:
            continue

        distance = parameters["distance"] * (0.25 + 0.75 * local)
        x = 64 + math.cos(parameters["angle"]) * distance
        y = 75 + math.sin(parameters["angle"]) * distance * 0.44 - parameters["lift"] * local
        radius = parameters["radius"] * (0.55 + local * 0.9)
        alpha = 150 * (0.18 + 0.82 * math.sin(local * math.pi)) * (1 - progress * 0.5)
        bounds = (
            scaled(x - radius),
            scaled(y - radius * 0.78),
            scaled(x + radius),
            scaled(y + radius * 0.78),
        )
        draw.ellipse(bounds, fill=rgba(smoke_colors[parameters["tone"]], alpha))

    smoke = smoke.filter(ImageFilter.GaussianBlur(scaled(2.0 + progress * 1.2)))
    frame = Image.alpha_composite(frame, smoke)

    sparks = transparent_frame()
    spark_draw = ImageDraw.Draw(sparks)
    for spark in range(12):
        rng = random.Random(SEED + 5000 + spark * 131)
        angle = rng.uniform(math.pi * 0.08, math.pi * 0.92)
        speed = rng.uniform(28, 53)
        x = 64 + math.cos(angle) * speed * progress
        y = 73 - math.sin(angle) * speed * progress + 48 * progress * progress
        alpha = 240 * (1 - progress) ** 1.4
        length = 2 + 5 * (1 - progress)
        spark_draw.line(
            (scaled(x), scaled(y), scaled(x - math.cos(angle) * length), scaled(y + math.sin(angle) * length)),
            fill=rgba((255, 164, 54), alpha),
            width=scaled(1.2),
        )

    frame = Image.alpha_composite(frame, sparks)
    return finish_frame(frame)


def make_sheet(frames):
    rows = (len(frames) + FRAMES_PER_ROW - 1) // FRAMES_PER_ROW
    sheet = Image.new(
        "RGBA",
        (FRAME_SIZE * FRAMES_PER_ROW, FRAME_SIZE * rows),
        (0, 0, 0, 0),
    )
    for index, frame in enumerate(frames):
        position = (index % FRAMES_PER_ROW * FRAME_SIZE, index // FRAMES_PER_ROW * FRAME_SIZE)
        sheet.alpha_composite(frame, position)
    return sheet


def save_sheet(path, frames):
    metadata = PngImagePlugin.PngInfo()
    metadata.add_text("FrameSize", f"{FRAME_SIZE},{FRAME_SIZE}", zip=False)
    metadata.add_text("FrameAmount", str(len(frames)), zip=False)
    make_sheet(frames).save(
        path,
        format="PNG",
        pnginfo=metadata,
        optimize=False,
        compress_level=9,
    )


def generate(output_dir):
    output_dir.mkdir(parents=True, exist_ok=True)
    warm_core = [core_frame(index, WARM, 0) for index in range(FRAME_COUNT)]
    tesla_core = [core_frame(index, TESLA, 100) for index in range(FRAME_COUNT)]
    warm_ring = [ring_frame(index, WARM) for index in range(FRAME_COUNT)]
    tesla_ring = [ring_frame(index, TESLA) for index in range(FRAME_COUNT)]
    debris = [debris_frame(index) for index in range(FRAME_COUNT)]

    save_sheet(output_dir / "nc-impact-core.png", warm_core + tesla_core)
    save_sheet(output_dir / "nc-impact-ring.png", warm_ring + tesla_ring)
    save_sheet(output_dir / "nc-impact-debris.png", debris)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--output-dir",
        type=Path,
        default=DEFAULT_OUTPUT_DIR,
        help="directory that receives the three deterministic PNG sprite sheets",
    )
    args = parser.parse_args()
    generate(args.output_dir)


if __name__ == "__main__":
    main()
