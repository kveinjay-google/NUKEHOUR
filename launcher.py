#!/usr/bin/env python3
"""NUKE HOUR macOS 启动中枢 —— 完整实现游戏主菜单全部功能：
开始游戏（遭遇战/联机/载入/主菜单）、显示/音频/输入/高级全项设置、
以及回放、地图编辑器、素材浏览器、音乐播放器、制作名单、内容管理等直达入口。
所有设置通过命令行参数传入引擎并在游戏退出时持久化。
"""

import glob
import json
import os
import plistlib
import random
import re
import shutil
import signal
import socket
import subprocess
import sys
import time
import tkinter as tk
import webbrowser
from tkinter import filedialog, messagebox, simpledialog

import ra2_files
import launcher_i18n as i18n


def runtime_root(frozen=None, executable=None, meipass=None, source_file=None):
    """Resolve repository data in source and frozen macOS app layouts."""
    if frozen is None:
        frozen = bool(getattr(sys, "frozen", False))
    if frozen:
        executable = executable or sys.executable
        app_resources = os.path.normpath(os.path.join(
            os.path.dirname(os.path.abspath(executable)), "..", "Resources", "runtime"))
        if os.path.isdir(app_resources) or executable:
            return app_resources
        return os.path.join(meipass or getattr(sys, "_MEIPASS", ""), "runtime")
    return os.path.dirname(os.path.abspath(source_file or __file__))


REPO = runtime_root()
GAME_SH = os.path.join(REPO, "launch-game.sh")
BRAND_NAME = "NUKE HOUR"
BRAND_WEBSITE_URL = "https://nukehour.com"
STARTUP_NOTICE_TITLE = "使用与发行声明"
STARTUP_NOTICE_COPY = """NUKE HOUR 是完全独立开发、完全免费且开源的软件项目。

EA 未认可且不支持本产品。本项目与 EA、Apple 或 Google 不存在隶属、赞助、授权或支持关系。

本软件不包含任何第三方零售游戏文件。用户必须从自己合法购买并拥有的正版游戏副本中手动导入兼容文件。界面中出现的游戏或软件名称仅用于说明可兼容导入文件的来源，不代表关联、授权、认可或支持。

本软件完全免费开源。从任何地方付费购买本软件都属于受骗上当；请向作者举报销售者，并向销售渠道申请退款。

请只从 NUKE HOUR 官方网站下载官方授权版本。不要从官方网站以外的任何地方下载安装，以防软件被篡改或植入恶意代码。"""
GAME_APP = os.path.join(REPO, "NUKE HOUR GAME.app")
GAME_HOST = os.path.join(GAME_APP, "Contents", "MacOS", "NuclearCrisis")
SETTINGS_FILE = os.path.join(ra2_files.SUPPORT, "settings.yaml")
LAUNCH_LOG = os.path.join(ra2_files.SUPPORT, "Logs", "launcher-launch.log")
ASSETS = os.path.join(REPO, "launcher_assets")
ENGINE = os.path.join(REPO, "engine")
BRAND_ICON = os.path.join(REPO, "branding", "NUKE-HOUR-1024.png")
BRAND_WALLPAPER_NAMES = tuple(
    f"NUCLEAR-CRISIS-BG-{index:02d}.png" for index in range(1, 10))
BRAND_WALLPAPER_DIR = os.path.join(REPO, "mods", "ra2", "uibits")
MAC_MOD_SEARCH_PATHS = f"./mods,{os.path.join(REPO, 'mods')}"
HOTKEY_DEF_GLOBS = [os.path.join(ENGINE, "mods", "common", "hotkeys", "*.yaml"),
                    os.path.join(REPO, "mods", "ra2", "hotkeys.yaml")]

try:
    from PIL import Image, ImageOps, ImageTk
    HAS_PIL = True
except ImportError:
    HAS_PIL = False

import ai_profiles as aip


def bind_runtime_modules(runtime):
    ra2_files.REPO = os.path.abspath(os.fspath(runtime))


bind_runtime_modules(REPO)


def _latest_hostfxr(runtime):
    hostfxr_root = os.path.join(os.fspath(runtime), "dotnet", "host", "fxr")
    if not os.path.isdir(hostfxr_root):
        return None
    for version in sorted(os.listdir(hostfxr_root), reverse=True):
        candidate = os.path.join(hostfxr_root, version, "libhostfxr.dylib")
        if os.path.isfile(candidate):
            return candidate
    return None


def game_command_for_runtime(runtime, args):
    runtime = os.path.abspath(os.fspath(runtime))
    game_app = os.path.join(runtime, "NUKE HOUR GAME.app")
    game_host = os.path.join(game_app, "Contents", "MacOS", "NuclearCrisis")
    hostfxr_lib = _latest_hostfxr(runtime)
    dll = os.path.join(runtime, "engine", "bin", "OpenRA.dll")
    if os.path.isfile(game_host) and hostfxr_lib and os.path.isfile(dll):
        return [
            game_host,
            hostfxr_lib,
            dll,
            f"Engine.LaunchPath={game_host}",
            "Engine.EngineDir=..",
            f"Engine.ModSearchPaths=./mods,{os.path.join(runtime, 'mods')}",
            "Game.Mod=ra2",
            *args,
        ]
    return ["bash", os.path.join(runtime, "launch-game.sh"), *args]


def initial_page(requested, content_ready):
    if requested == "content":
        return "content"
    return requested if content_ready else "content"


def with_content_manager_return_policy(args):
    """Mark content-manager cancellation as a return to this launcher."""
    return [*args, "Game.ContentManagerReturnToLauncher=true"]


def support_dir_launch_arg():
    """Return the shared branded support directory for game/server processes."""
    return f"Engine.SupportDir={ra2_files.SUPPORT}"


def startup_page():
    return "notice"


def notice_brand_link_spans(text, brand=BRAND_NAME):
    """Return every brand-name range that should link to the official website."""
    spans = []
    start = 0
    while True:
        start = text.find(brand, start)
        if start < 0:
            return tuple(spans)
        end = start + len(brand)
        spans.append((start, end))
        start = end


def guarded_page(requested, acknowledged):
    if acknowledged or requested == startup_page():
        return requested
    return startup_page()


def skirmish_map_menu_state(labels):
    """Return OptionMenu values and whether direct skirmish can start."""
    if labels:
        return list(labels), True
    return [_tr("尚无可用地图")], False


def _tr(text):
    return i18n.translate(text)


def _trf(text, **values):
    return i18n.format_text(text, **values)


def localized_options(options, effective_language=None):
    return [
        (i18n.translate(label, effective_language), value)
        for label, value in options
    ]


def hotkey_fluent_files(effective_language=None):
    language = effective_language or i18n.get_language()
    english = [
        os.path.join(ENGINE, "mods", "common", "fluent", "hotkeys.ftl"),
        os.path.join(REPO, "mods", "ra2", "fluent", "hotkeys.ftl"),
    ]
    chinese = [
        os.path.join(ENGINE, "mods", "common", "fluent", "zh-CN", "hotkeys.ftl"),
        os.path.join(REPO, "mods", "ra2", "fluent", "zh-CN", "hotkeys.ftl"),
    ]
    return chinese + english if str(language).lower().startswith("zh") else english + chinese


def language_launch_args(preference, system_tag):
    return [
        f"Game.Language={i18n.normalize_preference(preference)}",
        f"Engine.SystemLanguage={system_tag or 'en-US'}",
    ]


def default_player_name(effective_language=None):
    return BRAND_NAME


def migrate_legacy_player_name(name):
    if name in ("", "Commander", "指挥官"):
        return BRAND_NAME
    return name


def default_room_name(effective_language=None):
    return i18n.translate("红色警戒2 房间", effective_language)


def profile_display_name(profile, effective_language=None):
    if profile.get("builtin"):
        return i18n.translate(profile["name"], effective_language)
    return profile["name"]


def duplicate_profile_name(profile, effective_language=None):
    return (
        profile_display_name(profile, effective_language)
        + i18n.translate(" 副本", effective_language)
    )


def switch_localized_default(
        current_value, was_default, canonical_value, old_language, new_language):
    old_default = i18n.translate(canonical_value, old_language)
    if was_default and current_value == old_default:
        return i18n.translate(canonical_value, new_language)
    return current_value


def localize_credits_text(text, effective_language=None):
    for original in (
        "【Red Alert 2 模组】",
        "【OpenRA 引擎】",
        "未找到制作名单文件",
    ):
        text = text.replace(
            original,
            i18n.translate(original, effective_language),
        )
    return text


RA2_FILE_ERROR_TEXTS = frozenset({
    "长度前缀越界",
    "文件过小",
    "无效的 orasav 结尾标记",
    "无效的元数据标记",
    "缺少 map.yaml",
    "只允许删除用户目录下的文件",
    "未找到元数据",
})


def localize_ra2_file_error(text, effective_language=None):
    text = str(text or "")
    if text in RA2_FILE_ERROR_TEXTS:
        return i18n.translate(text, effective_language)
    return text


def localize_replay_text(text, effective_language=None):
    text = str(text or "")
    error = localize_ra2_file_error(text, effective_language)
    if error != text:
        return error
    if text in {"未知地图", "未知"}:
        return i18n.translate(text, effective_language)
    match = re.fullmatch(r"(\d+) \u5206 (\d+) \u79d2", text)
    if match:
        return i18n.format_text(
            "{minutes} 分 {seconds} 秒",
            effective_language,
            minutes=match.group(1),
            seconds=match.group(2),
        )
    return text

# ---------------------------------------------------------------- 调色板 / 字体

BG = "#15161a"        # 窗口底色（贴近背景纹理）
PANEL = "#1d1e24"     # 页面底色
CARD = "#26272f"      # 卡片底色
CARD_EDGE = "#3a3b45"
FG = "#e8e8ec"
DIM = "#9a9aa6"
ACCENT = "#c8352a"    # 苏联红
ACCENT_HOV = "#e04838"
GOLD = "#d4af37"
BTN = "#33343d"
BTN_HOV = "#454650"

FONT_TITLE = ("PingFang SC", 24, "bold")
FONT_SUB = ("PingFang SC", 11)
FONT_SECTION = ("PingFang SC", 13, "bold")
FONT_LABEL = ("PingFang SC", 12)
FONT_SMALL = ("PingFang SC", 10)
FONT_BTN = ("PingFang SC", 12)
FONT_NAV = ("PingFang SC", 13)
FONT_BIG = ("PingFang SC", 16, "bold")

WIN_W, WIN_H = 1000, 700
SIDEBAR_W = 190


def launcher_shell_layout(width=WIN_W, height=WIN_H, sidebar_width=SIDEBAR_W):
    """Return deterministic root-relative rectangles for the launcher shell."""
    side_padding = 12
    footer_height = 18
    footer_bottom = 10
    footer_y = height - footer_bottom - footer_height
    control_width = sidebar_width - side_padding * 2
    language_height = 34

    return {
        "content": (sidebar_width, 0, width - sidebar_width, height),
        "language_switcher": (
            side_padding,
            footer_y - 8 - language_height,
            control_width,
            language_height,
        ),
        "footer": (
            side_padding,
            footer_y,
            control_width,
            footer_height,
        ),
        "version_footer": (
            width - 196,
            height - 30,
            180,
            18,
        ),
    }


def notice_page_layout(width=WIN_W - SIDEBAR_W, height=WIN_H):
    """Return fixed notice rectangles that remain above the version footer."""
    padding = 30
    actions_height = 48
    actions_y = height - 90
    body_y = 123
    body_bottom = actions_y - 14
    return {
        "title": (padding, 28, width - 2 * padding - 72, 47),
        "subtitle": (padding, 83, width - 2 * padding - 72, 28),
        "body": (padding, body_y, width - 2 * padding, body_bottom - body_y),
        "actions": (padding, actions_y, width - 2 * padding, actions_height),
    }


def language_switcher_layout(width, height):
    """Return the deterministic rectangles for bilingual switcher controls."""
    arrow_width = 36
    gap = 4
    return {
        "previous": (0, 0, arrow_width, height),
        "label": (arrow_width + gap, 0, width - 2 * (arrow_width + gap), height),
        "next": (width - arrow_width, 0, arrow_width, height),
    }


def language_switch_target(effective_language):
    """Return the other supported language for the visible toggle."""
    return "en" if str(effective_language).lower().startswith("zh") else "zh-CN"


def language_switch_label(effective_language):
    """Return the bilingual label for the effective launcher language."""
    return "简体中文" if str(effective_language).lower().startswith("zh") else "English"


def launcher_product_version(root=REPO):
    root = os.path.abspath(os.fspath(root))
    version = None
    build = None

    manifest_path = os.path.join(root, "packaging", "nukehour-version.json")
    try:
        with open(manifest_path, encoding="utf-8") as stream:
            manifest = json.load(stream)
        version = str(manifest.get("version") or "").strip() or None
        build = int(manifest["build"])
    except (OSError, KeyError, TypeError, ValueError, json.JSONDecodeError):
        pass

    if version is None or build is None:
        plist_path = os.path.join(root, "..", "..", "Info.plist")
        try:
            with open(plist_path, "rb") as stream:
                info = plistlib.load(stream)
            if version is None:
                version = str(info.get("CFBundleShortVersionString") or "").strip() or None
            if build is None:
                build = int(info["CFBundleVersion"])
        except (OSError, KeyError, TypeError, ValueError, plistlib.InvalidFileException):
            pass

    return version or "0.0.0", build if build is not None else 0


def launcher_version_text(version, build):
    return f"{BRAND_NAME} {version} (Build {build})"

# ---------------------------------------------------------------- 设置读取

DEFAULTS = {
    "Player": {"Name": "", "Color": "C82020"},
    "Graphics": {
        "Mode": "PseudoFullscreen", "WindowedSize": "1024,768", "UIScale": "auto",
        "VSync": "True", "CapFramerateToGameFps": "False", "CursorDouble": "False",
        "ViewportDistance": "Medium", "GLProfile": "Automatic", "VideoDisplay": "0",
        "DisableHardwareCursors": "False", "DisableGLDebugMessageCallback": "False",
    },
    "Game": {
        "Language": "System", "TargetLines": "Manual", "StatusBars": "Standard",
        "UsePlayerStanceColors": "False", "TextNotificationPoolFilters": "Feedback, Transients",
        "PauseShellmap": "False", "HideReplayChat": "False",
        "UseClassicMouseStyle": "False", "MouseScroll": "Joystick",
        "UseAlternateScrollButton": "False", "ViewportEdgeScroll": "True",
        "ViewportEdgeScrollStep": "30", "ZoomSpeed": "0.04", "ZoomModifier": "None",
        "UIScrollSpeed": "50", "LockMouseWindow": "False",
    },
    "Sound": {
        "Mute": "False", "SoundVolume": "0.5", "MusicVolume": "0.5", "VideoVolume": "0.5",
        "CashTicks": "True", "Shuffle": "False", "Repeat": "False",
    },
    "Server": {"DiscoverNatDevices": "False"},
    "Debug": {
        "PerfGraph": "False", "PerfText": "False", "DisplayDeveloperSettings": "False",
        "BotDebug": "False", "LuaDebug": "False",
        "EnableDebugCommandsInReplays": "False", "EnableSimulationPerfLogging": "False",
    },
}

RETIRED_LAUNCHER_SETTINGS = {
    "Game": frozenset({"FetchNews"}),
    "Debug": frozenset({"CheckVersion", "SendSystemInformation"}),
}


def strip_retired_launcher_settings(lines):
    """Remove retired background-service keys from their original sections only."""
    out = []
    section = None
    for line in lines:
        is_section = (
            bool(line.strip())
            and not line.startswith((" ", "\t"))
            and line.rstrip().endswith(":")
        )
        if is_section:
            section = line.rstrip()[:-1].strip()
        elif section in RETIRED_LAUNCHER_SETTINGS and ":" in line:
            key, _separator, _value = line.strip().partition(":")
            if key.strip() in RETIRED_LAUNCHER_SETTINGS[section]:
                continue
        out.append(line)
    return out


def persist_language_preference(preference, settings_file=None):
    """Atomically persist only Game.Language while preserving the YAML text."""
    path = settings_file or SETTINGS_FILE
    preference = i18n.normalize_preference(preference)
    try:
        with open(path, "r", encoding="utf-8") as stream:
            lines = stream.read().splitlines()
    except FileNotFoundError:
        lines = []

    game_indices = []
    language_indices = []
    section = None
    for index, line in enumerate(lines):
        is_section = bool(line.strip()) and not line.startswith((" ", "\t")) and line.rstrip().endswith(":")
        if is_section:
            section = line.rstrip()[:-1].strip()
            if section == "Game":
                game_indices.append(index)
            continue
        if section == "Game" and ":" in line:
            key, _separator, _value = line.strip().partition(":")
            if key.strip() == "Language":
                language_indices.append(index)

    if len(game_indices) > 1:
        raise ValueError("settings.yaml contains duplicate Game sections")
    if len(language_indices) > 1:
        raise ValueError("settings.yaml contains duplicate Game.Language keys")

    game_index = game_indices[0] if game_indices else None
    language_index = language_indices[0] if language_indices else None
    next_section = len(lines)
    if game_index is not None:
        for index in range(game_index + 1, len(lines)):
            line = lines[index]
            if bool(line.strip()) and not line.startswith((" ", "\t")) and line.rstrip().endswith(":"):
                next_section = index
                break

    if game_index is None:
        if lines and lines[-1] != "":
            lines.append("")
        lines.extend(["Game:", f"\tLanguage: {preference}"])
    elif language_index is not None:
        indent = lines[language_index][: len(lines[language_index]) - len(lines[language_index].lstrip())]
        lines[language_index] = f"{indent}Language: {preference}"
    else:
        lines.insert(next_section, f"\tLanguage: {preference}")

    lines = strip_retired_launcher_settings(lines)

    parent = os.path.dirname(path)
    if parent:
        os.makedirs(parent, exist_ok=True)
    temporary = path + ".tmp"
    try:
        with open(temporary, "w", encoding="utf-8") as stream:
            stream.write("\n".join(lines) + "\n")
        os.replace(temporary, path)
    except Exception:
        try:
            os.remove(temporary)
        except OSError:
            pass
        raise


