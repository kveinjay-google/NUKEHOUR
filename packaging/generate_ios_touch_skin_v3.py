#!/usr/bin/env python3
"""Generate deterministic faction-specific iOS touch-control atlases.

The ImageGen style masters live in design-previews/ios-touch-controls-v3.  They
define the material language; this script redraws those motifs on the exact
runtime grid so the generated art has real transparency and stable hit-area
alignment.
"""

from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
from typing import Iterable, Sequence

from PIL import Image, ImageDraw


FACTIONS = ("allies", "soviets", "yuri")
ACTION_NAMES = ("stop", "deploy", "select-type", "force-attack", "return-base")
ACTION_STATES = (
    "normal",
    "disabled",
    "hover",
    "pressed",
    "active",
    "active-hover",
    "active-pressed",
)
QUICKBAR_STATES = (
    "normal",
    "hover",
    "pressed",
    "highlighted",
    "highlighted-hover",
    "highlighted-pressed",
    "disabled",
    "highlighted-disabled",
)
COMMAND_GLYPHS = (
    "group-1",
    "group-2",
    "group-3",
    "group-4",
    "group-5",
    "production-x5",
    "group-6",
    "group-7",
    "group-8",
    "group-9",
    "group-0",
    "select-all",
    "select-by-type",
    "cycle-base",
    "to-selection",
    "to-last-event",
    "cycle-harvesters",
    "remove-from-group",
    "attack-move",
    "force-move",
    "force-attack",
    "guard",
    "deploy",
    "scatter",
    "stop",
    "queue-orders",
    "attack-anything",
    "defend",
    "return-fire",
    "hold-fire",
    "sell",
    "repair",
    "beacon",
)


Color = tuple[int, int, int]


@dataclass(frozen=True)
class Skin:
    name: str
    base: Color
    recess: Color
    metal: Color
    edge: Color
    accent: Color
    active: Color
    warning: Color


SKINS = {
    "allies": Skin(
        "allies",
        (17, 31, 43),
        (5, 15, 25),
        (112, 139, 158),
        (205, 226, 233),
        (80, 207, 237),
        (151, 237, 255),
        (230, 171, 58),
    ),
    "soviets": Skin(
        "soviets",
        (31, 23, 21),
        (12, 10, 9),
        (119, 108, 96),
        (198, 184, 159),
        (164, 38, 31),
        (244, 80, 36),
        (229, 169, 52),
    ),
    "yuri": Skin(
        "yuri",
        (30, 15, 37),
        (12, 7, 17),
        (103, 76, 112),
        (190, 165, 198),
        (178, 74, 210),
        (221, 111, 255),
        (146, 205, 70),
    ),
}


def rgba(color: Color, alpha: int = 255) -> tuple[int, int, int, int]:
    return (*color, alpha)


def mix(first: Color, second: Color, amount: float) -> Color:
    return tuple(
        round(a + (b - a) * amount) for a, b in zip(first, second)
    )


def state_colors(skin: Skin, state: str) -> tuple[Color, Color, Color, int, int]:
    disabled = "disabled" in state
    active = "active" in state or "highlighted" in state
    hover = "hover" in state
    pressed = "pressed" in state
    base = mix(skin.base, skin.recess, 0.32 if pressed else 0.08)
    metal = mix(skin.metal, skin.edge, 0.24 if hover else 0.04)
    accent = skin.active if active else skin.accent
    if hover:
        accent = mix(accent, (255, 255, 255), 0.22)
    alpha = 128 if disabled else 255
    offset = 1 if pressed else 0
    if disabled:
        base = mix(base, (64, 64, 64), 0.55)
        metal = mix(metal, (83, 83, 83), 0.60)
        accent = mix(accent, (100, 100, 100), 0.72)
    return base, metal, accent, alpha, offset


def scaled_box(box: Sequence[float], scale: int) -> tuple[int, int, int, int]:
    return tuple(round(value * scale) for value in box)  # type: ignore[return-value]


def scaled_points(points: Iterable[tuple[float, float]], scale: int):
    return [(round(x * scale), round(y * scale)) for x, y in points]


def line(
    draw: ImageDraw.ImageDraw,
    points: Iterable[tuple[float, float]],
    fill,
    scale: int,
    width: float = 2,
):
    draw.line(scaled_points(points, scale), fill=fill, width=max(1, round(width * scale)), joint="curve")


