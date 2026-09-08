import ast
import hashlib
import importlib.util
import itertools
import math
import re
import tempfile
import unittest
from pathlib import Path
from unittest import mock

from PIL import Image, ImageChops, ImageFont, ImageStat


ROOT = Path(__file__).resolve().parents[2]
GENERATOR_PATH = ROOT / "packaging" / "generate_ios_touch_skin_assets.py"
ASSET_DIR = ROOT / "mods" / "ra2" / "uibits"
CHROME_PATH = ROOT / "mods" / "ra2" / "chrome.yaml"
METRICS_PATH = ROOT / "mods" / "ra2" / "metrics.yaml"
INGAME_LAYOUT_PATH = ROOT / "mods" / "ra2" / "chrome" / "ingame.yaml"
INGAME_PLAYER_PATH = ROOT / "mods" / "ra2" / "chrome" / "ingame-player.yaml"
MOD_PATH = ROOT / "mods" / "ra2" / "mod.yaml"
DEFAULT_FLUENT_PATH = ROOT / "mods" / "ra2" / "fluent" / "chrome.ftl"
ZH_CN_FLUENT_PATH = ROOT / "mods" / "ra2" / "fluent" / "zh-CN" / "chrome.ftl"
COMMAND_BAR_CATALOG_PATH = (
    ROOT / "OpenRA.Mods.RA2" / "Widgets" / "CommandBarPreferences.cs"
)
TOUCH_FACTION_SKIN_PATH = (
    ROOT / "engine" / "OpenRA.Mods.Common" / "Widgets" / "TouchFactionSkin.cs"
)
JOYSTICK_WIDGET_PATH = (
    ROOT
    / "engine"
    / "OpenRA.Mods.Common"
    / "Widgets"
    / "VirtualViewportJoystickWidget.cs"
)
ACTION_LOGIC_PATH = (
    ROOT
    / "engine"
    / "OpenRA.Mods.Common"
    / "Widgets"
    / "Logic"
    / "Ingame"
    / "IosViewportActionsLogic.cs"
)
COMMAND_BAR_WIDGET_PATH = (
    ROOT / "OpenRA.Mods.RA2" / "Widgets" / "CustomCommandBarWidget.cs"
)

FACTIONS = ("allies", "soviets", "yuri")
KINDS = ("actions", "joystick", "quickbar")
SIZES = {
    "actions": ((512, 512), (1024, 1024)),
    "joystick": ((256, 256), (512, 512)),
    "quickbar": ((512, 128), (1024, 256)),
}
NORMALIZED_SIZES = {
    "actions": (64, 64),
    "joystick": (64, 64),
    "quickbar": (128, 32),
}
MIN_FACTION_RMS_DIFFERENCE = 6.0

