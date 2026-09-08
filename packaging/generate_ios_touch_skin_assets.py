#!/usr/bin/env python3
"""Generate deterministic faction-specific iOS touch-control atlases."""

import argparse
from pathlib import Path
from typing import NamedTuple

from PIL import Image, ImageChops, ImageDraw


ROOT = Path(__file__).resolve().parents[1]
DEFAULT_OUTPUT_DIR = ROOT / "mods" / "ra2" / "uibits"

FACTIONS = ("allies", "soviets", "yuri")
KINDS = ("actions", "joystick", "quickbar")
ACTION_NAMES = (
    "stop",
    "deploy",
    "select-type",
    "force-attack",
    "return-base",
)
ACTION_STATES = (
    "normal",
    "disabled",
    "hover",
    "pressed",
    "active",
    "active-hover",
    "active-pressed",
)
ACTION_STATE_OFFSETS = {
    "normal": (0, 0),
    "disabled": (0, 0),
    "hover": (0, 0),
    "pressed": (4, 4),
    "active": (0, 0),
    "active-hover": (0, 0),
    "active-pressed": (4, 4),
}
ACTION_LABEL_REGION = (6, 36, 44, 14)
ACTION_LABEL_RADIUS = 4
ACTION_GLYPH_RISE = 8

BASE_SIZES = {
    "actions": (512, 512),
    "joystick": (256, 256),
    "quickbar": (512, 128),
}
CANONICAL_SCALE = 2
DRAW_SCALE = 4


class Palette(NamedTuple):
    base: tuple[int, int, int]
    metal: tuple[int, int, int]
    accent: tuple[int, int, int]
    active: tuple[int, int, int]
    face: tuple[int, int, int]
    face_light: tuple[int, int, int]
    frame: tuple[int, int, int]
    metal_light: tuple[int, int, int]
    glow: tuple[int, int, int]
    glyph: tuple[int, int, int]
    secondary: tuple[int, int, int]
    style: str


PALETTES = {
    "allies": Palette(
        base=(0x13, 0x28, 0x3A),
        metal=(0x8B, 0xA4, 0xB5),
        accent=(0x8F, 0xE7, 0xFF),
        active=(0x5B, 0xE3, 0xFF),
        face=(24, 53, 76),
        face_light=(45, 78, 101),
        frame=(11, 25, 36),
        metal_light=(205, 214, 220),
        glow=(178, 242, 255),
        glyph=(226, 239, 247),
        secondary=(76, 132, 175),
        style="rounded",
    ),
    "soviets": Palette(
        base=(0x24, 0x1A, 0x19),
        metal=(0x71, 0x6B, 0x64),
        accent=(0xF0, 0xA4, 0x3C),
        active=(0xD9, 0x34, 0x28),
        face=(92, 29, 24),
        face_light=(132, 47, 31),
        frame=(18, 17, 16),
        metal_light=(181, 168, 145),
        glow=(255, 190, 88),
        glyph=(239, 211, 158),
        secondary=(217, 52, 40),
        style="octagon",
    ),
    "yuri": Palette(
        base=(0x16, 0x0F, 0x1D),
        metal=(0x75, 0x67, 0x80),
        accent=(0xCF, 0x56, 0xDC),
        active=(0x9D, 0xCC, 0x62),
        face=(53, 22, 69),
        face_light=(82, 35, 99),
        frame=(22, 20, 29),
        metal_light=(175, 168, 190),
        glow=(237, 126, 245),
        glyph=(232, 190, 242),
        secondary=(157, 204, 98),
        style="crystal",
    ),
}


def mix(
    first: tuple[int, int, int],
    second: tuple[int, int, int],
    amount: float,
) -> tuple[int, int, int]:
    return tuple(
        round(left + (right - left) * amount)
        for left, right in zip(first, second)
    )


def rgba(color: tuple[int, int, int], alpha: int = 255):
    return (*color, alpha)