def polygon(draw: ImageDraw.ImageDraw, points, fill, scale: int):
    draw.polygon(scaled_points(points, scale), fill=fill)


def ellipse(draw: ImageDraw.ImageDraw, box, fill, scale: int, outline=None, width: int = 1):
    draw.ellipse(scaled_box(box, scale), fill=fill, outline=outline, width=max(1, round(width * scale)))


def rectangle(draw: ImageDraw.ImageDraw, box, fill, scale: int, outline=None, width: int = 1):
    draw.rectangle(scaled_box(box, scale), fill=fill, outline=outline, width=max(1, round(width * scale)))


def rounded_rectangle(draw: ImageDraw.ImageDraw, box, radius: int, fill, scale: int, outline=None, width: int = 1):
    draw.rounded_rectangle(
        scaled_box(box, scale),
        radius=radius * scale,
        fill=fill,
        outline=outline,
        width=max(1, width * scale),
    )


def draw_rivet(draw: ImageDraw.ImageDraw, x: float, y: float, skin: Skin, alpha: int, scale: int):
    ellipse(draw, (x - 1.4, y - 1.4, x + 1.4, y + 1.4), rgba(skin.recess, alpha), scale)
    ellipse(draw, (x - 0.8, y - 0.8, x + 0.8, y + 0.8), rgba(skin.warning, alpha), scale)


def draw_round_shell(draw: ImageDraw.ImageDraw, skin: Skin, state: str, size: int, scale: int):
    base, metal, accent, alpha, offset = state_colors(skin, state)
    center = size / 2
    outer = (3, 3 + offset, size - 4, size - 4 + offset)
    ellipse(draw, outer, rgba(skin.recess, min(alpha, 218)), scale)

    if skin.name == "allies":
        ellipse(draw, (5, 5 + offset, size - 6, size - 6 + offset), rgba(metal, alpha), scale)
        ellipse(draw, (8, 8 + offset, size - 9, size - 9 + offset), rgba(skin.recess, alpha), scale)
        ellipse(draw, (10, 10 + offset, size - 11, size - 11 + offset), rgba(base, alpha), scale,
                outline=rgba(accent, alpha), width=2)
        for angle in (0, 90, 180, 270):
            if angle == 0:
                draw_rivet(draw, center, 6 + offset, skin, alpha, scale)
            elif angle == 90:
                draw_rivet(draw, size - 7, center + offset, skin, alpha, scale)
            elif angle == 180:
                draw_rivet(draw, center, size - 7 + offset, skin, alpha, scale)
            else:
                draw_rivet(draw, 6, center + offset, skin, alpha, scale)
    elif skin.name == "soviets":
        ellipse(draw, (4, 4 + offset, size - 5, size - 5 + offset), rgba(metal, alpha), scale)
        ellipse(draw, (7, 7 + offset, size - 8, size - 8 + offset), rgba(skin.recess, alpha), scale)
        ellipse(draw, (9, 9 + offset, size - 10, size - 10 + offset), rgba(base, alpha), scale,
                outline=rgba(accent, alpha), width=2)
        for x, y in ((center, 6), (size - 7, center), (center, size - 7), (6, center)):
            draw_rivet(draw, x, y + offset, skin, alpha, scale)
        line(draw, ((13, 12 + offset), (size - 13, 12 + offset)), rgba(skin.edge, alpha), scale, 1)
    else:
        spikes = (
            (center, 1 + offset), (center + 5, 6 + offset), (size - 6, 5 + offset),
            (size - 5, center - 5 + offset), (size - 1, center + offset),
            (size - 5, center + 5 + offset), (size - 6, size - 6 + offset),
            (center + 5, size - 6 + offset), (center, size - 1 + offset),
            (center - 5, size - 6 + offset), (5, size - 6 + offset),
            (4, center + 5 + offset), (0, center + offset), (4, center - 5 + offset),
            (5, 5 + offset), (center - 5, 6 + offset),
        )
        polygon(draw, spikes, rgba(metal, alpha), scale)
        ellipse(draw, (6, 6 + offset, size - 7, size - 7 + offset), rgba(skin.recess, alpha), scale)
        ellipse(draw, (9, 9 + offset, size - 10, size - 10 + offset), rgba(base, alpha), scale,
                outline=rgba(accent, alpha), width=2)
        for x, y in ((center, 5), (size - 6, center), (center, size - 6), (5, center)):
            ellipse(draw, (x - 1.5, y - 1.5 + offset, x + 1.5, y + 1.5 + offset), rgba(skin.warning, alpha), scale)

    return accent, alpha, offset


