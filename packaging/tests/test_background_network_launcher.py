import inspect
import tempfile
import unittest
from pathlib import Path
from unittest import mock

import launcher
import launcher_i18n


class BackgroundNetworkLauncherTest(unittest.TestCase):
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

    def test_retired_background_service_translations_are_absent(self):
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
        self.assertIn(
            "self._open_brand_website",
            inspect.getsource(launcher.Launcher._page_notice),
        )
        self.assertIn("self._goto_net", inspect.getsource(launcher.Launcher._page_home))
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


if __name__ == "__main__":
    unittest.main()