def load_settings():
    """读取游戏 settings.yaml 的简单两层结构，与默认值合并。"""
    data = {k: dict(v) for k, v in DEFAULTS.items()}
    try:
        section = None
        with open(SETTINGS_FILE, "r", encoding="utf-8") as f:
            for raw in f:
                line = raw.rstrip("\n")
                if not line.strip() or line.lstrip().startswith("#"):
                    continue
                if not line.startswith("\t") and not line.startswith(" ") and line.rstrip().endswith(":"):
                    section = line.rstrip()[:-1]
                    continue
                stripped = line.strip()
                if section in data and ":" in stripped:
                    key, _, val = stripped.partition(":")
                    key = key.strip()
                    if key not in RETIRED_LAUNCHER_SETTINGS.get(section, ()):
                        data[section][key] = val.strip()
    except OSError:
        pass
    data["Player"]["Name"] = migrate_legacy_player_name(
        data["Player"].get("Name") or ""
    )
    return data


def brand_wallpaper_paths():
    """Return the canonical, stable-order legacy internal wallpaper filename set."""
    return [
        os.path.join(BRAND_WALLPAPER_DIR, name)
        for name in BRAND_WALLPAPER_NAMES
    ]


def choose_brand_wallpaper(selector=None):
    """Choose one canonical wallpaper; selector is injectable for tests."""
    paths = brand_wallpaper_paths()
    if selector is None:
        selector = random.SystemRandom().choice
    return selector(paths)


# ---------------------------------------------------------------- 选项映射

MODES = [("仅进入主菜单", "menu"), ("遭遇战", "skirmish"),
         ("多人联机", "multiplayer"), ("载入存档", "load")]

VIDEO_MODES = [("无边框全屏（推荐）", "PseudoFullscreen"),
               ("窗口化", "Windowed"), ("经典全屏", "Fullscreen")]
WINDOW_SIZES = ["1280,720", "1440,810", "1600,900", "1920,1080", "2560,1440"]
UI_SCALES = [("自动（推荐）", "auto"), ("80%", "0.8"), ("100%", "1"), ("125%", "1.25"),
             ("150%", "1.5"), ("200%", "2")]
LANGUAGES = list(i18n.PREFERENCES)
CURSOR_SIZES = [("标准", "False"), ("双倍", "True")]
GL_PROFILES = [("自动（推荐）", "Automatic"), ("ANGLE", "ANGLE"),
               ("现代 OpenGL", "Modern"), ("嵌入式 OpenGL ES", "Embedded")]
VIDEO_DISPLAYS = [("自动（主显示器）", "0"), ("显示器 2", "1"), ("显示器 3", "2"), ("显示器 4", "3")]
VIEWPORTS = [("原生分辨率", "Native"), ("近", "Close"), ("中（默认）", "Medium"), ("远", "Far")]
TARGET_LINES = [("禁用", "Disabled"), ("手动（按住快捷键）", "Manual"), ("自动", "Automatic")]
STATUS_BARS = [("标准", "Standard"), ("受损时显示", "DamageShow"), ("总是显示", "AlwaysShow")]
CONTROL_SCHEMES = [("现代（左键选取/右键命令）", "False"), ("经典（左键命令）", "True")]
MOUSE_SCROLL = [("摇杆式（推荐）", "Joystick"), ("标准", "Standard"),
                ("反向", "Inverted"), ("禁用", "Disabled")]
ZOOM_MODIFIERS = [("无", "None"), ("Alt", "Alt"), ("Ctrl", "Ctrl"),
                  ("Shift", "Shift"), ("Command", "Meta")]

PLAYER_COLORS = [
    ("苏联红", "C82020"), ("熔岩橙", "D8621E"), ("金黄", "E8C020"),
    ("翠绿", "3CA03C"), ("青", "28A0A0"), ("盟军蓝", "3C64C8"),
    ("紫", "8C50C8"), ("粉", "C86090"),
]

TOOLS_NATIVE = [
    ("回放管理", "观看、整理已保存的比赛回放", "replays"),
    ("存档管理", "载入或清理已保存的对局", "saves"),
    ("地图管理", "浏览官方地图，导入或删除玩家地图", "maps"),
    ("内容管理", "查看游戏内容资产安装状态", "content"),
    ("制作名单", "引擎与模组制作人员", "credits"),
]
TOOLS_ENGINE = [
    ("音乐播放器", "收听游戏原声（AUD 音频封包于 MIX 内，需引擎解码播放）", "music"),
    ("地图编辑器", "创建或编辑遭遇战地图（引擎内置工具）", "editor"),
    ("素材浏览器", "浏览单位、动画与音频素材（引擎内置工具）", "assetbrowser"),
    ("任务战役", "本模组尚未包含战役任务", None),
]

# AI 配置页旋钮：(分组标题, [(存储键, 显示名, 下限, 上限, 单位后缀, 是否小数, 步进)])
SKIRMISH_SAVE = os.path.join(REPO, "launcher_skirmish.json")
SKIRMISH_FACTIONS = [
    ("随机", "Random"), ("随机盟军", "random-allies"), ("随机苏军", "random-soviets"),
    ("美国", "america"), ("德国", "germany"), ("英国", "england"), ("法国", "france"),
    ("韩国", "korea"), ("古巴", "cuba"), ("利比亚", "libya"), ("伊拉克", "iraq"),
    ("苏俄", "russia"), ("尤里", "yuri"),
]
SKIRMISH_CASH = [("2500", "2500"), ("5000（默认）", "5000"), ("10000", "10000"), ("20000", "20000")]
SKIRMISH_SPEEDS = [("最慢", "slowest"), ("慢", "slower"), ("标准（默认）", "default"),
                   ("快", "faster"), ("最快", "fastest")]
SKIRMISH_TECH = [("仅步兵", "infantryonly"), ("低", "low"), ("中", "medium"),
                 ("无限制（默认）", "unrestricted")]
SKIRMISH_TEAMS = [("无", "0"), ("1 队", "1"), ("2 队", "2"), ("3 队", "3"), ("4 队", "4")]

SERVER_STATE = os.path.join(REPO, "launcher_server.json")
SERVER_LOG = os.path.join(REPO, "launcher_server.log")
DOTNET_CANDIDATES = [
    "/usr/local/share/dotnet/dotnet",
    "/usr/local/bin/dotnet",
    "/opt/homebrew/bin/dotnet",
    os.path.expanduser("~/.dotnet/dotnet"),
]


def find_dotnet():
    path = shutil.which("dotnet")
    if path:
        return path
    for c in DOTNET_CANDIDATES:
        if os.path.isfile(c):
            return c
    return None


def local_ips():
    ips = []
    try:
        s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        s.connect(("8.8.8.8", 80))
        ips.append(s.getsockname()[0])
        s.close()
    except OSError:
        pass
    try:
        host = socket.gethostbyname(socket.gethostname())
        if host.startswith(("10.", "172.", "192.168.")) and host not in ips:
            ips.append(host)
    except OSError:
        pass
    return ips


def port_open(host, port, timeout=1.0):
    try:
        with socket.create_connection((host, int(port)), timeout=timeout):
            return True
    except (OSError, ValueError):
        return False


def server_state():
    try:
        with open(SERVER_STATE, encoding="utf-8") as f:
            return json.load(f)
    except (OSError, ValueError):
        return {}


def server_running():
    st = server_state()
    pid = st.get("pid")
    if not pid:
        return None
    try:
        os.kill(pid, 0)
    except OSError:
        return None
    return st

AI_KNOB_DEFS = [
    ("进攻风格", [
        ("squad_size", "进攻小队规模", 1, 20, "", False, 1),
        ("squad_random", "规模随机加成", 0, 50, "", False, 1),
        ("rush_interval_s", "进攻尝试间隔", 4, 120, " 秒", False, 1),
        ("min_attack_delay_s", "成队最小间隔", 0, 60, " 秒", False, 1),
    ]),
    ("经济与生产", [
        ("build_active_delay_s", "建筑生产间隔", 0.4, 4.0, " 秒", True, 0.2),
        ("build_inactive_delay_s", "空闲检查间隔", 1, 20, " 秒", False, 1),
        ("min_cash", "停工现金门槛", 0, 3000, "", False, 100),
        ("refinery_limit", "矿场上限", 1, 8, "", False, 1),
        ("harvester_limit", "矿车上限", 1, 12, "", False, 1),
        ("mcv_count", "分矿扩张基地数", 1, 3, "", False, 1),
    ]),
]


def enum_value(options, current):
    """把存储值转换成下拉框显示值。"""
    for label, value in options:
        if value == current:
            return label
    return current


def option_value(options, label):
    for lab, value in options:
        if lab == label:
            return value
    return label


def option_label(options, stored_value, default_value=None):
    """Return the current localized label for a semantic or legacy label value."""
    display = localized_options(options)
    values = {value for _label, value in options}
    if stored_value in values:
        return enum_value(display, stored_value)
    legacy_value = option_value(options, stored_value)
    if legacy_value in values:
        return enum_value(display, legacy_value)
    localized_value = option_value(display, stored_value)
    if localized_value in values:
        return enum_value(display, localized_value)
    english_value = next(
        (
            value
            for label, value in options
            if i18n.translate(label, "en") == stored_value
        ),
        None,
    )
    if english_value in values:
        return enum_value(display, english_value)
    return enum_value(display, default_value) if default_value is not None else stored_value


def display_option_label(options, stored_value, default_value=None):
    values = {value for _label, value in options}
    if stored_value in values:
        return enum_value(options, stored_value)
    if any(label == stored_value for label, _value in options):
        return stored_value
    english_stored = i18n.translate(stored_value, "en")
    for label, _value in options:
        if i18n.translate(label, "en") == english_stored:
            return label
    return enum_value(options, default_value) if default_value is not None else stored_value


# ---------------------------------------------------------------- 热键基础设施

MOD_ORDER = ["Shift", "Alt", "Ctrl", "Meta"]

TYPE_NAMES = {
    "World": "战场操作", "Unit": "单位指令", "Stance": "部队姿态",
    "OrderGenerator": "指令切换", "ControlGroups": "编队控制",
    "Viewport": "镜头与视野", "Production": "生产建造", "ProductionSlot": "生产槽位",
    "SupportPower": "支援技能", "Chat": "聊天", "Observer": "观战视角",
    "Replay": "回放控制", "Music": "音乐控制", "Editor": "地图编辑器",
    "General": "通用",
}
TYPE_ORDER = ["World", "Unit", "Stance", "OrderGenerator", "ControlGroups", "Viewport",
              "Production", "ProductionSlot", "SupportPower", "Chat", "Observer",
              "Replay", "Music", "Editor", "General"]

KEY_FRIENDLY = {
    "SPACE": "空格", "RETURN": "回车", "TAB": "Tab", "ESCAPE": "Esc",
    "BACKSPACE": "退格", "DELETE": "Del", "INSERT": "Ins", "HOME": "Home",
    "END": "End", "PAGEUP": "PgUp", "PAGEDOWN": "PgDn", "UP": "↑",
    "DOWN": "↓", "LEFT": "←", "RIGHT": "→", "BACKQUOTE": "`",
    "CAPSLOCK": "Caps", "KP_ENTER": "小键盘回车", "COMMA": ",",
    "PERIOD": ".", "MINUS": "-", "EQUALS": "=", "SLASH": "/",
    "SEMICOLON": ";", "QUOTE": "'", "LEFTBRACKET": "[", "RIGHTBRACKET": "]",
    "BACKSLASH": "\\",
}
for _i in range(10):
    KEY_FRIENDLY[f"NUMBER_{_i}"] = str(_i)
    KEY_FRIENDLY[f"KP_{_i}"] = f"小键盘{_i}"

TK_TO_KEYCODE = {
    "space": "SPACE", "Return": "RETURN", "Tab": "TAB", "Escape": "ESCAPE",
    "BackSpace": "BACKSPACE", "Delete": "DELETE", "Insert": "INSERT",
    "Home": "HOME", "End": "END", "Prior": "PAGEUP", "Next": "PAGEDOWN",
    "Up": "UP", "Down": "DOWN", "Left": "LEFT", "Right": "RIGHT",
    "grave": "BACKQUOTE", "minus": "MINUS", "equal": "EQUALS",
    "bracketleft": "LEFTBRACKET", "bracketright": "RIGHTBRACKET",
    "backslash": "BACKSLASH", "semicolon": "SEMICOLON", "apostrophe": "QUOTE",
    "comma": "COMMA", "period": "PERIOD", "slash": "SLASH",
    "Caps_Lock": "CAPSLOCK", "Print": "PRINTSCREEN", "Scroll_Lock": "SCROLLLOCK",
    "Pause": "PAUSE", "KP_Enter": "KP_ENTER", "KP_Add": "KP_PLUS",
    "KP_Subtract": "KP_MINUS", "KP_Multiply": "KP_MULTIPLY",
    "KP_Divide": "KP_DIVIDE", "KP_Decimal": "KP_PERIOD",
}
for _i in range(10):
    TK_TO_KEYCODE[str(_i)] = f"NUMBER_{_i}"
    TK_TO_KEYCODE[f"KP_{_i}"] = f"KP_{_i}"
for _i in range(1, 25):
    TK_TO_KEYCODE[f"F{_i}"] = f"F{_i}"

PURE_MODIFIERS = {"Shift_L", "Shift_R", "Control_L", "Control_R",
                  "Alt_L", "Alt_R", "Meta_L", "Meta_R", "Command", "Super_L", "Super_R"}


def parse_hotkey_definitions():
    """解析引擎/模组的热键定义 yaml，返回有序定义列表。"""
    defs = []
    files = []
    for pattern in HOTKEY_DEF_GLOBS:
        files.extend(sorted(glob.glob(pattern)))

    for path in files:
        current = None
        in_platform = False
        try:
            with open(path, "r", encoding="utf-8") as f:
                lines = f.readlines()
        except OSError:
            continue

        for raw in lines:
            line = raw.rstrip("\n")
            if not line.strip():
                continue
            if not line.startswith("\t") and not line.startswith(" "):
                name, _, value = line.partition(":")
                current = {"name": name.strip(), "default": value.strip(),
                           "desc": "", "types": [], "contexts": [], "readonly": False}
                defs.append(current)
                in_platform = False
                continue

            if current is None:
                continue
            stripped = line.strip()
            key, _, value = stripped.partition(":")
            key, value = key.strip(), value.strip()
            if line.startswith("\t\t") or line.startswith("    "):
                if in_platform and key == "OSX" and value:
                    current["default"] = value
                continue

            in_platform = key == "Platform"
            if key == "Description":
                current["desc"] = value
            elif key == "Types":
                current["types"] = [t.strip() for t in value.split(",") if t.strip()]
            elif key == "Contexts":
                current["contexts"] = [c.strip() for c in value.split(",") if c.strip()]
            elif key == "Readonly":
                current["readonly"] = value == "True"
    return defs


def parse_hotkey_labels():
    """解析 fluent 文件中的热键中文描述（英文兜底）。"""
    labels = {}
    for path in hotkey_fluent_files():
        try:
            with open(path, "r", encoding="utf-8") as f:
                for raw in f:
                    line = raw.rstrip("\n")
                    if line.startswith((" ", "\t", "#")) or " = " not in line:
                        continue
                    key, _, value = line.partition(" = ")
                    key = key.strip()
                    if key.startswith("hotkey-description-") and key not in labels:
                        labels[key] = value.strip()
        except OSError:
            continue
    return labels


def normalize_hotkey(value):
    """'K' / 'K Ctrl' -> ('K', ['Ctrl']) 规范形。"""
    parts = value.split()
    key = parts[0] if parts and parts[0] else "UNKNOWN"
    mods = []
    for token in " ".join(parts[1:]).split(","):
        token = token.strip().capitalize()
        if token in MOD_ORDER and token not in mods:
            mods.append(token)
    mods.sort(key=MOD_ORDER.index)
    return key, mods


def hotkey_value(key, mods):
    return f"{key} {', '.join(mods) if mods else 'None'}"


def hotkey_friendly(value):
    key, mods = normalize_hotkey(value)
    label = KEY_FRIENDLY.get(key, key)
    if key.startswith("KP_") and key[3:].isdigit():
        label = _trf("小键盘{_i}", _i=key[3:])
    else:
        label = _tr(label)
    if not mods:
        return label
    pretty = ["⌘" if m == "Meta" else m for m in mods]
    return " + ".join(pretty + [label])


def load_user_keys():
    """读取 settings.yaml 的 Keys 段（用户自定义绑定）。"""
    keys = {}
    try:
        in_keys = False
        with open(SETTINGS_FILE, "r", encoding="utf-8") as f:
            for raw in f:
                line = raw.rstrip("\n")
                if not line.strip():
                    continue
                top = not line.startswith("\t") and not line.startswith(" ")
                if top:
                    in_keys = line.rstrip() == "Keys:"
                    continue
                if in_keys and ":" in line:
                    name, _, value = line.strip().partition(":")
                    keys[name.strip()] = value.strip()
    except OSError:
        pass
    return keys