class VectorCanvas:
    """Draw base-coordinate vector art and resolve it to the canonical 2x size."""

    def __init__(self, width: int, height: int):
        self.base_size = (width, height)
        self.image = Image.new(
            "RGBA",
            (width * DRAW_SCALE, height * DRAW_SCALE),
            (0, 0, 0, 0),
        )
        self.draw = ImageDraw.Draw(self.image, "RGBA")

    @staticmethod
    def unit(value: float) -> int:
        return round(value * DRAW_SCALE)

    def box(self, values):
        return tuple(self.unit(value) for value in values)

    def points(self, values):
        return [(self.unit(x), self.unit(y)) for x, y in values]

    def line(self, values, fill, width: float = 1, joint: str = "curve"):
        self.draw.line(
            self.points(values),
            fill=fill,
            width=max(1, self.unit(width)),
            joint=joint,
        )

    def polygon(self, values, fill=None, outline=None, width: float = 1):
        points = self.points(values)
        self.draw.polygon(points, fill=fill)
        if outline is not None:
            self.draw.line(
                [*points, points[0]],
                fill=outline,
                width=max(1, self.unit(width)),
                joint="curve",
            )

    def rectangle(self, values, fill=None, outline=None, width: float = 1):
        self.draw.rectangle(
            self.box(values),
            fill=fill,
            outline=outline,
            width=max(1, self.unit(width)),
        )

    def rounded_rectangle(
        self,
        values,
        radius: float,
        fill=None,
        outline=None,
        width: float = 1,
    ):
        self.draw.rounded_rectangle(
            self.box(values),
            radius=self.unit(radius),
            fill=fill,
            outline=outline,
            width=max(1, self.unit(width)),
        )

    def ellipse(self, values, fill=None, outline=None, width: float = 1):
        self.draw.ellipse(
            self.box(values),
            fill=fill,
            outline=outline,
            width=max(1, self.unit(width)),
        )

    def arc(self, values, start: float, end: float, fill, width: float = 1):
        self.draw.arc(
            self.box(values),
            start=start,
            end=end,
            fill=fill,
            width=max(1, self.unit(width)),
        )

    def geometry(
        self,
        style: str,
        inset: float,
        offset: tuple[float, float] = (0, 0),
    ):
        width, height = self.base_size
        offset_x, offset_y = offset
        left = inset + offset_x
        top = inset + offset_y
        right = width - inset - 1 + offset_x
        bottom = height - inset - 1 + offset_y
        span = min(right - left, bottom - top)

        if style == "rounded":
            return (
                "rounded",
                self.box((left, top, right, bottom)),
                self.unit(max(4, span * 0.34)),
            )
        if style == "ellipse":
            return ("ellipse", self.box((left, top, right, bottom)))

        cut = min(span * 0.19, 12)
        if style == "octagon":
            points = (
                (left + cut, top),
                (right - cut, top),
                (right, top + cut),
                (right, bottom - cut),
                (right - cut, bottom),
                (left + cut, bottom),
                (left, bottom - cut),
                (left, top + cut),
            )
        else:
            points = (
                (left + cut * 0.7, top),
                (right - cut * 1.35, top + cut * 0.1),
                (right, top + cut * 0.85),
                (right - cut * 0.15, bottom - cut * 0.7),
                (right - cut * 1.05, bottom),
                (left + cut * 0.55, bottom - cut * 0.2),
                (left, bottom - cut * 1.15),
                (left + cut * 0.1, top + cut * 0.75),
            )
        return ("polygon", self.points(points))

    def draw_geometry(self, geometry, fill=None, outline=None, width: float = 1):
        line_width = max(1, self.unit(width))
        if geometry[0] == "rounded":
            self.draw.rounded_rectangle(
                geometry[1],
                radius=geometry[2],
                fill=fill,
                outline=outline,
                width=line_width,
            )
        elif geometry[0] == "ellipse":
            self.draw.ellipse(
                geometry[1],
                fill=fill,
                outline=outline,
                width=line_width,
            )
        else:
            self.draw.polygon(geometry[1], fill=fill)
            if outline is not None:
                self.draw.line(
                    [*geometry[1], geometry[1][0]],
                    fill=outline,
                    width=line_width,
                    joint="curve",
                )

    def gradient_geometry(self, geometry, top, bottom, alpha: int = 255):
        gradient = Image.new("RGBA", self.image.size, (0, 0, 0, 0))
        gradient_draw = ImageDraw.Draw(gradient, "RGBA")
        denominator = max(1, gradient.height - 1)
        for y in range(gradient.height):
            color = mix(top, bottom, y / denominator)
            gradient_draw.line((0, y, gradient.width, y), fill=rgba(color, alpha))

        mask = Image.new("L", self.image.size)
        mask_draw = ImageDraw.Draw(mask)
        if geometry[0] == "rounded":
            mask_draw.rounded_rectangle(
                geometry[1],
                radius=geometry[2],
                fill=255,
            )
        elif geometry[0] == "ellipse":
            mask_draw.ellipse(geometry[1], fill=255)
        else:
            mask_draw.polygon(geometry[1], fill=255)
        gradient.putalpha(ImageChops.multiply(gradient.getchannel("A"), mask))
        self.image.alpha_composite(gradient)

    def finish(self) -> Image.Image:
        width, height = self.base_size
        return self.image.resize(
            (width * CANONICAL_SCALE, height * CANONICAL_SCALE),
            Image.Resampling.LANCZOS,
        )


def inset_sprite(sprite: Image.Image, padding: int) -> Image.Image:
    """Scale a sprite inward so atlas sampling cannot leak into its gutter."""
    if padding <= 0:
        return sprite
    inset = sprite.resize(
        (sprite.width - padding * 2, sprite.height - padding * 2),
        Image.Resampling.LANCZOS,
    )
    guarded = Image.new("RGBA", sprite.size, (0, 0, 0, 0))
    guarded.alpha_composite(inset, (padding, padding))
    return guarded


def guard_panel_lanczos_edges(panel: Image.Image) -> Image.Image:
    """Keep a visible panel edge without leaking into 1x atlas gutters."""
    # Pillow's LANCZOS kernel has a three-lobe sampling tail.  This calibrated
    # seven-pixel canonical profile stays non-zero on the source edge, resolves
    # to a non-zero 1x edge, and rounds every sample outside the slot to zero.
    profile = (131, 249, 184, 159, 160, 226, 203)
    horizontal = Image.new("L", panel.size, 255)
    vertical = Image.new("L", panel.size, 255)
    for offset, alpha in enumerate(profile):
        horizontal.paste(alpha, (offset, 0, offset + 1, panel.height))
        horizontal.paste(
            alpha,
            (panel.width - offset - 1, 0, panel.width - offset, panel.height),
        )
        vertical.paste(alpha, (0, offset, panel.width, offset + 1))
        vertical.paste(
            alpha,
            (0, panel.height - offset - 1, panel.width, panel.height - offset),
        )

    mask = ImageChops.multiply(horizontal, vertical)
    guarded = panel.copy()
    guarded.putalpha(ImageChops.multiply(panel.getchannel("A"), mask))
    return guarded


