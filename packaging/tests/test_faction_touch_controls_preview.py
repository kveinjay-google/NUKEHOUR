import hashlib
import itertools
import math
import re
import unittest
from pathlib import Path

from PIL import Image, ImageChops, ImageStat


ROOT = Path(__file__).resolve().parents[2]
PREVIEW = ROOT / "design-demos" / "faction-touch-controls"
INDEX = PREVIEW / "index.html"
STYLES = PREVIEW / "styles.css"
SCRIPT = PREVIEW / "app.js"
FACTIONS = ("allies", "soviet", "yuri")
NORMALIZED_BOARD_SIZE = (64, 48)
MIN_BOARD_RMS_CONTRAST = 8.0
MIN_FACTION_RMS_DIFFERENCE = 12.0


def preview_source():
    source = "\n".join(
        path.read_text(encoding="utf-8") for path in (INDEX, STYLES, SCRIPT)
    )
    source = re.sub(r"<!--.*?-->", "", source, flags=re.DOTALL)
    source = re.sub(r"/\*.*?\*/", "", source, flags=re.DOTALL)
    return re.sub(r"^[ \t]*//.*$", "", source, flags=re.MULTILINE)


class FactionTouchControlsPreviewTest(unittest.TestCase):
    def test_three_distinct_concept_boards_exist(self):
        digests = set()
        normalized_boards = {}
        for faction in FACTIONS:
            path = PREVIEW / "assets" / f"{faction}-control-kit.png"
            self.assertTrue(path.is_file(), path)
            with Image.open(path) as image:
                self.assertEqual("PNG", image.format, path)
                self.assertGreaterEqual(image.width, 1024, path)
                self.assertGreaterEqual(image.height, 768, path)
                image.verify()

            digests.add(hashlib.sha256(path.read_bytes()).hexdigest())

            with Image.open(path) as image:
                normalized = image.convert("RGB").resize(
                    NORMALIZED_BOARD_SIZE,
                    Image.Resampling.LANCZOS,
                )
                normalized.load()

            channel_variances = ImageStat.Stat(normalized).var
            rms_contrast = math.sqrt(sum(channel_variances) / len(channel_variances))
            self.assertGreaterEqual(
                rms_contrast,
                MIN_BOARD_RMS_CONTRAST,
                (
                    f"{faction} concept board has insufficient normalized pixel "
                    f"variance: RMS contrast {rms_contrast:.2f} < "
                    f"{MIN_BOARD_RMS_CONTRAST:.2f}"
                ),
            )
            normalized_boards[faction] = normalized

        self.assertEqual(
            3,
            len(digests),
            "faction concept boards must have distinct SHA-256 byte hashes",
        )

        for first, second in itertools.combinations(FACTIONS, 2):
            difference = ImageChops.difference(
                normalized_boards[first],
                normalized_boards[second],
            )
            channel_rms = ImageStat.Stat(difference).rms
            rms_difference = math.sqrt(
                sum(value * value for value in channel_rms) / len(channel_rms)
            )
            self.assertGreaterEqual(
                rms_difference,
                MIN_FACTION_RMS_DIFFERENCE,
                (
                    f"{first} and {second} concept boards are too similar after "
                    f"{NORMALIZED_BOARD_SIZE[0]}x{NORMALIZED_BOARD_SIZE[1]} RGB "
                    f"normalization: RMS difference {rms_difference:.2f} < "
                    f"{MIN_FACTION_RMS_DIFFERENCE:.2f}"
                ),
            )

    def test_preview_exposes_all_factions_sizes_states_and_actions(self):
        html = INDEX.read_text(encoding="utf-8")
        source = preview_source()
        self.assertIn(
            '<link rel="icon" href="data:,">',
            html,
            "served preview must not trigger an implicit /favicon.ico request",
        )
        for faction in FACTIONS:
            self.assertIn(f'data-faction="{faction}"', source)
            self.assertIn(f"assets/{faction}-control-kit.png", source)
        for size in ("112", "128", "144"):
            self.assertIn(f'data-size="{size}"', source)
        for state in ("normal", "pressed", "active", "disabled"):
            self.assertIn(f'data-state="{state}"', source)
        for action in ("stop", "deploy", "select-type", "force-attack", "return-base"):
            self.assertIn(f'"{action}"', source)

    def test_shortcut_symbols_are_reused_but_left_controls_are_redesigned(self):
        source = preview_source()
        self.assertIn("../../mods/ra2/uibits/ios-commandbar-icons-v2.png", source)
        self.assertIn("../../mods/ra2/uibits/production-x5-icon.png", source)
        self.assertNotIn("ios-joystick-command-icons.png", source)
        self.assertNotIn("ios-viewport-joystick.png", source)
        self.assertIn("action-glyph", source)

    def test_preview_contains_deterministic_ring_and_pointer_interaction(self):
        source = preview_source()
        self.assertIn("const RING_ANGLES = [-150, -115, -80, -45, -10]", source)
        self.assertIn("setPointerCapture", source)
        self.assertIn("Math.hypot", source)
        self.assertIn("renderActionRing", source)
        self.assertIn("toggleQuickbar", source)

    def test_compact_mode_uses_one_width_or_height_predicate(self):
        script = SCRIPT.read_text(encoding="utf-8")
        styles = STYLES.read_text(encoding="utf-8")
        predicate = "(max-width: 900px), (max-height: 500px)"
        self.assertIn(f'const COMPACT_MEDIA_QUERY = "{predicate}"', script)
        self.assertEqual(1, script.count("matchMedia("))
        self.assertIn("window.matchMedia(COMPACT_MEDIA_QUERY)", script)
        self.assertIn(f"@media {predicate}", styles)

    def test_compact_mode_restores_compare_focus_to_active_faction(self):
        script = SCRIPT.read_text(encoding="utf-8")
        compact = script[
            script.index("function syncCompactMode(event)"):
            script.index('joystick.addEventListener("pointerdown"')
        ]
        focus_check = (
            "const restoreFactionFocus = compact && "
            "document.activeElement === compareButton;"
        )
        focus_target = (
            'document.querySelector(`.faction-button[data-faction="${activeFaction}"]`)'
            ".focus()"
        )
        compare_target = (
            "document.querySelector('.faction-button[data-faction=\"compare\"]')"
        )
        self.assertIn(compare_target, compact)
        self.assertNotIn(
            "document.querySelector('[data-faction=\"compare\"]')",
            compact,
        )
        self.assertIn(focus_check, compact)
        self.assertIn(focus_target, compact)
        self.assertLess(compact.index(focus_check), compact.index("compareButton.disabled = compact"))
        self.assertIn(
            "if (closingComparison)\n"
            "    selectFaction(activeFaction);\n"
            "  if (restoreFactionFocus)\n"
            "    requestAnimationFrame(() => {\n"
            "      requestAnimationFrame(() => {\n"
            f"        {focus_target};\n"
            "      });\n"
            "    });",
            compact,
        )

    def test_joystick_owns_one_pointer_and_supports_keyboard_control(self):
        script = SCRIPT.read_text(encoding="utf-8")
        pointerdown = script[script.index('joystick.addEventListener("pointerdown"'):]
        self.assertIn("activePointerId !== null", pointerdown[:700])
        self.assertNotIn("!event.isPrimary", pointerdown[:700])
        self.assertIn('event.pointerType === "mouse"', pointerdown[:700])
        self.assertIn("event.button !== 0", pointerdown[:700])

        reset = re.search(
            r"function resetJoystick\([^)]*\) \{(?P<body>.*?)\n\}",
            script,
            flags=re.DOTALL,
        )
        self.assertIsNotNone(reset)
        self.assertIn("hasPointerCapture(activePointerId)", reset.group("body"))
        self.assertIn("releasePointerCapture(activePointerId)", reset.group("body"))
        self.assertIn("function handleLostPointerCapture(event)", script)
        self.assertIn("event.pointerId === activePointerId", script)
        self.assertIn(
            'joystick.addEventListener("lostpointercapture", handleLostPointerCapture)',
            script,
        )

        self.assertIn('joystick.addEventListener("keydown"', script)
        for key in ("ArrowUp", "ArrowDown", "ArrowLeft", "ArrowRight", "Home", "Escape"):
            self.assertIn(f'"{key}"', script)

    def test_joystick_instructions_and_live_status_remain_accessible(self):
        html = INDEX.read_text(encoding="utf-8")
        styles = STYLES.read_text(encoding="utf-8")
        script = SCRIPT.read_text(encoding="utf-8")
        self.assertIn('id="joystickInstructions"', html)
        self.assertIn('id="joystickValue"', html)
        self.assertIn('aria-describedby="joystickInstructions joystickValue"', html)
        self.assertIn("joystickValue.textContent", script)

        self.assertEqual(1, html.count('aria-live="polite"'))
        status_index = html.index('id="interactionStatus"')
        tag_start = html.rfind("<", 0, status_index)
        tag_end = html.index(">", status_index)
        self.assertIn('class="sr-only"', html[tag_start:tag_end])
        self.assertLess(status_index, html.index('<main class="review-layout">'))
        sr_only = re.search(r"\.sr-only\s*\{(?P<body>.*?)\}", styles, re.DOTALL)
        self.assertIsNotNone(sr_only)
        self.assertNotIn("display: none", sr_only.group("body"))
        self.assertIn("interactionStatus.textContent", script)
        self.assertIn('updateStatus("三阵营并排比较")', script)

    def test_dvh_core_sizes_have_immediate_vh_fallbacks(self):
        styles = STYLES.read_text(encoding="utf-8")
        declarations = list(
            re.finditer(
                r"(?P<property>(?:min-|max-)?height):\s*(?P<value>[^;]*dvh[^;]*);",
                styles,
            )
        )
        self.assertGreaterEqual(len(declarations), 5)
        for declaration in declarations:
            fallback = (
                f'{declaration.group("property")}: '
                f'{declaration.group("value").replace("dvh", "vh")};'
            )
            before = styles[: declaration.start()].rstrip()
            self.assertTrue(
                before.endswith(fallback),
                f"missing immediate vh fallback before {declaration.group(0)}",
            )

    def test_quickbar_uses_all_atlas_state_rows_without_reordering(self):
        script = SCRIPT.read_text(encoding="utf-8")
        styles = STYLES.read_text(encoding="utf-8")
        self.assertIn('glyph.style.setProperty("--sprite-x"', script)
        self.assertNotIn('glyph.style.setProperty("--sprite-y"', script)
        base_glyph = re.search(r"\.shortcut-glyph\s*\{(?P<body>.*?)\}", styles, re.DOTALL)
        self.assertIsNotNone(base_glyph)
        self.assertIn("--sprite-y: -3px", base_glyph.group("body"))
        for selector, y in (
            (".shortcut-slot:hover .shortcut-glyph", "-67px"),
            ('body[data-state="active"] .shortcut-glyph', "-99px"),
            ('body[data-state="disabled"] .shortcut-glyph', "-35px"),
        ):
            self.assertIn(selector, styles)
            self.assertIn(f"--sprite-y: {y}", styles)
        for state in ("pressed", "active", "disabled"):
            self.assertIn(f'body[data-state="{state}"] .shortcut-slot img', styles)

        expected_order = (
            "group-1", "group-2", "group-3", "group-4", "group-5",
            "production-x5", "attack-move", "scatter", "queue-orders",
        )
        positions = [script.index(f'action: "{action}"') for action in expected_order]
        self.assertEqual(sorted(positions), positions)

    def test_quickbar_persistent_rows_override_hover_and_focus(self):
        styles = STYLES.read_text(encoding="utf-8")
        rules = list(re.finditer(r"(?P<selectors>[^{}]+)\{(?P<body>[^{}]*)\}", styles))

        def declarations(selector):
            for rule in rules:
                selectors = [part.strip() for part in rule.group("selectors").split(",")]
                if selector in selectors:
                    return rule.group("body")
            self.fail(f"missing selector: {selector}")

        for state, row in (("active", "-99px"), ("disabled", "-35px")):
            for interaction in ("hover", "focus-visible"):
                selector = (
                    f'body[data-state="{state}"] '
                    f'.shortcut-slot:{interaction} .shortcut-glyph'
                )
                self.assertIn(f"--sprite-y: {row}", declarations(selector))

    def test_ring_radius_scales_from_one_ratio(self):
        script = SCRIPT.read_text(encoding="utf-8")
        self.assertIn("const RING_RADIUS_RATIO = 127 / 128", script)
        self.assertIn("Math.round(joystickSize * RING_RADIUS_RATIO)", script)
        radius = re.search(
            r"function ringRadius\(joystickSize\) \{(?P<body>.*?)\n\}",
            script,
            flags=re.DOTALL,
        )
        self.assertIsNotNone(radius)
        self.assertNotIn("+", radius.group("body"))


if __name__ == "__main__":
    unittest.main()