def save_user_keys(bindings):
    """把非默认绑定写回 settings.yaml 的 Keys 段（与引擎序列化格式一致）。"""
    try:
        with open(SETTINGS_FILE, "r", encoding="utf-8") as f:
            lines = f.read().splitlines()
    except OSError:
        lines = []

    out, skip_children = [], False
    for line in lines:
        top = line.strip() and not line.startswith("\t") and not line.startswith(" ")
        if top:
            skip_children = line.rstrip() == "Keys:"
            if skip_children:
                continue
        if not skip_children or top:
            out.append(line)

    out = strip_retired_launcher_settings(out)

    if bindings:
        out.append("Keys:")
        for name in sorted(bindings):
            out.append(f"\t{name}: {bindings[name]}")

    tmp = SETTINGS_FILE + ".tmp"
    with open(tmp, "w", encoding="utf-8") as f:
        f.write("\n".join(out) + "\n")
    os.replace(tmp, SETTINGS_FILE)


# ---------------------------------------------------------------- 主程序

class Launcher(tk.Tk):
    def __init__(self):
        super().__init__()
        self.saved = load_settings()
        self.language_preference = i18n.normalize_preference(
            self.saved["Game"].get("Language")
        )
        self.system_language_tag = i18n.detect_system_language_tag()
        self.effective_language = i18n.resolve_language(
            self.language_preference,
            self.system_language_tag,
        )
        i18n.set_language(self.effective_language)
        self._net_refresh_after_id = None
        self.startup_notice_acknowledged = False
        self.post_notice_page = "home"

        self.title(f"{BRAND_NAME}{_tr(' · 启动中枢')}")
        self.configure(bg=BG)
        self.resizable(False, False)
        x = (self.winfo_screenwidth() - WIN_W) // 2
        y = max(0, (self.winfo_screenheight() - WIN_H) // 2 - 30)
        self.geometry(f"{WIN_W}x{WIN_H}+{x}+{y}")

        self.vars = {}
        self._make_vars()

        self._images = []
        self._build_background()
        self._build_sidebar()

        self.content = tk.Frame(self, bg=PANEL)
        content_x, content_y, content_width, content_height = \
            launcher_shell_layout()["content"]
        self.content.place(
            x=content_x,
            y=content_y,
            width=content_width,
            height=content_height,
        )

        self.pages = {}
        self._build_pages()
        self._build_language_switcher()
        self._build_version_footer()
        start_page = "home"
        if "-p" in sys.argv:
            idx = sys.argv.index("-p")
            if (idx + 1 < len(sys.argv)
                    and sys.argv[idx + 1] in self.pages
                    and sys.argv[idx + 1] != startup_page()):
                start_page = sys.argv[idx + 1]
        self.post_notice_page = initial_page(
            start_page,
            ra2_files.content_status()["healthy"],
        )
        self.show_page(startup_page())

    # ------------------------------------------------------------ 变量

    def _make_vars(self):
        g, p, s = self.saved["Game"], self.saved["Player"], self.saved["Sound"]
        gfx, srv, dbg = self.saved["Graphics"], self.saved["Server"], self.saved["Debug"]

        def v(key, val):
            var = tk.StringVar(value=val)
            self.vars[key] = var
            return var

        v("mode", "menu")
        self.player_name_is_default = (
            p.get("Name") == default_player_name(self.effective_language)
        )
        v("player_name", p.get("Name") or default_player_name(self.effective_language))
        v("player_color", p.get("Color", "C82020").lstrip("#").upper())
        v("video_mode_label", enum_value(localized_options(VIDEO_MODES), gfx.get("Mode")))
        v("window_size", gfx.get("WindowedSize", "1024,768"))
        v("ui_scale_label", enum_value(localized_options(UI_SCALES), gfx.get("UIScale")))
        v("vsync", gfx.get("VSync"))
        v("cap_game_fps", gfx.get("CapFramerateToGameFps"))
        v("cursor_label", enum_value(localized_options(CURSOR_SIZES), gfx.get("CursorDouble")))
        v("gl_label", enum_value(localized_options(GL_PROFILES), gfx.get("GLProfile")))
        v("viewport_label", enum_value(localized_options(VIEWPORTS), gfx.get("ViewportDistance")))
        v("video_display_label", enum_value(localized_options(VIDEO_DISPLAYS), gfx.get("VideoDisplay", "0")))
        v("target_lines_label", enum_value(localized_options(TARGET_LINES), g.get("TargetLines")))
        v("status_bars_label", enum_value(localized_options(STATUS_BARS), g.get("StatusBars")))
        v("stance_colors", g.get("UsePlayerStanceColors"))
        filters = g.get("TextNotificationPoolFilters", "Feedback, Transients")
        v("notify_feedback", "True" if "Feedback" in filters else "False")
        v("notify_transients", "True" if "Transients" in filters else "False")
        v("pause_shellmap", g.get("PauseShellmap"))
        v("hide_replay_chat", g.get("HideReplayChat"))

        v("mute", s.get("Mute"))
        v("sound_vol", str(int(float(s.get("SoundVolume", "0.5")) * 100)))
        v("music_vol", str(int(float(s.get("MusicVolume", "0.5")) * 100)))
        v("video_vol", str(int(float(s.get("VideoVolume", "0.5")) * 100)))
        v("cash_ticks", s.get("CashTicks"))
        v("shuffle", s.get("Shuffle"))
        v("repeat", s.get("Repeat"))

        v("control_scheme_label", enum_value(localized_options(CONTROL_SCHEMES), g.get("UseClassicMouseStyle")))
        v("mouse_scroll_label", enum_value(localized_options(MOUSE_SCROLL), g.get("MouseScroll")))
        v("alternate_scroll", g.get("UseAlternateScrollButton"))
        v("edge_scroll", g.get("ViewportEdgeScroll"))
        v("scroll_speed", str(int(float(g.get("ViewportEdgeScrollStep", "30")))))
        v("zoom_speed", str(int(float(g.get("ZoomSpeed", "0.04")) * 100)))
        v("zoom_modifier_label", enum_value(localized_options(ZOOM_MODIFIERS), g.get("ZoomModifier")))
        v("ui_scroll_speed", str(int(float(g.get("UIScrollSpeed", "50")))))
        v("lock_mouse", g.get("LockMouseWindow"))

        v("nat_discovery", srv.get("DiscoverNatDevices"))
        v("perf_graph", dbg.get("PerfGraph"))
        v("perf_text", dbg.get("PerfText"))
        v("dev_settings", dbg.get("DisplayDeveloperSettings"))
        v("bot_debug", dbg.get("BotDebug"))
        v("lua_debug", dbg.get("LuaDebug"))
        v("debug_cmds_replays", dbg.get("EnableDebugCommandsInReplays"))
        v("sim_perf_logging", dbg.get("EnableSimulationPerfLogging"))
        v("disable_hw_cursors", gfx.get("DisableHardwareCursors"))
        v("disable_gl_debug", gfx.get("DisableGLDebugMessageCallback"))

    def _enum_variable_options(self):
        return {
            "video_mode_label": VIDEO_MODES,
            "ui_scale_label": UI_SCALES,
            "cursor_label": CURSOR_SIZES,
            "gl_label": GL_PROFILES,
            "viewport_label": VIEWPORTS,
            "video_display_label": VIDEO_DISPLAYS,
            "target_lines_label": TARGET_LINES,
            "status_bars_label": STATUS_BARS,
            "control_scheme_label": CONTROL_SCHEMES,
            "mouse_scroll_label": MOUSE_SCROLL,
            "zoom_modifier_label": ZOOM_MODIFIERS,
        }

    def _capture_ui_state(self):
        enum_options = self._enum_variable_options()
        values = {}
        for key, variable in self.vars.items():
            value = variable.get()
            if key in enum_options:
                value = option_value(
                    localized_options(enum_options[key], self.effective_language),
                    value,
                )
            values[key] = value

        state = {
            "current_page": getattr(self, "current_page", "home"),
            "vars": values,
            "ai_profiles": json.loads(json.dumps(getattr(self, "ai_profiles", []))),
            "ai_current": getattr(self, "ai_current", 0),
            "ai_name": None,
            "ai_values": {
                key: variable.get()
                for key, variable in getattr(self, "ai_vars", {}).items()
            },
            "player_name_is_default": (
                self.player_name_is_default
                and self.vars["player_name"].get()
                == default_player_name(self.effective_language)
            ),
        }
        if getattr(self, "ai_profiles", None) and hasattr(self, "ai_name_var"):
            current_profile = self.ai_profiles[self.ai_current]
            current_name = self.ai_name_var.get()
            if current_name != profile_display_name(
                    current_profile, self.effective_language):
                state["ai_name"] = current_name

        if hasattr(self, "sk_map_var"):
            selected_map = None
            for item in self.sk_maps:
                if self._sk_map_label(item) == self.sk_map_var.get():
                    selected_map = item.get("pkg") or item.get("name")
                    break
            profile_options = self._sk_profiles_options()
            state["skirmish"] = {
                "map": selected_map,
                "cash": option_value(localized_options(SKIRMISH_CASH), self.sk_cash_var.get()),
                "speed": option_value(localized_options(SKIRMISH_SPEEDS), self.sk_speed_var.get()),
                "tech": option_value(localized_options(SKIRMISH_TECH), self.sk_tech_var.get()),
                "faction": option_value(localized_options(SKIRMISH_FACTIONS), self.sk_faction_var.get()),
                "short": self.sk_short_var.get(),
                "ai_count": self.sk_count_var.get(),
                "bots": [
                    {
                        "profile": option_value(profile_options, profile.get()),
                        "faction": option_value(localized_options(SKIRMISH_FACTIONS), faction.get()),
                        "team": option_value(localized_options(SKIRMISH_TEAMS), team.get()),
                    }
                    for profile, faction, team in self.sk_bot_vars
                ],
            }

        if hasattr(self, "net_addr"):
            state["network"] = {
                "address": self.net_addr.get(),
                "name": self.net_name.get(),
                "port": self.net_port.get(),
                "name_is_default": (
                    self.net_name_is_default
                    and self.net_name.get()
                    == default_room_name(self.effective_language)
                ),
            }
        return state

    def _restore_ui_state(self, state):
        enum_options = self._enum_variable_options()
        for key, value in state["vars"].items():
            if key in enum_options:
                value = enum_value(localized_options(enum_options[key]), value)
            self.vars[key].set(value)
        self.player_name_is_default = state["player_name_is_default"]
        self.vars["player_name"].set(switch_localized_default(
            state["vars"]["player_name"],
            state["player_name_is_default"],
            BRAND_NAME,
            state["old_language"],
            self.effective_language,
        ))

        if state.get("ai_profiles"):
            self.ai_profiles = state["ai_profiles"]
            self.ai_current = min(state["ai_current"], len(self.ai_profiles) - 1)
            self._ai_refresh_list(select=self.ai_current)
            if state["ai_name"] is not None:
                self.ai_name_var.set(state["ai_name"])
            for key, value in state["ai_values"].items():
                if key in self.ai_scales:
                    scale, render = self.ai_scales[key]
                    scale.set(float(value))
                    render(float(value))

        skirmish = state.get("skirmish")
        if skirmish:
            for item in self.sk_maps:
                if (item.get("pkg") or item.get("name")) == skirmish["map"]:
                    self.sk_map_var.set(self._sk_map_label(item))
                    break
            self.sk_cash_var.set(enum_value(localized_options(SKIRMISH_CASH), skirmish["cash"]))
            self.sk_speed_var.set(enum_value(localized_options(SKIRMISH_SPEEDS), skirmish["speed"]))
            self.sk_tech_var.set(enum_value(localized_options(SKIRMISH_TECH), skirmish["tech"]))
            self.sk_faction_var.set(enum_value(localized_options(SKIRMISH_FACTIONS), skirmish["faction"]))
            self.sk_short_var.set(skirmish["short"])
            self.sk_count_var.set(skirmish["ai_count"])
            self._sk_rebuild_bots(saved_bots=skirmish["bots"])

        network = state.get("network")
        if network:
            room_name = switch_localized_default(
                network["name"],
                network["name_is_default"],
                "红色警戒2 房间",
                state["old_language"],
                self.effective_language,
            )
            for entry, value in (
                (self.net_addr, network["address"]),
                (self.net_name, room_name),
                (self.net_port, network["port"]),
            ):
                entry.delete(0, "end")
                entry.insert(0, value)
            self.net_name_is_default = network["name_is_default"]

    def _toggle_language(self):
        try:
            self._change_language_preference(
                language_switch_target(self.effective_language))
        except Exception as exc:
            messagebox.showerror(_tr("操作失败"), str(exc), parent=self)

    def _change_language_preference(self, preference):
        preference = i18n.normalize_preference(preference)
        if preference == "System":
            raise ValueError("The visible language switcher requires an explicit language")
        if preference == self.language_preference:
            return

        state = self._capture_ui_state()
        state["old_language"] = self.effective_language
        current_page = state["current_page"]
        persist_language_preference(preference)
        self.language_preference = preference
        self.saved["Game"]["Language"] = preference
        self.effective_language = i18n.resolve_language(
            preference,
            self.system_language_tag,
        )
        i18n.set_language(self.effective_language)

        if self._net_refresh_after_id is not None:
            try:
                self.after_cancel(self._net_refresh_after_id)
            except Exception:
                pass
            self._net_refresh_after_id = None

        self.sidebar.destroy()
        self.content.destroy()
        self.language_switcher.destroy()
        self.title(f"{BRAND_NAME}{_tr(' · 启动中枢')}")
        self._build_sidebar()
        self.content = tk.Frame(self, bg=PANEL)
        content_x, content_y, content_width, content_height = \
            launcher_shell_layout()["content"]
        self.content.place(
            x=content_x,
            y=content_y,
            width=content_width,
            height=content_height,
        )
        self.pages = {}
        self._build_pages()
        self._build_language_switcher()
        if state.get("skirmish"):
            self._ensure_page_built("skirmish")
        self._restore_ui_state(state)
        self.show_page(current_page)
        self.version_label.lift()

    # ------------------------------------------------------------ 背景与侧栏

    def _build_background(self):
        if not HAS_PIL:
            return
        try:
            img = Image.open(choose_brand_wallpaper()).convert("RGB")
            img = ImageOps.fit(img, (WIN_W, WIN_H), method=Image.LANCZOS)
            self.bg_photo = ImageTk.PhotoImage(img)
            tk.Label(self, image=self.bg_photo, bd=0).place(x=0, y=0, relwidth=1, relheight=1)
        except OSError:
            pass

    def _build_sidebar(self):
        layout = launcher_shell_layout()
        bar = tk.Frame(self, bg=BG)
        self.sidebar = bar
        bar.place(x=0, y=0, width=SIDEBAR_W, height=WIN_H)

        if HAS_PIL:
            try:
                img = Image.open(BRAND_ICON).convert("RGB")
                img = ImageOps.fit(img, (150, 150), method=Image.LANCZOS)
                self.emblem_photo = ImageTk.PhotoImage(img)
                emblem = tk.Label(
                    bar,
                    image=self.emblem_photo,
                    bg=BG,
                    bd=0,
                    cursor="hand2",
                )
                emblem.pack(pady=(22, 4))
                emblem.bind("<Button-1>", self._open_brand_website)
            except OSError:
                pass

        tk.Label(bar, text=BRAND_NAME, font=("PingFang SC", 17, "bold"),
                 bg=BG, fg=FG).pack(pady=(0, 14))

        self.nav_buttons = {}
        nav = [("home", "开始游戏"), ("display", "显示设置"), ("audio", "音频设置"),
               ("input", "输入设置"), ("hotkeys", "热键设置"), ("ai", "AI 配置"),
               ("advanced", "高级设置"), ("tools", "更多功能")]
        for key, label in nav:
            btn = tk.Label(bar, text=_tr(label), font=FONT_NAV, bg=BG, fg=DIM,
                           padx=18, pady=9, anchor="w", cursor="hand2")
            btn.pack(fill="x", padx=12, pady=1)
            btn.bind("<Button-1>", lambda _e, k=key: self.show_page(k))
            btn.bind("<Enter>", lambda _e, b=btn, k=key: self._nav_hover(b, k, True))
            btn.bind("<Leave>", lambda _e, b=btn, k=key: self._nav_hover(b, k, False))
            self.nav_buttons[key] = btn

        footer_x, footer_y, footer_width, footer_height = layout["footer"]
        tk.Label(
            bar,
            text=f"{BRAND_NAME} · macOS",
            font=FONT_SMALL,
            bg=BG,
            fg=DIM,
        ).place(
            x=footer_x,
            y=footer_y,
            width=footer_width,
            height=footer_height,
        )

    def _build_language_switcher(self):
        layout = launcher_shell_layout()
        x, y, width, height = layout["language_switcher"]
        controls = language_switcher_layout(width, height)
        frame = tk.Frame(self, bg=BG)
        self.language_switcher = frame
        frame.place(x=x, y=y, width=width, height=height)

        button_base = self._control_surface(34, height - 2, "#292b33", "#666977")
        button_hover = self._control_surface(34, height - 2, "#393c46", GOLD)
        for key, glyph in (("previous", "‹"), ("next", "›")):
            bx, by, bw, bh = controls[key]
            button = tk.Button(
                frame,
                text=glyph,
                command=self._toggle_language,
                image=button_base,
                compound="center",
                font=("PingFang SC", 17, "bold"),
                bg=BG,
                fg="#f7f7fa",
                activebackground=BG,
                activeforeground="white",
                relief="flat",
                bd=0,
                highlightthickness=1,
                highlightbackground=BG,
                highlightcolor=GOLD,
                cursor="hand2",
                takefocus=True,
            )
            button.bind("<Enter>", lambda _event, widget=button: widget.config(image=button_hover))
            button.bind("<Leave>", lambda _event, widget=button: widget.config(image=button_base))
            button.place(x=bx, y=by, width=bw, height=bh)

        lx, ly, lw, lh = controls["label"]
        label_surface = self._control_surface(
            lw - 2, lh - 2, "#22242b", "#50535f")
        tk.Label(
            frame,
            text=language_switch_label(self.effective_language),
            image=label_surface,
            compound="center",
            font=("PingFang SC", 11, "bold"),
            bg=BG,
            fg=FG,
            anchor="center",
        ).place(x=lx, y=ly, width=lw, height=lh)
        frame.lift()

    def _open_brand_website(self, _event=None):
        try:
            if not webbrowser.open_new_tab(BRAND_WEBSITE_URL):
                raise RuntimeError("Unable to open the default browser")
        except Exception as exc:
            messagebox.showerror(_tr("操作失败"), str(exc), parent=self)

    def _build_version_footer(self):
        x, y, width, height = launcher_shell_layout()["version_footer"]
        version, build = launcher_product_version()
        self.version_label = tk.Label(
            self,
            text=launcher_version_text(version, build),
            font=FONT_SMALL,
            bg=PANEL,
            fg=DIM,
            anchor="e",
        )
        self.version_label.place(x=x, y=y, width=width, height=height)

    def _nav_hover(self, btn, key, enter):
        if key == getattr(self, "current_page", None):
            return
        btn.config(fg=FG if enter else DIM)

    # ------------------------------------------------------------ 页面骨架

    def _build_pages(self):
        builders = {
            "notice": (self._page_notice, False),
            "home": (self._page_home, False),
            "display": (self._page_display, True),
            "audio": (self._page_audio, True),
            "input": (self._page_input, True),
            "hotkeys": (self._page_hotkeys, True),
            "ai": (self._page_ai, True),
            "advanced": (self._page_advanced, True),
            "tools": (self._page_tools, False),
            "replays": (self._page_replays, True),
            "saves": (self._page_saves, True),
            "maps": (self._page_maps, True),
            "content": (self._page_content, True),
            "credits": (self._page_credits, False),
            "skirmish": (self._page_skirmish, True),
            "net": (self._page_net, False),
        }
        self._page_builders = builders
        self._built_pages = set()
        self.scroll_canvases = {}
        for key in builders:
            self.pages[key] = tk.Frame(self.content, bg=PANEL)
        for key in builders:
            if key != "skirmish":
                self._ensure_page_built(key)

    def _ensure_page_built(self, key):
        if key in self._built_pages:
            return
        build, scrollable = self._page_builders[key]
        page = self.pages[key]
        if scrollable:
            build(self._scrollable(page, key))
        else:
            build(page)
        self._built_pages.add(key)

    def _invalidate_page(self, key):
        if key not in self._built_pages:
            return
        page = self.pages[key]
        for child in page.winfo_children():
            child.destroy()
        self.scroll_canvases.pop(key, None)
        self._built_pages.remove(key)

    def _scrollable(self, page, key):
        """在页面内放置可滚动的内容框架，返回内容父容器。"""
        canvas = tk.Canvas(page, bg=PANEL, highlightthickness=0, bd=0)
        scrollbar = tk.Scrollbar(page, orient="vertical", command=canvas.yview)
        canvas.configure(yscrollcommand=scrollbar.set)
        scrollbar.pack(side="right", fill="y")
        canvas.pack(side="left", fill="both", expand=True)

        inner = tk.Frame(canvas, bg=PANEL)
        window_id = canvas.create_window((0, 0), window=inner, anchor="nw")
        inner.bind("<Configure>", lambda _e: canvas.configure(scrollregion=canvas.bbox("all")))
        canvas.bind("<Configure>", lambda e: canvas.itemconfigure(window_id, width=e.width))

        self.scroll_canvases[key] = canvas
        return inner

    def show_page(self, key):
        key = guarded_page(key, self.startup_notice_acknowledged)
        self._ensure_page_built(key)
        self.current_page = key
        for k, page in self.pages.items():
            page.place_forget()
        self.pages[key].place(x=0, y=0, relwidth=1, relheight=1)

        self.unbind_all("<MouseWheel>")
        canvas = self.scroll_canvases.get(key)
        if canvas is not None:
            self.bind_all("<MouseWheel>",
                          lambda e: canvas.yview_scroll(-1 * (e.delta // 3 or 1), "units"))
        for k, btn in self.nav_buttons.items():
            if k == key:
                btn.config(bg=ACCENT, fg="white")
            else:
                btn.config(bg=BG, fg=DIM)

    # ------------------------------------------------------------ 控件工厂

    def _card(self, parent, title):
        outer = tk.Frame(parent, bg=CARD_EDGE)
        outer.pack(fill="x", padx=18, pady=(8, 0))
        inner = tk.Frame(outer, bg=CARD)
        inner.pack(fill="x", padx=1, pady=1)
        tk.Label(inner, text=_tr(title), font=FONT_SECTION, bg=CARD, fg=GOLD,
                 anchor="w").pack(fill="x", padx=14, pady=(6, 1))
        return inner

    def _row(self, card, label):
        row = tk.Frame(card, bg=CARD)
        row.pack(fill="x", padx=14, pady=2)
        tk.Label(row, text=_tr(label), font=FONT_LABEL, bg=CARD, fg=FG,
                 width=16, anchor="w").pack(side="left")
        return row

    def _combo(self, row, var_label, options, width=22, already_localized=False):
        display_options = options if already_localized else localized_options(options)
        vals = [lab for lab, _ in display_options]
        cb = tk.Frame(row, bg=CARD)
        menu_var = var_label
        opt = tk.OptionMenu(cb, menu_var, *vals)
        opt.config(font=FONT_LABEL, bg=BTN, fg=FG, activebackground=BTN_HOV,
                   activeforeground=FG, highlightthickness=0, bd=0, width=width, anchor="w")
        opt["menu"].config(bg=BTN, fg=FG, activebackground=ACCENT, activeforeground="white",
                           font=FONT_LABEL)
        opt.pack(side="left")
        cb.pack(side="left", padx=4)

    def _check(self, card, label, key, hint=None):
        var = self.vars[key]
        cb = tk.Checkbutton(card, text=_tr(label), variable=var, onvalue="True", offvalue="False",
                            font=FONT_LABEL, bg=CARD, fg=FG, selectcolor=BTN,
                            activebackground=CARD, activeforeground=FG, anchor="w")
        cb.pack(fill="x", padx=14, pady=1)
        if hint:
            tk.Label(card, text=_tr(hint), font=FONT_SMALL, bg=CARD, fg=DIM,
                     anchor="w").pack(fill="x", padx=36, pady=(0, 4))

    def _slider(self, card, label, key, lo, hi, unit=""):
        row = self._row(card, label)
        var = self.vars[key]
        val_lbl = tk.Label(row, text=f"{var.get()}{unit}", font=FONT_LABEL,
                           bg=CARD, fg=GOLD, width=6, anchor="e")
        val_lbl.pack(side="right")

        def on_move(v):
            val_lbl.config(text=f"{int(float(v))}{unit}")
            var.set(str(int(float(v))))

        scale = tk.Scale(row, from_=lo, to=hi, orient="horizontal", showvalue=False,
                         command=on_move, bg=CARD, fg=FG, troughcolor=BTN,
                         highlightthickness=0, bd=0, activebackground=ACCENT, length=360)
        scale.set(int(float(var.get())))
        scale.pack(side="right", padx=8)

    def _control_surface(self, width, height, fill, border):
        """Create an image-backed surface that Aqua cannot recolor."""
        surface = tk.PhotoImage(master=self, width=width, height=height)
        surface.put(border, to=(0, 0, width, height))
        surface.put(fill, to=(2, 2, width - 2, height - 2))
        self._images.append(surface)
        return surface

    def _button(self, parent, text, command, accent=False, big=False, enabled=True):
        btn = tk.Label(parent, text=_tr(text), font=FONT_BIG if big else FONT_BTN,
                       bg=ACCENT if accent and enabled else BTN,
                       fg="white" if enabled else DIM,
                       padx=22, pady=12 if big else 8,
                       cursor="hand2" if enabled else "arrow")
        base = ACCENT if accent else BTN
        hover = ACCENT_HOV if accent else BTN_HOV
        if not enabled:
            return btn
        btn.bind("<Enter>", lambda _e: btn.config(bg=hover))
        btn.bind("<Leave>", lambda _e: btn.config(bg=base))
        def _run(_e=None):
            try:
                command()
            except Exception as exc:
                messagebox.showerror(_tr("操作失败"), str(exc), parent=self)

        btn.bind("<Button-1>", _run)
        return btn

    def _native_button(self, parent, text, command, accent=False):
        """Create an accessible native button for first-run critical actions."""
        base = ACCENT if accent else "#343640"
        hover = ACCENT_HOV if accent else "#464955"
        border = "#e86a60" if accent else "#676a77"
        hover_border = "#ff9a90" if accent else GOLD
        base_image = self._control_surface(148, 42, base, border)
        hover_image = self._control_surface(148, 42, hover, hover_border)

        def _run():
            try:
                command()
            except Exception as exc:
                messagebox.showerror(_tr("操作失败"), str(exc), parent=self)

        btn = tk.Button(
            parent,
            text=_tr(text),
            command=_run,
            image=base_image,
            compound="center",
            font=("PingFang SC", 12, "bold"),
            bg=PANEL,
            fg="#f7f7fa",
            activebackground=PANEL,
            activeforeground="white",
            relief="flat",
            bd=0,
            highlightthickness=2,
            highlightbackground=PANEL,
            highlightcolor=GOLD,
            padx=0,
            pady=0,
            cursor="hand2",
            takefocus=True,
        )
        btn.bind("<Enter>", lambda _e: btn.config(image=hover_image))
        btn.bind("<Leave>", lambda _e: btn.config(image=base_image))
        return btn

    # ------------------------------------------------------------ 页面：声明 / 开始

    def _page_notice(self, page):
        layout = notice_page_layout()
        title_x, title_y, title_width, title_height = layout["title"]
        tk.Label(
            page,
            text=_tr(STARTUP_NOTICE_TITLE),
            font=("PingFang SC", 22, "bold"),
            bg=PANEL,
            fg=FG,
            anchor="w",
        ).place(x=title_x, y=title_y, width=title_width, height=title_height)

        subtitle_x, subtitle_y, subtitle_width, subtitle_height = layout["subtitle"]
        tk.Label(
            page,
            text=_tr("请在继续前阅读以下重要信息"),
            font=FONT_LABEL,
            bg=PANEL,
            fg=GOLD,
            anchor="w",
        ).place(
            x=subtitle_x,
            y=subtitle_y,
            width=subtitle_width,
            height=subtitle_height,
        )

        actions = tk.Frame(page, bg=PANEL)
        actions_x, actions_y, actions_width, actions_height = layout["actions"]
        actions.place(
            x=actions_x,
            y=actions_y,
            width=actions_width,
            height=actions_height,
        )
        self._native_button(actions, "官方网站", self._open_brand_website).pack(side="left")
        self._native_button(actions, "同意并继续", self._accept_startup_notice,
                            accent=True).pack(side="right")

        notice_body = tk.Frame(page, bg=CARD, highlightbackground=CARD_EDGE,
                               highlightthickness=1)
        body_x, body_y, body_width, body_height = layout["body"]
        notice_body.place(x=body_x, y=body_y, width=body_width, height=body_height)
        scrollbar = tk.Scrollbar(notice_body, orient="vertical")
        copy = tk.Text(notice_body, wrap="word", font=FONT_LABEL, bg=CARD, fg=FG,
                       bd=0, highlightthickness=0, padx=18, pady=16,
                       yscrollcommand=scrollbar.set, cursor="arrow")
        scrollbar.config(command=copy.yview)
        scrollbar.pack(side="right", fill="y")
        copy.pack(side="left", fill="both", expand=True)
        localized_notice = _tr(STARTUP_NOTICE_COPY)
        copy.insert("1.0", localized_notice)
        for start, end in notice_brand_link_spans(localized_notice):
            copy.tag_add("brand_website_link", f"1.0+{start}c", f"1.0+{end}c")
        copy.tag_configure("brand_website_link", foreground=GOLD, underline=True)
        copy.tag_bind("brand_website_link", "<Enter>",
                      lambda _e: copy.config(cursor="hand2"))
        copy.tag_bind("brand_website_link", "<Leave>",
                      lambda _e: copy.config(cursor="arrow"))
        copy.tag_bind("brand_website_link", "<Button-1>", self._open_brand_website)
        copy.config(state="disabled")

    def _accept_startup_notice(self):
        self.startup_notice_acknowledged = True
        self.show_page(self.post_notice_page)

    def _page_home(self, page):
        tk.Label(page, text=_tr("开始游戏"), font=("PingFang SC", 22, "bold"),
                 bg=PANEL, fg=FG).pack(anchor="w", padx=24, pady=(28, 2))
        tk.Label(page, text=_tr("选择模式后点击下方按钮启动，或从左侧进入详细设置"),
                 font=FONT_LABEL, bg=PANEL, fg=DIM).pack(anchor="w", padx=24)

        btns = tk.Frame(page, bg=PANEL)
        btns.pack(fill="x", padx=24, pady=20)
        entries = [
            ("单人遭遇战", self._goto_skirmish),
            ("多人联机（局域网）", self._goto_net),
            ("载入存档", self._goto_saves),
            ("进入主菜单", lambda: self.launch("menu")),
        ]
        for text, cmd in entries:
            self._button(btns, text, cmd, accent=True, big=True).pack(fill="x", pady=6, ipady=5)

        tk.Label(page, text=_tr("提示：「更多功能」页可直达回放、存档、地图等管理面板"),
                 font=FONT_SMALL, bg=PANEL, fg=DIM).pack(anchor="w", padx=24, pady=(2, 0))
        self.home_status = tk.Label(page, text="", font=FONT_SMALL, bg=PANEL, fg=GOLD)
        self.home_status.pack(anchor="w", padx=24, pady=(10, 0))

    def _goto_saves(self):
        self._saves_refresh()
        self.show_page("saves")

    def _goto_skirmish(self):
        self.show_page("skirmish")

    def _engine_version(self):
        try:
            with open(os.path.join(ENGINE, "VERSION"), encoding="utf-8") as f:
                return f.read().strip()
        except OSError:
            return "release-20250330"

    # ------------------------------------------------------------ 页面：显示

    def _page_display(self, page):
        prof = self._card(page, "个人资料")
        row = self._row(prof, "玩家名称")
        tk.Entry(row, textvariable=self.vars["player_name"], font=FONT_LABEL,
                 bg=BTN, fg=FG, insertbackground=FG, relief="flat", width=20
                 ).pack(side="left", padx=4, ipady=3)
        row = self._row(prof, "偏好颜色")
        swatches = tk.Frame(row, bg=CARD)
        swatches.pack(side="left")
        self.color_marks = {}
        for name, hexv in PLAYER_COLORS:
            sw = tk.Label(swatches, bg="#" + hexv, width=3, height=1, cursor="hand2",
                          highlightthickness=2, highlightbackground="#" + hexv)
            sw.pack(side="left", padx=3)
            sw.bind("<Button-1>", lambda _e, h=hexv: self._pick_color(h))
            self.color_marks[hexv] = sw
        self._pick_color(self.vars["player_color"].get(), init=True)
        tk.Frame(prof, bg=CARD, height=6).pack()

        video = self._card(page, "视频")
        row = self._row(video, "视频模式")
        self._combo(row, self.vars["video_mode_label"], VIDEO_MODES)
        row = self._row(video, "窗口尺寸")
        tk.Entry(row, textvariable=self.vars["window_size"], font=FONT_LABEL,
                 bg=BTN, fg=FG, insertbackground=FG, relief="flat", width=12
                 ).pack(side="left", padx=4, ipady=3)
        preset_var = tk.StringVar(value=_tr("常用尺寸"))
        om = tk.OptionMenu(row, preset_var, *WINDOW_SIZES,
                           command=lambda s: self.vars["window_size"].set(s))
        om.config(font=FONT_LABEL, bg=BTN, fg=DIM, activebackground=BTN_HOV,
                  activeforeground=FG, highlightthickness=0, bd=0, anchor="w")
        om["menu"].config(bg=BTN, fg=FG, activebackground=ACCENT, activeforeground="white",
                          font=FONT_LABEL)
        om.pack(side="left", padx=4)
        tk.Label(row, text=_tr("格式 宽,高"), font=FONT_SMALL, bg=CARD, fg=DIM).pack(side="left")
        row = self._row(video, "UI 缩放")
        self._combo(row, self.vars["ui_scale_label"], UI_SCALES)
        row = self._row(video, "光标大小")
        self._combo(row, self.vars["cursor_label"], CURSOR_SIZES)
        row = self._row(video, "OpenGL 渲染")
        self._combo(row, self.vars["gl_label"], GL_PROFILES)
        row = self._row(video, "全屏显示器")
        self._combo(row, self.vars["video_display_label"], VIDEO_DISPLAYS)
        self._check(video, "垂直同步", "vsync")
        self._check(video, "限制帧率为游戏逻辑速度", "cap_game_fps",
                    "降低画面流畅度，但可减少 CPU 与电量消耗")
        self._check(video, "禁用系统硬件光标", "disable_hw_cursors",
                    "光标有残影或闪烁时可尝试开启")

        disp = self._card(page, "显示")
        row = self._row(disp, "战场镜头")
        self._combo(row, self.vars["viewport_label"], VIEWPORTS)
        row = self._row(disp, "目标指示线")
        self._combo(row, self.vars["target_lines_label"], TARGET_LINES)
        row = self._row(disp, "状态栏")
        self._combo(row, self.vars["status_bars_label"], STATUS_BARS)
        self._check(disp, "按玩家关系显示颜色（敌红/友黄）", "stance_colors")
        self._check(disp, "界面反馈通知", "notify_feedback", "显示指令反馈与快捷聊天提示")
        self._check(disp, "游戏事件通知", "notify_transients", "显示探测到敌军、建筑丢失等提示")
        self._check(disp, "暂停菜单背景动画", "pause_shellmap")
        self._check(disp, "回放中隐藏聊天", "hide_replay_chat")
        tk.Frame(disp, bg=CARD, height=8).pack()

    def _pick_color(self, hexv, init=False):
        self.vars["player_color"].set(hexv)
        for h, sw in self.color_marks.items():
            sw.config(highlightbackground="#ffffff" if h == hexv else "#" + h)

    # ------------------------------------------------------------ 页面：音频

    def _page_audio(self, page):
        vol = self._card(page, "音量")
        self._check(vol, "静音", "mute")
        self._slider(vol, "音效音量", "sound_vol", 0, 100, "%")
        self._slider(vol, "音乐音量", "music_vol", 0, 100, "%")
        self._slider(vol, "视频音量", "video_vol", 0, 100, "%")
        tk.Frame(vol, bg=CARD, height=6).pack()

        misc = self._card(page, "其他")
        self._check(misc, "资金音效", "cash_ticks", "资金增减时播放点钞声")
        self._check(misc, "随机播放音乐", "shuffle")
        self._check(misc, "循环播放音乐", "repeat")
        tk.Frame(misc, bg=CARD, height=8).pack()

    # ------------------------------------------------------------ 页面：输入

    def _page_input(self, page):
        mouse = self._card(page, "鼠标")
        row = self._row(mouse, "操作方案")
        self._combo(row, self.vars["control_scheme_label"], CONTROL_SCHEMES)
        row = self._row(mouse, "鼠标滚动方向")
        self._combo(row, self.vars["mouse_scroll_label"], MOUSE_SCROLL)
        self._check(mouse, "备用鼠标平移（中键/右键拖拽）", "alternate_scroll")
        self._check(mouse, "屏幕边缘滚动", "edge_scroll")
        self._check(mouse, "锁定鼠标于游戏窗口内", "lock_mouse")
        tk.Frame(mouse, bg=CARD, height=6).pack()

        speed = self._card(page, "速度")
        self._slider(speed, "地图平移速度", "scroll_speed", 10, 60)
        self._slider(speed, "UI 滚动速度", "ui_scroll_speed", 10, 100)
        self._slider(speed, "缩放速度", "zoom_speed", 1, 10)
        row = self._row(speed, "缩放修饰键")
        self._combo(row, self.vars["zoom_modifier_label"], ZOOM_MODIFIERS)
        tk.Frame(speed, bg=CARD, height=8).pack()

    # ------------------------------------------------------------ 页面：热键

    def _page_hotkeys(self, page):
        head = tk.Frame(page, bg=PANEL)
        head.pack(fill="x", padx=18, pady=(14, 0))
        tk.Label(head, text=_tr("热键设置"), font=("PingFang SC", 18, "bold"),
                 bg=PANEL, fg=FG).pack(side="left")
        self._button(head, "全部重置", self._hk_reset_all).pack(side="right")
        tk.Label(page, text=_tr("点击「重绑定」后按下新的按键组合；Esc 取消，退格清除绑定。修改立即写入游戏配置。"),
                 font=FONT_SMALL, bg=PANEL, fg=DIM).pack(anchor="w", padx=18, pady=(2, 0))

        self.hk_defs = [d for d in parse_hotkey_definitions() if not d["readonly"]]
        self.hk_labels = parse_hotkey_labels()
        self.hk_user = load_user_keys()
        self.hk_defaults = {}
        self.hk_current = {}
        for d in self.hk_defs:
            key, mods = normalize_hotkey(d["default"]) if d["default"] else ("UNKNOWN", [])
            self.hk_defaults[d["name"]] = hotkey_value(key, mods)
            self.hk_current[d["name"]] = self.hk_user.get(d["name"], self.hk_defaults[d["name"]])

        self.hk_binding_labels = {}
        groups = {}
        for d in self.hk_defs:
            group = next((t for t in TYPE_ORDER if t in d["types"]),
                         d["types"][0] if d["types"] else "General")
            groups.setdefault(group, []).append(d)

        for group in [g for g in TYPE_ORDER if g in groups] + \
                     [g for g in groups if g not in TYPE_ORDER]:
            card = self._card(page, TYPE_NAMES.get(group, group))
            for d in groups[group]:
                row = tk.Frame(card, bg=CARD)
                row.pack(fill="x", padx=14, pady=1)
                label = self.hk_labels.get(d["desc"], d["name"])
                tk.Label(row, text=label, font=FONT_LABEL, bg=CARD, fg=FG,
                         width=22, anchor="w").pack(side="left")

                binding = tk.Label(row, text=hotkey_friendly(self.hk_current[d["name"]]),
                                   font=FONT_LABEL, bg=CARD, width=16, anchor="w")
                binding.pack(side="left")
                self.hk_binding_labels[d["name"]] = binding
                self._hk_refresh_color(d["name"])

                btn = tk.Label(row, text=_tr("重绑定"), font=FONT_SMALL, bg=BTN, fg=FG,
                               padx=10, pady=3, cursor="hand2")
                btn.pack(side="right", padx=2)
                btn.bind("<Button-1>", lambda _e, n=d["name"]: self._hk_rebind(n))
                reset = tk.Label(row, text=_tr("重置"), font=FONT_SMALL, bg=BTN, fg=DIM,
                                 padx=10, pady=3, cursor="hand2")
                reset.pack(side="right", padx=2)
                reset.bind("<Button-1>", lambda _e, n=d["name"]: self._hk_reset(n))
            tk.Frame(card, bg=CARD, height=6).pack()

    def _hk_refresh_color(self, name):
        customized = self.hk_current[name] != self.hk_defaults[name]
        self.hk_binding_labels[name].config(fg=GOLD if customized else FG)

    def _hk_contexts_overlap(self, a, b):
        return bool(set(a) & set(b))

    def _hk_find_conflicts(self, name, value):
        key_a, mods_a = normalize_hotkey(value)
        if key_a == "UNKNOWN":
            return []
        conflicts = []
        defs = {d["name"]: d for d in self.hk_defs}
        mine = defs[name]
        for other_name, other_val in self.hk_current.items():
            if other_name == name or other_name not in defs:
                continue
            if not self._hk_contexts_overlap(mine["contexts"], defs[other_name]["contexts"]):
                continue
            if normalize_hotkey(other_val) == (key_a, mods_a):
                conflicts.append(self.hk_labels.get(defs[other_name]["desc"], other_name))
        return conflicts

    def _hk_apply(self, name, value):
        conflicts = self._hk_find_conflicts(name, value)
        if conflicts and not messagebox.askyesno(
                _tr("按键冲突"),
                _trf("该按键已绑定：{conflicts}\n\n仍要绑定吗？", conflicts="、".join(conflicts))):
            return
        self.hk_current[name] = value
        self.hk_binding_labels[name].config(text=hotkey_friendly(value))
        self._hk_refresh_color(name)
        self._hk_save()

    def _hk_reset(self, name):
        self.hk_current[name] = self.hk_defaults[name]
        self.hk_binding_labels[name].config(text=hotkey_friendly(self.hk_defaults[name]))
        self._hk_refresh_color(name)
        self._hk_save()

    def _hk_reset_all(self):
        if self.game_running():
            messagebox.showwarning(_tr("游戏运行中"), _tr("请先退出游戏再修改热键。"))
            return
        for name in list(self.hk_current):
            self.hk_current[name] = self.hk_defaults[name]
            self.hk_binding_labels[name].config(text=hotkey_friendly(self.hk_defaults[name]))
            self._hk_refresh_color(name)
        self._hk_save()

    def _hk_save(self):
        overrides = {}
        for name, value in self.hk_current.items():
            if normalize_hotkey(value) != normalize_hotkey(self.hk_defaults[name]):
                overrides[name] = value
        try:
            save_user_keys(overrides)
        except OSError as exc:
            messagebox.showerror(_tr("保存失败"), str(exc))

    def _hk_rebind(self, name):
        if self.game_running():
            messagebox.showwarning(_tr("游戏运行中"), _tr("请先退出游戏再修改热键。"))
            return

        top = tk.Toplevel(self)
        top.configure(bg=PANEL)
        top.resizable(False, False)
        top.title(_tr("重绑定"))
        label_text = self.hk_labels.get(
            next((d["desc"] for d in self.hk_defs if d["name"] == name), ""), name)
        tk.Label(top, text=_trf("为「{label_text}」按下新的按键组合", label_text=label_text),
                 font=FONT_SECTION, bg=PANEL, fg=FG, padx=30, pady=18).pack()
        tk.Label(top, text=_tr("Esc 取消 · 退格清除绑定"),
                 font=FONT_SMALL, bg=PANEL, fg=DIM, pady=12).pack()
        top.transient(self)
        top.grab_set()
        top.focus_set()

        def on_key(event):
            keysym = event.keysym
            if keysym in PURE_MODIFIERS:
                return "break"
            if keysym == "Escape":
                top.destroy()
                return "break"
            if keysym == "BackSpace":
                top.destroy()
                self._hk_apply(name, "UNKNOWN None")
                return "break"

            key = TK_TO_KEYCODE.get(keysym)
            if key is None and len(keysym) == 1 and keysym.isalpha():
                key = keysym.upper()
            if key is None:
                return "break"

            mods = []
            if event.state & 0x1:
                mods.append("Shift")
            if event.state & 0x8:
                mods.append("Alt")
            if event.state & 0x4:
                mods.append("Ctrl")
            if event.state & 0x10:
                mods.append("Meta")

            top.destroy()
            self._hk_apply(name, hotkey_value(key, mods))
            return "break"

        top.bind("<Key>", on_key)
        top.wait_window()

    # ------------------------------------------------------------ 页面：高级

    # ------------------------------------------------------------ AI 配置

    def _page_ai(self, page):
        tk.Label(page, text=_tr("AI 对手配置"), font=("PingFang SC", 18, "bold"),
                 bg=PANEL, fg=FG).pack(anchor="w", padx=18, pady=(16, 2))
        tk.Label(page, text=_tr("调整电脑的经济、生产与进攻参数并保存为配置；开局时在电脑槽位的 AI 人格下拉框中按名字选用"),
                 font=FONT_SMALL, bg=PANEL, fg=DIM).pack(anchor="w", padx=18)

        self.ai_profiles = aip.load_profiles()
        self.ai_current = 0
        self.ai_vars = {}
        self.ai_scales = {}

        body = tk.Frame(page, bg=PANEL)
        body.pack(fill="both", expand=True, padx=18, pady=8)

        left = tk.Frame(body, bg=CARD, highlightbackground=CARD_EDGE,
                        highlightthickness=1, width=200)
        left.pack(side="left", fill="y", padx=(0, 10))
        left.pack_propagate(False)
        tk.Label(left, text=_tr("已保存配置（★为预设）"), font=FONT_SECTION, bg=CARD,
                 fg=GOLD, anchor="w").pack(fill="x", padx=12, pady=(10, 4))
        self.ai_listbox = tk.Listbox(left, font=FONT_LABEL, bg=BTN, fg=FG,
                                     selectbackground=ACCENT, selectforeground="white",
                                     highlightthickness=0, bd=0, activestyle="none",
                                     exportselection=False, height=14)
        self.ai_listbox.pack(fill="both", expand=True, padx=12)
        self.ai_listbox.bind("<<ListboxSelect>>", self._ai_on_select)
        btnbar = tk.Frame(left, bg=CARD)
        btnbar.pack(fill="x", padx=12, pady=10)
        for text, cmd in (("新建", self._ai_new), ("复制", self._ai_dup), ("删除", self._ai_del)):
            self._button(btnbar, text, cmd).pack(side="left", expand=True, fill="x", padx=2)

        right = tk.Frame(body, bg=PANEL)
        right.pack(side="left", fill="both", expand=True)

        namecard = self._card(right, "配置名称与风格模板")
        namerow = tk.Frame(namecard, bg=CARD)
        namerow.pack(fill="x", padx=14, pady=(2, 4))
        self.ai_name_var = tk.StringVar()
        ent = tk.Entry(namerow, textvariable=self.ai_name_var, font=FONT_LABEL,
                       bg=BTN, fg=FG, insertbackground=FG, highlightthickness=1,
                       highlightbackground=CARD_EDGE, bd=0, width=14)
        ent.pack(side="left", ipady=4)
        tk.Label(namerow, text=_tr("  一键套用："), font=FONT_SMALL, bg=CARD,
                 fg=DIM).pack(side="left")
        for preset in aip.BUILTIN_PRESETS:
            self._button(namerow, preset["name"],
                         lambda p=preset: self._ai_apply_preset(p)).pack(side="left", padx=3)
        tk.Label(namecard, text=_tr("模板只填入数值，点「保存配置并生效」后才会写入游戏"),
                 font=FONT_SMALL, bg=CARD, fg=DIM, anchor="w").pack(fill="x", padx=14, pady=(0, 8))

        for title, knobs in AI_KNOB_DEFS:
            card = self._card(right, title)
            for key, label, lo, hi, unit, is_float, res in knobs:
                self._ai_slider(card, label, key, lo, hi, unit, is_float, res)
            tk.Frame(card, bg=CARD, height=6).pack()

        bar = tk.Frame(right, bg=PANEL)
        bar.pack(fill="x", pady=10)
        self._button(bar, "保存配置并生效", self._ai_save, accent=True).pack(side="left")
        self._button(bar, "恢复出厂预设", self._ai_reset_builtins).pack(side="left", padx=8)
        self.ai_status = tk.Label(bar, text="", font=FONT_SMALL, bg=PANEL, fg=DIM,
                                  wraplength=360, justify="left")
        self.ai_status.pack(side="left", padx=10)

        self._ai_refresh_list(select=0)

    def _ai_slider(self, card, label, key, lo, hi, unit, is_float, res):
        row = self._row(card, label)
        var = self.ai_vars.setdefault(key, tk.StringVar(value=str(aip.KNOB_DEFAULTS[key])))
        val_lbl = tk.Label(row, font=FONT_LABEL, bg=CARD, fg=GOLD, width=7, anchor="e")
        val_lbl.pack(side="right")

        def render(x):
            text = f"{x:.1f}" if is_float else f"{int(round(x))}"
            val_lbl.config(text=f"{text}{_tr(unit)}")
            var.set(text)

        scale = tk.Scale(row, from_=lo, to=hi, resolution=res, orient="horizontal",
                         showvalue=False, command=lambda v: render(float(v)),
                         bg=CARD, fg=FG, troughcolor=BTN, highlightthickness=0, bd=0,
                         activebackground=ACCENT, length=300)
        scale.pack(side="right", padx=8)
        self.ai_scales[key] = (scale, render)

    def _ai_load(self, idx):
        p = self.ai_profiles[idx]
        self.ai_name_var.set(profile_display_name(p))
        for key in aip.KNOB_DEFAULTS:
            val = float(p.get(key, aip.KNOB_DEFAULTS[key]))
            scale, render = self.ai_scales[key]
            scale.set(val)
            render(val)

    def _ai_refresh_list(self, select=None):
        self.ai_listbox.delete(0, "end")
        for p in self.ai_profiles:
            tag = "★ " if p.get("builtin") else "   "
            self.ai_listbox.insert("end", f"{tag}{profile_display_name(p)}")
        idx = self.ai_current if select is None else select
        idx = max(0, min(idx, len(self.ai_profiles) - 1))
        self.ai_listbox.selection_set(idx)
        self.ai_current = idx
        self._ai_load(idx)

    def _ai_on_select(self, _event):
        sel = self.ai_listbox.curselection()
        if sel:
            self.ai_current = sel[0]
            self._ai_load(sel[0])

    def _ai_new(self):
        name = simpledialog.askstring(_tr("新建 AI 配置"), _tr("配置名称："),
                                      initialvalue=_tr("自定义 AI"), parent=self)
        if not name:
            return
        self.ai_profiles.append(aip.normalize_profile(
            {"id": aip.new_profile_id(), "name": name}))
        self._ai_refresh_list(select=len(self.ai_profiles) - 1)
        self.ai_status.config(text=_tr("已创建空白配置，调整数值后点「保存配置并生效」"))

    def _ai_dup(self):
        src = self.ai_profiles[self.ai_current]
        p = dict(src)
        p.update(
            id=aip.new_profile_id(),
            name=duplicate_profile_name(src),
            builtin=False,
        )
        self.ai_profiles.append(aip.normalize_profile(p))
        self._ai_refresh_list(select=len(self.ai_profiles) - 1)

    def _ai_del(self):
        p = self.ai_profiles[self.ai_current]
        if p.get("builtin"):
            self.ai_status.config(text=_tr("出厂预设不可删除，可修改数值后用「复制」另存"))
            return
        del self.ai_profiles[self.ai_current]
        aip.save_profiles(self.ai_profiles)
        aip.generate_yaml(self.ai_profiles)
        self._ai_refresh_list(select=max(0, self.ai_current - 1))
        self.ai_status.config(text=_trf("已删除「{name}」并重新生成游戏人格", name=p["name"]))

    def _ai_apply_preset(self, preset):
        for key in aip.KNOB_DEFAULTS:
            val = float(preset.get(key, aip.KNOB_DEFAULTS[key]))
            scale, render = self.ai_scales[key]
            scale.set(val)
            render(val)
        self.ai_status.config(text=_trf(
            "已把「{name}」数值填入编辑器，保存后生效",
            name=profile_display_name(preset),
        ))

    def _ai_save(self):
        p = self.ai_profiles[self.ai_current]
        entered_name = self.ai_name_var.get().strip()
        if entered_name and entered_name != profile_display_name(p):
            p["name"] = entered_name
        for key, default in aip.KNOB_DEFAULTS.items():
            try:
                val = float(self.ai_vars[key].get())
            except ValueError:
                val = float(default)
            p[key] = int(val) if isinstance(default, int) else val
        self.ai_profiles[self.ai_current] = aip.normalize_profile(p)
        aip.save_profiles(self.ai_profiles)
        n = aip.generate_yaml(self.ai_profiles)
        self._ai_refresh_list(select=self.ai_current)
        self.ai_status.config(text=_trf(
            "已生效（共 {n} 个人格）：开局在电脑槽位 AI 下拉选择「{name}」",
            n=n,
            name=profile_display_name(p),
        ))

    def _ai_reset_builtins(self):
        presets = {p["id"]: aip.normalize_profile(p) for p in aip.BUILTIN_PRESETS}
        self.ai_profiles = [presets.get(p["id"], p) for p in self.ai_profiles]
        have = {p["id"] for p in self.ai_profiles}
        for pid, preset in presets.items():
            if pid not in have:
                self.ai_profiles.append(preset)
        aip.save_profiles(self.ai_profiles)
        aip.generate_yaml(self.ai_profiles)
        self._ai_refresh_list(select=self.ai_current)
        self.ai_status.config(text=_tr("出厂预设已恢复默认数值并重新生成"))

    def _page_advanced(self, page):
        net = self._card(page, "网络")
        self._check(net, "启用 NAT-PMP / UPnP 端口映射", "nat_discovery",
                    "联机开房时自动在路由器上打开端口")
        tk.Frame(net, bg=CARD, height=6).pack()

        perf = self._card(page, "性能调试")
        self._check(perf, "显示性能图表", "perf_graph")
        self._check(perf, "显示性能文本", "perf_text")
        self._check(perf, "模拟性能日志", "sim_perf_logging", "输出 perf.log（仅排查卡顿需要）")
        tk.Frame(perf, bg=CARD, height=6).pack()

        dev = self._card(page, "开发者选项")
        self._check(dev, "启用游戏内开发者设置", "dev_settings",
                    "在游戏内高级设置页显示隐藏的开发者选项")
        self._check(dev, "AI 机器人调试信息", "bot_debug")
        self._check(dev, "Lua 脚本调试信息", "lua_debug")
        self._check(dev, "回放中启用调试命令", "debug_cmds_replays")
        self._check(dev, "禁用 OpenGL 调试回调", "disable_gl_debug")
        tk.Frame(dev, bg=CARD, height=6).pack()

        more = self._card(page, "游戏内设置面板")
        row = tk.Frame(more, bg=CARD)
        row.pack(fill="x", padx=14, pady=(4, 12))
        tk.Label(row, text=_tr("热键请使用左侧「热键设置」页；此处可打开游戏内完整设置面板"),
                 font=FONT_LABEL, bg=CARD, fg=DIM).pack(side="left", padx=(0, 12))
        self._button(row, "打开完整设置", lambda: self.launch("settings")).pack(side="left")

    # ------------------------------------------------------------ 页面：更多功能

    def _page_tools(self, page):
        tk.Label(page, text=_tr("更多功能"), font=("PingFang SC", 18, "bold"),
                 bg=PANEL, fg=FG).pack(anchor="w", padx=18, pady=(16, 2))
        tk.Label(page, text=_tr("「启动器内功能」无需启动游戏即可使用；「引擎功能」将启动游戏并直达对应面板"),
                 font=FONT_SMALL, bg=PANEL, fg=DIM).pack(anchor="w", padx=18)

        tk.Label(page, text=_tr("启动器内功能"), font=FONT_SECTION, bg=PANEL,
                 fg=GOLD, anchor="w").pack(fill="x", padx=18, pady=(10, 0))
        native_grid = tk.Frame(page, bg=PANEL)
        native_grid.pack(fill="x", padx=13, pady=4)
        for i, (name, desc, target) in enumerate(TOOLS_NATIVE):
            self._tool_cell(native_grid, i, name, desc,
                            lambda t=target: self._goto_tool(t), enabled=True, badge=None)

        tk.Label(page, text=_tr("引擎功能（点击后启动游戏）"), font=FONT_SECTION, bg=PANEL,
                 fg=GOLD, anchor="w").pack(fill="x", padx=18, pady=(8, 0))
        engine_grid = tk.Frame(page, bg=PANEL)
        engine_grid.pack(fill="x", padx=13, pady=4)
        for i, (name, desc, target) in enumerate(TOOLS_ENGINE):
            enabled = target is not None
            cmd = (lambda t=target: self.launch(t)) if enabled else None
            self._tool_cell(engine_grid, i, name, desc, cmd, enabled=enabled,
                            badge="需启动引擎" if enabled else "不可用")

    def _goto_tool(self, key):
        refresh = getattr(self, f"_{key}_refresh", None)
        if refresh:
            refresh()
        self.show_page(key)

    def _tool_cell(self, grid, i, name, desc, command, enabled=True, badge=None):
        cell = tk.Frame(grid, bg=CARD, highlightbackground=CARD_EDGE,
                        highlightthickness=1)
        cell.grid(row=i // 2, column=i % 2, sticky="nsew", padx=5, pady=5)
        grid.rowconfigure(i // 2, weight=1)
        grid.columnconfigure(i % 2, weight=1, uniform="tc")

        head = tk.Frame(cell, bg=CARD)
        head.pack(fill="x", padx=14, pady=(10, 0))
        title = tk.Label(head, text=_tr(name), font=("PingFang SC", 14, "bold"),
                         bg=CARD, fg=FG if enabled else DIM,
                         cursor="hand2" if enabled else "arrow")
        title.pack(side="left")
        if badge:
            tk.Label(head, text=_tr(badge), font=("PingFang SC", 8), bg=BTN, fg=DIM,
                     padx=6, pady=2).pack(side="right")
        tk.Label(cell, text=_tr(desc), font=FONT_SMALL, bg=CARD, fg=DIM,
                 wraplength=300, justify="left").pack(anchor="w", padx=14, pady=(2, 10))
        if enabled and command:
            for w in (cell, title):
                w.bind("<Button-1>", lambda _e: command())
                w.bind("<Enter>", lambda _e, c=cell: c.config(highlightbackground=ACCENT))
                w.bind("<Leave>", lambda _e, c=cell: c.config(highlightbackground=CARD_EDGE))

    # ------------------------------------------------------------ 原生功能页面

    def _sub_header(self, page, title, desc):
        bar = tk.Frame(page, bg=PANEL)
        bar.pack(fill="x", padx=18, pady=(14, 2))
        self._button(bar, "← 返回", lambda: self.show_page("tools")).pack(side="right")
        tk.Label(bar, text=_tr(title), font=("PingFang SC", 18, "bold"),
                 bg=PANEL, fg=FG).pack(side="left")
        tk.Label(page, text=_tr(desc), font=FONT_SMALL, bg=PANEL, fg=DIM,
                 wraplength=560, justify="left").pack(anchor="w", padx=18)

    def _toolbar(self, page, buttons):
        bar = tk.Frame(page, bg=PANEL)
        bar.pack(fill="x", padx=18, pady=6)
        for i, button in enumerate(buttons):
            text, cmd = button[:2]
            enabled = button[2] if len(button) > 2 else True
            self._button(bar, text, cmd, enabled=enabled).pack(
                side="left", padx=(0 if i == 0 else 6, 0))
        return bar

    def _empty_hint(self, parent, text):
        tk.Label(parent, text=_tr(text), font=FONT_LABEL, bg=PANEL, fg=DIM,
                 wraplength=520, justify="left").pack(padx=18, pady=20, anchor="w")

    # ---- 回放管理 ----

    def _page_replays(self, page):
        self._sub_header(page, "回放管理",
                         "观看或清理已保存的比赛回放；「观看」将直接启动游戏进入该回放，无需经过游戏内菜单")
        self._toolbar(page, [("刷新", self._replays_refresh),
                             ("打开回放目录", lambda: ra2_files.open_dir(ra2_files.replays_dir()))])
        self.replays_box = tk.Frame(page, bg=PANEL)
        self.replays_box.pack(fill="both", expand=True)
        self._replays_refresh()

    def _replays_refresh(self):
        for w in self.replays_box.winfo_children():
            w.destroy()
        try:
            reps = ra2_files.list_replays()
        except Exception as exc:
            self._empty_hint(self.replays_box, _trf("读取回放失败：{exc}", exc=exc))
            return
        if not reps:
            self._empty_hint(self.replays_box,
                             "暂无回放。对局结束后在游戏内勾选「保存回放」，即可在此观看。")
            return
        for r in reps:
            card = tk.Frame(self.replays_box, bg=CARD, highlightbackground=CARD_EDGE,
                            highlightthickness=1)
            card.pack(fill="x", padx=10, pady=4)
            head = tk.Frame(card, bg=CARD)
            head.pack(fill="x", padx=12, pady=(8, 0))
            if r["ok"]:
                title = (
                    f'{localize_replay_text(r["map_title"])} · '
                    f'{localize_replay_text(r["duration"])}'
                )
            else:
                title = _trf(
                    "{name}（无法读取：{error}）",
                    name=r["name"],
                    error=localize_ra2_file_error(r["error"]),
                )
            tk.Label(head, text=title, font=("PingFang SC", 13, "bold"),
                     bg=CARD, fg=FG, anchor="w").pack(side="left")
            acts = tk.Frame(head, bg=CARD)
            acts.pack(side="right")
            if r["ok"]:
                self._button(acts, "观看", lambda rr=r: self.launch("replay " + rr["path"]),
                             accent=True).pack(side="left", padx=2)
            self._button(acts, "删除", lambda rr=r: self._delete_record(rr, self._replays_refresh)
                         ).pack(side="left", padx=2)
            players = r.get("players") or []
            pv = "、".join(
                f"{p['name']}({'AI' if p['is_bot'] else _tr('玩家')}·{_tr(p['faction'])})"
                for p in players
            ) or _tr("无玩家信息")
            start = localize_replay_text(r.get("start")) or _tr("时间未知")
            info = f"{start} · {pv} · {ra2_files.fmt_size(r['size'])}"
            tk.Label(card, text=info,
                     font=FONT_SMALL, bg=CARD, fg=DIM, anchor="w").pack(fill="x", padx=12, pady=(2, 8))

    # ---- 存档管理 ----

    def _page_saves(self, page):
        self._sub_header(page, "存档管理",
                         "载入或清理已保存的对局；「载入」将直接启动游戏并继续该存档，无需经过游戏内菜单")
        self._toolbar(page, [("刷新", self._saves_refresh),
                             ("打开存档目录", lambda: ra2_files.open_dir(ra2_files.saves_dir()))])
        self.saves_box = tk.Frame(page, bg=PANEL)
        self.saves_box.pack(fill="both", expand=True)
        self._saves_refresh()

    def _saves_refresh(self):
        for w in self.saves_box.winfo_children():
            w.destroy()
        try:
            saves = ra2_files.list_saves()
        except Exception as exc:
            self._empty_hint(self.saves_box, _trf("读取存档失败：{exc}", exc=exc))
            return
        if not saves:
            self._empty_hint(self.saves_box,
                             "暂无存档。遭遇战对局中按 Esc → 保存游戏，即可在此继续。")
            return
        import time
        for s in saves:
            card = tk.Frame(self.saves_box, bg=CARD, highlightbackground=CARD_EDGE,
                            highlightthickness=1)
            card.pack(fill="x", padx=10, pady=4)
            head = tk.Frame(card, bg=CARD)
            head.pack(fill="x", padx=12, pady=(8, 0))
            tk.Label(head, text=s["name"], font=("PingFang SC", 13, "bold"),
                     bg=CARD, fg=FG, anchor="w").pack(side="left")
            acts = tk.Frame(head, bg=CARD)
            acts.pack(side="right")
            if s["ok"]:
                self._button(acts, "载入",
                             lambda ss=s: self.launch("loadsave " + os.path.basename(ss["path"])),
                             accent=True).pack(side="left", padx=2)
            self._button(acts, "删除", lambda ss=s: self._delete_record(ss, self._saves_refresh)
                         ).pack(side="left", padx=2)
            when = time.strftime("%Y-%m-%d %H:%M", time.localtime(s["mtime"]))
            players = "、".join(s.get("players") or []) or _tr("玩家信息未知")
            error = localize_ra2_file_error(s.get("error"))
            extra = error or f"{players} · {ra2_files.fmt_size(s['size'])}"
            tk.Label(card, text=f"{when} · {extra}", font=FONT_SMALL, bg=CARD, fg=DIM,
                     anchor="w").pack(fill="x", padx=12, pady=(2, 8))

    def _delete_record(self, rec, refresh):
        if not messagebox.askyesno(
                _tr("确认删除"),
                _trf("确定删除「{name}」吗？此操作不可恢复。", name=rec["name"])):
            return
        try:
            ra2_files.delete_file(rec["path"])
        except (OSError, ValueError) as exc:
            messagebox.showerror(
                _tr("删除失败"),
                localize_ra2_file_error(str(exc)),
            )
        refresh()

    # ---- 地图管理 ----

    def _page_maps(self, page):
        self._sub_header(page, "地图管理",
                         "浏览官方地图、导入或删除玩家地图；编辑地图请使用「更多功能 → 地图编辑器」")
        self._toolbar(page, [("导入地图…", self._import_map),
                             ("刷新", self._maps_refresh),
                             ("打开地图目录", lambda: ra2_files.open_dir(ra2_files.user_maps_dir()))])
        self.maps_box = tk.Frame(page, bg=PANEL)
        self.maps_box.pack(fill="both", expand=True)
        self._maps_refresh()

    def _maps_refresh(self):
        for w in self.maps_box.winfo_children():
            w.destroy()
        try:
            official, custom = ra2_files.list_maps()
        except Exception as exc:
            self._empty_hint(self.maps_box, _trf("读取地图失败：{exc}", exc=exc))
            return

        def section(title):
            tk.Label(self.maps_box, text=title, font=FONT_SECTION, bg=PANEL, fg=GOLD,
                     anchor="w").pack(fill="x", padx=10, pady=(8, 2))

        def map_row(m, deletable):
            card = tk.Frame(self.maps_box, bg=CARD, highlightbackground=CARD_EDGE,
                            highlightthickness=1)
            card.pack(fill="x", padx=10, pady=3)
            head = tk.Frame(card, bg=CARD)
            head.pack(fill="x", padx=12, pady=6)
            meta = []
            if m.get("size"):
                meta.append(m["size"])
            if m.get("players"):
                meta.append(_trf("{players} 玩家", players=m["players"]))
            if m.get("author"):
                meta.append(_trf("作者 {author}", author=m["author"]))
            label = m["name"] + ("　" + " · ".join(meta) if meta else "")
            tk.Label(head, text=label, font=FONT_LABEL, bg=CARD,
                     fg=FG if m["ok"] else DIM, anchor="w").pack(side="left")
            if deletable:
                self._button(head, "删除", lambda mm=m: self._delete_record(mm, self._maps_refresh)
                             ).pack(side="right")

        section(_trf("官方地图（{count} 张，只读）", count=len(official)))
        for m in official:
            map_row(m, deletable=False)
        section(_trf("玩家地图（{count} 张）", count=len(custom)))
        if not custom:
            self._empty_hint(self.maps_box, "暂无玩家地图。点「导入地图…」选择 .oramap 文件即可添加。")
        for m in custom:
            map_row(m, deletable=True)

    def _import_map(self):
        src = filedialog.askopenfilename(
            title=_tr("选择地图文件"),
            filetypes=[(_tr("游戏地图"), "*.oramap *.zip"), (_tr("所有文件"), "*")])
        if not src:
            return
        try:
            dst = ra2_files.import_map(src)
            self._invalidate_page("skirmish")
            messagebox.showinfo(
                _tr("导入成功"),
                _trf("已导入到：\n{dst}\n\n重启游戏后即可在地图列表中使用。", dst=dst),
            )
        except OSError as exc:
            messagebox.showerror(_tr("导入失败"), str(exc))
        self._maps_refresh()

    # ---- 内容管理 ----

    def _page_content(self, page):
        self._sub_header(page, "内容管理",
                         "从你合法持有的游戏目录、光盘或 MIX 文件离线导入；文件不会上传")
        self._toolbar(page, [("导入游戏目录…", self._import_retail_folder),
                             ("选择 MIX / 地图…", self._import_retail_files),
                             ("刷新", self._content_refresh),
                             ("打开内容目录", lambda: ra2_files.open_dir(ra2_files.content_dir()))])
        self.content_box = tk.Frame(page, bg=PANEL)
        self.content_box.pack(fill="both", expand=True)
        self._content_refresh()

    def _content_refresh(self):
        for w in self.content_box.winfo_children():
            w.destroy()
        st = ra2_files.content_status()
        card = tk.Frame(self.content_box, bg=CARD, highlightbackground=CARD_EDGE,
                        highlightthickness=1)
        card.pack(fill="x", padx=10, pady=6)
        status_text = _tr("完整，游戏可正常运行") if st["healthy"] else _trf(
            "缺失核心文件：{files}",
            files="、".join(st["missing_core"]) or _tr("全部"),
        )
        tk.Label(card, text=_trf("状态：{status}", status=status_text), font=("PingFang SC", 13, "bold"),
                 bg=CARD, fg=("#3CA03C" if st["healthy"] else ACCENT),
                 anchor="w").pack(fill="x", padx=12, pady=(8, 0))
        tk.Label(card, text=_trf(
            "目录：{directory}\n共 {count} 个文件 · {total_size}",
            directory=st["dir"], count=st["count"], total_size=st["total_size"]),
                 font=FONT_SMALL, bg=CARD, fg=DIM, anchor="w",
                 justify="left").pack(fill="x", padx=12, pady=(2, 8))
        capabilities = st.get("capabilities", {})
        capability_text = " · ".join((
            _tr("基础游戏") + (" ✓" if capabilities.get("base_game") else " —"),
            _tr("音乐") + (" ✓" if capabilities.get("music") else " —"),
            _tr("尤里的复仇") + (" ✓" if capabilities.get("yuris_revenge") else " —"),
            _tr("地图") + (" ✓" if capabilities.get("maps") else " —"),
        ))
        tk.Label(card, text=capability_text, font=FONT_SMALL, bg=CARD, fg=GOLD,
                 anchor="w").pack(fill="x", padx=12, pady=(0, 8))
        for f in st["files"]:
            row = tk.Frame(self.content_box, bg=CARD, highlightbackground=CARD_EDGE,
                           highlightthickness=1)
            row.pack(fill="x", padx=10, pady=3)
            tk.Label(row, text=f["name"], font=FONT_LABEL, bg=CARD, fg=FG,
                     anchor="w").pack(side="left", padx=12, pady=6)
            tk.Label(row, text=f["size"], font=FONT_LABEL, bg=CARD, fg=GOLD,
                     anchor="e").pack(side="right", padx=12)

    def _import_retail_folder(self):
        source = filedialog.askdirectory(title=_tr("选择游戏目录或已挂载光盘"))
        if source:
            self._import_retail_sources([source])

    def _import_retail_files(self):
        sources = filedialog.askopenfilenames(
            title=_tr("选择零售内容文件"),
            filetypes=[
                (_tr("零售内容与地图"), "*.mix *.bag *.idx *.map *.mpr *.yrm *.oramap"),
                (_tr("所有文件"), "*"),
            ],
        )
        if sources:
            self._import_retail_sources(sources)

    def _import_retail_sources(self, sources):
        try:
            status = ra2_files.import_retail_content(sources)
        except (OSError, ra2_files.RetailImportError) as exc:
            messagebox.showerror(_tr("导入失败"), str(exc), parent=self)
            return
        self._content_refresh()
        self._invalidate_page("skirmish")
        if status["healthy"]:
            messagebox.showinfo(
                _tr("导入成功"),
                _tr("基础内容已就绪，现在可以开始游戏。"),
                parent=self,
            )
        else:
            messagebox.showwarning(
                _tr("仍缺少核心文件"),
                _trf("还需要：{files}", files="、".join(status["missing_core"])),
                parent=self,
            )

    # ---- 制作名单 ----

    def _page_credits(self, page):
        self._sub_header(page, "制作名单", "游戏引擎与 Red Alert 2 模组的贡献者")
        frame = tk.Frame(page, bg=PANEL)
        frame.pack(fill="both", expand=True, padx=18, pady=8)
        text = tk.Text(frame, font=FONT_LABEL, bg=CARD, fg=FG, wrap="word", bd=0,
                       highlightthickness=1, highlightbackground=CARD_EDGE,
                       insertbackground=FG)
        sb = tk.Scrollbar(frame, command=text.yview)
        text.configure(yscrollcommand=sb.set)
        sb.pack(side="right", fill="y")
        text.pack(side="left", fill="both", expand=True)
        text.insert("1.0", localize_credits_text(ra2_files.credits_text()))
        text.configure(state="disabled")

    # ------------------------------------------------------------ 页面：遭遇战开局

    def _page_skirmish(self, page):
        self._sub_header(page, "遭遇战开局",
                         "选好地图、规则与电脑对手后直接开局，跳过游戏内大厅；AI 人格来自「AI 配置」页")

        self.sk_maps = [m for m in ra2_files.list_maps()[0] if m["ok"]] + \
                       [m for m in ra2_files.list_maps()[1] if m["ok"]]
        labels = [self._sk_map_label(m) for m in self.sk_maps]
        menu_values, self.skirmish_ready = skirmish_map_menu_state(labels)
        self._toolbar(
            page,
            [("开始游戏", self._skirmish_start, self.skirmish_ready)],
        )
        saved = self._sk_load()

        self.sk_map_var = tk.StringVar(value="")
        self.sk_cash_var = tk.StringVar(value=option_label(
            SKIRMISH_CASH, saved.get("cash"), "5000"))
        self.sk_speed_var = tk.StringVar(value=option_label(
            SKIRMISH_SPEEDS, saved.get("speed"), "default"))
        self.sk_tech_var = tk.StringVar(value=option_label(
            SKIRMISH_TECH, saved.get("tech"), "unrestricted"))
        self.sk_faction_var = tk.StringVar(value=option_label(
            SKIRMISH_FACTIONS, saved.get("faction"), "Random"))
        self.sk_short_var = tk.StringVar(value=saved.get("short", "False"))
        self.sk_count_var = tk.StringVar(value=str(saved.get("ai_count", 3)))
        self.sk_bot_vars = []

        rules = self._card(page, "战场与规则")
        row = self._row(rules, "选择地图")
        saved_map = saved.get("map")
        selected_map = next(
            (
                self._sk_map_label(item)
                for item in self.sk_maps
                if self._saved_map_matches(item, saved_map)
            ),
            menu_values[0],
        )
        self.sk_map_var.set(selected_map)
        om = tk.OptionMenu(row, self.sk_map_var, menu_values[0], *menu_values[1:],
                           command=lambda _v: self._sk_clamp())
        om.config(font=FONT_LABEL, bg=BTN, fg=FG, activebackground=BTN_HOV,
                  activeforeground=FG, highlightthickness=0, bd=0, width=30, anchor="w")
        om["menu"].config(bg=BTN, fg=FG, activebackground=ACCENT,
                          activeforeground="white", font=FONT_LABEL)
        if not self.skirmish_ready:
            om.config(state="disabled", disabledforeground=DIM)
        om.pack(side="left", padx=4)
        if not self.skirmish_ready:
            tk.Label(
                rules,
                text=_tr("当前没有可用地图。请先在“地图管理”或“内容管理”中导入合法地图。"),
                font=FONT_SMALL,
                bg=CARD,
                fg=DIM,
                wraplength=500,
                justify="left",
                anchor="w",
            ).pack(fill="x", padx=14, pady=(2, 6))
        row = self._row(rules, "初始资金")
        self._combo(row, self.sk_cash_var, SKIRMISH_CASH)
        row = self._row(rules, "游戏速度")
        self._combo(row, self.sk_speed_var, SKIRMISH_SPEEDS)
        row = self._row(rules, "科技等级")
        self._combo(row, self.sk_tech_var, SKIRMISH_TECH)
        short_cb = tk.Checkbutton(rules, text=_tr("短局模式（消灭全部敌人即获胜）"),
                                  variable=self.sk_short_var, onvalue="True", offvalue="False",
                                  font=FONT_LABEL, bg=CARD, fg=FG, selectcolor=BTN,
                                  activebackground=CARD, activeforeground=FG, anchor="w")
        short_cb.pack(fill="x", padx=14, pady=2)
        tk.Frame(rules, bg=CARD, height=6).pack()

        own = self._card(page, "你的阵营")
        row = self._row(own, "阵营")
        self._combo(row, self.sk_faction_var, SKIRMISH_FACTIONS)
        tk.Frame(own, bg=CARD, height=6).pack()

        bots = self._card(page, "电脑对手")
        row = self._row(bots, "AI 数量")
        cnt_val = tk.Label(row, textvariable=self.sk_count_var, font=FONT_LABEL,
                           bg=CARD, fg=GOLD, width=4, anchor="e")
        cnt_val.pack(side="right")

        def on_count(v):
            cnt_val.config(text=str(int(float(v))))
            self.sk_count_var.set(str(int(float(v))))
            self._sk_rebuild_bots()

        scale = tk.Scale(row, from_=1, to=7, orient="horizontal", showvalue=False,
                         variable=self.sk_count_var,
                         command=on_count, bg=CARD, fg=FG, troughcolor=BTN,
                         highlightthickness=0, bd=0, activebackground=ACCENT, length=300)
        scale.set(int(self.sk_count_var.get()))
        scale.pack(side="right", padx=8)
        self.sk_bots_frame = tk.Frame(bots, bg=CARD)
        self.sk_bots_frame.pack(fill="x", padx=6, pady=(0, 8))
        self._sk_rebuild_bots(saved_bots=saved.get("bots"))

        bar = tk.Frame(page, bg=PANEL)
        bar.pack(fill="x", pady=12)
        self._button(
            bar,
            "开 始 游 戏",
            self._skirmish_start,
            accent=True,
            big=True,
            enabled=self.skirmish_ready,
        ).pack()

    def _sk_profiles_options(self):
        try:
            import ai_profiles as aip
            profiles = aip.load_profiles()
            opts = [(profile_display_name(p), p["id"]) for p in profiles]
        except Exception:
            opts = []
        return [(_tr("标准 AI"), "test")] + opts

    def _sk_map_label(self, item):
        return _trf(
            "{name}（{players} 人）",
            name=item["name"],
            players=item["players"],
        )

    def _saved_map_matches(self, item, saved_map):
        if saved_map in {item.get("pkg"), item.get("name"), self._sk_map_label(item)}:
            return True
        saved_map = str(saved_map or "")
        name = str(item.get("name") or "")
        return saved_map.startswith(name + "（") or saved_map.startswith(name + " (")

    def _sk_rebuild_bots(self, saved_bots=None):
        for w in self.sk_bots_frame.winfo_children():
            w.destroy()
        count = int(self.sk_count_var.get())
        playable = self._sk_playable_slots()
        count = max(1, min(count, max(1, len(playable) - 1)))
        self.sk_bot_vars = []
        profiles_opts = self._sk_profiles_options()
        for i in range(count):
            sv = saved_bots[i] if saved_bots and i < len(saved_bots) else {}
            row = tk.Frame(self.sk_bots_frame, bg=CARD)
            row.pack(fill="x", padx=8, pady=2)
            tk.Label(row, text=f"AI {i + 1}", font=FONT_LABEL, bg=CARD, fg=FG,
                     width=6, anchor="w").pack(side="left")
            pv = tk.StringVar(value=display_option_label(
                profiles_opts, sv.get("profile"), "test"))
            fv = tk.StringVar(value=option_label(
                SKIRMISH_FACTIONS, sv.get("faction"), "Random"))
            tv = tk.StringVar(value=option_label(
                SKIRMISH_TEAMS, sv.get("team"), "0"))
            self.sk_bot_vars.append((pv, fv, tv))
            self._combo(row, pv, profiles_opts, width=14, already_localized=True)
            self._combo(row, fv, SKIRMISH_FACTIONS, width=10)
            self._combo(row, tv, SKIRMISH_TEAMS, width=8)

    def _sk_playable_slots(self):
        label = self.sk_map_var.get()
        for m in self.sk_maps:
            if self._sk_map_label(m) == label:
                return [s for s in m.get("slots", []) if s.startswith("Multi")]
        return ["Multi0", "Multi1"]

    def _sk_clamp(self):
        playable = self._sk_playable_slots()
        cnt = int(self.sk_count_var.get())
        if cnt > len(playable) - 1:
            self.sk_count_var.set(str(max(1, len(playable) - 1)))
        self._sk_rebuild_bots()

    def _sk_load(self):
        try:
            with open(SKIRMISH_SAVE, encoding="utf-8") as f:
                return json.load(f)
        except (OSError, ValueError):
            return {}

    def _sk_save(self, data):
        try:
            with open(SKIRMISH_SAVE, "w", encoding="utf-8") as f:
                json.dump(data, f, ensure_ascii=False, indent=1)
        except OSError:
            pass

    def _skirmish_start(self):
        label = self.sk_map_var.get()
        target = None
        for m in self.sk_maps:
            if self._sk_map_label(m) == label:
                target = m
                break
        if target is None:
            messagebox.showerror(_tr("无法开局"), _tr("没有可用地图"))
            return

        cmds = [
            f"option startingcash {option_value(localized_options(SKIRMISH_CASH), self.sk_cash_var.get())}",
            f"option gamespeed {option_value(localized_options(SKIRMISH_SPEEDS), self.sk_speed_var.get())}",
            f"option techlevel {option_value(localized_options(SKIRMISH_TECH), self.sk_tech_var.get())}",
        ]
        if self.sk_short_var.get() == "True":
            cmds.append("option shortgame true")

        pf = option_value(localized_options(SKIRMISH_FACTIONS), self.sk_faction_var.get())
        if pf != "Random":
            cmds.append(f"faction 0 {pf}")

        playable = [s for s in target.get("slots", []) if s.startswith("Multi")]
        count = max(1, min(int(self.sk_count_var.get()), len(playable) - 1))
        profiles_opts = self._sk_profiles_options()
        for i in range(count):
            pv, _fv, _tv = self.sk_bot_vars[i]
            bot_type = option_value(profiles_opts, pv.get()) or "test"
            cmds.append(f"slot_bot {playable[i + 1]} 0 {bot_type}")
        for i in range(count):
            _pv, fv, tv = self.sk_bot_vars[i]
            idx = i + 1
            fac = option_value(localized_options(SKIRMISH_FACTIONS), fv.get())
            if fac != "Random":
                cmds.append(f"faction {idx} {fac}")
            team = option_value(localized_options(SKIRMISH_TEAMS), tv.get())
            if team != "0":
                cmds.append(f"team {idx} {team}")

        self._sk_save({
            "map": target.get("pkg") or target["name"],
            "cash": option_value(localized_options(SKIRMISH_CASH), self.sk_cash_var.get()),
            "speed": option_value(localized_options(SKIRMISH_SPEEDS), self.sk_speed_var.get()),
            "tech": option_value(localized_options(SKIRMISH_TECH), self.sk_tech_var.get()),
            "faction": option_value(localized_options(SKIRMISH_FACTIONS), self.sk_faction_var.get()),
            "short": self.sk_short_var.get(), "ai_count": int(self.sk_count_var.get()),
            "bots": [{
                "profile": option_value(profiles_opts, pv.get()),
                "faction": option_value(localized_options(SKIRMISH_FACTIONS), fv.get()),
                "team": option_value(localized_options(SKIRMISH_TEAMS), tv.get()),
            }
                     for pv, fv, tv in self.sk_bot_vars],
        })

        arg = target.get("pkg") or target["name"]
        self.launch(f"skirmish {arg} {'|'.join(cmds)}")

    def _goto_net(self):
        self._net_refresh_status()
        self.show_page("net")

    # ------------------------------------------------------------ 页面：局域网联机

    def _page_net(self, page):
        saved_server = server_state()
        saved_room_name = saved_server.get("name")
        self.net_name_is_default = not bool(saved_room_name)

        tk.Label(page, text=_tr("多人联机"), font=("PingFang SC", 18, "bold"),
                 bg=PANEL, fg=FG).pack(anchor="w", padx=18, pady=(16, 2))
        tk.Label(page, text=_tr("同一局域网（家中 Wi-Fi / 公司内网）即可对战，无需联网"),
                 font=FONT_SMALL, bg=PANEL, fg=DIM).pack(anchor="w", padx=18)

        # ---- 加入房间 ----
        join = self._card(page, "加入房间")
        row = self._row(join, "房间地址")
        self.net_addr = tk.Entry(row, font=FONT_LABEL, bg=BTN, fg=FG, bd=0,
                                 insertbackground=FG, width=26)
        self.net_addr.pack(side="left", padx=4, ipady=4)
        last = saved_server.get("last_addr", "127.0.0.1:1234")
        self.net_addr.insert(0, last)
        self._button(row, "检测连接", self._net_check_addr).pack(side="left", padx=6)
        self.net_check_label = tk.Label(join, text="", font=FONT_SMALL, bg=CARD,
                                        fg=DIM, anchor="w")
        self.net_check_label.pack(fill="x", padx=14)
        row = tk.Frame(join, bg=CARD)
        row.pack(fill="x", padx=14, pady=(4, 10))
        self._button(row, "加 入 游 戏", self._net_join, accent=True).pack(side="left")
        tk.Label(row, text=_tr("输入房主告诉你的 IP（如 192.168.1.8），端口默认 1234"),
                 font=FONT_SMALL, bg=CARD, fg=DIM).pack(side="left", padx=10)

        # ---- 开设房间 ----
        host = self._card(page, "开设房间（本机做主机）")
        row = self._row(host, "房间名")
        self.net_name = tk.Entry(row, font=FONT_LABEL, bg=BTN, fg=FG, bd=0,
                                 insertbackground=FG, width=22)
        self.net_name.pack(side="left", padx=4, ipady=4)
        self.net_name.insert(
            0,
            saved_room_name or default_room_name(self.effective_language),
        )
        row = self._row(host, "端口")
        self.net_port = tk.Entry(row, font=FONT_LABEL, bg=BTN, fg=FG, bd=0,
                                 insertbackground=FG, width=8)
        self.net_port.pack(side="left", padx=4, ipady=4)
        self.net_port.insert(0, str(saved_server.get("port", 1234)))
        tk.Label(row, text=_tr("一般无需修改"), font=FONT_SMALL, bg=CARD, fg=DIM,
                 ).pack(side="left", padx=8)

        self.net_status = tk.Label(host, text="", font=("PingFang SC", 12, "bold"),
                                   bg=CARD, fg=DIM, anchor="w", justify="left")
        self.net_status.pack(fill="x", padx=14, pady=(4, 2))

        ips = local_ips()
        ip_text = _trf(
            "本机 IP：{addresses}",
            addresses="　".join(ips) if ips else _tr("未能获取"),
        )
        tk.Label(host, text=ip_text, font=FONT_LABEL, bg=CARD, fg=GOLD,
                 anchor="w").pack(fill="x", padx=14, pady=2)

        row = tk.Frame(host, bg=CARD)
        row.pack(fill="x", padx=14, pady=(4, 10))
        self.net_start_btn = self._button(row, "启动服务器", self._net_server_start,
                                          accent=True)
        self.net_start_btn.pack(side="left")
        self.net_stop_btn = self._button(row, "停止服务器", self._net_server_stop)
        self.net_stop_btn.pack(side="left", padx=8)
        tk.Label(host, text=_tr("启动后把本机 IP 告诉朋友；你自己也用「加入房间」填 127.0.0.1 进入。"),
                 font=FONT_SMALL, bg=CARD, fg=DIM, anchor="w",
                 wraplength=520, justify="left").pack(fill="x", padx=14, pady=(0, 10))

        self._net_refresh_status()

    def _net_refresh_status(self):
        st = server_running()
        if not hasattr(self, "net_status"):
            return
        if st:
            ok = port_open("127.0.0.1", st.get("port", 1234), timeout=0.5)
            self.net_status.config(
                text=_trf(
                    "● 服务器运行中（PID {pid}）· 端口 {port} {connection}\n房间名：{name}",
                    pid=st["pid"],
                    port=st.get("port"),
                    connection=_tr("可连接") if ok else _tr("暂时不可连接"),
                    name=st.get("name"),
                ),
                fg="#3CA03C")
            self.net_start_btn.config(state="disabled")
            self.net_stop_btn.config(state="normal")
        else:
            self.net_status.config(text=_tr("○ 服务器未启动"), fg=DIM)
            self.net_start_btn.config(state="normal")
            self.net_stop_btn.config(state="disabled")

    def _schedule_net_refresh(self, delay_ms):
        if self._net_refresh_after_id is not None:
            try:
                self.after_cancel(self._net_refresh_after_id)
            except Exception:
                pass

        def refresh_once():
            self._net_refresh_after_id = None
            self._net_refresh_status()

        self._net_refresh_after_id = self.after(delay_ms, refresh_once)

    def _net_server_start(self):
        if server_running():
            self._net_refresh_status()
            return
        dotnet = find_dotnet()
        if not dotnet:
            messagebox.showerror(_tr("启动失败"), _tr("未找到 dotnet，请先安装 .NET 运行时"))
            return
        name = self.net_name.get().strip() or default_room_name(self.effective_language)
        try:
            port = int(self.net_port.get().strip() or "1234")
            if not (1024 <= port <= 65535):
                raise ValueError
        except ValueError:
            messagebox.showerror(_tr("端口无效"), _tr("端口需为 1024-65535 之间的数字"))
            return

        env = dict(os.environ)
        env.setdefault("DOTNET_ROLL_FORWARD", "Major")
        env["MOD_SEARCH_PATHS"] = MAC_MOD_SEARCH_PATHS
        args = [dotnet, "bin/OpenRA.Server.dll", "Engine.EngineDir=..", "Game.Mod=ra2",
                f"Server.Name={name}", f"Server.ListenPort={port}",
				"Server.RequireAuthentication=False",
                "Server.EnableSingleplayer=True", "Server.EnableLintChecks=False",
                support_dir_launch_arg()]
        try:
            with open(SERVER_LOG, "ab") as log:
                proc = subprocess.Popen(args, cwd=ENGINE, env=env,
                                        stdout=log, stderr=log, start_new_session=True)
        except OSError as exc:
            messagebox.showerror(_tr("启动失败"), str(exc))
            return
        with open(SERVER_STATE, "w", encoding="utf-8") as f:
            json.dump({"pid": proc.pid, "name": name, "port": port}, f)
        self._schedule_net_refresh(2500)

    def _net_server_stop(self):
        st = server_running()
        if st:
            try:
                os.kill(st["pid"], signal.SIGTERM)
            except OSError:
                pass
        try:
            os.remove(SERVER_STATE)
        except OSError:
            pass
        self._schedule_net_refresh(800)

    def _net_check_addr(self):
        addr = self.net_addr.get().strip()
        host, _, port = addr.partition(":")
        port = port or "1234"
        if not host:
            self.net_check_label.config(text=_tr("请输入地址"), fg=ACCENT)
            return
        self.net_check_label.config(text=_tr("检测中…"), fg=DIM)
        self.update_idletasks()
        ok = port_open(host, port, timeout=2.0)
        self.net_check_label.config(
            text=_tr("✓ 可以连接到该房间") if ok else _tr("✗ 连不上，请确认 IP/端口与房主服务器已启动"),
            fg=("#3CA03C" if ok else ACCENT))

    def _net_join(self):
        addr = self.net_addr.get().strip()
        if not addr:
            messagebox.showerror(
                _tr("缺少地址"),
                _tr("请输入房间地址，如 192.168.1.8 或 127.0.0.1"),
            )
            return
        st = server_state()
        st["last_addr"] = addr
        try:
            with open(SERVER_STATE, "w", encoding="utf-8") as f:
                json.dump(st, f)
        except OSError:
            pass
        self.launch("connect " + addr)

    # ------------------------------------------------------------ 启动

    def _auto_ui_scale(self):
        try:
            w, h = self.winfo_screenwidth(), self.winfo_screenheight()
        except Exception:
            return "1"

        scale = 1.0
        if h >= 890 or w >= 2300:
            scale = 1.5
        elif h >= 700 or w >= 1550:
            scale = 1.25

        # 最高的自适应面板约 540 逻辑像素,缩放后的逻辑高度需 ≥ 面板 + 60 边距
        for value, label in ((2.0, "2"), (1.5, "1.5"), (1.25, "1.25"), (1.0, "1"), (0.8, "0.8")):
            if scale >= value and h / value >= 600:
                return label

        return "1"

    def build_args(self, launch_into=None):
        v = self.vars

        def get(k):
            return v[k].get().strip()

        filters = []
        if get("notify_feedback") == "True":
            filters.append("Feedback")
        if get("notify_transients") == "True":
            filters.append("Transients")
        filter_str = ",".join(filters) if filters else "None"

        player_name = get("player_name")
        name = (
            migrate_legacy_player_name(player_name)
            if player_name
            else default_player_name(self.effective_language)
        )

        size = get("window_size")
        parts = size.replace("x", ",").replace("X", ",").split(",")
        if len(parts) != 2 or not all(p.strip().isdigit() for p in parts):
            size = "1024,768"
        else:
            size = f"{parts[0].strip()},{parts[1].strip()}"

        ui_scale = option_value(localized_options(UI_SCALES), get('ui_scale_label'))
        if not ui_scale or ui_scale == "auto":
            ui_scale = self._auto_ui_scale()

        args = [
            support_dir_launch_arg(),
            f"Player.Name={name}",
            f"Player.Color={get('player_color')}",
            *language_launch_args(self.language_preference, self.system_language_tag),
            f"Graphics.Mode={option_value(localized_options(VIDEO_MODES), get('video_mode_label'))}",
            f"Graphics.WindowedSize={size}",
            f"Graphics.VideoDisplay={option_value(localized_options(VIDEO_DISPLAYS), get('video_display_label'))}",
            f"Graphics.DisableHardwareCursors={get('disable_hw_cursors')}",
            f"Graphics.DisableGLDebugMessageCallback={get('disable_gl_debug')}",
            f"Debug.DisplayDeveloperSettings={get('dev_settings')}",
            f"Debug.BotDebug={get('bot_debug')}",
            f"Debug.LuaDebug={get('lua_debug')}",
            f"Debug.EnableDebugCommandsInReplays={get('debug_cmds_replays')}",
            f"Debug.EnableSimulationPerfLogging={get('sim_perf_logging')}",
            f"Graphics.UIScale={ui_scale}",
            f"Graphics.VSync={get('vsync')}",
            f"Graphics.CapFramerateToGameFps={get('cap_game_fps')}",
            f"Graphics.CursorDouble={option_value(localized_options(CURSOR_SIZES), get('cursor_label'))}",
            f"Graphics.GLProfile={option_value(localized_options(GL_PROFILES), get('gl_label'))}",
            f"Graphics.ViewportDistance={option_value(localized_options(VIEWPORTS), get('viewport_label'))}",
            f"Game.TargetLines={option_value(localized_options(TARGET_LINES), get('target_lines_label'))}",
            f"Game.StatusBars={option_value(localized_options(STATUS_BARS), get('status_bars_label'))}",
            f"Game.UsePlayerStanceColors={get('stance_colors')}",
            f"Game.TextNotificationPoolFilters={filter_str}",
            f"Game.PauseShellmap={get('pause_shellmap')}",
            f"Game.HideReplayChat={get('hide_replay_chat')}",
            f"Sound.Mute={get('mute')}",
            f"Sound.SoundVolume={int(get('sound_vol')) / 100}",
            f"Sound.MusicVolume={int(get('music_vol')) / 100}",
            f"Sound.VideoVolume={int(get('video_vol')) / 100}",
            f"Sound.CashTicks={get('cash_ticks')}",
            f"Sound.Shuffle={get('shuffle')}",
            f"Sound.Repeat={get('repeat')}",
            f"Game.UseClassicMouseStyle={option_value(localized_options(CONTROL_SCHEMES), get('control_scheme_label'))}",
            f"Game.MouseScroll={option_value(localized_options(MOUSE_SCROLL), get('mouse_scroll_label'))}",
            f"Game.UseAlternateScrollButton={get('alternate_scroll')}",
            f"Game.ViewportEdgeScroll={get('edge_scroll')}",
            f"Game.ViewportEdgeScrollStep={get('scroll_speed')}",
            f"Game.UIScrollSpeed={get('ui_scroll_speed')}",
            f"Game.ZoomSpeed={int(get('zoom_speed')) / 100}",
            f"Game.ZoomModifier={option_value(localized_options(ZOOM_MODIFIERS), get('zoom_modifier_label'))}",
            f"Game.LockMouseWindow={get('lock_mouse')}",
            f"Server.DiscoverNatDevices={get('nat_discovery')}",
            f"Debug.PerfGraph={get('perf_graph')}",
            f"Debug.PerfText={get('perf_text')}",
        ]

        mode = launch_into if launch_into else get("mode")
        if mode and mode != "menu":
            args.append(f"Game.LaunchInto={mode}")
        return with_content_manager_return_policy(args)

    def game_pids(self):
        """Return PIDs of a running RA2 game (apphost or legacy dotnet launch)."""
        patterns = [
            r"NuclearCrisis.*Game\.Mod=ra2",
            r"OpenRA\.dll.*Game\.Mod=ra2",
        ]
        pids = []
        for pat in patterns:
            try:
                out = subprocess.check_output(["pgrep", "-f", pat],
                                              text=True, stderr=subprocess.DEVNULL)
            except (subprocess.CalledProcessError, FileNotFoundError):
                continue
            pids.extend(int(p) for p in out.split() if p.isdigit())
        return sorted(set(pids))

    def game_running(self):
        return bool(self.game_pids())

    def _set_launch_status(self, text, error=False):
        label = getattr(self, "home_status", None)
        if label is not None:
            label.config(text=text, fg=ACCENT if error else GOLD)

    def _activate_game_window(self):
        # Bring an already-running game to the front (common when PseudoFullscreen
        # left the launcher sitting on top of a live session).
        script = (
            'tell application "System Events" to set frontmost of '
            '(first process whose name contains "NuclearCrisis" or name contains "OpenRA" or name is "dotnet") to true'
        )
        subprocess.Popen(["osascript", "-e", script],
                         stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)

    def launch(self, launch_into):
        readiness = ra2_files.content_status()
        if not readiness["healthy"]:
            missing = "、".join(readiness["missing_core"])
            self._set_launch_status(_trf("缺少核心文件：{files}", files=missing), error=True)
            self.show_page("content")
            messagebox.showwarning(
                _tr("需要导入游戏内容"),
                _trf("请先导入你合法持有的游戏文件：{files}", files=missing),
                parent=self,
            )
            return
        try:
            self._launch_impl(launch_into)
        except Exception as exc:
            self._set_launch_status(_trf("启动失败：{exc}", exc=exc), error=True)
            messagebox.showerror(_tr("启动失败"), str(exc), parent=self)


    def _game_command(self, args):
        """Prefer the branded macOS apphost so Dock shows the game icon, not dotnet."""
        embedded = game_command_for_runtime(REPO, args)
        if embedded[0] != "bash":
            return embedded

        hostfxr_lib = None
        hostfxr = "/usr/local/share/dotnet/host/fxr"
        if os.path.isdir(hostfxr):
            for ver in sorted(os.listdir(hostfxr), reverse=True):
                candidate = os.path.join(hostfxr, ver, "libhostfxr.dylib")
                if os.path.isfile(candidate):
                    hostfxr_lib = candidate
                    break
        if os.path.isfile(GAME_HOST) and hostfxr_lib:
            dll = os.path.join(REPO, "engine", "bin", "OpenRA.dll")
            return [GAME_HOST, hostfxr_lib, dll, f"Engine.LaunchPath={GAME_HOST}",
                    "Engine.EngineDir=..", f"Engine.ModSearchPaths={MAC_MOD_SEARCH_PATHS}",
                    "Game.Mod=ra2", *args]
        return embedded

    def _launch_impl(self, launch_into):
        pids = self.game_pids()
        if pids:
            self.lift()
            self.focus_force()
            self._activate_game_window()
            retry = messagebox.askyesno(
                _tr("游戏运行中"),
                _tr("检测到游戏已经在运行。\n\n"
                    "选「是」将强制退出旧进程并重新启动；\n"
                    "选「否」则切到已有游戏窗口。"),
                parent=self)
            if not retry:
                self._set_launch_status(_tr("已切到正在运行的游戏"))
                return
            for pid in pids:
                try:
                    os.kill(pid, signal.SIGTERM)
                except OSError:
                    pass
            # Wait briefly for the old process to release the GPU context.
            for _ in range(20):
                if not self.game_pids():
                    break
                time.sleep(0.1)
                self.update()

        args = self.build_args(launch_into)
        env = dict(os.environ)
        env.setdefault("DOTNET_ROLL_FORWARD", "Major")
        # GUI-launched apps inherit a minimal PATH; ensure the official .NET
        # install and Homebrew are visible to launch-game.sh.
        embedded_dotnet_root = os.path.join(REPO, "dotnet")
        path_parts = [
            embedded_dotnet_root,
            "/usr/local/share/dotnet",
            "/opt/homebrew/bin",
            "/usr/local/bin",
            "/usr/bin",
            "/bin",
            "/usr/sbin",
            "/sbin",
        ]
        existing = env.get("PATH", "")
        env["PATH"] = ":".join(path_parts + ([existing] if existing else []))
        if os.path.isfile(os.path.join(embedded_dotnet_root, "dotnet")):
            env["DOTNET_ROOT"] = embedded_dotnet_root
        else:
            env.setdefault("DOTNET_ROOT", "/usr/local/share/dotnet")
        cmd = self._game_command(args)
        has_embedded_host = cmd[0] != "bash"
        if not has_embedded_host and not any(
                os.path.exists(os.path.join(p, "dotnet")) for p in path_parts):
            messagebox.showerror(
                _tr("缺少 .NET"),
                _tr("未找到 dotnet。请安装 .NET 6/8 Runtime 后重试。\n"
                    "常见路径：/usr/local/share/dotnet"),
                parent=self)
            self._set_launch_status(_tr("启动失败：未找到 dotnet"), error=True)
            return
        log_dir = os.path.dirname(LAUNCH_LOG)
        os.makedirs(log_dir, exist_ok=True)
        log_path = LAUNCH_LOG
        self._set_launch_status(_tr("正在启动游戏…"))
        self.update_idletasks()
        try:
            with open(log_path, "w", encoding="utf-8") as logf:
                logf.write("argv: " + " ".join(cmd) + "\n\n")
                logf.write("PATH=" + env["PATH"] + "\n\n")
                logf.flush()
                # cwd=engine so Engine.EngineDir=.. and ./mods resolve like launch-game.sh
                cwd = os.path.join(REPO, "engine") if cmd[0] != "bash" else REPO
                self._game_process = subprocess.Popen(
                    cmd, cwd=cwd, stdout=logf, stderr=subprocess.STDOUT,
                    start_new_session=True, env=env)
        except OSError as exc:
            self._set_launch_status(_trf("启动失败：{exc}", exc=exc), error=True)
            messagebox.showerror(_tr("启动失败"), str(exc), parent=self)
            return

        self._set_launch_status(_tr("游戏已启动"))
        self.withdraw()
        self.after(500, self._watch_game_process)

    def _watch_game_process(self):
        process = getattr(self, "_game_process", None)
        if process is not None and process.poll() is None:
            self.after(500, self._watch_game_process)
            return

        self._game_process = None
        self.deiconify()
        self.lift()
        self.focus_force()
        self.show_page("home")
        self._set_launch_status(_tr("已返回启动器"))


def main():
    if not os.path.exists(GAME_SH):
        messagebox.showerror(
            _tr("缺少文件"),
            _trf("未找到游戏启动脚本：\n{game_sh}", game_sh=GAME_SH),
        )
        sys.exit(1)
    try:
        ra2_files.migrate_legacy_support()
    except (OSError, ValueError) as exc:
        messagebox.showerror(
            _tr("操作失败"),
            f"Unable to migrate NUKE HOUR user data:\n{exc}",
        )
        return 2
    Launcher().mainloop()
    return 0


if __name__ == "__main__":
    main()