def state_colors(palette: Palette, state: str):
    disabled = state == "disabled"
    active = state.startswith("active")
    hover = state.endswith("hover") or state == "hover"
    pressed = state.endswith("pressed") or state == "pressed"

    if disabled:
        neutral = (55, 58, 62)
        return {
            "outer": mix(palette.metal, neutral, 0.72),
            "outer_light": mix(palette.metal_light, neutral, 0.76),
            "top": mix(palette.face_light, neutral, 0.76),
            "bottom": mix(palette.base, (28, 30, 32), 0.58),
            "accent": mix(palette.accent, neutral, 0.82),
            "glyph": mix(palette.glyph, neutral, 0.67),
            "glow_alpha": 0,
        }

    face_top = palette.face_light
    face_bottom = palette.base
    if pressed:
        face_top = mix(palette.face, palette.base, 0.58)
        face_bottom = mix(palette.base, (0, 0, 0), 0.35)
    elif active:
        face_top = mix(palette.face_light, palette.active, 0.22)
        face_bottom = mix(palette.face, palette.base, 0.42)
    elif hover:
        face_top = mix(palette.face_light, palette.glyph, 0.16)

    return {
        "outer": palette.metal,
        "outer_light": palette.metal_light,
        "top": face_top,
        "bottom": face_bottom,
        "accent": palette.active if active else palette.accent,
        "glyph": palette.glyph,
        "glow_alpha": 150 if active and hover else 105 if active else 75 if hover else 0,
    }


def draw_action_decorations(
    canvas: VectorCanvas,
    palette: Palette,
    colors,
    offset: tuple[int, int],
    disabled: bool,
):
    offset_x, offset_y = offset
    detail_accent = mix(palette.accent, (65, 68, 70), 0.72) if disabled else palette.accent
    accent = rgba(detail_accent, 150 if disabled else 245)
    dark = rgba(palette.frame, 245)

    if palette.style == "rounded":
        canvas.rounded_rectangle(
            (21 + offset_x, 4 + offset_y, 35 + offset_x, 7 + offset_y),
            radius=1.5,
            fill=dark,
            outline=rgba(colors["outer_light"], 205),
            width=0.7,
        )
        canvas.rounded_rectangle(
            (23 + offset_x, 5 + offset_y, 33 + offset_x, 6.5 + offset_y),
            radius=0.7,
            fill=accent,
        )
        for x in (8, 48):
            canvas.ellipse(
                (x - 1 + offset_x, 27 + offset_y, x + 1 + offset_x, 29 + offset_y),
                fill=rgba(colors["outer_light"], 190),
            )
    elif palette.style == "octagon":
        canvas.rounded_rectangle(
            (21 + offset_x, 3.5 + offset_y, 35 + offset_x, 7.5 + offset_y),
            radius=1,
            fill=rgba(palette.frame, 255),
            outline=rgba(colors["outer_light"], 210),
            width=0.8,
        )
        canvas.rectangle(
            (23 + offset_x, 4.8 + offset_y, 33 + offset_x, 7.2 + offset_y),
            fill=accent,
        )
        for x, y in ((9, 10), (47, 10), (9, 46), (47, 46)):
            canvas.ellipse(
                (x - 1.2 + offset_x, y - 1.2 + offset_y, x + 1.2 + offset_x, y + 1.2 + offset_y),
                fill=rgba((31, 32, 32), 255),
                outline=rgba(colors["outer_light"], 180),
                width=0.7,
            )
    else:
        canvas.polygon(
            (
                (39 + offset_x, 3 + offset_y),
                (48 + offset_x, 8 + offset_y),
                (43 + offset_x, 14 + offset_y),
                (36 + offset_x, 10 + offset_y),
            ),
            fill=rgba(detail_accent, 215),
            outline=rgba(colors["outer_light"], 180),
            width=0.7,
        )
        canvas.polygon(
            (
                (8 + offset_x, 40 + offset_y),
                (13 + offset_x, 48 + offset_y),
                (18 + offset_x, 42 + offset_y),
                (13 + offset_x, 36 + offset_y),
            ),
            fill=rgba(detail_accent, 180),
        )
        indicator = mix(palette.secondary, (65, 68, 70), 0.72) if disabled else palette.secondary
        canvas.rounded_rectangle(
            (48 + offset_x, 24 + offset_y, 50.5 + offset_x, 33 + offset_y),
            radius=1,
            fill=rgba(palette.frame, 255),
        )
        canvas.rounded_rectangle(
            (48.7 + offset_x, 25.5 + offset_y, 49.8 + offset_x, 31.5 + offset_y),
            radius=0.5,
            fill=rgba(indicator, 240),
        )