def draw_arrow(draw, cx, cy, direction, color, scale, length=12, width=3):
    dx, dy = direction
    start = (cx - dx * length * 0.45, cy - dy * length * 0.45)
    end = (cx + dx * length * 0.45, cy + dy * length * 0.45)
    line(draw, (start, end), color, scale, width)
    px, py = -dy, dx
    tip = end
    polygon(
        draw,
        (
            tip,
            (tip[0] - dx * 5 + px * 4, tip[1] - dy * 5 + py * 4),
            (tip[0] - dx * 5 - px * 4, tip[1] - dy * 5 - py * 4),
        ),
        color,
        scale,
    )


def draw_crosshair(draw, cx, cy, radius, color, scale, width=2):
    ellipse(draw, (cx - radius, cy - radius, cx + radius, cy + radius), None, scale, outline=color, width=width)
    line(draw, ((cx - radius - 4, cy), (cx - radius + 2, cy)), color, scale, width)
    line(draw, ((cx + radius - 2, cy), (cx + radius + 4, cy)), color, scale, width)
    line(draw, ((cx, cy - radius - 4), (cx, cy - radius + 2)), color, scale, width)
    line(draw, ((cx, cy + radius - 2), (cx, cy + radius + 4)), color, scale, width)


def draw_house(draw, cx, cy, color, scale, width=2):
    polygon(draw, ((cx - 8, cy - 1), (cx, cy - 8), (cx + 8, cy - 1)), color, scale)
    rectangle(draw, (cx - 6, cy - 1, cx + 6, cy + 8), color, scale)
    rectangle(draw, (cx - 1.5, cy + 3, cx + 1.5, cy + 8), rgba((18, 18, 20), 255), scale)


def draw_people(draw, cx, cy, color, scale):
    for ox, oy, radius in ((0, -6, 3), (-7, -3, 2.5), (7, -3, 2.5)):
        ellipse(draw, (cx + ox - radius, cy + oy - radius, cx + ox + radius, cy + oy + radius), color, scale)
    ellipse(draw, (cx - 6, cy - 2, cx + 6, cy + 9), color, scale)
    ellipse(draw, (cx - 12, cy + 1, cx - 3, cy + 9), color, scale)
    ellipse(draw, (cx + 3, cy + 1, cx + 12, cy + 9), color, scale)


def draw_action_glyph(draw, name: str, color, scale: int, offset: int = 0):
    cx, cy = 28, 28 + offset
    if name == "stop":
        polygon(draw, ((cx - 8, cy - 11), (cx + 8, cy - 11), (cx + 11, cy - 8),
                       (cx + 11, cy + 8), (cx + 8, cy + 11), (cx - 8, cy + 11),
                       (cx - 11, cy + 8), (cx - 11, cy - 8)), color, scale)
        rectangle(draw, (cx - 5, cy - 6, cx - 1.5, cy + 6), rgba((18, 18, 20), 255), scale)
        rectangle(draw, (cx + 1.5, cy - 6, cx + 5, cy + 6), rgba((18, 18, 20), 255), scale)
    elif name == "deploy":
        draw_arrow(draw, cx, cy - 2, (0, 1), color, scale, 18, 4)
        line(draw, ((cx - 10, cy + 9), (cx + 10, cy + 9)), color, scale, 3)
    elif name == "select-type":
        draw_people(draw, cx, cy, color, scale)
    elif name == "force-attack":
        draw_crosshair(draw, cx, cy, 8, color, scale, 2)
        polygon(draw, ((cx + 1, cy - 12), (cx + 8, cy - 3), (cx + 3, cy - 3),
                       (cx + 8, cy + 8), (cx - 1, cy - 1), (cx + 4, cy - 1)), color, scale)
    else:
        draw_house(draw, cx, cy + 2, color, scale)
        line(draw, ((cx - 12, cy - 5), (cx - 8, cy - 11), (cx, cy - 13), (cx + 7, cy - 10)), color, scale, 2)
        polygon(draw, ((cx + 7, cy - 14), (cx + 13, cy - 9), (cx + 6, cy - 7)), color, scale)


