import inspect
import json
import plistlib
import tempfile
import unittest
from pathlib import Path
from unittest import mock

import launcher


class MacosLauncherLayoutTest(unittest.TestCase):
    def test_right_content_panel_covers_the_complete_non_sidebar_area(self):
        layout = launcher.launcher_shell_layout()

        self.assertEqual(
            (launcher.SIDEBAR_W, 0, launcher.WIN_W - launcher.SIDEBAR_W, launcher.WIN_H),
            layout["content"],
        )

    def test_language_switcher_fills_sidebar_above_footer(self):
        layout = launcher.launcher_shell_layout()
        self.assertIn("language_switcher", layout)
        if "language_switcher" not in layout:
            return

        x, y, width, height = layout["language_switcher"]
        footer_x, footer_y, footer_width, _footer_height = layout["footer"]
        self.assertEqual(x, footer_x)
        self.assertEqual(width, footer_width)
        self.assertEqual(y + height, footer_y - 8)
        self.assertGreaterEqual(height, 34)
        self.assertGreaterEqual(width, 160)

        self.assertTrue(hasattr(launcher, "language_switcher_layout"))
        if not hasattr(launcher, "language_switcher_layout"):
            return
        controls = launcher.language_switcher_layout(width, height)
        self.assertEqual((0, 0, 36, height), controls["previous"])
        self.assertEqual((width - 36, 0, 36, height), controls["next"])
        self.assertEqual((40, 0, width - 80, height), controls["label"])

    def test_language_switcher_uses_two_buttons_without_a_popup_menu(self):
        source = inspect.getsource(launcher.Launcher._build_language_switcher)

        self.assertIn('for key, glyph in (("previous", "‹"), ("next", "›")):', source)
        self.assertEqual(1, source.count("tk.Button("))
        self.assertIn("language_switch_label(self.effective_language)", source)
        self.assertIn("command=self._toggle_language", source)
        self.assertNotIn("tk.Menubutton(", source)
        self.assertNotIn("tk.Menu(", source)
        self.assertNotIn("System", source)

    def test_version_footer_is_inside_content_and_right_bottom_aligned(self):
        layout = launcher.launcher_shell_layout()
        x, y, width, height = layout["version_footer"]
        content_x, content_y, content_width, content_height = layout["content"]

        self.assertGreaterEqual(x, content_x)
        self.assertGreaterEqual(y, content_y)
        self.assertLessEqual(x + width, content_x + content_width)
        self.assertLessEqual(y + height, content_y + content_height)
        self.assertEqual(x + width, launcher.WIN_W - 16)
        self.assertEqual(y + height, launcher.WIN_H - 12)

    def test_language_refresh_raises_existing_version_footer_above_new_content(self):
        source = inspect.getsource(launcher.Launcher._change_language_preference)
        lift = "self.version_label.lift()"

        self.assertIn(lift, source)
        self.assertLess(source.index("self._build_language_switcher()"), source.index(lift))
        self.assertLess(source.index("self.show_page(current_page)"), source.index(lift))
        self.assertNotIn("self._build_version_footer()", source)

    def test_version_text_uses_brand_and_product_version_without_ra2(self):
        text = launcher.launcher_version_text("1.0.3", 3)

        self.assertEqual("NUKE HOUR 1.0.3 (Build 3)", text)
        self.assertNotIn("RA2", text)

    def test_product_version_reads_source_manifest_and_frozen_bundle(self):
        with tempfile.TemporaryDirectory(prefix="nukehour-version-") as temporary:
            root = Path(temporary)
            (root / "packaging").mkdir()
            (root / "packaging/nukehour-version.json").write_text(
                json.dumps({"version": "1.2.3", "build": 7}), encoding="utf-8")
            self.assertEqual(("1.2.3", 7), launcher.launcher_product_version(root))

            frozen = root / "NUKE HOUR.app/Contents/Resources/runtime"
            frozen.mkdir(parents=True)
            with (root / "NUKE HOUR.app/Contents/Info.plist").open("wb") as stream:
                plistlib.dump({"CFBundleShortVersionString": "2.0.0", "CFBundleVersion": "9"}, stream)
            self.assertEqual(("2.0.0", 9), launcher.launcher_product_version(frozen))

    def test_startup_notice_uses_scrollable_copy_and_fixed_actions(self):
        root = Path(launcher.__file__).resolve().parent
        source = (root / "launcher.py").read_text(encoding="utf-8")
        translations = (root / "launcher_i18n.py").read_text(encoding="utf-8")

        self.assertIn("def _page_notice", source)
        self.assertIn("tk.Text(notice_body", source)
        self.assertIn('self._native_button(actions, "官方网站"', source)
        self.assertIn('self._native_button(actions, "同意并继续"', source)
        self.assertIn("actions.place(", source)
        self.assertIn("notice_body.place(", source)
        self.assertNotIn('actions.pack(side="bottom"', source)
        self.assertIn("EA 未认可且不支持本产品", source)
        self.assertIn("完全免费开源", source)
        self.assertIn("合法购买并拥有的正版游戏副本", source)
        self.assertIn("官方授权版本", source)
        self.assertIn("恶意代码", source)
        self.assertIn("EA has not endorsed and does not support this product.", translations)
        self.assertTrue(hasattr(launcher, "notice_page_layout"))
        if not hasattr(launcher, "notice_page_layout"):
            return
        layout = launcher.notice_page_layout()
        body_x, body_y, body_width, body_height = layout["body"]
        actions_x, actions_y, actions_width, actions_height = layout["actions"]
        self.assertEqual((body_x, body_width), (actions_x, actions_width))
        self.assertLessEqual(body_y + body_height, actions_y - 12)
        self.assertLessEqual(actions_y + actions_height, launcher.WIN_H - 36)

    def test_startup_notice_brand_names_are_linked_to_the_official_website(self):
        self.assertTrue(hasattr(launcher, "notice_brand_link_spans"))
        if not hasattr(launcher, "notice_brand_link_spans"):
            return

        spans = launcher.notice_brand_link_spans(launcher.STARTUP_NOTICE_COPY)

        self.assertEqual(2, len(spans))
        self.assertTrue(all(
            launcher.STARTUP_NOTICE_COPY[start:end] == launcher.BRAND_NAME
            for start, end in spans
        ))

        source = Path(launcher.__file__).read_text(encoding="utf-8")
        self.assertIn('copy.tag_add("brand_website_link"', source)
        self.assertIn('copy.tag_bind("brand_website_link", "<Button-1>"', source)
        self.assertIn("self._open_brand_website", source)

    def test_aqua_controls_use_branded_image_surfaces_with_readable_text(self):
        source = Path(launcher.__file__).read_text(encoding="utf-8")

        self.assertIn("def _control_surface", source)
        self.assertIn("image=base_image", source)
        self.assertIn('compound="center"', source)
        self.assertIn("tk.Button(", source)
        self.assertIn("takefocus=True", source)
        self.assertIn("font=FONT_LABEL", source)
        self.assertIn("def _build_language_switcher", source)
        self.assertNotIn("language_menu = tk.OptionMenu(", source)

    @mock.patch("launcher.webbrowser.open_new_tab", return_value=True)
    def test_open_brand_website_uses_nuke_hour_url(self, open_new_tab):
        launcher.Launcher._open_brand_website(object())

        open_new_tab.assert_called_once_with("https://nukehour.com")


if __name__ == "__main__":
    unittest.main()