def draw_action_glyph(
    canvas: VectorCanvas,
    action: str,
    color,
    offset: tuple[int, int],
    underlay,
):
    offset_x, offset_y = offset
    ox = offset_x
    oy = offset_y + 1

    if action == "stop":
        outer = (
            (20 + ox, 14 + oy),
            (36 + ox, 14 + oy),
            (42 + ox, 20 + oy),
            (42 + ox, 36 + oy),
            (36 + ox, 42 + oy),
            (20 + ox, 42 + oy),
            (14 + ox, 36 + oy),
            (14 + ox, 20 + oy),
        )
        canvas.polygon(outer, fill=color, outline=rgba((255, 255, 255), 105), width=0.8)
        inner = (
            (22 + ox, 20 + oy),
            (34 + ox, 20 + oy),
            (36 + ox, 22 + oy),
            (36 + ox, 34 + oy),
            (34 + ox, 36 + oy),
            (22 + ox, 36 + oy),
            (20 + ox, 34 + oy),
            (20 + ox, 22 + oy),
        )
        canvas.polygon(inner, fill=underlay)
    elif action == "deploy":
        canvas.polygon(
            (
                (24 + ox, 13 + oy),
                (32 + ox, 13 + oy),
                (32 + ox, 27 + oy),
                (38 + ox, 27 + oy),
                (28 + ox, 39 + oy),
                (18 + ox, 27 + oy),
                (24 + ox, 27 + oy),
            ),
            fill=color,
            outline=rgba((255, 255, 255), 85),
            width=0.7,
        )
        canvas.line(((17 + ox, 42 + oy), (39 + ox, 42 + oy)), color, width=3)
        canvas.line(((20 + ox, 39 + oy), (20 + ox, 43 + oy)), color, width=1.5)
        canvas.line(((36 + ox, 39 + oy), (36 + ox, 43 + oy)), color, width=1.5)
    elif action == "select-type":
        for lines in (
            ((14, 22), (14, 15), (21, 15)),
            ((35, 15), (42, 15), (42, 22)),
            ((14, 34), (14, 41), (21, 41)),
            ((35, 41), (42, 41), (42, 34)),
        ):
            canvas.line(tuple((x + ox, y + oy) for x, y in lines), color, width=2.5)
        for center_x, center_y, size in ((23, 25, 4), (33, 24, 3.5), (29, 34, 4.5)):
            canvas.polygon(
                (
                    (center_x + ox, center_y - size + oy),
                    (center_x + size + ox, center_y + oy),
                    (center_x + ox, center_y + size + oy),
                    (center_x - size + ox, center_y + oy),
                ),
                fill=color,
                outline=rgba((255, 255, 255), 75),
                width=0.6,
            )
    elif action == "force-attack":
        canvas.ellipse(
            (18 + ox, 18 + oy, 38 + ox, 38 + oy),
            outline=color,
            width=2.7,
        )
        canvas.ellipse(
            (25 + ox, 25 + oy, 31 + ox, 31 + oy),
            fill=color,
        )
        for line in (
            ((28, 12), (28, 21)),
            ((28, 35), (28, 44)),
            ((12, 28), (21, 28)),
            ((35, 28), (44, 28)),
        ):
            canvas.line(tuple((x + ox, y + oy) for x, y in line), color, width=3)
        canvas.polygon(
            (
                (28 + ox, 21 + oy),
                (31 + ox, 28 + oy),
                (28 + ox, 35 + oy),
                (25 + ox, 28 + oy),
            ),
            fill=rgba((255, 255, 255), 185),
        )
    else:
        canvas.arc(
            (14 + ox, 13 + oy, 42 + ox, 41 + oy),
            start=36,
            end=292,
            fill=color,
            width=4,
        )
        canvas.polygon(
            (
                (13 + ox, 29 + oy),
                (13 + ox, 40 + oy),
                (24 + ox, 36 + oy),
            ),
            fill=color,
        )
        canvas.polygon(
            (
                (23 + ox, 31 + oy),
                (33 + ox, 31 + oy),
                (37 + ox, 36 + oy),
                (33 + ox, 40 + oy),
                (23 + ox, 40 + oy),
                (19 + ox, 36 + oy),
            ),
            fill=underlay,
            outline=color,
            width=2,
        )
        canvas.line(((25 + ox, 36 + oy), (31 + ox, 36 + oy)), color, width=2)


def render_action_label_well(palette: Palette) -> Image.Image:
    x, y, width, height = ACTION_LABEL_REGION
    face = mix(palette.frame, palette.base, 0.5)
    outline = mix(palette.metal, palette.frame, 0.72)
    canvas = VectorCanvas(56, 56)
    canvas.rounded_rectangle(
        (x, y, x + width, y + height),
        radius=ACTION_LABEL_RADIUS,
        fill=rgba(face, 255),
        outline=rgba(outline, 255),
        width=1,
    )

    rendered = canvas.finish()
    canonical_box = (
        x * CANONICAL_SCALE,
        y * CANONICAL_SCALE,
        (x + width) * CANONICAL_SCALE,
        (y + height) * CANONICAL_SCALE,
    )
    return rendered.crop(canonical_box)