def action_atlas(skin: Skin, scale: int = 2) -> Image.Image:
    atlas = Image.new("RGBA", (512 * scale, 512 * scale), (0, 0, 0, 0))
    for row, state in enumerate(ACTION_STATES):
        for column, name in enumerate(ACTION_NAMES):
            cell = Image.new("RGBA", (56 * scale, 56 * scale), (0, 0, 0, 0))
            draw = ImageDraw.Draw(cell)
            accent, alpha, offset = draw_round_shell(draw, skin, state, 56, scale)
            draw_action_glyph(draw, name, rgba(accent, alpha), scale, offset)
            atlas.alpha_composite(cell, (column * 64 * scale, row * 64 * scale))
    return atlas


def draw_joystick_base(draw: ImageDraw.ImageDraw, skin: Skin, scale: int):
    size = 144
    base, metal, accent, alpha, _ = state_colors(skin, "normal")
    center = 72
    if skin.name == "yuri":
        points = []
        for index in range(32):
            import math
            angle = math.tau * index / 32 - math.pi / 2
            radius = 69 if index % 4 else 72
            points.append((center + math.cos(angle) * radius, center + math.sin(angle) * radius))
        polygon(draw, points, rgba(metal, 240), scale)
    else:
        ellipse(draw, (2, 2, 141, 141), rgba(metal, 240), scale)
    ellipse(draw, (7, 7, 136, 136), rgba(skin.recess, 245), scale)
    ellipse(draw, (12, 12, 131, 131), rgba(base, 245), scale, outline=rgba(accent, 255), width=2)
    ellipse(draw, (31, 31, 112, 112), rgba(skin.recess, 220), scale, outline=rgba(skin.edge, 220), width=2)
    ellipse(draw, (48, 48, 95, 95), rgba(base, 235), scale, outline=rgba(accent, 210), width=2)
    for dx, dy in ((0, -1), (1, 0), (0, 1), (-1, 0)):
        cx, cy = center + dx * 57, center + dy * 57
        px, py = -dy, dx
        polygon(draw, ((cx + dx * 6, cy + dy * 6),
                       (cx - dx * 3 + px * 5, cy - dy * 3 + py * 5),
                       (cx - dx * 3 - px * 5, cy - dy * 3 - py * 5)), rgba(skin.warning, 255), scale)
    if skin.name == "allies":
        line(draw, ((72, 34), (72, 47)), rgba(skin.active, 180), scale, 1)
        line(draw, ((72, 96), (72, 109)), rgba(skin.active, 180), scale, 1)
        line(draw, ((34, 72), (47, 72)), rgba(skin.active, 180), scale, 1)
        line(draw, ((96, 72), (109, 72)), rgba(skin.active, 180), scale, 1)
    elif skin.name == "soviets":
        for x, y in ((14, 14), (130, 14), (14, 130), (130, 130)):
            draw_rivet(draw, x, y, skin, 255, scale)
    else:
        for x, y in ((72, 7), (137, 72), (72, 137), (7, 72)):
            ellipse(draw, (x - 2.2, y - 2.2, x + 2.2, y + 2.2), rgba(skin.warning, 255), scale)


def draw_thumb(draw: ImageDraw.ImageDraw, skin: Skin, active: bool, scale: int):
    state = "active-hover" if active else "normal"
    accent, alpha, _ = draw_round_shell(draw, skin, state, 64, scale)
    draw_crosshair(draw, 32, 32, 8, rgba(accent, alpha), scale, 1)


def joystick_atlas(skin: Skin, scale: int = 2) -> Image.Image:
    atlas = Image.new("RGBA", (256 * scale, 256 * scale), (0, 0, 0, 0))
    base = Image.new("RGBA", (144 * scale, 144 * scale), (0, 0, 0, 0))
    draw_joystick_base(ImageDraw.Draw(base), skin, scale)
    atlas.alpha_composite(base, (0, 0))
    for y, active in ((0, False), (72, True)):
        thumb = Image.new("RGBA", (64 * scale, 64 * scale), (0, 0, 0, 0))
        draw_thumb(ImageDraw.Draw(thumb), skin, active, scale)
        atlas.alpha_composite(thumb, (160 * scale, y * scale))
    return atlas


