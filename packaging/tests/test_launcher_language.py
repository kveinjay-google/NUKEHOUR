import ast
import inspect
import os
import re
import struct
import tempfile
import unittest
from pathlib import Path
from unittest import mock

import launcher
import launcher_i18n
import ra2_files


ROOT = Path(__file__).resolve().parents[2]
LAUNCHER_SOURCE = ROOT / "launcher.py"


def _contains_chinese(node):
    return any(
        isinstance(child, ast.Constant)
        and isinstance(child.value, str)
        and re.search(r"[\u3400-\u9fff]", child.value)
        for child in ast.walk(node)
    )


def _is_translation_call(node):
    def visit(child, translated=False):
        translated = translated or (
            isinstance(child, ast.Call)
            and isinstance(child.func, ast.Name)
            and child.func.id in {"_tr", "_trf"}
        )
        if (
            isinstance(child, ast.Constant)
            and isinstance(child.value, str)
            and re.search(r"[\u3400-\u9fff]", child.value)
        ):
            return translated
        return all(visit(grandchild, translated) for grandchild in ast.iter_child_nodes(child))

    return visit(node)


class LauncherLanguagePolicyTest(unittest.TestCase):
    def test_three_state_preference_and_resolution_policy(self):
        self.assertEqual(
            (("System", "System"), ("简体中文", "zh-CN"), ("English", "en")),
            launcher_i18n.PREFERENCES,
        )
        self.assertEqual("System", launcher_i18n.normalize_preference(None))
        self.assertEqual("System", launcher_i18n.normalize_preference("invalid"))
        self.assertEqual("zh-CN", launcher_i18n.normalize_preference("zh-Hans"))
        self.assertEqual("en", launcher_i18n.normalize_preference("en-US"))
        self.assertEqual("zh-CN", launcher_i18n.resolve_language("System", "zh-Hans-CN"))
        self.assertEqual("en", launcher_i18n.resolve_language("en", "zh-Hans-CN"))
        self.assertEqual("zh-CN", launcher_i18n.resolve_language("zh-CN", "en-US"))

    def test_malformed_language_tags_fail_closed(self):
        malformed = ("zh-", "zh--CN", "zh-中文", "zh-ThisSubtagIsTooLong")
        for value in malformed:
            with self.subTest(value=value):
                self.assertEqual("System", launcher_i18n.normalize_preference(value))
                self.assertEqual("en", launcher_i18n.resolve_language("System", value))
        self.assertEqual("zh-CN", launcher_i18n.normalize_preference("Chinese"))
        self.assertEqual("en", launcher_i18n.normalize_preference("English"))

    def test_apple_languages_wins_over_conflicting_apple_locale(self):
        values = {
            "AppleLanguages": '(\n    "en-US",\n    "zh-Hans-CN"\n)',
            "AppleLocale": "zh_CN@calendar=gregorian\n",
        }

        def defaults(command, **_kwargs):
            return values[command[-1]]

        with mock.patch.object(launcher_i18n.subprocess, "check_output", side_effect=defaults):
            with mock.patch.dict(os.environ, {"LC_ALL": "zh_CN.UTF-8"}, clear=True):
                self.assertEqual("en-US", launcher_i18n.detect_system_language_tag())

    def test_environment_and_locale_are_safe_fallbacks(self):
        with mock.patch.object(
            launcher_i18n.subprocess,
            "check_output",
            side_effect=FileNotFoundError,
        ):
            with mock.patch.dict(os.environ, {"LC_ALL": "zh_Hans_CN.UTF-8"}, clear=True):
                self.assertEqual("zh-Hans-CN", launcher_i18n.detect_system_language_tag())

        with mock.patch.object(
            launcher_i18n.subprocess,
            "check_output",
            side_effect=RuntimeError,
        ), mock.patch.dict(os.environ, {}, clear=True), mock.patch.object(
            launcher_i18n.locale,
            "getlocale",
            return_value=("en_GB", "UTF-8"),
        ):
            self.assertEqual("en-GB", launcher_i18n.detect_system_language_tag())

    def test_missing_setting_defaults_to_system_and_manual_values_stay_raw(self):
        with tempfile.TemporaryDirectory() as temporary:
            settings = Path(temporary) / "settings.yaml"
            with mock.patch.object(launcher, "SETTINGS_FILE", str(settings)):
                self.assertEqual("System", launcher.load_settings()["Game"]["Language"])

                settings.write_text("Game:\n\tLanguage: en\n", encoding="utf-8")
                self.assertEqual("en", launcher.load_settings()["Game"]["Language"])

                settings.write_text("Game:\n\tLanguage: zh-CN\n", encoding="utf-8")
                self.assertEqual("zh-CN", launcher.load_settings()["Game"]["Language"])

    def test_manual_preference_is_persisted_atomically_without_touching_other_values(self):
        with tempfile.TemporaryDirectory() as temporary:
            settings = Path(temporary) / "settings.yaml"
            settings.write_text(
                "Player:\n\tName: Saved User\nGame:\n\tLanguage: zh-CN\n"
                "\tPauseShellmap: True\n",
                encoding="utf-8",
            )

            launcher.persist_language_preference("en", str(settings))

            self.assertEqual(
                "Player:\n\tName: Saved User\nGame:\n\tLanguage: en\n"
                "\tPauseShellmap: True\n",
                settings.read_text(encoding="utf-8"),
            )
            self.assertFalse(settings.with_suffix(".yaml.tmp").exists())

    def test_preference_persistence_fails_closed_on_existing_file_read_error(self):
        with tempfile.TemporaryDirectory() as temporary:
            settings = Path(temporary) / "settings.yaml"
            original = b"Player:\n\tName: Saved User\n"
            settings.write_bytes(original)
            real_open = open

            def controlled_open(path, mode="r", *args, **kwargs):
                if Path(path) == settings and "r" in mode:
                    raise PermissionError("denied")
                return real_open(path, mode, *args, **kwargs)

            with mock.patch("builtins.open", side_effect=controlled_open):
                with self.assertRaises(PermissionError):
                    launcher.persist_language_preference("en", str(settings))
            self.assertEqual(original, settings.read_bytes())
            self.assertFalse(Path(str(settings) + ".tmp").exists())

    def test_preference_persistence_rejects_duplicate_game_or_language_keys(self):
        cases = (
            "Game:\n\tLanguage: en\nGame:\n\tLanguage: zh-CN\n",
            "Game:\n\tLanguage: en\n\tLanguage: zh-CN\n",
        )
        for original in cases:
            with self.subTest(original=original):
                with tempfile.TemporaryDirectory() as temporary:
                    settings = Path(temporary) / "settings.yaml"
                    settings.write_text(original, encoding="utf-8")
                    with self.assertRaises(ValueError):
                        launcher.persist_language_preference("System", str(settings))
                    self.assertEqual(original, settings.read_text(encoding="utf-8"))
                    self.assertFalse(Path(str(settings) + ".tmp").exists())

    def test_launch_arguments_keep_raw_preference_and_system_tag(self):
        self.assertEqual(
            ["Game.Language=System", "Engine.SystemLanguage=zh-Hans-CN"],
            launcher.language_launch_args("System", "zh-Hans-CN"),
        )
        self.assertEqual(
            ["Game.Language=en", "Engine.SystemLanguage=zh-Hans-CN"],
            launcher.language_launch_args("en", "zh-Hans-CN"),
        )
        build_source = inspect.getsource(launcher.Launcher.build_args)
        self.assertIn(
            "*language_launch_args(self.language_preference, self.system_language_tag)",
            build_source,
        )
        source = LAUNCHER_SOURCE.read_text(encoding="utf-8")
        self.assertNotIn('"Language": "zh-CN"', source)
        self.assertNotIn("Game.Language=zh-CN", source)

    def test_hotkey_fluent_search_order_matches_effective_language(self):
        english = [Path(path) for path in launcher.hotkey_fluent_files("en")]
        chinese = [Path(path) for path in launcher.hotkey_fluent_files("zh-CN")]
        self.assertNotIn("zh-CN", english[0].parts)
        self.assertNotIn("zh-CN", english[1].parts)
        self.assertIn("zh-CN", chinese[0].parts)
        self.assertIn("zh-CN", chinese[1].parts)

    def test_only_builtin_profile_display_names_are_localized(self):
        builtin = {"id": "profturtle", "name": "简单·龟缩", "builtin": True}
        custom = {"id": "custom", "name": "我的 AI", "builtin": False}
        self.assertEqual("Easy · Turtle", launcher.profile_display_name(builtin, "en"))
        self.assertEqual("我的 AI", launcher.profile_display_name(custom, "en"))

    def test_language_specific_defaults_do_not_translate_saved_values(self):
        self.assertEqual("指挥官", launcher.default_player_name("zh-CN"))
        self.assertEqual("Commander", launcher.default_player_name("en"))
        self.assertEqual("红色警戒2 房间", launcher.default_room_name("zh-CN"))
        self.assertEqual("Red Alert 2 Room", launcher.default_room_name("en"))

    def test_switching_language_updates_only_unsaved_generated_defaults(self):
        self.assertEqual(
            "Commander",
            launcher.switch_localized_default(
                "指挥官", True, "指挥官", "zh-CN", "en"
            ),
        )
        self.assertEqual(
            "指挥官",
            launcher.switch_localized_default(
                "指挥官", False, "指挥官", "zh-CN", "en"
            ),
        )
        self.assertEqual(
            "Red Alert 2 Room",
            launcher.switch_localized_default(
                "红色警戒2 房间", True, "红色警戒2 房间", "zh-CN", "en"
            ),
        )
        self.assertEqual(
            "我的房间",
            launcher.switch_localized_default(
                "我的房间", True, "红色警戒2 房间", "zh-CN", "en"
            ),
        )

    def test_builtin_duplicate_uses_localized_display_name_without_translating_custom_names(self):
        builtin = {"id": "profturtle", "name": "简单·龟缩", "builtin": True}
        custom = {"id": "custom", "name": "我的 AI", "builtin": False}
        self.assertEqual(
            "Easy · Turtle Copy",
            launcher.duplicate_profile_name(builtin, "en"),
        )
        self.assertEqual("我的 AI Copy", launcher.duplicate_profile_name(custom, "en"))

    def test_ai_duplicate_action_uses_the_localized_display_name(self):
        source = inspect.getsource(launcher.Launcher._ai_dup)
        self.assertIn("name=duplicate_profile_name(src)", source)

    def test_external_credits_and_replay_placeholders_are_localized(self):
        credits = (
            "【Red Alert 2 模组】\n\nAlice\n\n\n"
            "【OpenRA 引擎】\n\nBob"
        )
        self.assertEqual(
            "【Red Alert 2 Mod】\n\nAlice\n\n\n【OpenRA Engine】\n\nBob",
            launcher.localize_credits_text(credits, "en"),
        )
        self.assertEqual(
            "Credits files were not found",
            launcher.localize_credits_text("未找到制作名单文件", "en"),
        )
        cases = {
            "未知地图": "Unknown map",
            "未知": "Unknown",
            "未找到元数据": "Metadata not found",
            "2 分 7 秒": "2 min 7 sec",
            "User Map": "User Map",
        }
        for original, expected in cases.items():
            with self.subTest(original=original):
                self.assertEqual(
                    expected,
                    launcher.localize_replay_text(original, "en"),
                )

    def test_external_dynamic_text_is_localized_at_the_ui_boundary(self):
        credits_source = inspect.getsource(launcher.Launcher._page_credits)
        replay_source = inspect.getsource(launcher.Launcher._replays_refresh)
        self.assertIn(
            "localize_credits_text(ra2_files.credits_text())",
            credits_source,
        )
        for expression in (
            'localize_replay_text(r["map_title"])',
            'localize_replay_text(r["duration"])',
            'localize_replay_text(r.get("start"))',
        ):
            with self.subTest(expression=expression):
                self.assertIn(expression, replay_source)

    def test_ra2_file_fixed_errors_are_localized_but_free_text_is_preserved(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            replay = root / "bad.orarep"
            replay.write_bytes(struct.pack("<ii", -1, 1) + b"\x80")
            small_save = root / "small.orasav"
            small_save.write_bytes(b"tiny")
            bad_end_save = root / "bad-end.orasav"
            bad_end_save.write_bytes(struct.pack("<iii", 0, 0, 0))
            bad_metadata_save = root / "bad-metadata.orasav"
            bad_metadata_save.write_bytes(struct.pack("<iii", 0, 0, -2))
            empty_map = root / "empty-map"
            empty_map.mkdir()
            outside_support = root / "outside-support.orasav"
            outside_support.write_bytes(b"preserve")

            actual = [
                ra2_files.parse_replay(str(replay))["error"],
                ra2_files.parse_save(str(small_save))["error"],
                ra2_files.parse_save(str(bad_end_save))["error"],
                ra2_files.parse_save(str(bad_metadata_save))["error"],
            ]
            with self.assertRaises(ValueError) as missing_map:
                ra2_files._parse_map_dir(str(empty_map))
            actual.append(str(missing_map.exception))
            with self.assertRaises(ValueError) as unsafe_delete:
                ra2_files.delete_file(str(outside_support))
            actual.append(str(unsafe_delete.exception))
            self.assertTrue(outside_support.exists())

        localize = getattr(
            launcher,
            "localize_ra2_file_error",
            lambda text, _language=None: text,
        )
        expected = [
            "Length prefix is out of bounds",
            "File is too small",
            "Invalid orasav end marker",
            "Invalid metadata marker",
            "map.yaml is missing",
            "Only files in the user data folder can be deleted",
        ]
        self.assertEqual(expected, [localize(error, "en") for error in actual])
        self.assertEqual(
            "用户提供的自由文本",
            localize("用户提供的自由文本", "en"),
        )

    def test_ra2_file_errors_are_localized_at_active_ui_boundaries(self):
        replay_source = inspect.getsource(launcher.Launcher._replays_refresh)
        save_source = inspect.getsource(launcher.Launcher._saves_refresh)
        delete_source = inspect.getsource(launcher.Launcher._delete_record)
        self.assertIn('localize_ra2_file_error(r["error"])', replay_source)
        self.assertIn('localize_ra2_file_error(s.get("error"))', save_source)
        self.assertIn("localize_ra2_file_error(str(exc))", delete_source)

    def test_generated_room_default_is_tracked_when_network_ui_is_created(self):
        source = inspect.getsource(launcher.Launcher._page_net)
        self.assertIn("self.net_name_is_default = not bool(saved_room_name)", source)

    def test_skirmish_count_widgets_share_the_preserved_state_variable(self):
        source = inspect.getsource(launcher.Launcher._page_skirmish)
        self.assertIn("textvariable=self.sk_count_var", source)
        self.assertIn("variable=self.sk_count_var", source)

    def test_language_change_rebuild_is_in_process_and_not_deferred(self):
        source = inspect.getsource(launcher.Launcher._change_language)
        self.assertNotIn("Popen", source)
        self.assertNotIn("after(", source)
        self.assertIn("current_page", source)
        self.assertIn("_capture_ui_state", source)
        self.assertIn("_restore_ui_state", source)

    def test_every_active_chinese_literal_has_an_english_catalog_entry(self):
        tree = ast.parse(LAUNCHER_SOURCE.read_text(encoding="utf-8"))
        docstrings = set()
        for owner in [tree, *ast.walk(tree)]:
            if isinstance(owner, (ast.Module, ast.ClassDef, ast.FunctionDef, ast.AsyncFunctionDef)):
                if owner.body and isinstance(owner.body[0], ast.Expr):
                    value = owner.body[0].value
                    if isinstance(value, ast.Constant) and isinstance(value.value, str):
                        docstrings.add(id(value))

        missing = []
        for node in ast.walk(tree):
            if (
                isinstance(node, ast.Constant)
                and isinstance(node.value, str)
                and id(node) not in docstrings
                and re.search(r"[\u3400-\u9fff]", node.value)
                and node.value not in launcher_i18n.EN
            ):
                missing.append((getattr(node, "lineno", 0), node.value))
        self.assertEqual([], sorted(set(missing)))

        chinese_outputs = {
            key: value
            for key, value in launcher_i18n.EN.items()
            if key != "简体中文" and re.search(r"[\u3400-\u9fff]", value)
        }
        self.assertEqual({}, chinese_outputs)

    def test_direct_ui_text_is_routed_through_translation_helpers(self):
        tree = ast.parse(LAUNCHER_SOURCE.read_text(encoding="utf-8"))
        failures = []
        for node in ast.walk(tree):
            if not isinstance(node, ast.Call):
                continue
            dotted = ""
            if isinstance(node.func, ast.Attribute):
                if isinstance(node.func.value, ast.Name):
                    dotted = f"{node.func.value.id}.{node.func.attr}"
                else:
                    dotted = node.func.attr
            elif isinstance(node.func, ast.Name):
                dotted = node.func.id

            expressions = []
            if dotted in {"tk.Label", "tk.Checkbutton", "config", "configure"}:
                expressions.extend(kw.value for kw in node.keywords if kw.arg == "text")
            elif dotted.startswith("messagebox.") or dotted.startswith("simpledialog."):
                expressions.extend(node.args[:2])
            elif dotted.startswith("filedialog."):
                expressions.extend(kw.value for kw in node.keywords if kw.arg in {"title", "filetypes"})
            elif dotted == "title":
                expressions.extend(node.args[:1])

            for expression in expressions:
                if _contains_chinese(expression) and not _is_translation_call(expression):
                    failures.append((getattr(expression, "lineno", 0), dotted))

        self.assertEqual([], failures)


if __name__ == "__main__":
    unittest.main()