def render_action_cell(palette: Palette, action: str, state: str) -> Image.Image:
    canvas = VectorCanvas(56, 56)
    colors = state_colors(palette, state)
    offset = ACTION_STATE_OFFSETS[state]
    active = state.startswith("active")
    hover = state.endswith("hover") or state == "hover"
    pressed = state.endswith("pressed") or state == "pressed"
    disabled = state == "disabled"
    outer_inset = 6.0 if pressed else 3.0
    frame_inset = 8.0 if pressed else 5.2
    face_inset = 10.0 if pressed else 7.3

    if colors["glow_alpha"]:
        glow_geometry = canvas.geometry(
            palette.style,
            5.0 if pressed else 1.5,
            offset,
        )
        canvas.draw_geometry(
            glow_geometry,
            fill=rgba(colors["accent"], colors["glow_alpha"] // 3),
            outline=rgba(colors["accent"], colors["glow_alpha"]),
            width=2.2 if active else 1.5,
        )

    shadow_offset = (3.0, 3.6) if pressed else (0.5, 1.8)
    canvas.draw_geometry(
        canvas.geometry(
            palette.style,
            5.0 if pressed else 3.0,
            shadow_offset,
        ),
        fill=(0, 0, 0, 175 if pressed else 120),
    )

    outer = canvas.geometry(palette.style, outer_inset, offset)
    canvas.draw_geometry(
        outer,
        fill=rgba(colors["outer"], 255),
        outline=rgba(colors["outer_light"], 235),
        width=1.2,
    )
    canvas.draw_geometry(
        canvas.geometry(palette.style, frame_inset, offset),
        fill=rgba(palette.frame, 255),
        outline=rgba((3, 5, 8), 235),
        width=1.1,
    )

    inner = canvas.geometry(palette.style, face_inset, offset)
    canvas.gradient_geometry(inner, colors["top"], colors["bottom"])
    canvas.draw_geometry(
        inner,
        outline=rgba(colors["accent"], 235 if active or hover else 125),
        width=2.0 if active else 1.0,
    )
    if active:
        canvas.draw_geometry(
            canvas.geometry(palette.style, face_inset + 2.5, offset),
            outline=rgba(colors["accent"], 205),
            width=1.0,
        )

    draw_action_decorations(canvas, palette, colors, offset, disabled)
    glyph_color = mix(colors["glyph"], palette.base, 0.38) if pressed else colors["glyph"]
    glyph = rgba(glyph_color, 150 if disabled else 255)
    underlay = rgba(palette.base, 255)
    glyph_offset = (offset[0], offset[1] - ACTION_GLYPH_RISE)
    draw_action_glyph(canvas, action, glyph, glyph_offset, underlay)

    if hover:
        canvas.arc(
            (10 + offset[0], 9 + offset[1], 46 + offset[0], 45 + offset[1]),
            start=205,
            end=330,
            fill=rgba(colors["accent"], 210),
            width=1.2,
        )
    sprite = inset_sprite(canvas.finish(), padding=8)
    x, y, _, _ = ACTION_LABEL_REGION
    sprite.alpha_composite(
        render_action_label_well(palette),
        (x * CANONICAL_SCALE, y * CANONICAL_SCALE),
    )
    return sprite


def render_actions(palette: Palette) -> Image.Image:
    master = Image.new("RGBA", (1024, 1024), (0, 0, 0, 0))
    for row, state in enumerate(ACTION_STATES):
        for column, action in enumerate(ACTION_NAMES):
            sprite = render_action_cell(palette, action, state)
            master.alpha_composite(sprite, (column * 128, row * 128))
    return master


def draw_direction_cues(canvas: VectorCanvas, palette: Palette, color):
    cues = (
        ((72, 18), (66, 27), (78, 27)),
        ((126, 72), (117, 66), (117, 78)),
        ((72, 126), (66, 117), (78, 117)),
        ((18, 72), (27, 66), (27, 78)),
    )
    for cue in cues:
        canvas.polygon(cue, fill=color)

    if palette.style == "rounded":
        for start, end in ((214, 326), (34, 146)):
            canvas.arc((19, 19, 125, 125), start, end, rgba(palette.glow, 205), width=2)
    elif palette.style == "octagon":
        for x, y in ((21, 21), (123, 21), (21, 123), (123, 123)):
            canvas.rectangle((x - 2, y - 2, x + 2, y + 2), fill=rgba(palette.glow, 230))
    else:
        canvas.polygon(
            ((17, 54), (8, 72), (17, 90), (24, 72)),
            fill=rgba(palette.accent, 225),
            outline=rgba(palette.metal_light, 190),
            width=1,
        )
        for y in (57, 66, 75, 84):
            canvas.rounded_rectangle(
                (122, y, 126, y + 5),
                radius=1,
                fill=rgba(palette.secondary, 240),
            )


def render_joystick_base(palette: Palette) -> Image.Image:
    canvas = VectorCanvas(144, 144)
    style = "ellipse" if palette.style == "rounded" else palette.style
    canvas.draw_geometry(
        canvas.geometry(style, 4, (0, 2)),
        fill=(0, 0, 0, 150),
    )
    outer = canvas.geometry(style, 4)
    canvas.gradient_geometry(outer, palette.metal_light, palette.metal)
    canvas.draw_geometry(outer, outline=rgba((220, 226, 232), 150), width=1.2)
    canvas.draw_geometry(
        canvas.geometry(style, 9),
        fill=rgba(palette.frame, 255),
        outline=rgba((5, 6, 9), 245),
        width=2,
    )
    face = canvas.geometry(style, 14)
    canvas.gradient_geometry(face, palette.face_light, palette.base)
    canvas.draw_geometry(face, outline=rgba(palette.accent, 210), width=2)
    canvas.draw_geometry(
        canvas.geometry(style, 26),
        fill=rgba(mix(palette.face, palette.base, 0.45), 205),
        outline=rgba(palette.metal, 190),
        width=1.5,
    )
    canvas.draw_geometry(
        canvas.geometry("ellipse", 48),
        fill=rgba(palette.base, 215),
        outline=rgba(palette.accent, 150),
        width=1.5,
    )
    draw_direction_cues(canvas, palette, rgba(palette.glyph, 225))
    return inset_sprite(canvas.finish(), padding=8)


def render_joystick_thumb(palette: Palette, active: bool) -> Image.Image:
    canvas = VectorCanvas(64, 64)
    style = "ellipse" if palette.style == "rounded" else palette.style
    if active:
        canvas.draw_geometry(
            canvas.geometry(style, 2.5),
            fill=rgba(palette.accent, 70),
            outline=rgba(palette.glow, 230),
            width=2.5,
        )
    canvas.draw_geometry(
        canvas.geometry(style, 5, (0, 2)),
        fill=(0, 0, 0, 165),
    )
    outer = canvas.geometry(style, 5)
    canvas.gradient_geometry(
        outer,
        palette.metal_light if active else palette.metal,
        palette.metal,
    )
    canvas.draw_geometry(
        outer,
        outline=rgba(palette.active if active else palette.metal_light, 230),
        width=1.4,
    )
    face = canvas.geometry(style, 10)
    canvas.gradient_geometry(
        face,
        mix(palette.face_light, palette.accent, 0.32 if active else 0.08),
        palette.base,
    )
    canvas.draw_geometry(
        face,
        outline=rgba(palette.accent, 245 if active else 150),
        width=1.8,
    )

    if palette.style == "crystal":
        canvas.polygon(
            ((32, 13), (49, 30), (35, 49), (16, 39), (18, 20)),
            fill=rgba(mix(palette.face_light, palette.metal_light, 0.25), 150),
            outline=rgba(palette.glow, 170 if active else 95),
            width=1,
        )
        canvas.polygon(
            ((32, 13), (35, 49), (24, 34)),
            fill=rgba(palette.accent, 105 if active else 55),
        )
        canvas.rounded_rectangle(
            (50, 27, 53, 37),
            radius=1,
            fill=rgba(palette.secondary, 255 if active else 175),
        )
    else:
        canvas.ellipse(
            (17, 17, 47, 47),
            fill=rgba(mix(palette.face_light, palette.base, 0.42), 210),
            outline=rgba(palette.glyph, 110),
            width=1.2,
        )
        canvas.arc(
            (18, 17, 46, 45),
            start=195,
            end=330,
            fill=rgba(palette.glyph, 135),
            width=1.1,
        )
        if palette.style == "octagon":
            for x, y in ((15, 15), (49, 15), (15, 49), (49, 49)):
                canvas.ellipse((x - 1.5, y - 1.5, x + 1.5, y + 1.5), fill=rgba(palette.glow, 225))
    return inset_sprite(canvas.finish(), padding=8)


def render_joystick(palette: Palette) -> Image.Image:
    master = Image.new("RGBA", (512, 512), (0, 0, 0, 0))
    master.alpha_composite(render_joystick_base(palette), (0, 0))
    master.alpha_composite(render_joystick_thumb(palette, active=False), (320, 0))
    master.alpha_composite(render_joystick_thumb(palette, active=True), (320, 144))
    return master


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


def quickbar_state_colors(palette: Palette, state: str):
    highlighted = state.startswith("highlighted")
    disabled = state.endswith("disabled") or state == "disabled"
    hover = state.endswith("hover") or state == "hover"
    pressed = state.endswith("pressed") or state == "pressed"

    if disabled:
        neutral = (24, 26, 28)
        return {
            "outer": mix(palette.metal, neutral, 0.76),
            "outer_light": mix(palette.metal_light, neutral, 0.82),
            "face_top": mix(palette.face, neutral, 0.78),
            "face_bottom": mix(palette.base, (8, 9, 10), 0.72),
            "rim": palette.active if highlighted else mix(palette.accent, neutral, 0.82),
            "rim_alpha": 175 if highlighted else 105,
        }

    if pressed:
        face_top = mix(palette.face, palette.base, 0.66)
        face_bottom = mix(palette.base, (0, 0, 0), 0.46)
    elif hover:
        face_top = mix(
            palette.face_light,
            palette.active if highlighted else palette.accent,
            0.24,
        )
        face_bottom = palette.base
    elif highlighted:
        face_top = mix(palette.face_light, palette.active, 0.18)
        face_bottom = palette.base
    else:
        face_top = palette.face_light
        face_bottom = palette.base

    return {
        "outer": palette.metal,
        "outer_light": palette.metal_light,
        "face_top": face_top,
        "face_bottom": face_bottom,
        "rim": palette.active if highlighted else palette.accent,
        "rim_alpha": 255 if highlighted or hover else 175,
    }


def render_quickbar_button(palette: Palette, state: str) -> Image.Image:
    if state not in QUICKBAR_STATES:
        raise ValueError(f"unknown quickbar state: {state}")

    canvas = VectorCanvas(48, 52)
    style = palette.style
    colors = quickbar_state_colors(palette, state)
    highlighted = state.startswith("highlighted")
    disabled = state.endswith("disabled") or state == "disabled"
    hover = state.endswith("hover") or state == "hover"
    pressed = state.endswith("pressed") or state == "pressed"
    offset = (4, 4) if pressed else (0, 0)
    outer_inset = 6 if pressed else 4
    frame_inset = 7.5 if pressed else 6.5
    face_inset = 8.5 if pressed else 8

    if (hover or highlighted) and not pressed:
        halo_color = palette.accent if hover and highlighted else colors["rim"]
        halo = canvas.geometry(style, 3)
        canvas.draw_geometry(
            halo,
            fill=rgba(halo_color, 36 if highlighted else 24),
            outline=rgba(halo_color, 210 if highlighted else 150),
            width=1.4,
        )

    canvas.draw_geometry(
        canvas.geometry(
            style,
            6 if pressed else 4,
            (4, 4.6) if pressed else (0, 1.2),
        ),
        fill=(0, 0, 0, 175 if pressed else 135),
    )
    outer = canvas.geometry(style, outer_inset, offset)
    canvas.gradient_geometry(outer, colors["outer_light"], colors["outer"])
    canvas.draw_geometry(
        outer,
        outline=rgba(colors["outer_light"], 215),
        width=1,
    )
    canvas.draw_geometry(
        canvas.geometry(style, frame_inset, offset),
        fill=rgba(palette.frame, 255),
        outline=rgba((3, 4, 6), 245),
        width=1,
    )
    face = canvas.geometry(style, face_inset, offset)
    canvas.gradient_geometry(face, colors["face_top"], colors["face_bottom"])
    canvas.draw_geometry(
        face,
        outline=rgba(colors["rim"], colors["rim_alpha"]),
        width=2.0 if highlighted else 1.2,
    )
    if highlighted:
        canvas.draw_geometry(
            canvas.geometry(style, face_inset + 2, offset),
            outline=rgba(palette.active, 205 if disabled else 245),
            width=0.9,
        )

    well_offset = (1, 1) if pressed else (0, 0)
    well_color = mix(colors["face_top"], colors["face_bottom"], 0.64)
    canvas.rounded_rectangle(
        (
            9 + well_offset[0],
            12 + well_offset[1],
            39 + well_offset[0],
            42 + well_offset[1],
        ),
        radius=5,
        fill=rgba(well_color, 245),
        outline=rgba(colors["rim"], 72 if disabled else 105),
        width=0.7,
    )

    detail_offset = (2, 4) if pressed else offset

    if palette.style == "rounded":
        canvas.rounded_rectangle(
            (
                18 + detail_offset[0],
                4 + detail_offset[1],
                30 + detail_offset[0],
                6.5 + detail_offset[1],
            ),
            radius=1,
            fill=rgba(colors["rim"], 145 if disabled else 255),
        )
    elif palette.style == "octagon":
        canvas.rectangle(
            (
                19 + detail_offset[0],
                4 + detail_offset[1],
                29 + detail_offset[0],
                6.5 + detail_offset[1],
            ),
            fill=rgba(colors["rim"], 145 if disabled else 255),
        )
        for x, y in ((8, 10), (40, 10), (8, 42), (40, 42)):
            canvas.ellipse(
                (
                    x - 1 + detail_offset[0],
                    y - 1 + detail_offset[1],
                    x + 1 + detail_offset[0],
                    y + 1 + detail_offset[1],
                ),
                fill=rgba(colors["outer_light"], 190),
            )
    else:
        canvas.polygon(
            (
                (34 + detail_offset[0], 4 + detail_offset[1]),
                (43 + detail_offset[0], 8 + detail_offset[1]),
                (39 + detail_offset[0], 13 + detail_offset[1]),
                (33 + detail_offset[0], 9 + detail_offset[1]),
            ),
            fill=rgba(colors["rim"], 150 if disabled else 230),
        )
        canvas.rounded_rectangle(
            (
                40 + detail_offset[0],
                24 + detail_offset[1],
                42 + detail_offset[0],
                33 + detail_offset[1],
            ),
            radius=0.7,
            fill=rgba(palette.active, 150 if disabled else 245),
        )

    return canvas.finish()


def render_panel(palette: Palette, compact: bool) -> Image.Image:
    # Chrome consumes this as 12 + 104 + 12 by 12 + 40 + 12.  The
    # stretchable strips are deliberately axis-invariant: horizontal strips
    # repeat a single column, vertical strips repeat a single row, and the
    # center is flat.  All localized hardware stays inside the four corners.
    canvas = VectorCanvas(128, 64)
    center = mix(
        palette.face,
        palette.base,
        0.52 if compact else 0.28,
    )
    edge = mix(palette.metal, palette.base, 0.18 if compact else 0.08)
    inner_edge = mix(palette.face_light, palette.base, 0.34 if compact else 0.18)
    corner_accent = palette.active if compact else palette.accent

    canvas.rectangle((0, 0, 128, 64), fill=rgba(center, 255))

    # Horizontally repeatable top and bottom tracks.  They stop four base
    # pixels before the 12 px slice line so Lanczos filtering cannot carry a
    # corner detail into a stretchable strip.
    canvas.rectangle((0, 0, 128, 1.5), fill=rgba(palette.metal_light, 255))
    canvas.rectangle((0, 1.5, 128, 3.5), fill=rgba(edge, 255))
    canvas.rectangle((0, 3.5, 128, 5.5), fill=rgba(palette.frame, 255))
    canvas.rectangle((0, 5.5, 128, 8), fill=rgba(inner_edge, 255))
    canvas.rectangle((0, 6.5, 128, 7.5), fill=rgba(corner_accent, 255))

    canvas.rectangle((0, 56, 128, 58.5), fill=rgba(inner_edge, 255))
    canvas.rectangle((0, 58.5, 128, 60.5), fill=rgba(palette.frame, 255))
    canvas.rectangle((0, 60.5, 128, 62.5), fill=rgba(edge, 255))
    canvas.rectangle((0, 62.5, 128, 64), fill=rgba(palette.metal_light, 255))
    canvas.rectangle((0, 56.5, 128, 57.5), fill=rgba(corner_accent, 255))

    # Vertically repeatable side tracks use the same four-pixel safety gap.
    canvas.rectangle((0, 0, 1.5, 64), fill=rgba(palette.metal_light, 255))
    canvas.rectangle((1.5, 0, 3.5, 64), fill=rgba(edge, 255))
    canvas.rectangle((3.5, 0, 5.5, 64), fill=rgba(palette.frame, 255))
    canvas.rectangle((5.5, 0, 8, 64), fill=rgba(inner_edge, 255))

    canvas.rectangle((120, 0, 122.5, 64), fill=rgba(inner_edge, 255))
    canvas.rectangle((122.5, 0, 124.5, 64), fill=rgba(palette.frame, 255))
    canvas.rectangle((124.5, 0, 126.5, 64), fill=rgba(edge, 255))
    canvas.rectangle((126.5, 0, 128, 64), fill=rgba(palette.metal_light, 255))

    if palette.style == "rounded":
        for x, y in ((5, 5), (123, 59)):
            canvas.ellipse(
                (x - 3, y - 3, x + 3, y + 3),
                fill=rgba(palette.frame, 255),
                outline=rgba(corner_accent, 255),
                width=0.8,
            )
            canvas.ellipse(
                (x - 1.1, y - 1.1, x + 1.1, y + 1.1),
                fill=rgba(palette.glow, 255),
            )
    elif palette.style == "octagon":
        for x, y in ((5, 5), (123, 59)):
            canvas.polygon(
                (
                    (x - 2.5, y - 1.5),
                    (x - 1.5, y - 2.5),
                    (x + 1.5, y - 2.5),
                    (x + 2.5, y - 1.5),
                    (x + 2.5, y + 1.5),
                    (x + 1.5, y + 2.5),
                    (x - 1.5, y + 2.5),
                    (x - 2.5, y + 1.5),
                ),
                fill=rgba(palette.frame, 255),
                outline=rgba(corner_accent, 255),
                width=0.8,
            )
            canvas.rectangle(
                (x - 0.8, y - 0.8, x + 0.8, y + 0.8),
                fill=rgba(palette.glow, 255),
            )
    else:
        # Yuri's localized asymmetric crystals never cross a slice line.
        canvas.polygon(
            ((2, 7), (5, 2), (8.5, 3.5), (7, 8.5), (4, 7.5)),
            fill=rgba(palette.accent, 255),
            outline=rgba(palette.metal_light, 255),
            width=0.7,
        )
        canvas.polygon(
            ((119.5, 60), (123, 54.5), (126, 56), (125, 61.5), (122, 62)),
            fill=rgba(palette.accent, 255),
            outline=rgba(palette.metal_light, 255),
            width=0.7,
        )
        canvas.polygon(
            ((120.5, 3), (125.5, 2), (126, 7), (122.5, 8.5)),
            fill=rgba(palette.secondary, 255),
        )
    return guard_panel_lanczos_edges(canvas.finish())


def render_toggle(palette: Palette, expand: bool) -> Image.Image:
    canvas = VectorCanvas(26, 26)
    collapse_chevrons = (
        (
            (3.5, 13), (9.5, 5.5), (12.5, 8.5),
            (8.5, 13), (12.5, 17.5), (9.5, 20.5),
        ),
        (
            (12.5, 13), (18.5, 5.5), (21.5, 8.5),
            (17.5, 13), (21.5, 17.5), (18.5, 20.5),
        ),
    )
    for chevron in collapse_chevrons:
        canvas.polygon(
            chevron,
            fill=rgba(palette.frame, 235),
            outline=rgba(palette.frame, 245),
            width=1.8,
        )
    for chevron in collapse_chevrons:
        canvas.polygon(
            chevron,
            fill=rgba(palette.glyph, 250),
            outline=rgba(palette.glow, 180),
            width=0.7,
        )

    collapse = canvas.finish()
    return collapse.transpose(Image.Transpose.FLIP_LEFT_RIGHT) if expand else collapse


def render_quickbar(palette: Palette) -> Image.Image:
    master = Image.new("RGBA", (1024, 256), (0, 0, 0, 0))
    for index, state in enumerate(QUICKBAR_STATES):
        master.alpha_composite(
            render_quickbar_button(palette, state),
            (index * 128, 0),
        )
    master.alpha_composite(render_panel(palette, compact=False), (0, 128))
    master.alpha_composite(render_panel(palette, compact=True), (256, 128))
    master.alpha_composite(render_toggle(palette, expand=False), (518, 134))
    master.alpha_composite(render_toggle(palette, expand=True), (582, 134))
    return master


def render_master(kind: str, faction: str) -> Image.Image:
    if kind not in KINDS:
        raise ValueError(f"unknown atlas kind: {kind}")
    if faction not in FACTIONS:
        raise ValueError(f"unknown faction: {faction}")

    palette = PALETTES[faction]
    if kind == "actions":
        return render_actions(palette)
    if kind == "joystick":
        return render_joystick(palette)
    return render_quickbar(palette)


def save_png(image: Image.Image, path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    image.save(path, "PNG", optimize=False, compress_level=9)


def generate_assets(output_dir: Path = DEFAULT_OUTPUT_DIR) -> tuple[Path, ...]:
    written = []
    for faction in FACTIONS:
        for kind in KINDS:
            master = render_master(kind, faction)
            two_x_path = output_dir / f"ios-touch-{kind}-{faction}-2x.png"
            one_x_path = output_dir / f"ios-touch-{kind}-{faction}.png"
            save_png(master, two_x_path)
            save_png(
                master.resize(BASE_SIZES[kind], Image.Resampling.LANCZOS),
                one_x_path,
            )
            written.extend((one_x_path, two_x_path))
    return tuple(written)


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Generate deterministic iOS touch-control atlases.",
    )
    parser.add_argument(
        "--output-dir",
        type=Path,
        default=DEFAULT_OUTPUT_DIR,
        help="directory for the eighteen PNG files",
    )
    arguments = parser.parse_args()
    generated = generate_assets(arguments.output_dir)
    print(f"Generated {len(generated)} atlases in {arguments.output_dir}")


if __name__ == "__main__":
    main()