def draw_square_shell(draw: ImageDraw.ImageDraw, skin: Skin, state: str, width: int, height: int, scale: int):
    base, metal, accent, alpha, offset = state_colors(skin, state)
    if skin.name == "allies":
        polygon(draw, ((5, 1 + offset), (width - 6, 1 + offset), (width - 2, 5 + offset),
                       (width - 2, height - 6 + offset), (width - 6, height - 2 + offset),
                       (5, height - 2 + offset), (1, height - 6 + offset), (1, 5 + offset)), rgba(metal, alpha), scale)
        rounded_rectangle(draw, (4, 4 + offset, width - 5, height - 5 + offset), 5,
                          rgba(base, alpha), scale, outline=rgba(accent, alpha), width=2)
    elif skin.name == "soviets":
        polygon(draw, ((4, 1 + offset), (width - 5, 1 + offset), (width - 2, 4 + offset),
                       (width - 2, height - 5 + offset), (width - 5, height - 2 + offset),
                       (4, height - 2 + offset), (1, height - 5 + offset), (1, 4 + offset)), rgba(metal, alpha), scale)
        rectangle(draw, (4, 4 + offset, width - 5, height - 5 + offset), rgba(base, alpha), scale,
                  outline=rgba(accent, alpha), width=2)
        for x, y in ((5, 5), (width - 6, 5), (5, height - 6), (width - 6, height - 6)):
            draw_rivet(draw, x, y + offset, skin, alpha, scale)
    else:
        polygon(draw, ((7, 0 + offset), (width - 8, 0 + offset), (width - 1, 8 + offset),
                       (width - 1, height - 9 + offset), (width - 8, height - 1 + offset),
                       (7, height - 1 + offset), (0, height - 9 + offset), (0, 8 + offset)), rgba(metal, alpha), scale)
        rounded_rectangle(draw, (4, 4 + offset, width - 5, height - 5 + offset), 7,
                          rgba(base, alpha), scale, outline=rgba(accent, alpha), width=2)
        for x, y in ((width / 2, 3), (width - 4, height / 2), (width / 2, height - 4), (3, height / 2)):
            ellipse(draw, (x - 1.2, y - 1.2 + offset, x + 1.2, y + 1.2 + offset), rgba(skin.warning, alpha), scale)
    return accent, alpha, offset


def draw_panel(draw: ImageDraw.ImageDraw, skin: Skin, compact: bool, scale: int):
    state = "normal"
    base, metal, accent, alpha, _ = state_colors(skin, state)
    if skin.name == "allies":
        rounded_rectangle(draw, (1, 5, 126, 58), 9, rgba(metal, 240), scale)
        rounded_rectangle(draw, (5, 9, 122, 54), 6, rgba(skin.recess, 245), scale,
                          outline=rgba(accent, 220), width=2)
    elif skin.name == "soviets":
        polygon(draw, ((8, 2), (119, 2), (126, 9), (126, 55), (119, 62),
                       (8, 62), (1, 55), (1, 9)), rgba(metal, 245), scale)
        rectangle(draw, (6, 7, 121, 57), rgba(skin.recess, 245), scale,
                  outline=rgba(skin.accent, 230), width=2)
        for x in (10, 118):
            draw_rivet(draw, x, 10, skin, 255, scale)
            draw_rivet(draw, x, 54, skin, 255, scale)
    else:
        polygon(draw, ((12, 1), (115, 1), (127, 13), (127, 51), (115, 63),
                       (12, 63), (0, 51), (0, 13)), rgba(metal, 245), scale)
        rounded_rectangle(draw, (6, 7, 121, 57), 9, rgba(skin.recess, 245), scale,
                          outline=rgba(accent, 230), width=2)
        for x, y in ((10, 10), (117, 10), (10, 54), (117, 54)):
            ellipse(draw, (x - 1.5, y - 1.5, x + 1.5, y + 1.5), rgba(skin.warning, 255), scale)
    if compact:
        line(draw, ((15, 32), (112, 32)), rgba(mix(base, accent, 0.5), 170), scale, 1)