ACTION_NAMES = (
    "stop",
    "deploy",
    "select-type",
    "force-attack",
    "return-base",
)
ACTIVE_VIEWPORT_ACTIONS = (
    "DEPLOY",
    "SELECT_TYPE",
    "FORCE_ATTACK",
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
ACTION_LABEL_MESSAGES = (
    ("STOP", "button-ios-viewport-action-stop.label", "Stop", "停止"),
    ("DEPLOY", "button-ios-viewport-action-deploy.label", "Deploy", "部署"),
    ("SELECT_TYPE", "button-ios-viewport-action-select-type.label", "Type", "同类"),
    ("FORCE_ATTACK", "button-ios-viewport-action-force-attack.label", "Force", "强攻"),
    ("RETURN_BASE", "button-ios-viewport-action-return-base.label", "Base", "基地"),
)
ACTION_LABEL_SNAPSHOTS = (
    ((1560, 720), (932, 430)),
    ((1558, 720), (844, 390)),
    ((1133, 744), (1133, 744)),
    ((1180, 820), (1180, 820)),
    ((1366, 1024), (1366, 1024)),
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
PLAN_TOKENS = {
    "allies": {
        "base": (0x13, 0x28, 0x3A),
        "metal": (0x8B, 0xA4, 0xB5),
        "accent": (0x8F, 0xE7, 0xFF),
        "active": (0x5B, 0xE3, 0xFF),
    },
    "soviets": {
        "base": (0x24, 0x1A, 0x19),
        "metal": (0x71, 0x6B, 0x64),
        "accent": (0xF0, 0xA4, 0x3C),
        "active": (0xD9, 0x34, 0x28),
    },
    "yuri": {
        "base": (0x16, 0x0F, 0x1D),
        "metal": (0x75, 0x67, 0x80),
        "accent": (0xCF, 0x56, 0xDC),
        "active": (0x9D, 0xCC, 0x62),
    },
}
ACTION_REGIONS = tuple(
    (x, y, 56, 56)
    for y in (0, 64, 128, 192, 256, 320, 384)
    for x in (0, 64, 128, 192, 256)
)
JOYSTICK_REGIONS = (
    (0, 0, 144, 144),
    (160, 0, 64, 64),
    (160, 72, 64, 64),
)
PANEL_BORDER = 12
PANEL_CENTER_SIZE = (104, 40)
PANEL_SLOT_SIZE = (128, 64)
PANEL_SLOT_REGIONS = (
    (0, 64, *PANEL_SLOT_SIZE),
    (128, 64, *PANEL_SLOT_SIZE),
)
PANEL_RENDER_SIZE = (800, 68)
QUICKBAR_REGIONS = (
    *((x, 0, 48, 52) for x in range(0, 512, 64)),
    *PANEL_SLOT_REGIONS,
    (259, 67, 26, 26),
    (291, 67, 26, 26),
)


def asset_path(kind: str, faction: str, scale: int) -> Path:
    suffix = "-2x" if scale == 2 else ""
    return ASSET_DIR / f"ios-touch-{kind}-{faction}{suffix}.png"


def scaled_region(region: tuple[int, int, int, int], scale: int):
    return tuple(value * scale for value in region)


def parse_int_region(value: str) -> tuple[int, ...]:
    return tuple(int(part.strip()) for part in value.split(","))


def parse_chrome_collections():
    collections = {}
    current = None
    section = None
    for raw_line in CHROME_PATH.read_text(encoding="utf-8").splitlines():
        if not raw_line or raw_line.lstrip().startswith("#"):
            continue

        indent = len(raw_line) - len(raw_line.lstrip("\t"))
        content = raw_line.strip()
        if indent == 0 and content.endswith(":"):
            current = content[:-1]
            collections[current] = {}
            section = None
            continue

        if current is None or ":" not in content:
            continue

        key, value = content.split(":", 1)
        value = value.split(" #", 1)[0].strip()
        if indent == 1:
            section = key if not value else None
            if section == "Regions":
                collections[current][section] = {}
            elif value:
                collections[current][key] = value
        elif indent == 2 and section == "Regions":
            collections[current][section][key] = parse_int_region(value)

    return collections


def parse_metrics():
    metrics = {}
    for raw_line in METRICS_PATH.read_text(encoding="utf-8").splitlines():
        content = raw_line.strip()
        if not content or content.startswith("#") or ":" not in content:
            continue

        key, value = content.split(":", 1)
        value = value.split(" #", 1)[0].strip()
        if value:
            metrics[key] = value
    return metrics


def parse_miniyaml_nodes(path: Path):
    roots = []
    stack = [(-1, roots)]
    for line_number, raw_line in enumerate(
        path.read_text(encoding="utf-8").splitlines(), 1
    ):
        if not raw_line.strip() or raw_line.lstrip().startswith("#"):
            continue

        indent = len(raw_line) - len(raw_line.lstrip("\t"))
        content = raw_line.strip()
        if ":" not in content:
            raise AssertionError(f"invalid MiniYaml at {path}:{line_number}: {content}")

        key, value = content.split(":", 1)
        node = {
            "key": key,
            "value": value.split(" #", 1)[0].strip(),
            "children": [],
        }
        while stack[-1][0] >= indent:
            stack.pop()
        stack[-1][1].append(node)
        stack.append((indent, node["children"]))

    return roots


def parse_fluent_labels(path: Path):
    labels = {}
    current = None
    for raw_line in path.read_text(encoding="utf-8").splitlines():
        message = re.match(r"^([a-z0-9][a-z0-9-]*)\s*=", raw_line)
        if message:
            current = message.group(1)
            continue

        attribute = re.match(r"^\s+\.label\s*=\s*(\S(?:.*\S)?)\s*$", raw_line)
        if current is not None and attribute:
            labels[current] = attribute.group(1)

    return labels


def parse_font_definition(path: Path, name: str):
    lines = path.read_text(encoding="utf-8").splitlines()
    try:
        fonts_start = lines.index("Fonts:")
        definition_start = lines.index(f"\t{name}:", fonts_start + 1)
    except ValueError:
        return {}
    properties = {}
    for raw_line in lines[definition_start + 1 :]:
        if raw_line and not raw_line.startswith("\t"):
            break
        if raw_line.startswith("\t") and not raw_line.startswith("\t\t"):
            break
        if not raw_line.startswith("\t\t") or ":" not in raw_line:
            continue
        key, value = raw_line.strip().split(":", 1)
        properties[key] = value.strip()
    return properties


def find_miniyaml_node(nodes, key: str):
    matches = []

    def visit(items):
        for node in items:
            if node["key"] == key:
                matches.append(node)
            visit(node["children"])

    visit(nodes)
    if len(matches) != 1:
        raise AssertionError(f"expected one {key!r} node, found {len(matches)}")
    return matches[0]


def direct_miniyaml_child(node, key: str):
    matches = [child for child in node["children"] if child["key"] == key]
    if len(matches) != 1:
        raise AssertionError(
            f"expected one direct {key!r} child of {node['key']!r}, found {len(matches)}"
        )
    return matches[0]


def direct_miniyaml_value(node, key: str) -> str:
    return direct_miniyaml_child(node, key)["value"]


def parse_logic_list(value: str) -> tuple[str, ...]:
    return tuple(part.strip() for part in value.split(",") if part.strip())


def parse_command_bar_catalog_ids() -> tuple[str, ...]:
    source = COMMAND_BAR_CATALOG_PATH.read_text(encoding="utf-8")
    declaration = "public static readonly Slot[] All"
    start = source.index(declaration)
    end = source.index("\n\t\t};", start)
    initializer = source[start:end]
    return tuple(re.findall(r'new\(\s*"([A-Z0-9_]+)"\s*,', initializer))


def parse_command_bar_slots():
    tree = parse_miniyaml_nodes(INGAME_PLAYER_PATH)
    slots = find_miniyaml_node(tree, "Container@COMMAND_SLOTS")
    children = direct_miniyaml_child(slots, "Children")
    buttons = [
        node for node in children["children"] if node["key"].startswith("Button@")
    ]
    return slots, buttons


def parse_button_icon(button):
    children = direct_miniyaml_child(button, "Children")
    icon = direct_miniyaml_child(children, "Image@ICON")
    return (
        direct_miniyaml_value(icon, "ImageCollection"),
        direct_miniyaml_value(icon, "ImageName"),
    )


def resolve_chrome_collection(collections, name, resolving=()):
    if name in resolving:
        raise AssertionError(f"Chrome inheritance cycle: {' -> '.join((*resolving, name))}")

    source = collections[name]
    parent = source.get("Inherits")
    resolved = (
        resolve_chrome_collection(collections, parent, (*resolving, name))
        if parent
        else {}
    )
    resolved = {
        key: value.copy() if isinstance(value, dict) else value
        for key, value in resolved.items()
    }
    for key, value in source.items():
        if key == "Regions" and key in resolved:
            resolved[key].update(value)
        else:
            resolved[key] = value.copy() if isinstance(value, dict) else value
    return resolved


def panel_bounds(panel_region: tuple[int, ...]) -> tuple[int, int, int, int]:
    x, y, left, top, center_width, center_height, right, bottom = panel_region
    return (
        x,
        y,
        left + center_width + right,
        top + center_height + bottom,
    )


def rectangles_overlap(
    first: tuple[int, int, int, int],
    second: tuple[int, int, int, int],
) -> bool:
    first_x, first_y, first_width, first_height = first
    second_x, second_y, second_width, second_height = second
    return (
        first_x < second_x + second_width
        and second_x < first_x + first_width
        and first_y < second_y + second_height
        and second_y < first_y + first_height
    )


def crop_region(image: Image.Image, region: tuple[int, int, int, int]):
    x, y, width, height = region
    return image.crop((x, y, x + width, y + height))


def visual_alpha_bbox(image: Image.Image):
    return image.getchannel("A").getbbox()


def threshold_alpha_bbox(image: Image.Image, threshold: int = 96):
    mask = image.getchannel("A").point(
        lambda alpha: 255 if alpha >= threshold else 0
    )
    return mask.getbbox()


def horizontal_alpha_runs(image: Image.Image, y: int, threshold: int = 96):
    alpha = image.getchannel("A")
    occupied = [alpha.getpixel((x, y)) >= threshold for x in range(image.width)]
    runs = []
    start = None
    for x, visible in enumerate((*occupied, False)):
        if visible and start is None:
            start = x
        elif not visible and start is not None:
            runs.append((start, x))
            start = None
    return tuple(runs)


def horizontal_alpha_mean_x(image: Image.Image, y: int, threshold: int = 96):
    alpha = image.getchannel("A")
    visible_x = [
        x for x in range(image.width) if alpha.getpixel((x, y)) >= threshold
    ]
    if not visible_x:
        raise AssertionError(f"row {y} has no visible pixels")
    return sum(visible_x) / len(visible_x)


def alpha_weighted_centroid_x(image: Image.Image, threshold: int = 96) -> float:
    weighted_x = 0
    total_alpha = 0
    for y in range(image.height):
        for x in range(image.width):
            alpha = image.getpixel((x, y))[3]
            if alpha < threshold:
                continue
            weighted_x += x * alpha
            total_alpha += alpha
    if total_alpha == 0:
        raise AssertionError("image has no visible pixels")
    return weighted_x / total_alpha


def mean_visible_luminance(image: Image.Image, threshold: int = 128) -> float:
    visible = [
        (0.2126 * red + 0.7152 * green + 0.0722 * blue)
        for red, green, blue, alpha in image.getdata()
        if alpha >= threshold
    ]
    if not visible:
        return 0.0
    return sum(visible) / len(visible)


def adjacent_luminance_energy(image: Image.Image) -> float:
    grayscale = image.convert("L")
    pixels = grayscale.load()
    horizontal = (
        abs(pixels[x + 1, y] - pixels[x, y])
        for y in range(grayscale.height)
        for x in range(grayscale.width - 1)
    )
    vertical = (
        abs(pixels[x, y + 1] - pixels[x, y])
        for y in range(grayscale.height - 1)
        for x in range(grayscale.width)
    )
    differences = [*horizontal, *vertical]
    return sum(differences) / len(differences)


def close_color_count(
    image: Image.Image,
    target: tuple[int, int, int],
    tolerance: int = 3,
) -> int:
    return sum(
        alpha >= 128
        and all(abs(channel - expected) <= tolerance for channel, expected in zip(pixel, target))
        for *pixel, alpha in image.getdata()
    )


def close_color_bbox(
    image: Image.Image,
    target: tuple[int, int, int],
    tolerance: int = 3,
):
    mask = Image.new("1", image.size)
    mask.putdata(
        [
            alpha >= 128
            and all(
                abs(channel - expected) <= tolerance
                for channel, expected in zip(pixel, target)
            )
            for *pixel, alpha in image.getdata()
        ]
    )
    return mask.getbbox()


def render_nine_slice(
    panel: Image.Image,
    target_size: tuple[int, int],
    border: int,
) -> Image.Image:
    target_width, target_height = target_size
    if target_width < border * 2 or target_height < border * 2:
        raise ValueError("nine-slice target is smaller than its fixed borders")

    source_xs = (0, border, panel.width - border, panel.width)
    source_ys = (0, border, panel.height - border, panel.height)
    target_xs = (0, border, target_width - border, target_width)
    target_ys = (0, border, target_height - border, target_height)
    rendered = Image.new("RGBA", target_size, (0, 0, 0, 0))
    for row, column in itertools.product(range(3), range(3)):
        source = panel.crop(
            (
                source_xs[column],
                source_ys[row],
                source_xs[column + 1],
                source_ys[row + 1],
            )
        )
        destination = (
            target_xs[column],
            target_ys[row],
            target_xs[column + 1],
            target_ys[row + 1],
        )
        destination_size = (
            destination[2] - destination[0],
            destination[3] - destination[1],
        )
        if source.size != destination_size:
            source = source.resize(destination_size, Image.Resampling.BILINEAR)
        rendered.alpha_composite(source, destination[:2])
    return rendered


def unique_column_count(image: Image.Image) -> int:
    return len(
        {
            hashlib.sha256(image.crop((x, 0, x + 1, image.height)).tobytes()).digest()
            for x in range(image.width)
        }
    )


def unique_row_count(image: Image.Image) -> int:
    return len(
        {
            hashlib.sha256(image.crop((0, y, image.width, y + 1)).tobytes()).digest()
            for y in range(image.height)
        }
    )


def load_generator():
    spec = importlib.util.spec_from_file_location(
        "generate_ios_touch_skin_assets",
        GENERATOR_PATH,
    )
    if spec is None or spec.loader is None:
        raise RuntimeError(f"unable to load {GENERATOR_PATH}")

    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class IosTouchSkinAssetsTest(unittest.TestCase):
    def require_contract_files(self):
        self.assertTrue(GENERATOR_PATH.is_file(), GENERATOR_PATH)
        for kind, faction, scale in itertools.product(KINDS, FACTIONS, (1, 2)):
            path = asset_path(kind, faction, scale)
            self.assertTrue(path.is_file(), path)

    def test_generator_and_all_eighteen_atlases_exist(self):
        self.require_contract_files()

    def test_atlases_are_rgba_power_of_two_png_files_with_visible_pixels(self):
        self.require_contract_files()
        for kind, faction, scale in itertools.product(KINDS, FACTIONS, (1, 2)):
            path = asset_path(kind, faction, scale)
            with self.subTest(path=path.name), Image.open(path) as image:
                self.assertEqual("PNG", image.format)
                self.assertEqual("RGBA", image.mode)
                self.assertEqual(SIZES[kind][scale - 1], image.size)
                self.assertEqual(0, image.width & (image.width - 1))
                self.assertEqual(0, image.height & (image.height - 1))
                self.assertIsNotNone(image.getchannel("A").getbbox())

    def test_unused_atlas_gutters_are_fully_transparent(self):
        self.require_contract_files()
        regions_by_kind = {
            "actions": ACTION_REGIONS,
            "joystick": JOYSTICK_REGIONS,
            "quickbar": QUICKBAR_REGIONS,
        }

        for kind, faction, scale in itertools.product(KINDS, FACTIONS, (1, 2)):
            path = asset_path(kind, faction, scale)
            with self.subTest(path=path.name), Image.open(path) as image:
                alpha = image.getchannel("A")
                used = Image.new("1", image.size)
                for region in regions_by_kind[kind]:
                    x, y, width, height = scaled_region(region, scale)
                    used.paste(1, (x, y, x + width, y + height))

                unused_alpha = Image.new("L", image.size)
                unused_alpha.paste(alpha, mask=ImageChops.invert(used.convert("L")))
                self.assertIsNone(
                    unused_alpha.getbbox(),
                    f"opaque pixel escaped a declared region in {path.name}",
                )

    def test_faction_atlases_have_distinct_hashes_and_normalized_pixels(self):
        self.require_contract_files()
        for kind, scale in itertools.product(KINDS, (1, 2)):
            digests = set()
            normalized = {}
            for faction in FACTIONS:
                path = asset_path(kind, faction, scale)
                digests.add(hashlib.sha256(path.read_bytes()).hexdigest())
                with Image.open(path) as image:
                    matte = Image.new("RGBA", image.size, (18, 20, 24, 255))
                    composed = Image.alpha_composite(matte, image)
                    normalized[faction] = composed.convert("RGB").resize(
                        NORMALIZED_SIZES[kind],
                        Image.Resampling.LANCZOS,
                    )

            self.assertEqual(3, len(digests), f"{kind} {scale}x hashes")
            for first, second in itertools.combinations(FACTIONS, 2):
                difference = ImageChops.difference(
                    normalized[first],
                    normalized[second],
                )
                channel_rms = ImageStat.Stat(difference).rms
                rms = math.sqrt(
                    sum(value * value for value in channel_rms) / len(channel_rms)
                )
                self.assertGreaterEqual(
                    rms,
                    MIN_FACTION_RMS_DIFFERENCE,
                    f"{kind} {scale}x {first}/{second} RMS {rms:.2f}",
                )

    def test_action_grid_has_five_columns_and_seven_distinct_state_rows(self):
        self.require_contract_files()
        x_positions = (0, 64, 128, 192, 256)
        y_positions = (0, 64, 128, 192, 256, 320, 384)

        self.assertEqual(len(ACTION_NAMES), len(x_positions))
        self.assertEqual(len(ACTION_STATES), len(y_positions))
        for faction, scale in itertools.product(FACTIONS, (1, 2)):
            with Image.open(asset_path("actions", faction, scale)) as image:
                for action, x in zip(ACTION_NAMES, x_positions):
                    state_hashes = set()
                    for state, y in zip(ACTION_STATES, y_positions):
                        crop = crop_region(
                            image,
                            scaled_region((x, y, 56, 56), scale),
                        )
                        with self.subTest(
                            faction=faction,
                            scale=scale,
                            action=action,
                            state=state,
                        ):
                            self.assertEqual((56 * scale, 56 * scale), crop.size)
                            self.assertIsNotNone(crop.getchannel("A").getbbox())
                        state_hashes.add(hashlib.sha256(crop.tobytes()).hexdigest())

                    self.assertEqual(
                        len(ACTION_STATES),
                        len(state_hashes),
                        f"{faction} {action} {scale}x state crops",
                    )

    def test_action_pressed_pixels_are_inset_and_darker_than_normal(self):
        self.require_contract_files()
        generator = load_generator()
        label_region = getattr(generator, "ACTION_LABEL_REGION", (6, 36, 44, 14))

        self.assertEqual(ACTION_NAMES, generator.ACTION_NAMES)
        self.assertEqual(ACTION_STATES, generator.ACTION_STATES)
        for faction in FACTIONS:
            with Image.open(asset_path("actions", faction, 2)) as image:
                for column, action in enumerate(ACTION_NAMES):
                    normal = crop_region(
                        image,
                        (column * 128, 0, 112, 112),
                    )
                    pressed = crop_region(
                        image,
                        (column * 128, 384, 112, 112),
                    )
                    normal_bbox = visual_alpha_bbox(normal)
                    pressed_bbox = visual_alpha_bbox(pressed)
                    normal_shell = normal.copy()
                    pressed_shell = pressed.copy()
                    masked_label_region = scaled_region(label_region, 2)
                    mask_x, mask_y, mask_width, mask_height = masked_label_region
                    mask_box = (
                        mask_x,
                        mask_y,
                        mask_x + mask_width,
                        mask_y + mask_height,
                    )
                    normal_shell.paste((0, 0, 0, 0), mask_box)
                    pressed_shell.paste((0, 0, 0, 0), mask_box)
                    normal_bbox = visual_alpha_bbox(normal_shell)
                    pressed_bbox = visual_alpha_bbox(pressed_shell)
                    self.assertIsNotNone(normal_bbox)
                    self.assertIsNotNone(pressed_bbox)
                    with self.subTest(faction=faction, action=action, property="inset"):
                        self.assertGreaterEqual(pressed_bbox[0] - normal_bbox[0], 6)
                        self.assertGreaterEqual(pressed_bbox[1] - normal_bbox[1], 6)

                    normal_inner = normal.crop((28, 28, 84, 76))
                    pressed_inner = pressed.crop((28, 28, 84, 76))
                    with self.subTest(faction=faction, action=action, property="shade"):
                        self.assertLessEqual(
                            mean_visible_luminance(pressed_inner),
                            mean_visible_luminance(normal_inner) - 8.0,
                        )

    def test_action_label_wells_are_fixed_quiet_and_leave_distinct_glyphs_above(self):
        self.require_contract_files()
        generator = load_generator()

        self.assertEqual((6, 36, 44, 14), getattr(generator, "ACTION_LABEL_REGION", None))
        self.assertEqual(8, getattr(generator, "ACTION_GLYPH_RISE", None))
        label_region = generator.ACTION_LABEL_REGION
        for faction, scale in itertools.product(FACTIONS, (1, 2)):
            with Image.open(asset_path("actions", faction, scale)) as image:
                faction_wells = []
                for row, state in enumerate(ACTION_STATES):
                    wells = []
                    upper_hashes = set()
                    for column, action in enumerate(ACTION_NAMES):
                        cell = crop_region(
                            image,
                            scaled_region((column * 64, row * 64, 56, 56), scale),
                        )
                        well = crop_region(cell, scaled_region(label_region, scale))
                        core = well.crop(
                            (
                                4 * scale,
                                3 * scale,
                                well.width - 4 * scale,
                                well.height - 3 * scale,
                            )
                        )
                        upper = crop_region(cell, scaled_region((8, 5, 40, 34), scale))
                        below = crop_region(cell, scaled_region((6, 50, 44, 4), scale))
                        left_border = crop_region(cell, scaled_region((2, 35, 4, 15), scale))
                        right_border = crop_region(cell, scaled_region((50, 35, 4, 15), scale))
                        stable_well = core
                        wells.append(stable_well.tobytes())
                        faction_wells.append(stable_well.tobytes())
                        upper_hashes.add(hashlib.sha256(upper.tobytes()).digest())

                        alpha = list(well.getchannel("A").getdata())
                        center_alpha = well.getpixel((well.width // 2, well.height // 2))[3]
                        with self.subTest(
                            faction=faction,
                            scale=scale,
                            action=action,
                            state=state,
                        ):
                            self.assertGreaterEqual(
                                sum(value >= 128 for value in alpha) / len(alpha),
                                0.80,
                            )
                            self.assertGreaterEqual(center_alpha, 240)
                            self.assertIsNotNone(core.getchannel("A").getbbox())
                            self.assertLess(adjacent_luminance_energy(core), 3.0)
                            self.assertLess(mean_visible_luminance(core), 75.0)
                            self.assertIsNotNone(upper.getchannel("A").getbbox())
                            self.assertGreater(adjacent_luminance_energy(upper), 4.0)
                            self.assertIsNotNone(below.getchannel("A").getbbox())
                            side_alpha = (
                                list(left_border.getchannel("A").getdata())
                                + list(right_border.getchannel("A").getdata())
                            )
                            self.assertTrue(any(value > 0 for value in side_alpha))

                    with self.subTest(
                        faction=faction,
                        scale=scale,
                        state=state,
                        property="fixed-well",
                    ):
                        self.assertEqual(1, len(set(wells)))
                        self.assertEqual(
                            len(ACTION_NAMES),
                            len(upper_hashes),
                            "Each semantic action glyph must remain distinct above the label well.",
                        )

                with self.subTest(
                    faction=faction,
                    scale=scale,
                    property="state-stable-well",
                ):
                    self.assertEqual(1, len(set(faction_wells)))

    def test_action_label_well_alpha_composite_preserves_the_shell_at_clear_corners(self):
        self.require_contract_files()
        generator = load_generator()
        x, y, width, height = generator.ACTION_LABEL_REGION
        destination = (x * 2, y * 2)
        tile_size = (width * 2, height * 2)
        original_paste = Image.Image.paste
        original_alpha_composite = Image.Image.alpha_composite
        mismatches = []
        visible_shell_pixels = 0

        for faction, action, state in itertools.product(
            FACTIONS, ACTION_NAMES, ACTION_STATES
        ):
            palette = generator.PALETTES[faction]
            well = generator.render_action_label_well(palette)
            self.assertEqual(tile_size, well.size)
            corner_alpha = tuple(
                well.getpixel(point)[3]
                for point in (
                    (0, 0),
                    (well.width - 1, 0),
                    (0, well.height - 1),
                    (well.width - 1, well.height - 1),
                )
            )
            self.assertLess(
                max(corner_alpha),
                well.getpixel((well.width // 2, well.height // 2))[3],
            )

            before_well = []

            def capture_paste(target, overlay, box=None, mask=None):
                if box == destination and overlay.size == tile_size:
                    before_well.append(target.copy())
                return original_paste(target, overlay, box, mask)

            def capture_alpha_composite(target, overlay, dest=(0, 0), source=(0, 0)):
                if dest == destination and overlay.size == tile_size:
                    before_well.append(target.copy())
                return original_alpha_composite(target, overlay, dest, source)

            with mock.patch.object(Image.Image, "paste", capture_paste), mock.patch.object(
                Image.Image, "alpha_composite", capture_alpha_composite
            ):
                rendered = generator.render_action_cell(palette, action, state)

            self.assertEqual(1, len(before_well))
            shell = before_well[0]
            for local_y in range(well.height):
                for local_x in range(well.width):
                    if well.getpixel((local_x, local_y))[3] != 0:
                        continue

                    point = (destination[0] + local_x, destination[1] + local_y)
                    expected = shell.getpixel(point)
                    actual = rendered.getpixel(point)
                    if expected[3] > 0:
                        visible_shell_pixels += 1
                    if actual != expected:
                        mismatches.append((faction, action, state, point, expected, actual))

        self.assertGreater(visible_shell_pixels, 0, "The regression fixture must cover visible shell pixels.")
        self.assertEqual(
            [],
            mismatches[:10],
            f"{len(mismatches)} transparent well pixels erased their underlying shell RGBA.",
        )

    def test_joystick_regions_match_the_runtime_contract(self):
        self.require_contract_files()
        for faction, scale in itertools.product(FACTIONS, (1, 2)):
            with Image.open(asset_path("joystick", faction, scale)) as image:
                crops = [
                    crop_region(image, scaled_region(region, scale))
                    for region in JOYSTICK_REGIONS
                ]
                with self.subTest(faction=faction, scale=scale):
                    self.assertEqual((144 * scale, 144 * scale), crops[0].size)
                    self.assertEqual((64 * scale, 64 * scale), crops[1].size)
                    self.assertEqual((64 * scale, 64 * scale), crops[2].size)
                    self.assertTrue(
                        all(crop.getchannel("A").getbbox() for crop in crops)
                    )
                    self.assertNotEqual(crops[1].tobytes(), crops[2].tobytes())

    def test_quickbar_eight_button_states_and_twelve_pixel_panels(self):
        self.require_contract_files()
        generator = load_generator()
        for faction, scale in itertools.product(FACTIONS, (1, 2)):
            with Image.open(asset_path("quickbar", faction, scale)) as image:
                cells = [
                    crop_region(image, scaled_region((x, 0, 48, 52), scale))
                    for x in range(0, 512, 64)
                ]
                center_mask = (6 * scale, 7 * scale, 42 * scale, 48 * scale)
                shell_hashes = set()
                for cell in cells:
                    shell = cell.copy()
                    shell.paste((0, 0, 0, 0), center_mask)
                    shell_hashes.add(hashlib.sha256(shell.tobytes()).digest())
                with self.subTest(faction=faction, scale=scale, region="cells"):
                    self.assertEqual(8, len(shell_hashes))
                    self.assertTrue(
                        all(cell.getchannel("A").getbbox() for cell in cells)
                    )

                if scale == 2:
                    for state, cell in zip(QUICKBAR_STATES, cells):
                        center = cell.crop((24, 30, 72, 76))
                        with self.subTest(
                            faction=faction,
                            state=state,
                            property="empty-center",
                        ):
                            self.assertLess(adjacent_luminance_energy(center), 4.0)

                    for baseline, pressed in ((cells[0], cells[2]), (cells[3], cells[5])):
                        baseline_bbox = visual_alpha_bbox(baseline)
                        pressed_bbox = visual_alpha_bbox(pressed)
                        self.assertIsNotNone(baseline_bbox)
                        self.assertIsNotNone(pressed_bbox)
                        self.assertGreaterEqual(pressed_bbox[0] - baseline_bbox[0], 6)
                        self.assertGreaterEqual(pressed_bbox[1] - baseline_bbox[1], 6)

                    self.assertLess(
                        mean_visible_luminance(cells[6]),
                        mean_visible_luminance(cells[0]) - 8.0,
                    )
                    self.assertLess(
                        mean_visible_luminance(cells[7]),
                        mean_visible_luminance(cells[3]) - 8.0,
                    )

                panels = [
                    crop_region(image, scaled_region(region, scale))
                    for region in PANEL_SLOT_REGIONS
                ]
                self.assertNotEqual(panels[0].tobytes(), panels[1].tobytes())
                border = PANEL_BORDER * scale
                target_size = tuple(value * scale for value in PANEL_RENDER_SIZE)
                short_size = (PANEL_SLOT_SIZE[0] * scale, target_size[1])
                for panel_index, panel in enumerate(panels):
                    with self.subTest(
                        faction=faction,
                        scale=scale,
                        panel=panel_index,
                        property="total-slot-size",
                    ):
                        self.assertEqual(
                            tuple(value * scale for value in PANEL_SLOT_SIZE),
                            panel.size,
                        )

                    alpha = panel.getchannel("A")
                    xs = (0, border, panel.width - border, panel.width)
                    ys = (0, border, panel.height - border, panel.height)
                    with self.subTest(
                        faction=faction,
                        scale=scale,
                        panel=panel_index,
                        property="center-size",
                    ):
                        self.assertEqual(
                            tuple(value * scale for value in PANEL_CENTER_SIZE),
                            (xs[2] - xs[1], ys[2] - ys[1]),
                        )

                    for row, column in itertools.product(range(3), range(3)):
                        nine_slice = alpha.crop(
                            (xs[column], ys[row], xs[column + 1], ys[row + 1])
                        )
                        with self.subTest(
                            faction=faction,
                            scale=scale,
                            panel=panel_index,
                            row=row,
                            column=column,
                        ):
                            self.assertIsNotNone(nine_slice.getbbox())

                    top_center = panel.crop((xs[1], ys[0], xs[2], ys[1]))
                    bottom_center = panel.crop((xs[1], ys[2], xs[2], ys[3]))
                    left_center = panel.crop((xs[0], ys[1], xs[1], ys[2]))
                    right_center = panel.crop((xs[2], ys[1], xs[3], ys[2]))
                    stretch_center = panel.crop((xs[1], ys[1], xs[2], ys[2]))
                    with self.subTest(
                        faction=faction,
                        scale=scale,
                        panel=panel_index,
                        property="horizontal-stretch-safety",
                    ):
                        self.assertEqual(1, unique_column_count(top_center))
                        self.assertEqual(1, unique_column_count(bottom_center))
                        self.assertEqual(1, unique_column_count(stretch_center))
                    with self.subTest(
                        faction=faction,
                        scale=scale,
                        panel=panel_index,
                        property="vertical-stretch-safety",
                    ):
                        self.assertEqual(1, unique_row_count(left_center))
                        self.assertEqual(1, unique_row_count(right_center))

                    short_render = render_nine_slice(panel, short_size, border)
                    wide_render = render_nine_slice(panel, target_size, border)
                    with self.subTest(
                        faction=faction,
                        scale=scale,
                        panel=panel_index,
                        property="complete-render-coverage",
                    ):
                        self.assertGreater(alpha.getextrema()[0], 0)
                        self.assertGreater(
                            short_render.getchannel("A").getextrema()[0],
                            0,
                        )
                        self.assertGreater(
                            wide_render.getchannel("A").getextrema()[0],
                            0,
                        )

                    source_cap_boxes = (
                        (0, 0, border, panel.height),
                        (panel.width - border, 0, panel.width, panel.height),
                    )
                    short_cap_boxes = (
                        (0, 0, border, short_render.height),
                        (
                            short_render.width - border,
                            0,
                            short_render.width,
                            short_render.height,
                        ),
                    )
                    wide_cap_boxes = (
                        (0, 0, border, wide_render.height),
                        (
                            wide_render.width - border,
                            0,
                            wide_render.width,
                            wide_render.height,
                        ),
                    )
                    for side, short_box, wide_box in zip(
                        ("left", "right"),
                        short_cap_boxes,
                        wide_cap_boxes,
                    ):
                        short_cap = short_render.crop(short_box)
                        wide_cap = wide_render.crop(wide_box)
                        with self.subTest(
                            faction=faction,
                            scale=scale,
                            panel=panel_index,
                            side=side,
                            property="fixed-cap-hash",
                        ):
                            self.assertEqual(border, short_cap.width)
                            self.assertEqual(border, wide_cap.width)
                            self.assertEqual(
                                hashlib.sha256(short_cap.tobytes()).digest(),
                                hashlib.sha256(wide_cap.tobytes()).digest(),
                            )

                    corner_boxes = (
                        (0, 0, border, border),
                        (panel.width - border, 0, panel.width, border),
                        (0, panel.height - border, border, panel.height),
                        (
                            panel.width - border,
                            panel.height - border,
                            panel.width,
                            panel.height,
                        ),
                    )
                    rendered_corner_boxes = (
                        (0, 0, border, border),
                        (
                            wide_render.width - border,
                            0,
                            wide_render.width,
                            border,
                        ),
                        (
                            0,
                            wide_render.height - border,
                            border,
                            wide_render.height,
                        ),
                        (
                            wide_render.width - border,
                            wide_render.height - border,
                            wide_render.width,
                            wide_render.height,
                        ),
                    )
                    for corner, rendered_corner in zip(
                        corner_boxes,
                        rendered_corner_boxes,
                    ):
                        with self.subTest(
                            faction=faction,
                            scale=scale,
                            panel=panel_index,
                            corner=corner,
                            property="fixed-corner-hash",
                        ):
                            self.assertEqual(
                                hashlib.sha256(panel.crop(corner).tobytes()).digest(),
                                hashlib.sha256(
                                    wide_render.crop(rendered_corner).tobytes()
                                ).digest(),
                            )

                    if faction == "yuri":
                        crystal_widths = []
                        for source_box, short_box, wide_box in zip(
                            source_cap_boxes,
                            short_cap_boxes,
                            wide_cap_boxes,
                        ):
                            source_bbox = close_color_bbox(
                                panel.crop(source_box),
                                PLAN_TOKENS[faction]["accent"],
                                tolerance=6,
                            )
                            short_bbox = close_color_bbox(
                                short_render.crop(short_box),
                                PLAN_TOKENS[faction]["accent"],
                                tolerance=6,
                            )
                            wide_bbox = close_color_bbox(
                                wide_render.crop(wide_box),
                                PLAN_TOKENS[faction]["accent"],
                                tolerance=6,
                            )
                            if source_bbox is None:
                                continue
                            self.assertIsNotNone(short_bbox)
                            self.assertIsNotNone(wide_bbox)
                            crystal_widths.append(source_bbox[2] - source_bbox[0])
                            self.assertEqual(
                                short_bbox[2] - short_bbox[0],
                                wide_bbox[2] - wide_bbox[0],
                            )
                        with self.subTest(
                            faction=faction,
                            scale=scale,
                            panel=panel_index,
                            property="fixed-crystal-width",
                        ):
                            self.assertTrue(crystal_widths)

                collapse = crop_region(
                    image,
                    scaled_region((259, 67, 26, 26), scale),
                )
                expand = crop_region(
                    image,
                    scaled_region((291, 67, 26, 26), scale),
                )
                self.assertIsNotNone(collapse.getchannel("A").getbbox())
                self.assertIsNotNone(expand.getchannel("A").getbbox())
                self.assertNotEqual(collapse.tobytes(), expand.tobytes())

        self.assertEqual(
            QUICKBAR_STATES,
            getattr(generator, "QUICKBAR_STATES", None),
        )

    def test_quickbar_toggle_uses_large_centered_horizontal_double_chevrons(self):
        self.require_contract_files()
        generator = load_generator()
        base_regions = {
            "collapse": (259, 67, 26, 26),
            "expand": (291, 67, 26, 26),
        }

        for faction, scale in itertools.product(FACTIONS, (1, 2)):
            regions = {
                name: scaled_region(region, scale)
                for name, region in base_regions.items()
            }
            with Image.open(asset_path("quickbar", faction, scale)) as atlas:
                glyphs = {
                    name: crop_region(atlas, region)
                    for name, region in regions.items()
                }

            generated_atlas = generator.render_master("quickbar", faction)
            if scale == 1:
                generated_atlas = generated_atlas.resize(
                    SIZES["quickbar"][0], Image.Resampling.LANCZOS
                )
            expected = {
                name: crop_region(generated_atlas, region)
                for name, region in regions.items()
            }
            for name in ("collapse", "expand"):
                with self.subTest(
                    faction=faction,
                    scale=scale,
                    name=name,
                    property="installed",
                ):
                    self.assertEqual(expected[name].tobytes(), glyphs[name].tobytes())

                bbox = threshold_alpha_bbox(glyphs[name])
                with self.subTest(
                    faction=faction,
                    scale=scale,
                    name=name,
                    property="geometry",
                ):
                    self.assertIsNotNone(bbox)
                    left, top, right, bottom = bbox
                    self.assertGreaterEqual(right - left, 18 * scale)
                    self.assertGreaterEqual(bottom - top, 14 * scale)
                    self.assertLessEqual(right - left, 22 * scale)
                    self.assertLessEqual(bottom - top, 19 * scale)
                    self.assertLessEqual(abs((left + right) / 2 - 13 * scale), 1)
                    self.assertLessEqual(abs((top + bottom) / 2 - 13 * scale), 1)
                    self.assertLessEqual(
                        abs(
                            alpha_weighted_centroid_x(glyphs[name])
                            - (glyphs[name].width - 1) / 2
                        ),
                        0.35 * scale,
                    )

                runs = horizontal_alpha_runs(glyphs[name], 13 * scale)
                with self.subTest(
                    faction=faction,
                    scale=scale,
                    name=name,
                    property="double",
                ):
                    self.assertEqual(2, len(runs), runs)
                    for start, end in runs:
                        self.assertGreaterEqual(end - start, 6 * scale)
                        self.assertLessEqual(end - start, 8 * scale)
                    self.assertGreaterEqual(runs[1][0] - runs[0][1], scale)
                    self.assertLessEqual(runs[1][0] - runs[0][1], 3 * scale)

                tip_x = horizontal_alpha_mean_x(glyphs[name], 13 * scale)
                tail_x = sum(
                    horizontal_alpha_mean_x(glyphs[name], y * scale)
                    for y in (6, 20)
                ) / 2
                with self.subTest(
                    faction=faction,
                    scale=scale,
                    name=name,
                    property="direction",
                ):
                    if name == "collapse":
                        self.assertGreaterEqual(tail_x - tip_x, 2 * scale)
                    else:
                        self.assertGreaterEqual(tip_x - tail_x, 2 * scale)
                    self.assertGreaterEqual(
                        close_color_count(
                            glyphs[name],
                            generator.PALETTES[faction].glyph,
                            tolerance=6,
                        ),
                        50 * scale * scale,
                    )

            with self.subTest(
                faction=faction,
                scale=scale,
                property="horizontal-mirror",
            ):
                self.assertEqual(
                    glyphs["collapse"]
                    .transpose(Image.Transpose.FLIP_LEFT_RIGHT)
                    .tobytes(),
                    glyphs["expand"].tobytes(),
                )

    def test_one_x_files_are_exact_lanczos_downsamples_of_two_x_masters(self):
        self.require_contract_files()
        for kind, faction in itertools.product(KINDS, FACTIONS):
            with Image.open(asset_path(kind, faction, 2)) as master:
                expected = master.resize(SIZES[kind][0], Image.Resampling.LANCZOS)
            with Image.open(asset_path(kind, faction, 1)) as installed:
                with self.subTest(kind=kind, faction=faction):
                    self.assertEqual(expected.tobytes(), installed.tobytes())

    def test_two_x_runtime_regions_are_strict_doubles(self):
        all_regions = (*ACTION_REGIONS, *JOYSTICK_REGIONS, *QUICKBAR_REGIONS)
        for region in all_regions:
            self.assertEqual(
                tuple(value * 2 for value in region),
                scaled_region(region, 2),
            )

    def test_touch_metrics_preserve_global_yuri_and_define_all_skin_suffixes(self):
        metrics = parse_metrics()
        expected = {
            "america": "allies",
            "korea": "allies",
            "france": "allies",
            "germany": "allies",
            "england": "allies",
            "libya": "soviets",
            "cuba": "soviets",
            "iraq": "soviets",
            "russia": "soviets",
            "yuri": "yuri",
        }

        self.assertEqual("soviets", metrics.get("FactionSuffix-yuri"))
        for internal_name, skin in expected.items():
            with self.subTest(internal_name=internal_name):
                self.assertEqual(
                    skin,
                    metrics.get(f"TouchFactionSuffix-{internal_name}"),
                )

    def test_runtime_sources_select_every_exact_faction_collection(self):
        collections = parse_chrome_collections()
        exact_runtime_collections = set()
        for faction in FACTIONS:
            exact_runtime_collections.update(
                {
                    f"ios-touch-actions-{faction}",
                    f"ios-touch-joystick-{faction}",
                    f"ios-touch-quickbar-panel-{faction}",
                    f"ios-touch-quickbar-panel-compact-{faction}",
                    f"ios-touch-quickbar-button-{faction}",
                    f"ios-touch-quickbar-toggle-{faction}",
                }
            )

        self.assertTrue(exact_runtime_collections.issubset(collections))
        skin_source = TOUCH_FACTION_SKIN_PATH.read_text(encoding="utf-8")
        joystick_source = JOYSTICK_WIDGET_PATH.read_text(encoding="utf-8")
        action_source = ACTION_LOGIC_PATH.read_text(encoding="utf-8")
        command_bar_source = COMMAND_BAR_WIDGET_PATH.read_text(encoding="utf-8")
        self.assertIn("TouchFactionSkin.JoystickCollection(skin)", joystick_source)
        self.assertIn("TouchFactionSkin.ActionCollection(skin)", action_source)
        self.assertIn("TouchFactionSkin.QuickbarPanelCollection(skin, expanded)", command_bar_source)
        self.assertIn("TouchFactionSkin.QuickbarButtonCollection(skin)", command_bar_source)
        self.assertIn("TouchFactionSkin.QuickbarToggleCollection(skin)", command_bar_source)
        for prefix in (
            "ios-touch-actions-",
            "ios-touch-joystick-",
            "ios-touch-quickbar-panel-",
            "ios-touch-quickbar-panel-compact-",
            "ios-touch-quickbar-button-",
            "ios-touch-quickbar-toggle-",
        ):
            with self.subTest(prefix=prefix):
                self.assertIn(prefix, skin_source)

    def test_command_bar_catalog_order_and_touch_shortcut_symbols_are_preserved(self):
        slots, buttons = parse_command_bar_slots()
        catalog_ids = parse_command_bar_catalog_ids()
        yaml_ids = tuple(button["key"].removeprefix("Button@") for button in buttons)
        self.assertEqual(catalog_ids, yaml_ids)
        self.assertEqual(
            ("CommandBarLogic", "StanceSelectorLogic", "SelectionCommandBarLogic"),
            parse_logic_list(direct_miniyaml_value(slots, "Logic")),
        )

        by_id = {
            button["key"].removeprefix("Button@"): button for button in buttons
        }
        expected_touch_icons = {
            "GROUP_01": ("ios-commandbar-icons-v2", "group-1"),
            "GROUP_02": ("ios-commandbar-icons-v2", "group-2"),
            "GROUP_03": ("ios-commandbar-icons-v2", "group-3"),
            "GROUP_04": ("ios-commandbar-icons-v2", "group-4"),
            "GROUP_05": ("ios-commandbar-icons-v2", "group-5"),
            "PRODUCTION_X5": ("production-x5-icon", "production-x5"),
            "ATTACK_MOVE": ("ios-commandbar-icons-v2", "attack-move"),
            "SCATTER": ("ios-commandbar-icons-v2", "scatter"),
            "QUEUE_ORDERS": ("ios-commandbar-icons-v2", "queue-orders"),
        }
        for slot_id, expected_icon in expected_touch_icons.items():
            with self.subTest(slot=slot_id):
                self.assertEqual(expected_icon, parse_button_icon(by_id[slot_id]))

        self.assertEqual(
            ("ProductionBatchToggleLogic", "AddFactionSuffixLogic"),
            parse_logic_list(direct_miniyaml_value(by_id["PRODUCTION_X5"], "Logic")),
        )

    def test_three_active_viewport_action_ids_order_and_allied_fallbacks_are_preserved(self):
        tree = parse_miniyaml_nodes(INGAME_LAYOUT_PATH)
        actions = find_miniyaml_node(tree, "Container@IOS_VIEWPORT_ACTIONS")
        children = direct_miniyaml_child(actions, "Children")
        buttons = [
            node for node in children["children"] if node["key"].startswith("Button@")
        ]
        expected = (
            ("DEPLOY", "ios-touch-actions-allies", "ios-deploy"),
            ("SELECT_TYPE", "ios-touch-actions-allies", "ios-select-type"),
            ("FORCE_ATTACK", "ios-touch-actions-allies", "ios-force-attack"),
        )
        self.assertEqual(ACTIVE_VIEWPORT_ACTIONS, tuple(
            slot_id for slot_id, _, _ in expected
        ))
        self.assertEqual(tuple(slot_id for slot_id, _, _ in expected), tuple(
            button["key"].removeprefix("Button@") for button in buttons
        ))
        for button, (slot_id, image_collection, image_name) in zip(buttons, expected):
            with self.subTest(slot=slot_id):
                self.assertEqual(
                    (image_collection, image_name),
                    parse_button_icon(button),
                )

    def test_three_active_viewport_action_labels_have_exact_click_through_yaml_contracts(self):
        tree = parse_miniyaml_nodes(INGAME_LAYOUT_PATH)
        actions = find_miniyaml_node(tree, "Container@IOS_VIEWPORT_ACTIONS")
        children = direct_miniyaml_child(actions, "Children")
        buttons = {
            node["key"].removeprefix("Button@"): node
            for node in children["children"]
            if node["key"].startswith("Button@")
        }
        label_messages = {
            slot_id: message
            for slot_id, message, _, _ in ACTION_LABEL_MESSAGES
        }
        self.assertEqual(ACTIVE_VIEWPORT_ACTIONS, tuple(buttons))

        for slot_id in ACTIVE_VIEWPORT_ACTIONS:
            message = label_messages[slot_id]
            button_children = direct_miniyaml_child(buttons[slot_id], "Children")
            with self.subTest(slot=slot_id, property="child-order"):
                self.assertEqual(
                    ("Image@ICON", "Label@LABEL"),
                    tuple(node["key"] for node in button_children["children"]),
                )

            icon = direct_miniyaml_child(button_children, "Image@ICON")
            label = direct_miniyaml_child(button_children, "Label@LABEL")
            expected_values = {
                "Text": message,
                "Align": "Center",
                "VAlign": "Middle",
                "Font": "TinyBold",
                "TextColor": "F8F4E8",
                "Contrast": "true",
                "ContrastRadius": "1",
                "IgnoreMouseOver": "true",
            }
            with self.subTest(slot=slot_id, property="input"):
                self.assertEqual("true", direct_miniyaml_value(icon, "IgnoreMouseOver"))
                self.assertNotIn(
                    "ColorBlock@LABEL_PLATE",
                    tuple(node["key"] for node in button_children["children"]),
                )
            for key, expected in expected_values.items():
                with self.subTest(slot=slot_id, property=key):
                    self.assertEqual(expected, direct_miniyaml_value(label, key))

    def test_five_viewport_action_fluent_compatibility_messages_are_preserved(self):
        default_labels = parse_fluent_labels(DEFAULT_FLUENT_PATH)
        zh_cn_labels = parse_fluent_labels(ZH_CN_FLUENT_PATH)
        for _, reference, english, chinese in ACTION_LABEL_MESSAGES:
            message = reference.removesuffix(".label")
            with self.subTest(message=message, locale="default"):
                self.assertEqual(english, default_labels.get(message))
            with self.subTest(message=message, locale="zh-CN"):
                self.assertEqual(chinese, zh_cn_labels.get(message))

    def test_viewport_action_label_fonts_fit_every_localized_label_bounds(self):
        tiny_bold = parse_font_definition(MOD_PATH, "TinyBold")
        ios_touch = parse_font_definition(MOD_PATH, "IosTouchLabel")
        self.assertEqual("ra2|fonts/NotoSansCJKsc-Bold.otf", ios_touch.get("Font"))
        self.assertEqual("20", ios_touch.get("Size"))
        self.assertEqual("16", ios_touch.get("Ascender"))

        font_path = ROOT / ios_touch["Font"].replace("ra2|", "mods/ra2/")
        self.assertTrue(font_path.is_file(), font_path)
        font_sizes = {
            "TinyBold": int(tiny_bold["Size"]),
            "IosTouchLabel": int(ios_touch["Size"]),
        }
        contrast_radius = 1
        localized_values = tuple(
            value
            for _, _, english, chinese in ACTION_LABEL_MESSAGES
            for value in (english, chinese)
        )
        for effective, native in ACTION_LABEL_SNAPSHOTS:
            logical_per_point = max(
                effective[0] / native[0],
                effective[1] / native[1],
            )
            button_diameter = math.ceil(56 * logical_per_point)
            horizontal_inset = math.ceil(6 * logical_per_point)
            label_width = button_diameter - 2 * horizontal_inset
            label_height = math.ceil(14 * logical_per_point)
            font_name = "IosTouchLabel" if min(native) < 600 else "TinyBold"
            font = ImageFont.truetype(str(font_path), font_sizes[font_name])

            for value in localized_values:
                bounding_box = font.getbbox(value)
                rendered_width = max(
                    math.ceil(font.getlength(value)),
                    bounding_box[2] - bounding_box[0],
                ) + 2 * contrast_radius
                rendered_height = bounding_box[3] - bounding_box[1] + 2 * contrast_radius
                with self.subTest(
                    effective=effective,
                    native=native,
                    font=font_name,
                    text=value,
                ):
                    self.assertLessEqual(rendered_width, label_width)
                    self.assertLessEqual(rendered_height, label_height)

    def test_touch_action_chrome_grid_and_highlight_remap_match_the_atlas(self):
        collections = parse_chrome_collections()
        for faction in FACTIONS:
            base_name = f"ios-touch-actions-{faction}"
            highlighted_name = f"{base_name}-highlighted"
            self.assertIn(base_name, collections)
            self.assertIn(highlighted_name, collections)

            expected_regions = {}
            for column, action in enumerate(ACTION_NAMES):
                image_name = f"ios-{action}"
                for row, state in enumerate(ACTION_STATES):
                    suffix = "" if state == "normal" else f"-{state}"
                    expected_regions[f"{image_name}{suffix}"] = (
                        column * 64,
                        row * 64,
                        56,
                        56,
                    )

            base = collections[base_name]
            with self.subTest(faction=faction, collection=base_name):
                self.assertEqual(f"ios-touch-actions-{faction}.png", base.get("Image"))
                self.assertEqual(
                    f"ios-touch-actions-{faction}-2x.png",
                    base.get("Image2x"),
                )
                self.assertEqual(expected_regions, base.get("Regions"))

            expected_highlighted = {}
            for column, action in enumerate(ACTION_NAMES):
                image_name = f"ios-{action}"
                expected_highlighted[image_name] = (column * 64, 256, 56, 56)
                expected_highlighted[f"{image_name}-hover"] = (
                    column * 64,
                    320,
                    56,
                    56,
                )
                expected_highlighted[f"{image_name}-pressed"] = (
                    column * 64,
                    384,
                    56,
                    56,
                )

            highlighted = collections[highlighted_name]
            with self.subTest(faction=faction, collection=highlighted_name):
                self.assertEqual(base_name, highlighted.get("Inherits"))
                self.assertEqual(expected_highlighted, highlighted.get("Regions"))

    def test_touch_joystick_and_quickbar_chrome_regions_match_the_atlases(self):
        collections = parse_chrome_collections()
        button_suffixes = (
            "",
            "-hover",
            "-pressed",
            "-highlighted",
            "-highlighted-hover",
            "-highlighted-pressed",
            "-disabled",
            "-highlighted-disabled",
        )
        expected_joystick_regions = {
            "base": (0, 0, 144, 144),
            "thumb": (160, 0, 64, 64),
            "thumb-active": (160, 72, 64, 64),
        }
        expected_toggle_regions = {
            "collapse": (259, 67, 26, 26),
            "expand": (291, 67, 26, 26),
        }

        for faction in FACTIONS:
            joystick_name = f"ios-touch-joystick-{faction}"
            joystick = collections.get(joystick_name, {})
            with self.subTest(faction=faction, collection=joystick_name):
                self.assertEqual(
                    f"ios-touch-joystick-{faction}.png",
                    joystick.get("Image"),
                )
                self.assertEqual(
                    f"ios-touch-joystick-{faction}-2x.png",
                    joystick.get("Image2x"),
                )
                self.assertEqual(expected_joystick_regions, joystick.get("Regions"))

            for index, suffix in enumerate(button_suffixes):
                button_name = f"ios-touch-quickbar-button-{faction}{suffix}"
                button = collections.get(button_name, {})
                with self.subTest(faction=faction, collection=button_name):
                    self.assertEqual(
                        f"ios-touch-quickbar-{faction}.png",
                        button.get("Image"),
                    )
                    self.assertEqual(
                        f"ios-touch-quickbar-{faction}-2x.png",
                        button.get("Image2x"),
                    )
                    self.assertEqual(
                        (index * 64, 0, 0, 0, 48, 52, 0, 0),
                        parse_int_region(button.get("PanelRegion", "")),
                    )
                    self.assertEqual("Center", button.get("PanelSides"))

            panel_names = (
                f"ios-touch-quickbar-panel-{faction}",
                f"ios-touch-quickbar-panel-compact-{faction}",
            )
            expected_panels = (
                (0, 64, 12, 12, 104, 40, 12, 12),
                (128, 64, 12, 12, 104, 40, 12, 12),
            )
            panel_rectangles = []
            for panel_name, expected_panel in zip(panel_names, expected_panels):
                panel = collections.get(panel_name, {})
                with self.subTest(faction=faction, collection=panel_name):
                    self.assertEqual(
                        f"ios-touch-quickbar-{faction}.png",
                        panel.get("Image"),
                    )
                    self.assertEqual(
                        f"ios-touch-quickbar-{faction}-2x.png",
                        panel.get("Image2x"),
                    )
                    actual_panel = parse_int_region(panel.get("PanelRegion", ""))
                    self.assertEqual(expected_panel, actual_panel)
                    self.assertEqual((128, 64), panel_bounds(actual_panel)[2:])
                    panel_rectangles.append(panel_bounds(actual_panel))

            toggle_name = f"ios-touch-quickbar-toggle-{faction}"
            toggle = collections.get(toggle_name, {})
            with self.subTest(faction=faction, collection=toggle_name):
                self.assertEqual(
                    f"ios-touch-quickbar-{faction}.png",
                    toggle.get("Image"),
                )
                self.assertEqual(
                    f"ios-touch-quickbar-{faction}-2x.png",
                    toggle.get("Image2x"),
                )
                self.assertEqual(expected_toggle_regions, toggle.get("Regions"))

            for panel_rectangle, toggle_rectangle in itertools.product(
                panel_rectangles,
                expected_toggle_regions.values(),
            ):
                with self.subTest(
                    faction=faction,
                    panel=panel_rectangle,
                    toggle=toggle_rectangle,
                ):
                    self.assertFalse(
                        rectangles_overlap(panel_rectangle, toggle_rectangle)
                    )

    def test_every_touch_chrome_region_fits_its_one_x_and_two_x_image(self):
        collections = parse_chrome_collections()
        button_suffixes = (
            "",
            "-hover",
            "-pressed",
            "-highlighted",
            "-highlighted-hover",
            "-highlighted-pressed",
            "-disabled",
            "-highlighted-disabled",
        )
        names = []
        for faction in FACTIONS:
            names.extend(
                (
                    f"ios-touch-actions-{faction}",
                    f"ios-touch-actions-{faction}-highlighted",
                    f"ios-touch-joystick-{faction}",
                    *(f"ios-touch-quickbar-button-{faction}{suffix}" for suffix in button_suffixes),
                    f"ios-touch-quickbar-panel-{faction}",
                    f"ios-touch-quickbar-panel-compact-{faction}",
                    f"ios-touch-quickbar-toggle-{faction}",
                )
            )

        for name in names:
            with self.subTest(collection=name, property="declared"):
                self.assertIn(name, collections)
            if name not in collections:
                continue

            collection = resolve_chrome_collection(collections, name)
            image_names = (collection.get("Image"), collection.get("Image2x"))
            with self.subTest(collection=name, property="image-fields"):
                self.assertTrue(all(image_names))
            if not all(image_names):
                continue

            regions = list(collection.get("Regions", {}).values())
            if "PanelRegion" in collection:
                panel_region = parse_int_region(collection["PanelRegion"])
                with self.subTest(collection=name, property="panel-region-length"):
                    self.assertEqual(8, len(panel_region))
                if len(panel_region) == 8:
                    regions.append(panel_bounds(panel_region))

            with self.subTest(collection=name, property="drawable-region"):
                self.assertTrue(regions)

            for scale, image_name in enumerate(image_names, start=1):
                image_path = ASSET_DIR / image_name
                with self.subTest(
                    collection=name,
                    scale=scale,
                    property="image-exists",
                ):
                    self.assertTrue(image_path.is_file(), image_path)
                if not image_path.is_file():
                    continue

                with Image.open(image_path) as image:
                    for region_name, region in enumerate(regions):
                        x, y, width, height = scaled_region(region, scale)
                        with self.subTest(
                            collection=name,
                            scale=scale,
                            region=region_name,
                        ):
                            self.assertGreater(width, 0)
                            self.assertGreater(height, 0)
                            self.assertGreaterEqual(x, 0)
                            self.assertGreaterEqual(y, 0)
                            self.assertLessEqual(x + width, image.width)
                            self.assertLessEqual(y + height, image.height)

    def test_faction_palettes_and_active_crops_use_exact_plan_tokens(self):
        self.require_contract_files()
        generator = load_generator()
        for faction in FACTIONS:
            palette = generator.PALETTES[faction]
            for name, token in PLAN_TOKENS[faction].items():
                with self.subTest(faction=faction, token=name, property="palette"):
                    self.assertEqual(token, getattr(palette, name, None))

            with Image.open(asset_path("actions", faction, 2)) as actions:
                active_row = actions.crop((0, 512, 624, 624))
                for name, token in PLAN_TOKENS[faction].items():
                    with self.subTest(faction=faction, token=name, atlas="actions"):
                        self.assertGreaterEqual(close_color_count(active_row, token), 8)

            with Image.open(asset_path("quickbar", faction, 2)) as quickbar:
                highlighted = crop_region(quickbar, (384, 0, 96, 104))
                with self.subTest(faction=faction, atlas="quickbar-highlighted"):
                    self.assertGreaterEqual(
                        close_color_count(highlighted, PLAN_TOKENS[faction]["active"]),
                        8,
                    )

    def test_generator_is_self_contained_and_reproduces_installed_bytes(self):
        self.require_contract_files()
        generator = load_generator()
        source = GENERATOR_PATH.read_text(encoding="utf-8")
        for forbidden in ("ImageFont", ".text(", "watermark", "logo"):
            self.assertNotIn(forbidden, source)

        tree = ast.parse(source, filename=str(GENERATOR_PATH))
        for node in ast.walk(tree):
            if not isinstance(node, ast.Call):
                continue
            function = node.func
            if isinstance(function, ast.Name):
                self.assertNotEqual("open", function.id)
            elif isinstance(function, ast.Attribute):
                self.assertNotIn(
                    function.attr,
                    ("open", "read_bytes", "read_text", "text", "multiline_text"),
                )

        with tempfile.TemporaryDirectory() as directory:
            temporary = Path(directory)
            generated = generator.generate_assets(temporary)
            self.assertEqual(18, len(generated))
            self.assertEqual(
                {asset_path(kind, faction, scale).name for kind, faction, scale in itertools.product(KINDS, FACTIONS, (1, 2))},
                {path.name for path in generated},
            )
            for generated_path in generated:
                installed_path = ASSET_DIR / generated_path.name
                with self.subTest(path=generated_path.name):
                    self.assertEqual(
                        installed_path.read_bytes(),
                        generated_path.read_bytes(),
                    )


if __name__ == "__main__":
    unittest.main()
