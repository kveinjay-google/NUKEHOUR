import inspect
import tempfile
import unittest
from pathlib import Path
from unittest import mock

import launcher
import launcher_i18n


class MacOSPublicCleanLauncherTest(unittest.TestCase):
    def test_retired_background_services_have_no_launcher_controls_or_arguments(self):
        self.assertNotIn("FetchNews", launcher.DEFAULTS["Game"])
        self.assertNotIn("CheckVersion", launcher.DEFAULTS["Debug"])
        self.assertNotIn("SendSystemInformation", launcher.DEFAULTS["Debug"])

        for token in ("fetch_news", "check_version", "send_sysinfo"):
            self.assertNotIn(token, inspect.getsource(launcher.Launcher._make_vars))
            self.assertNotIn(token, inspect.getsource(launcher.Launcher.build_args))

        advanced_source = inspect.getsource(launcher.Launcher._page_advanced)
        for text in (
            "在线服务",
            "获取社区新闻",
            "自动检查新版本",
            "发送匿名系统信息",
        ):
            self.assertNotIn(text, advanced_source)

        for text in (
            "在线服务",
            "获取社区新闻",
            "自动检查新版本",
            "发送匿名系统信息",
            "帮助开发者了解硬件与系统环境，不含个人隐私",
            "启动时从社区服务器拉取新闻与公告。",
            "自动检查游戏/引擎是否有新版本可更新。",
            "向开发团队发送匿名硬件与系统信息，帮助改进兼容性；不含个人隐私内容。",
            "网络端口映射、在线服务、性能调试与开发者选项。",
        ):
            self.assertNotIn(text, launcher_i18n.EN)

    def test_user_initiated_website_and_multiplayer_paths_remain_available(self):
        notice_source = inspect.getsource(launcher.Launcher._page_notice)
        home_source = inspect.getsource(launcher.Launcher._page_home)

        self.assertIn("self._open_brand_website", notice_source)
        self.assertIn("self._goto_net", home_source)
        self.assertIn(("多人联机", "multiplayer"), launcher.MODES)

    def test_legacy_background_service_settings_are_ignored_when_loaded(self):
        with tempfile.TemporaryDirectory() as temporary:
            settings = Path(temporary) / "settings.yaml"
            settings.write_text(
                "Game:\n\tLanguage: en\n\tFetchNews: True\n"
                "Debug:\n\tCheckVersion: True\n\tSendSystemInformation: True\n"
                "Extension:\n\tFetchNews: extension value\n",
                encoding="utf-8",
            )
            with mock.patch.object(launcher, "SETTINGS_FILE", str(settings)):
                loaded = launcher.load_settings()

        self.assertNotIn("FetchNews", loaded["Game"])
        self.assertNotIn("CheckVersion", loaded["Debug"])
        self.assertNotIn("SendSystemInformation", loaded["Debug"])

    def test_launcher_paths_derive_from_the_branded_support_directory(self):
        self.assertEqual(
            str(Path(launcher.ra2_files.SUPPORT) / "settings.yaml"),
            launcher.SETTINGS_FILE,
        )
        self.assertEqual(
            str(Path(launcher.ra2_files.SUPPORT) / "Logs" / "launcher-launch.log"),
            launcher.LAUNCH_LOG,
        )

    def test_client_and_local_server_use_the_branded_engine_support_directory(self):
        expected = f"Engine.SupportDir={launcher.ra2_files.SUPPORT}"
        self.assertEqual(expected, launcher.support_dir_launch_arg())

        source = Path(launcher.__file__).read_text(encoding="utf-8")
        self.assertIn("support_dir_launch_arg()", source)
        self.assertNotIn("Application Support/OpenRA/Logs", source)

    def test_main_migrates_before_constructing_the_launcher(self):
        events = []
        application = mock.Mock()
        application.mainloop.side_effect = lambda: events.append("launcher")
        with mock.patch("launcher.os.path.exists", return_value=True), mock.patch(
            "launcher.ra2_files.migrate_legacy_support",
            side_effect=lambda: events.append("migration"),
        ), mock.patch("launcher.Launcher", return_value=application):
            result = launcher.main()

        self.assertEqual(0, result)
        self.assertEqual(["migration", "launcher"], events)

    @mock.patch("launcher.messagebox.showerror")
    def test_main_stops_if_support_migration_fails(self, showerror):
        with mock.patch("launcher.os.path.exists", return_value=True), mock.patch(
            "launcher.ra2_files.migrate_legacy_support",
            side_effect=OSError("disk full"),
        ), mock.patch("launcher.Launcher") as launcher_type:
            result = launcher.main()

        self.assertEqual(2, result)
        launcher_type.assert_not_called()
        showerror.assert_called_once()

    def test_launcher_marks_engine_content_cancel_as_return_to_launcher(self):
        self.assertEqual(
            ["Game.ContentManagerReturnToLauncher=true"],
            launcher.with_content_manager_return_policy([]),
        )

    def test_game_process_exit_restores_launcher_home(self):
        target = object.__new__(launcher.Launcher)
        target._game_process = mock.Mock()
        target._game_process.poll.return_value = 0
        target.deiconify = mock.Mock()
        target.lift = mock.Mock()
        target.focus_force = mock.Mock()
        target.show_page = mock.Mock()
        target._set_launch_status = mock.Mock()

        target._watch_game_process()

        target.deiconify.assert_called_once()
        target.show_page.assert_called_once_with("home")
        target._set_launch_status.assert_called_once()

    def test_build_pages_defers_skirmish_until_first_navigation(self):
        target = object.__new__(launcher.Launcher)
        target.content = object()
        target.pages = {}
        page_keys = (
            "notice", "home", "display", "audio", "input", "hotkeys",
            "ai", "advanced", "tools", "replays", "saves", "maps",
            "content", "credits", "skirmish", "net",
        )
        builders = {}
        for key in page_keys:
            builder = mock.Mock(name=f"page_{key}")
            setattr(target, f"_page_{key}", builder)
            builders[key] = builder
        target._scrollable = mock.Mock(side_effect=lambda page, _key: page)

        with mock.patch("launcher.tk.Frame", side_effect=[mock.Mock() for _ in page_keys]):
            target._build_pages()

        builders["notice"].assert_called_once()
        builders["content"].assert_called_once()
        builders["skirmish"].assert_not_called()
        self.assertIn("skirmish", target.pages)
        self.assertNotIn("skirmish", target._built_pages)

    def test_empty_skirmish_map_catalog_has_disabled_placeholder(self):
        values, ready = launcher.skirmish_map_menu_state([])

        self.assertEqual([launcher._tr("尚无可用地图")], values)
        self.assertFalse(ready)

    def test_nonempty_skirmish_map_catalog_preserves_labels(self):
        labels = ["Arena（2 人）", "Oasis（4 人）"]

        values, ready = launcher.skirmish_map_menu_state(labels)

        self.assertEqual(labels, values)
        self.assertTrue(ready)

    @mock.patch("launcher.messagebox.showinfo")
    @mock.patch("launcher.ra2_files.import_retail_content")
    def test_retail_import_invalidates_skirmish_page(self, import_content, _showinfo):
        import_content.return_value = {"healthy": True, "missing_core": []}
        target = object.__new__(launcher.Launcher)
        target._content_refresh = mock.Mock()
        target._invalidate_page = mock.Mock()

        target._import_retail_sources(["/legal/game"])

        target._invalidate_page.assert_called_once_with("skirmish")

    @mock.patch("launcher.messagebox.showinfo")
    @mock.patch("launcher.filedialog.askopenfilename", return_value="/legal/map.oramap")
    @mock.patch("launcher.ra2_files.import_map", return_value="/support/maps/map.oramap")
    def test_map_import_invalidates_skirmish_page(
            self, _import_map, _askopenfilename, _showinfo):
        target = object.__new__(launcher.Launcher)
        target._maps_refresh = mock.Mock()
        target._invalidate_page = mock.Mock()

        target._import_map()

        target._invalidate_page.assert_called_once_with("skirmish")

    def test_startup_notice_precedes_the_existing_content_route(self):
        self.assertEqual("notice", launcher.startup_page())
        self.assertEqual("notice", launcher.guarded_page("home", acknowledged=False))
        self.assertEqual("home", launcher.guarded_page("home", acknowledged=True))

    def test_first_start_opens_content_page_until_minimum_is_ready(self):
        self.assertEqual("content", launcher.initial_page("home", content_ready=False))
        self.assertEqual("home", launcher.initial_page("home", content_ready=True))
        self.assertEqual("content", launcher.initial_page("content", content_ready=True))

    def test_accepting_notice_unlocks_and_routes_to_the_saved_target(self):
        target = object.__new__(launcher.Launcher)
        target.startup_notice_acknowledged = False
        target.post_notice_page = "content"
        target.show_page = mock.Mock()

        target._accept_startup_notice()

        self.assertTrue(target.startup_notice_acknowledged)
        target.show_page.assert_called_once_with("content")

    def test_runtime_root_uses_source_file_outside_frozen_app(self):
        root = launcher.runtime_root(
            frozen=False,
            source_file="/workspace/ra2-mac/launcher.py",
        )

        self.assertEqual("/workspace/ra2-mac", root)

    def test_runtime_root_uses_app_resources_when_frozen(self):
        root = launcher.runtime_root(
            frozen=True,
            executable="/Applications/NUKE HOUR.app/Contents/MacOS/NUKE HOUR",
            meipass="/ignored/_internal",
        )

        self.assertEqual(
            "/Applications/NUKE HOUR.app/Contents/Resources/runtime",
            root,
        )

    def test_runtime_binding_updates_file_management_module(self):
        original = launcher.ra2_files.REPO
        self.addCleanup(setattr, launcher.ra2_files, "REPO", original)

        launcher.bind_runtime_modules("/Applications/NUKE HOUR.app/Contents/Resources/runtime")

        self.assertEqual(
            "/Applications/NUKE HOUR.app/Contents/Resources/runtime",
            launcher.ra2_files.REPO,
        )

    def test_embedded_game_command_precedes_system_runtime(self):
        with tempfile.TemporaryDirectory(prefix="nukehour-runtime-test-") as temporary:
            root = Path(temporary)
            host = root / "NUKE HOUR GAME.app/Contents/MacOS/NuclearCrisis"
            host.parent.mkdir(parents=True)
            host.write_bytes(b"host")
            hostfxr = root / "dotnet/host/fxr/8.0.27/libhostfxr.dylib"
            hostfxr.parent.mkdir(parents=True)
            hostfxr.write_bytes(b"fxr")
            dll = root / "engine/bin/OpenRA.dll"
            dll.parent.mkdir(parents=True)
            dll.write_bytes(b"dll")

            command = launcher.game_command_for_runtime(root, ["Launch.Into=Menu"])

            self.assertEqual(str(host), command[0])
            self.assertEqual(str(hostfxr), command[1])
            self.assertEqual(str(dll), command[2])
            self.assertIn("Engine.ModSearchPaths=./mods," + str(root / "mods"), command)

    @mock.patch("launcher.messagebox.showwarning")
    def test_launch_routes_to_content_page_when_minimum_is_missing(self, showwarning):
        target = object.__new__(launcher.Launcher)
        target._set_launch_status = mock.Mock()
        target.show_page = mock.Mock()
        target._launch_impl = mock.Mock()

        with mock.patch("launcher.ra2_files.content_status", return_value={
            "healthy": False,
            "missing_core": ["language.mix"],
        }):
            target.launch("menu")

        target._launch_impl.assert_not_called()
        target.show_page.assert_called_once_with("content")
        target._set_launch_status.assert_called_once()
        showwarning.assert_called_once()

    @mock.patch("launcher.messagebox.showwarning")
    def test_launch_continues_when_minimum_content_is_ready(self, showwarning):
        target = object.__new__(launcher.Launcher)
        target._set_launch_status = mock.Mock()
        target.show_page = mock.Mock()
        target._launch_impl = mock.Mock()

        with mock.patch("launcher.ra2_files.content_status", return_value={
            "healthy": True,
            "missing_core": [],
        }):
            target.launch("menu")

        target._launch_impl.assert_called_once_with("menu")
        target.show_page.assert_not_called()
        showwarning.assert_not_called()


if __name__ == "__main__":
    unittest.main()