def draw_chevrons(draw, direction: int, skin: Skin, scale: int):
    color = rgba(skin.active, 255)
    for offset in (-4, 4):
        cx = 13 + offset
        if direction < 0:
            line(draw, ((cx + 3, 7), (cx - 3, 13), (cx + 3, 19)), color, scale, 3)
        else:
            line(draw, ((cx - 3, 7), (cx + 3, 13), (cx - 3, 19)), color, scale, 3)


def quickbar_atlas(skin: Skin, scale: int = 2) -> Image.Image:
    atlas = Image.new("RGBA", (512 * scale, 128 * scale), (0, 0, 0, 0))
    for column, state in enumerate(QUICKBAR_STATES):
        cell = Image.new("RGBA", (48 * scale, 52 * scale), (0, 0, 0, 0))
        draw_square_shell(ImageDraw.Draw(cell), skin, state, 48, 52, scale)
        atlas.alpha_composite(cell, (column * 64 * scale, 0))
    for x, compact in ((0, False), (128, True)):
        cell = Image.new("RGBA", (128 * scale, 64 * scale), (0, 0, 0, 0))
        draw_panel(ImageDraw.Draw(cell), skin, compact, scale)
        atlas.alpha_composite(cell, (x * scale, 64 * scale))
    for x, direction in ((259, -1), (291, 1)):
        cell = Image.new("RGBA", (26 * scale, 26 * scale), (0, 0, 0, 0))
        draw_chevrons(ImageDraw.Draw(cell), direction, skin, scale)
        atlas.alpha_composite(cell, (x * scale, 67 * scale))
    return atlas


SEGMENTS = {
    "0": "abcedf",
    "1": "bc",
    "2": "abged",
    "3": "abgcd",
    "4": "fgbc",
    "5": "afgcd",
    "6": "afgecd",
    "7": "abc",
    "8": "abcdefg",
    "9": "abfgcd",
}


def draw_digit(draw, digit: str, x: float, y: float, color, scale: int, width=7, height=13, thickness=2):
    half = height / 2
    segments = {
        "a": (x + thickness, y, x + width - thickness, y + thickness),
        "b": (x + width - thickness, y + thickness, x + width, y + half - 1),
        "c": (x + width - thickness, y + half + 1, x + width, y + height - thickness),
        "d": (x + thickness, y + height - thickness, x + width - thickness, y + height),
        "e": (x, y + half + 1, x + thickness, y + height - thickness),
        "f": (x, y + thickness, x + thickness, y + half - 1),
        "g": (x + thickness, y + half - thickness / 2, x + width - thickness, y + half + thickness / 2),
    }
    for name in SEGMENTS[digit]:
        rectangle(draw, segments[name], color, scale)


def draw_number(draw, value: str, color, scale: int, y=9):
    digit_width = 7
    gap = 2
    total = len(value) * digit_width + (len(value) - 1) * gap
    x = (32 - total) / 2
    for digit in value:
        draw_digit(draw, digit, x, y, color, scale)
        x += digit_width + gap


def draw_small_plus(draw, cx, cy, color, scale, width=2):
    line(draw, ((cx - 4, cy), (cx + 4, cy)), color, scale, width)
    line(draw, ((cx, cy - 4), (cx, cy + 4)), color, scale, width)


def draw_command_glyph(draw: ImageDraw.ImageDraw, name: str, color, secondary, scale: int):
    cx = cy = 16
    if name.startswith("group-"):
        value = "10" if name == "group-0" else name.removeprefix("group-")
        polygon(draw, ((10, 6), (16, 3), (22, 6), (20, 8), (16, 6), (12, 8)), secondary, scale)
        draw_number(draw, value, color, scale, 10)
        return
    if name == "production-x5":
        for x, height in ((5, 6), (9, 10), (13, 14)):
            rectangle(draw, (x, 17 - height, x + 2, 17), color, scale)
        line(draw, ((18, 12), (23, 17)), color, scale, 1.6)
        line(draw, ((23, 12), (18, 17)), color, scale, 1.6)
        draw_digit(draw, "5", 24, 11, color, scale, 5, 9, 1.4)
    elif name == "select-all":
        draw_people(draw, cx, cy, color, scale)
        draw_crosshair(draw, cx, cy, 12, secondary, scale, 1)
    elif name == "select-by-type":
        for x in (9, 16, 23):
            ellipse(draw, (x - 3, 11, x + 3, 17), color, scale)
            line(draw, ((x - 4, 21), (x, 18), (x + 4, 21)), color, scale, 2)
    elif name == "cycle-base":
        draw_house(draw, cx, cy + 2, color, scale)
        line(draw, ((7, 11), (11, 6), (18, 4), (24, 8)), secondary, scale, 1.6)
        polygon(draw, ((23, 4), (28, 8), (22, 10)), secondary, scale)
    elif name == "to-selection":
        draw_crosshair(draw, cx, cy, 8, color, scale, 1.5)
        polygon(draw, ((16, 4), (20, 9), (17, 9), (17, 14), (15, 14), (15, 9), (12, 9)), secondary, scale)
    elif name == "to-last-event":
        polygon(draw, ((16, 3), (27, 25), (5, 25)), secondary, scale)
        rectangle(draw, (15, 10, 17, 18), rgba((20, 20, 20), 255), scale)
        ellipse(draw, (15, 20, 17, 22), rgba((20, 20, 20), 255), scale)
    elif name == "cycle-harvesters":
        polygon(draw, ((16, 5), (23, 12), (16, 19), (9, 12)), color, scale)
        line(draw, ((7, 21), (11, 26), (19, 27), (25, 21)), secondary, scale, 1.5)
        polygon(draw, ((22, 18), (28, 20), (25, 25)), secondary, scale)
    elif name == "remove-from-group":
        draw_number(draw, "1", color, scale, 7)
        line(draw, ((20, 21), (28, 21)), secondary, scale, 3)
    elif name == "attack-move":
        draw_crosshair(draw, 13, 18, 7, color, scale, 1.5)
        draw_arrow(draw, 21, 11, (0.75, -0.65), secondary, scale, 11, 2)
    elif name == "force-move":
        draw_arrow(draw, 16, 16, (0, -1), color, scale, 19, 3)
        line(draw, ((7, 25), (25, 25)), secondary, scale, 2)
    elif name == "force-attack":
        draw_crosshair(draw, cx, cy, 8, color, scale, 1.5)
        polygon(draw, ((18, 3), (24, 12), (20, 12), (24, 23), (15, 13), (19, 13)), secondary, scale)
    elif name == "guard":
        polygon(draw, ((16, 4), (25, 8), (24, 19), (16, 27), (8, 19), (7, 8)), color, scale)
        polygon(draw, ((16, 8), (21, 10), (20, 18), (16, 22), (12, 18), (11, 10)), rgba((18, 18, 20), 255), scale)
    elif name == "deploy":
        draw_arrow(draw, 16, 13, (0, 1), color, scale, 15, 3)
        line(draw, ((7, 23), (25, 23)), secondary, scale, 3)
    elif name == "scatter":
        for direction in ((0, -1), (1, 0), (0, 1), (-1, 0)):
            draw_arrow(draw, 16 + direction[0] * 4, 16 + direction[1] * 4, direction, color, scale, 10, 1.5)
    elif name == "stop":
        polygon(draw, ((10, 5), (22, 5), (27, 10), (27, 22), (22, 27), (10, 27), (5, 22), (5, 10)), color, scale)
        rectangle(draw, (11, 10, 14, 22), rgba((18, 18, 20), 255), scale)
        rectangle(draw, (18, 10, 21, 22), rgba((18, 18, 20), 255), scale)
    elif name == "queue-orders":
        for offset in (0, 3, 6):
            line(draw, ((6 + offset, 7 + offset), (17 + offset, 7 + offset),
                        (17 + offset, 18 + offset), (6 + offset, 18 + offset), (6 + offset, 7 + offset)), color, scale, 1.3)
        draw_small_plus(draw, 24, 24, secondary, scale, 1.5)
    elif name == "attack-anything":
        draw_crosshair(draw, cx, cy, 9, color, scale, 2)
        for direction in ((1, 1), (-1, 1), (1, -1), (-1, -1)):
            draw_arrow(draw, 16 + direction[0] * 3, 16 + direction[1] * 3, direction, secondary, scale, 8, 1)
    elif name == "defend":
        polygon(draw, ((16, 4), (26, 8), (24, 20), (16, 28), (8, 20), (6, 8)), color, scale)
        line(draw, ((11, 15), (15, 19), (22, 11)), rgba((18, 18, 20), 255), scale, 2)
    elif name == "return-fire":
        draw_arrow(draw, 11, 16, (-1, 0), color, scale, 11, 2)
        draw_crosshair(draw, 21, 16, 6, secondary, scale, 1.5)
    elif name == "hold-fire":
        line(draw, ((8, 8), (24, 24)), color, scale, 3)
        line(draw, ((24, 8), (8, 24)), color, scale, 3)
        ellipse(draw, (12, 12, 20, 20), None, scale, outline=secondary, width=2)
    elif name == "sell":
        line(draw, ((16, 4), (16, 27)), color, scale, 2)
        line(draw, ((23, 8), (11, 8), (8, 12), (11, 16), (21, 16), (24, 20), (21, 24), (9, 24)), color, scale, 2)
    elif name == "repair":
        line(draw, ((8, 24), (22, 10)), color, scale, 4)
        polygon(draw, ((20, 5), (27, 4), (25, 11), (21, 14), (17, 10)), color, scale)
        draw_small_plus(draw, 8, 8, secondary, scale, 1.5)
    elif name == "beacon":
        line(draw, ((16, 12), (16, 27)), color, scale, 2)
        polygon(draw, ((12, 27), (20, 27), (18, 22), (14, 22)), color, scale)
        for radius in (5, 9):
            draw.arc(scaled_box((16 - radius, 4 - radius, 16 + radius, 4 + radius), scale),
                     25, 155, fill=secondary, width=max(1, 2 * scale))


def glyph_atlas(skin: Skin, scale: int = 2) -> Image.Image:
    atlas = Image.new("RGBA", (2048 * scale, 128 * scale), (0, 0, 0, 0))
    states = ("normal", "disabled", "hover", "active")
    for row, state in enumerate(states):
        _, _, accent, alpha, offset = state_colors(skin, state)
        primary = rgba(skin.warning if state == "normal" else accent, alpha)
        secondary = rgba(accent, alpha)
        for column, name in enumerate(COMMAND_GLYPHS):
            cell = Image.new("RGBA", (32 * scale, 32 * scale), (0, 0, 0, 0))
            cell_draw = ImageDraw.Draw(cell)
            if offset:
                translated = Image.new("RGBA", cell.size, (0, 0, 0, 0))
                draw_command_glyph(ImageDraw.Draw(translated), name, primary, secondary, scale)
                cell.alpha_composite(translated, (0, offset * scale))
            else:
                draw_command_glyph(cell_draw, name, primary, secondary, scale)
            atlas.alpha_composite(cell, (column * 32 * scale, row * 32 * scale))
    return atlas


def save_pair(master: Image.Image, output_dir: Path, stem: str) -> list[Path]:
    two_x = output_dir / f"{stem}-2x.png"
    one_x = output_dir / f"{stem}.png"
    master.save(two_x, optimize=True)
    master.resize((master.width // 2, master.height // 2), Image.Resampling.LANCZOS).save(one_x, optimize=True)
    return [one_x, two_x]


def generate_assets(output_dir: Path) -> list[Path]:
    output_dir.mkdir(parents=True, exist_ok=True)
    generated: list[Path] = []
    for faction in FACTIONS:
        skin = SKINS[faction]
        generated.extend(save_pair(action_atlas(skin), output_dir, f"ios-touch-actions-{faction}-v3"))
        generated.extend(save_pair(joystick_atlas(skin), output_dir, f"ios-touch-joystick-{faction}-v3"))
        generated.extend(save_pair(quickbar_atlas(skin), output_dir, f"ios-touch-quickbar-{faction}-v3"))
        glyph_path = output_dir / f"ios-commandbar-glyphs-{faction}-v3.png"
        glyph_atlas(skin).resize((2048, 128), Image.Resampling.LANCZOS).save(glyph_path, optimize=True)
        generated.append(glyph_path)
    return generated


def main() -> None:
    repository_root = Path(__file__).resolve().parents[1]
    output_dir = repository_root / "mods" / "ra2" / "uibits"
    for generated in generate_assets(output_dir):
        print(generated.relative_to(repository_root))


if __name__ == "__main__":
    main()
