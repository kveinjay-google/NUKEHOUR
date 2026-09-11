#!/usr/bin/env python3
"""Audit the finished personal iOS app for the production touch-control runtime."""

from __future__ import annotations

import argparse
import hashlib
import plistlib
import re
import struct
import sys
from pathlib import Path


FACTIONS = ("allies", "soviets", "yuri")
ASSET_SIZES = {
	"actions": ((512, 512), (1024, 1024)),
	"joystick": ((256, 256), (512, 512)),
	"quickbar": ((512, 128), (1024, 256)),
}
PNG_SIGNATURE = b"\x89PNG\r\n\x1a\n"

CONFIG_PATHS = (
	(Path("mods/ra2/mod.yaml"), Path("mods/ra2/mod.yaml")),
	(Path("mods/ra2/chrome.yaml"), Path("mods/ra2/chrome.yaml")),
	(Path("mods/ra2/metrics.yaml"), Path("mods/ra2/metrics.yaml")),
	(Path("mods/ra2/chrome/ingame.yaml"), Path("mods/ra2/chrome/ingame.yaml")),
	(
		Path("mods/ra2/chrome/ingame-player.yaml"),
		Path("mods/ra2/chrome/ingame-player.yaml"),
	),
	(
		Path("engine/mods/common/chrome/settings-input.yaml"),
		Path("mods/common/chrome/settings-input.yaml"),
	),
	(
		Path("engine/mods/common/chrome/lobby-players.yaml"),
		Path("mods/common/chrome/lobby-players.yaml"),
	),
	(
		Path("engine/mods/common/fluent/chrome.ftl"),
		Path("mods/common/fluent/chrome.ftl"),
	),
	(
		Path("engine/mods/common/fluent/zh-CN/chrome.ftl"),
		Path("mods/common/fluent/zh-CN/chrome.ftl"),
	),
	(
		Path("engine/mods/common/fluent/common.ftl"),
		Path("mods/common/fluent/common.ftl"),
	),
	(
		Path("engine/mods/common/fluent/zh-CN/common.ftl"),
		Path("mods/common/fluent/zh-CN/common.ftl"),
	),
	(Path("mods/ra2/fluent/chrome.ftl"), Path("mods/ra2/fluent/chrome.ftl")),
	(
		Path("mods/ra2/fluent/zh-CN/chrome.ftl"),
		Path("mods/ra2/fluent/zh-CN/chrome.ftl"),
	),
)

COMMAND_SLOTS = (
	"GROUP_01", "GROUP_02", "GROUP_03", "GROUP_04", "GROUP_05",
	"PRODUCTION_X5", "GROUP_06", "GROUP_07", "GROUP_08", "GROUP_09",
	"GROUP_10", "SELECT_ALL", "SELECT_BY_TYPE", "CYCLE_BASE", "TO_SELECTION",
	"TO_LAST_EVENT", "CYCLE_HARVESTERS", "REMOVE_FROM_GROUP", "ATTACK_MOVE",
	"FORCE_MOVE", "FORCE_ATTACK", "GUARD", "DEPLOY", "SCATTER", "STOP",
	"QUEUE_ORDERS", "STANCE_ATTACKANYTHING", "STANCE_DEFEND",
	"STANCE_RETURNFIRE", "STANCE_HOLDFIRE", "SELL", "REPAIR", "BEACON",
)
SEMANTIC_SHORTCUTS = {
	"GROUP_01": ("ios-commandbar-glyphs", "group-1"),
	"GROUP_02": ("ios-commandbar-glyphs", "group-2"),
	"GROUP_03": ("ios-commandbar-glyphs", "group-3"),
	"GROUP_04": ("ios-commandbar-glyphs", "group-4"),
	"GROUP_05": ("ios-commandbar-glyphs", "group-5"),
	"PRODUCTION_X5": ("ios-commandbar-glyphs", "production-x5"),
	"ATTACK_MOVE": ("ios-commandbar-glyphs", "attack-move"),
	"SCATTER": ("ios-commandbar-glyphs", "scatter"),
	"QUEUE_ORDERS": ("ios-commandbar-glyphs", "queue-orders"),
	"STOP": ("ios-commandbar-glyphs", "stop"),
	"CYCLE_BASE": ("ios-commandbar-glyphs", "cycle-base"),
}
SEMANTIC_ATLAS_SHA256 = {
	"commandbar-icons.png": "af6d27aa0418e3451920d7c46806f8a3a02d21df8a54ff4f35d4b846dbf3d3f1",
	"ios-commandbar-icons-v2.png": "3cc75fa7aac7bfe96309ad54db17ee366cb41f7d77ac127739f21e48f3d8ba5c",
	"production-x5-icon.png": "10bd4117540a5c25715f937b4c891a638c43a4170a3ca091a8fa25b99287dea5",
	"ios-commandbar-glyphs-unified.png": "605dee72791016b6dfd0ac2a3764429c6c464607b8231bada0dad37ea1324f06",
	"ios-commandbar-glyphs-allies-v3.png": "743acdda86d44d1aee091a911ab0d7481b99119a3a811815369f62f600a2ecf3",
	"ios-commandbar-glyphs-soviets-v3.png": "35badd84d23969f6e144ab01bbe3bfac2ea8851f829499360ecf2d891ec04c43",
	"ios-commandbar-glyphs-yuri-v3.png": "4833665b926ad76112d8e1ce2b8f02263e28e272396e6d96c1cf6460dbdd11d2",
}
SEMANTIC_COLLECTIONS = {
	"ios-commandbar-glyphs": ("ios-commandbar-glyphs-unified.png", (512, 128)),
	"ios-commandbar-icons-v2": ("ios-commandbar-icons-v2.png", (256, 128)),
	"production-x5-icon": ("production-x5-icon.png", (32, 32)),
	"ios-commandbar-glyphs-allies": ("ios-commandbar-glyphs-allies-v3.png", (2048, 128)),
	"ios-commandbar-glyphs-soviets": ("ios-commandbar-glyphs-soviets-v3.png", (2048, 128)),
	"ios-commandbar-glyphs-yuri": ("ios-commandbar-glyphs-yuri-v3.png", (2048, 128)),
}
ACTION_ATLAS_GLYPHS = {
	"STOP": "ios-stop",
	"DEPLOY": "ios-deploy",
	"SELECT_TYPE": "ios-select-type",
	"FORCE_ATTACK": "ios-force-attack",
	"RETURN_BASE": "ios-return-base",
}
ACTIVE_VIEWPORT_ACTIONS = (
	"DEPLOY",
	"SELECT_TYPE",
	"FORCE_ATTACK",
)
ACTION_LABELS = {
	"STOP": ("button-ios-viewport-action-stop.label", "Stop", "停止"),
	"DEPLOY": ("button-ios-viewport-action-deploy.label", "Deploy", "部署"),
	"SELECT_TYPE": ("button-ios-viewport-action-select-type.label", "Type", "同类"),
	"FORCE_ATTACK": ("button-ios-viewport-action-force-attack.label", "Force", "强攻"),
	"RETURN_BASE": ("button-ios-viewport-action-return-base.label", "Base", "基地"),
}
ACTION_REGION_SUFFIXES = (
	"", "-disabled", "-hover", "-pressed", "-active", "-active-hover",
	"-active-pressed",
)
ACTION_REGIONS = frozenset(
	action + suffix
	for action in ACTION_ATLAS_GLYPHS.values()
	for suffix in ACTION_REGION_SUFFIXES
)
HIGHLIGHTED_ACTION_REGIONS = frozenset(
	action + suffix
	for action in ACTION_ATLAS_GLYPHS.values()
	for suffix in ("", "-hover", "-pressed")
)
JOYSTICK_REGIONS = frozenset(("base", "thumb", "thumb-active"))
QUICKBAR_TOGGLE_REGIONS = frozenset(("collapse", "expand"))
MOD_MANIFEST_WIRING = {
	"Chrome": frozenset(("ra2|chrome.yaml",)),
	"ChromeLayout": frozenset((
		"ra2|chrome/ingame-player.yaml",
		"ra2|chrome/ingame.yaml",
		"common|chrome/settings-input.yaml",
	)),
	"FluentMessages": frozenset(("common|fluent/common.ftl",)),
	"ChromeMetrics": frozenset(("common|metrics.yaml", "ra2|metrics.yaml")),
}
EXACT_MANIFEST_SECTIONS = frozenset(("Chrome", "ChromeMetrics"))
TOUCH_FACTION_METRICS = {
	"FactionSuffix-yuri": "soviets",
	"TouchFactionSuffix-america": "allies",
	"TouchFactionSuffix-korea": "allies",
	"TouchFactionSuffix-france": "allies",
	"TouchFactionSuffix-germany": "allies",
	"TouchFactionSuffix-england": "allies",
	"TouchFactionSuffix-libya": "soviets",
	"TouchFactionSuffix-cuba": "soviets",
	"TouchFactionSuffix-iraq": "soviets",
	"TouchFactionSuffix-russia": "soviets",
	"TouchFactionSuffix-yuri": "yuri",
}

IOS_LOBBY_SOURCE_PATHS = {
	"protocol": Path("engine/OpenRA.Game/Server/ProtocolVersion.cs"),
	"session": Path("engine/OpenRA.Game/Network/Session.cs"),
	"readiness": Path("engine/OpenRA.Game/Network/ClientMapReadiness.cs"),
	"safe_start_request": Path("engine/OpenRA.Game/Network/LobbySafeStartRequest.cs"),
	"safe_start_state": Path("engine/OpenRA.Game/Network/LobbySafeStartRequestState.cs"),
	"order_manager": Path("engine/OpenRA.Game/Network/OrderManager.cs"),
	"unit_orders": Path("engine/OpenRA.Game/Network/UnitOrders.cs"),
	"start_availability": Path("engine/OpenRA.Game/Network/LobbyStartAvailability.cs"),
	"server": Path("engine/OpenRA.Game/Server/Server.cs"),
	"lobby_commands": Path("engine/OpenRA.Mods.Common/ServerTraits/LobbyCommands.cs"),
	"lobby_logic": Path("engine/OpenRA.Mods.Common/Widgets/Logic/Lobby/LobbyLogic.cs"),
	"lobby_layout": Path("engine/OpenRA.Mods.Common/Widgets/Logic/Lobby/IosLobbyLayout.cs"),
	"touch_policy": Path("engine/OpenRA.Mods.Common/Widgets/Logic/IosTouchWidgetPolicy.cs"),
	"lobby_utils": Path("engine/OpenRA.Mods.Common/Widgets/Logic/Lobby/LobbyUtils.cs"),
	"dropdown": Path("engine/OpenRA.Mods.Common/Widgets/DropDownButtonWidget.cs"),
	"start_status": Path(
		"engine/OpenRA.Mods.Common/Widgets/Logic/Lobby/IosLobbyStartStatus.cs"
	),
	"map_status": Path(
		"engine/OpenRA.Mods.Common/Widgets/Logic/Lobby/IosLobbyMapStatusPresentation.cs"
	),
	"smoke": Path("engine/OpenRA.Game/Support/IosMultiplayerSmoke.cs"),
}
CLIENT_MAP_PHASES = (
	"Unknown", "Searching", "WaitingForDownload", "Downloading",
	"InstallingOrVerifying", "Ready", "Unavailable", "Error",
)
IOS_LOBBY_FLUENT = {
	Path("mods/common/fluent/chrome.ftl"): {
		"button-ios-lobby-start-validating": "Validating map…",
		"button-ios-lobby-start-map-unavailable": "Map unavailable",
		"button-ios-lobby-start-client-failed": "{ $player } failed",
		"button-ios-lobby-start-clients-failed": "{ $count } failed",
		"button-ios-lobby-start-client-verifying": "{ $player } verifying…",
		"button-ios-lobby-start-clients-verifying": "{ $count } verifying…",
		"button-ios-lobby-start-client-downloading": "{ $player } { $progress }%",
		"button-ios-lobby-start-clients-downloading": "{ $count } downloading · { $progress }%",
		"button-ios-lobby-start-client-waiting": "Waiting for { $player }",
		"button-ios-lobby-start-clients-waiting": "Waiting for { $count } players",
		"button-ios-lobby-start-client-and-others": "{ $status } · +{ $count }",
		"button-ios-lobby-start-clients-not-ready": "{ $count } maps not ready",
		"button-ios-lobby-start-required-slot": "Fill required slots",
		"button-ios-lobby-start-no-players": "Add a player",
		"button-ios-lobby-start-two-players": "Need 2 players",
		"button-ios-lobby-start-spawns": "Enable more spawns",
		"button-ios-lobby-start-waiting": "Please wait…",
	},
	Path("mods/common/fluent/zh-CN/chrome.ftl"): {
		"button-ios-lobby-start-validating": "地图校验中…",
		"button-ios-lobby-start-map-unavailable": "地图不可用",
		"button-ios-lobby-start-client-failed": "{ $player } 下载失败",
		"button-ios-lobby-start-clients-failed": "{ $count } 人下载失败",
		"button-ios-lobby-start-client-verifying": "{ $player } 校验中…",
		"button-ios-lobby-start-clients-verifying": "{ $count } 人校验中…",
		"button-ios-lobby-start-client-downloading": "{ $player } 下载 { $progress }%",
		"button-ios-lobby-start-clients-downloading": "{ $count } 人下载 · { $progress }%",
		"button-ios-lobby-start-client-waiting": "等待 { $player } 下载",
		"button-ios-lobby-start-clients-waiting": "等待 { $count } 人下载",
		"button-ios-lobby-start-client-and-others": "{ $status } · 另 { $count } 人",
		"button-ios-lobby-start-clients-not-ready": "{ $count } 人地图未就绪",
		"button-ios-lobby-start-required-slot": "请填满必需位置",
		"button-ios-lobby-start-no-players": "请加入玩家",
		"button-ios-lobby-start-two-players": "至少需要 2 人",
		"button-ios-lobby-start-spawns": "出生点不足",
		"button-ios-lobby-start-waiting": "请稍候…",
	},
}

ASSEMBLY_METADATA = {
	"OpenRA.Game.dll": {
		"definitions": {
			"OpenRA.GameSettings": {"fields": {"IosVirtualJoystickSize"}},
			"OpenRA.IosScreenSnapshot": {
				"methods": {"LogicalPoints", "get_IsCompactPhone"},
			},
			"OpenRA.Network.ClientMapReadinessUpdate": {
				"methods": {"TryDeserialize", "ApplyTo"},
			},
			"OpenRA.Network.LobbyStartAvailability": {
				"methods": {"EvaluateMapSafety", "Evaluate", "ToRejection"},
			},
			"OpenRA.Network.LobbySafeStartRequest": {
				"methods": {
					".ctor", "get_ExpectedMapUid", "get_RequestId", "ToCommand", "TryParse",
				},
			},
			"OpenRA.Network.LobbySafeStartRequestState": {
				"methods": {
					".ctor", "get_IsPending", "get_PendingRequestId", "get_Rejection",
					"Begin", "TryAcceptRejection", "TryTimeout", "ObserveReadinessChange",
					"ObserveLobbySync", "Reset",
				},
			},
			"OpenRA.Network.LobbyStartRejection": {
				"methods": {
					".ctor", "get_Reason", "get_ClientIndex", "get_ClientMapPhase",
					"get_Progress", "get_ExpectedMapUid", "get_RequestId",
					"ToAvailability", "Serialize", "Deserialize",
				},
			},
			"OpenRA.Network.OrderManager": {"methods": {
				"add_ClientMapReadinessChanged", "remove_ClientMapReadinessChanged",
				"NotifyClientMapReadinessChanged", "add_SafeStartRejected",
				"remove_SafeStartRejected", "NotifySafeStartRejected",
			}},
			"OpenRA.Network.UnitOrders": {"methods": {"ProcessOrder"}},
			"OpenRA.Server.ProtocolVersion": {"fields": {"Orders"}},
			"OpenRA.VirtualViewportJoystickState": {"methods": {"SetRadius"}},
		},
		"user_strings": set(),
	},
	"OpenRA.Mods.Common.dll": {
		"definitions": {
			"OpenRA.Mods.Common.Widgets.TouchFactionSkin": {"methods": {
				"Resolve", "ResolveForPlayer", "ActionCollection", "JoystickCollection",
				"QuickbarPanelCollection", "QuickbarButtonCollection",
				"QuickbarToggleCollection", "CommandGlyphCollection",
			}},
			"OpenRA.Mods.Common.Widgets.IosViewportControlsLayout": {"methods": {
				"NormalizeJoystickPoints", "ShouldRefreshForJoystickSize",
			}},
			"OpenRA.Mods.Common.Widgets.IosViewportActionLabelLayout": {
				"methods": {"Create", "ApplyTo"},
			},
			"OpenRA.Mods.Common.Widgets.Logic.IosMapPreviewLayout": {
				"methods": {"Create", "Apply", "ApplyActionState"},
			},
			"OpenRA.Mods.Common.Widgets.Logic.IosTouchWidgetPolicy": {
				"methods": {"CenteredDecorationOrigin"},
			},
			"OpenRA.Mods.Common.Widgets.Logic.IosDropDownLayout": {
				"methods": {"get_Default", "get_FactionPicker"},
			},
			"OpenRA.Mods.Common.Widgets.Logic.IosLobbyLayout": {
				"methods": {"Create"},
			},
			"OpenRA.Mods.Common.Widgets.Logic.IosLobbyMapStatusPresentation": {
				"methods": {"For"},
			},
			"OpenRA.Mods.Common.Widgets.Logic.IosLobbyStartStatus": {
				"methods": {"Format"},
			},
			"OpenRA.Mods.Common.Widgets.Logic.LobbyLogic": {
				"methods": {"BeginSafeStart"},
			},
			"OpenRA.Mods.Common.Server.LobbyCommands": {
				"methods": {"StartGameSafe"},
			},
			"OpenRA.Mods.Common.Widgets.Logic.InputSettingsLogic": {"methods": {
				"IosVirtualJoystickSizes", "BindIosVirtualJoystickSizeDropdown",
				"SelectIosVirtualJoystickSize", "ResetIosVirtualJoystickSize",
			}},
			"OpenRA.Mods.Common.Widgets.Logic.Ingame.IosSupportPowerLayoutMode": {},
			"OpenRA.Mods.Common.Widgets.Logic.Ingame.IosSupportPowerObstacle": {},
			"OpenRA.Mods.Common.Widgets.Logic.Ingame.IosSupportPowerObstacles": {},
			"OpenRA.Mods.Common.Widgets.Logic.Ingame.IosSupportPowerLayout": {},
			"OpenRA.Mods.Common.Widgets.Logic.Ingame.IosSupportPowerLayoutPolicy": {},
			"OpenRA.Mods.Common.Widgets.Logic.Ingame.IosSupportPowerLayoutSignature": {},
			"OpenRA.Mods.Common.Widgets.Logic.Ingame.IosSupportPowerLayoutTracker": {},
			"OpenRA.Mods.Common.Widgets.Logic.Ingame.IosSupportPowerCellPolicy": {
				"methods": {"TryGetCells", "ShouldHandleMouseDown", "CenterArt"},
			},
			"OpenRA.Mods.Common.Widgets.Logic.Ingame.IosSupportPowerTooltipPolicy": {
				"methods": {"ClampOrigin", "MaximumSize", "FitText"},
			},
			"OpenRA.Mods.Common.Widgets.Logic.Ingame.IosViewportActionsLogic": {},
			"OpenRA.Mods.Common.Widgets.Logic.SupportPowerBinLogic": {},
			"OpenRA.Mods.Common.Widgets.Logic.SupportPowerTooltipLogic": {},
			"OpenRA.Mods.Common.Widgets.SupportPowersWidget": {},
			"OpenRA.Mods.Common.Widgets.TooltipContainerWidget": {},
			"OpenRA.Mods.Common.Widgets.VirtualViewportJoystickWidget": {
				"methods": {".ctor"},
			},
		},
		"user_strings": {
			"ios-touch-actions-", "ios-touch-joystick-", "ios-touch-quickbar-panel-",
			"ios-touch-quickbar-panel-compact-", "ios-touch-quickbar-button-",
			"ios-touch-quickbar-toggle-", "ios-touch-joystick-allies",
			"ios-commandbar-glyphs-",
			"TOUCH_JOYSTICK_SIZE_DROPDOWN",
		},
		"user_string_bindings": {
			("OpenRA.Mods.Common.Widgets.TouchFactionSkin", "ActionCollection"):
				{"ios-touch-actions-"},
			("OpenRA.Mods.Common.Widgets.TouchFactionSkin", "JoystickCollection"):
				{"ios-touch-joystick-"},
			("OpenRA.Mods.Common.Widgets.TouchFactionSkin", "QuickbarPanelCollection"):
				{"ios-touch-quickbar-panel-", "ios-touch-quickbar-panel-compact-"},
			("OpenRA.Mods.Common.Widgets.TouchFactionSkin", "QuickbarButtonCollection"):
				{"ios-touch-quickbar-button-"},
			("OpenRA.Mods.Common.Widgets.TouchFactionSkin", "QuickbarToggleCollection"):
				{"ios-touch-quickbar-toggle-"},
			("OpenRA.Mods.Common.Widgets.TouchFactionSkin", "CommandGlyphCollection"):
				{"ios-commandbar-glyphs-"},
			("OpenRA.Mods.Common.Widgets.VirtualViewportJoystickWidget", ".ctor"):
				{"ios-touch-joystick-allies"},
			(
				"OpenRA.Mods.Common.Widgets.Logic.InputSettingsLogic",
				"BindIosVirtualJoystickSizeDropdown",
			): {"TOUCH_JOYSTICK_SIZE_DROPDOWN"},
		},
	},
	"OpenRA.Mods.RA2.dll": {
		"definitions": {
			"OpenRA.Mods.RA2.Widgets.CustomCommandBarWidget": {"methods": {
				"PanelBackgroundFor", "ButtonBackgroundFor", "ToggleCollectionFor",
				"ShouldRefreshTouchChrome", "ApplyTouchChrome",
			}},
		},
		"user_strings": set(),
	},
	"OpenRA.iOS.dll": {
		"definitions": {
			"OpenRA.iOS.GeneratedAotObjectRegistryRegistration": {
				"fields": {"RegisteredTypeNames"},
				"methods": {".cctor"},
			},
		},
		"user_strings": {
			"OpenRA.Network.ClientMapReadinessUpdate",
			"OpenRA.Mods.Common.Widgets.Logic.Ingame.IosSupportPowerLayoutTracker",
			"OpenRA.Mods.Common.Widgets.Logic.Ingame.IosViewportActionsLogic",
			"OpenRA.Mods.Common.Widgets.Logic.InputSettingsLogic",
			"OpenRA.Mods.Common.Widgets.Logic.SupportPowerBinLogic",
			"OpenRA.Mods.Common.Widgets.Logic.SupportPowerTooltipLogic",
			"OpenRA.Mods.Common.Widgets.SupportPowersWidget",
			"OpenRA.Mods.Common.Widgets.TooltipContainerWidget",
			"OpenRA.Mods.Common.Widgets.VirtualViewportJoystickWidget",
			"OpenRA.Mods.RA2.Widgets.CustomCommandBarWidget",
		},
		"user_string_bindings": {
			("OpenRA.iOS.GeneratedAotObjectRegistryRegistration", ".cctor"): {
				"OpenRA.Network.ClientMapReadinessUpdate",
				"OpenRA.Mods.Common.Widgets.Logic.Ingame.IosSupportPowerLayoutTracker",
				"OpenRA.Mods.Common.Widgets.Logic.Ingame.IosViewportActionsLogic",
				"OpenRA.Mods.Common.Widgets.Logic.InputSettingsLogic",
				"OpenRA.Mods.Common.Widgets.Logic.SupportPowerBinLogic",
				"OpenRA.Mods.Common.Widgets.Logic.SupportPowerTooltipLogic",
				"OpenRA.Mods.Common.Widgets.SupportPowersWidget",
				"OpenRA.Mods.Common.Widgets.TooltipContainerWidget",
				"OpenRA.Mods.Common.Widgets.VirtualViewportJoystickWidget",
				"OpenRA.Mods.RA2.Widgets.CustomCommandBarWidget",
			},
		},
	},
}

LEGACY_COMPILED_JOYSTICK_LITERALS = {
	"ios-viewport-joystick",
	"ios-joystick-command-icons",
}
CONCEPT_PATH_MARKERS = (
	"design-demos",
	"control-kit",
	"screenshot",
	"faction-touch-controls",
	"concept-board",
)


def _sha256(path: Path) -> str:
	digest = hashlib.sha256()
	with path.open("rb") as stream:
		for block in iter(lambda: stream.read(1024 * 1024), b""):
			digest.update(block)
	return digest.hexdigest()


def _is_power_of_two(value: int) -> bool:
	return value > 0 and value & (value - 1) == 0


def _validate_app_bundle_path(app_bundle: Path) -> str | None:
	if app_bundle.suffix != ".app":
		return f"app bundle path must end in .app: {app_bundle}"
	if app_bundle.is_symlink():
		return f"app bundle cannot be a symlink: {app_bundle}"
	if not app_bundle.is_dir():
		return f"app bundle is missing: {app_bundle}"
	return None


def _bundle_member(app_bundle: Path, relative: Path | str) -> Path:
	candidate = app_bundle / relative
	try:
		root = app_bundle.resolve(strict=False)
		resolved_parent = candidate.parent.resolve(strict=False)
	except (OSError, RuntimeError) as error:
		raise ValueError(
			f"unable to resolve bundle member (possible symlink loop): {candidate}"
		) from error
	try:
		resolved_parent.relative_to(root)
	except ValueError as error:
		raise ValueError(f"bundle member escapes through a symlink: {candidate}") from error
	if candidate.is_symlink():
		raise ValueError(f"bundle member cannot be a symlink: {candidate}")
	return candidate


def _enumerate_bundle_paths(app_bundle: Path, errors: list[str]) -> tuple[Path, ...]:
	"""Walk the app without following symlinks and report traversal failures."""
	paths = []
	directories = [app_bundle]
	while directories:
		directory = directories.pop()
		try:
			children = tuple(directory.iterdir())
		except (OSError, RuntimeError) as error:
			errors.append(f"unable to traverse final app directory {directory}: {error}")
			continue
		for path in children:
			paths.append(path)
			try:
				if path.is_symlink():
					errors.append(
						f"final app traversal cannot follow symlink: "
						f"{path.relative_to(app_bundle).as_posix()}"
					)
					continue
				if path.is_dir():
					directories.append(path)
			except (OSError, RuntimeError) as error:
				errors.append(f"unable to inspect final app path {path}: {error}")
	return tuple(paths)


def _png_ihdr(path: Path) -> tuple[int, int, int, int]:
	with path.open("rb") as stream:
		header = stream.read(29)
	if (
		len(header) != 29 or header[:8] != PNG_SIGNATURE or
		header[8:12] != struct.pack(">I", 13) or header[12:16] != b"IHDR"
	):
		raise ValueError("missing PNG signature or leading IHDR")
	width, height = struct.unpack(">II", header[16:24])
	return width, height, header[24], header[25]


def _touch_asset_manifest() -> dict[str, tuple[int, int]]:
	manifest = {}
	for faction in FACTIONS:
		for kind, sizes in ASSET_SIZES.items():
			manifest[f"ios-touch-{kind}-{faction}-v3.png"] = sizes[0]
			manifest[f"ios-touch-{kind}-{faction}-v3-2x.png"] = sizes[1]
	return manifest


def _expected_collections() -> dict[str, tuple[str, str]]:
	collections = {}
	button_states = (
		"", "-hover", "-pressed", "-highlighted", "-highlighted-hover",
		"-highlighted-pressed", "-disabled", "-highlighted-disabled",
	)
	for faction in FACTIONS:
		actions = (f"ios-touch-actions-{faction}-v3.png", f"ios-touch-actions-{faction}-v3-2x.png")
		quickbar = (f"ios-touch-quickbar-{faction}-v3.png", f"ios-touch-quickbar-{faction}-v3-2x.png")
		collections[f"ios-touch-actions-{faction}"] = actions
		collections[f"ios-touch-actions-{faction}-highlighted"] = actions
		collections[f"ios-touch-joystick-{faction}"] = (
			f"ios-touch-joystick-{faction}-v3.png",
			f"ios-touch-joystick-{faction}-v3-2x.png",
		)
		for suffix in button_states:
			collections[f"ios-touch-quickbar-button-{faction}{suffix}"] = quickbar
		collections[f"ios-touch-quickbar-panel-{faction}"] = quickbar
		collections[f"ios-touch-quickbar-panel-compact-{faction}"] = quickbar
		collections[f"ios-touch-quickbar-toggle-{faction}"] = quickbar
	return collections


def _indentation(line: str) -> int:
	prefix_length = len(line) - len(line.lstrip())
	return len(line[:prefix_length].expandtabs(4))


def _parse_ints(value: str, count: int) -> tuple[int, ...] | None:
	try:
		values = tuple(int(part.strip()) for part in value.split(","))
	except ValueError:
		return None
	return values if len(values) == count else None


def _is_protected_chrome_collection(name: str) -> bool:
	return name.startswith("ios-touch-") or name in SEMANTIC_COLLECTIONS


def _parse_chrome_collections(text: str) -> tuple[dict[str, dict], list[str]]:
	collections: dict[str, dict] = {}
	errors: list[str] = []
	current = None
	direct_indent = None
	regions_indent = None
	region_indent = None
	for line_number, line in enumerate(text.splitlines(), start=1):
		stripped = line.split("#", 1)[0].rstrip().strip()
		if not stripped:
			continue
		indent = _indentation(line)
		if indent == 0 and stripped.endswith(":") and stripped.count(":") == 1:
			current = stripped[:-1].strip()
			if current.startswith("-"):
				removed = current[1:]
				if _is_protected_chrome_collection(removed):
					errors.append(
						f"required Chrome removal is forbidden at line {line_number}: "
						f"{current}"
					)
				current = None
				direct_indent = None
				regions_indent = None
				region_indent = None
				continue
			collections[current] = {
				"Image": None, "Image2x": None, "Inherits": None,
				"regions": {}, "panel": None,
			}
			direct_indent = None
			regions_indent = None
			region_indent = None
			continue
		if current is None or ":" not in stripped:
			continue

		key, value = (part.strip() for part in stripped.split(":", 1))
		if regions_indent is not None and indent > regions_indent:
			if region_indent is None:
				region_indent = indent
			if indent != region_indent:
				continue
			if key.startswith("-") and _is_protected_chrome_collection(current):
				errors.append(
					f"required Chrome region removal is forbidden at line "
					f"{line_number}: {current}.{key}"
				)
				continue
			region = _parse_ints(value, 4)
			if region is None:
				errors.append(f"malformed Chrome region at line {line_number}: {stripped}")
			else:
				collections[current]["regions"][key] = region
			continue
		if regions_indent is not None and indent <= regions_indent:
			regions_indent = None
			region_indent = None
		if direct_indent is None:
			direct_indent = indent
		if indent != direct_indent:
			continue
		if key.startswith("-") and _is_protected_chrome_collection(current):
			errors.append(
				f"required Chrome field removal is forbidden at line {line_number}: "
				f"{current}.{key}"
			)
			continue
		if key.startswith("Inherits@") and _is_protected_chrome_collection(current):
			errors.append(
				f"additional protected Chrome inheritance is forbidden at line "
				f"{line_number}: {current}.{key}"
			)
			continue
		if key == "Regions" and not value:
			regions_indent = indent
			region_indent = None
		elif key in ("Image", "Image2x", "Inherits"):
			collections[current][key] = value
		elif key == "PanelRegion":
			panel = _parse_ints(value, 8)
			if panel is None:
				errors.append(f"malformed Chrome panel region at line {line_number}: {stripped}")
			else:
				collections[current]["panel"] = panel
	return collections, errors


def _resolve_collection(
	name: str,
	collections: dict[str, dict],
	cache: dict[str, dict],
	stack: tuple[str, ...] = (),
) -> dict:
	if name in cache:
		return cache[name]
	if name in stack:
		raise ValueError("Chrome collection inheritance cycle: " + " -> ".join(stack + (name,)))
	if name not in collections:
		raise ValueError(f"missing inherited Chrome collection: {name}")
	direct = collections[name]
	result = {"Image": None, "Image2x": None, "regions": {}, "panel": None}
	parent = direct["Inherits"]
	if parent:
		result.update(_resolve_collection(parent, collections, cache, stack + (name,)))
		result["regions"] = dict(result["regions"])
	for key in ("Image", "Image2x", "panel"):
		if direct[key] is not None:
			result[key] = direct[key]
	result["regions"].update(direct["regions"])
	cache[name] = result
	return result


def _region_fits(region: tuple[int, ...], size: tuple[int, int], scale: int) -> bool:
	x, y, width, height = region
	return (
		x >= 0 and y >= 0 and width > 0 and height > 0 and
		scale * (x + width) <= size[0] and scale * (y + height) <= size[1]
	)


def _panel_fits(panel: tuple[int, ...], size: tuple[int, int], scale: int) -> bool:
	x, y, left, top, center_width, center_height, right, bottom = panel
	values = (x, y, left, top, center_width, center_height, right, bottom)
	return (
		all(value >= 0 for value in values) and center_width > 0 and center_height > 0 and
		scale * (x + left + center_width + right) <= size[0] and
		scale * (y + top + center_height + bottom) <= size[1]
	)


def _find_node_block(text: str, marker: str) -> list[str] | None:
	lines = text.splitlines()
	for index, line in enumerate(lines):
		if line.strip() != marker:
			continue
		indent = _indentation(line)
		end = len(lines)
		for cursor in range(index + 1, len(lines)):
			if lines[cursor].strip() and _indentation(lines[cursor]) <= indent:
				end = cursor
				break
		return lines[index:end]
	return None


def _find_top_level_node_blocks(text: str, marker: str) -> tuple[list[str], ...]:
	lines = text.splitlines()
	blocks = []
	for index, line in enumerate(lines):
		if line.split("#", 1)[0].strip() != marker or _indentation(line) != 0:
			continue
		end = len(lines)
		for cursor in range(index + 1, len(lines)):
			if (
				lines[cursor].split("#", 1)[0].strip() and
				_indentation(lines[cursor]) == 0
			):
				end = cursor
				break
		blocks.append(lines[index:end])
	return tuple(blocks)


def _find_top_level_node_block(text: str, marker: str) -> list[str] | None:
	blocks = _find_top_level_node_blocks(text, marker)
	return blocks[0] if len(blocks) == 1 else None


def _find_direct_child_blocks(
	block: list[str] | None, marker: str
) -> tuple[list[str], ...]:
	if not block:
		return ()
	parent_indent = _indentation(block[0])
	direct_indents = [
		_indentation(line)
		for line in block[1:]
		if line.split("#", 1)[0].strip() and _indentation(line) > parent_indent
	]
	if not direct_indents:
		return ()
	direct_indent = min(direct_indents)
	blocks = []
	for index, line in enumerate(block[1:], start=1):
		if line.split("#", 1)[0].strip() != marker or _indentation(line) != direct_indent:
			continue
		end = len(block)
		for cursor in range(index + 1, len(block)):
			if (
				block[cursor].split("#", 1)[0].strip() and
				_indentation(block[cursor]) <= direct_indent
			):
				end = cursor
				break
		blocks.append(block[index:end])
	return tuple(blocks)


def _find_direct_child_block(
	block: list[str] | None, marker: str
) -> list[str] | None:
	blocks = _find_direct_child_blocks(block, marker)
	return blocks[0] if len(blocks) == 1 else None


def _direct_child_markers(block: list[str] | None) -> tuple[str, ...]:
	if not block:
		return ()
	parent_indent = _indentation(block[0])
	active_lines = [
		line
		for line in block[1:]
		if line.split("#", 1)[0].strip() and _indentation(line) > parent_indent
	]
	if not active_lines:
		return ()
	direct_indent = min(_indentation(line) for line in active_lines)
	return tuple(
		line.split("#", 1)[0].strip()
		for line in active_lines
		if _indentation(line) == direct_indent
	)


def _follow_widget_path(
	text: str, root_marker: str, child_markers: tuple[str, ...]
) -> list[str] | None:
	node = _find_top_level_node_block(text, root_marker)
	for marker in child_markers:
		children = _find_direct_child_block(node, "Children:")
		node = _find_direct_child_block(children, marker)
		if node is None:
			return None
	return node


def _node_property(block: list[str] | None, key: str) -> str | None:
	if not block:
		return None
	parent_indent = _indentation(block[0])
	direct_indents = [
		_indentation(line)
		for line in block[1:]
		if line.split("#", 1)[0].strip() and _indentation(line) > parent_indent
	]
	if not direct_indents:
		return None
	direct_indent = min(direct_indents)
	for line in block[1:]:
		if _indentation(line) != direct_indent:
			continue
		stripped = line.split("#", 1)[0].strip()
		if ":" not in stripped:
			continue
		candidate, value = (part.strip() for part in stripped.split(":", 1))
		if candidate == key:
			return value
	return None


def _compressed_uint(data: bytes, offset: int) -> tuple[int, int]:
	if offset >= len(data):
		raise ValueError("truncated compressed integer")
	first = data[offset]
	if first & 0x80 == 0:
		return first, 1
	if first & 0xC0 == 0x80:
		if offset + 2 > len(data):
			raise ValueError("truncated two-byte compressed integer")
		return ((first & 0x3F) << 8) | data[offset + 1], 2
	if first & 0xE0 == 0xC0:
		if offset + 4 > len(data):
			raise ValueError("truncated four-byte compressed integer")
		return (
			((first & 0x1F) << 24) | (data[offset + 1] << 16) |
			(data[offset + 2] << 8) | data[offset + 3],
			4,
		)
	raise ValueError("invalid compressed integer")


def _rva_to_offset(
	data: bytes,
	sections: list[tuple[int, int, int, int]],
	rva: int,
	size: int = 1,
) -> int:
	if size < 0:
		raise ValueError(f"negative PE mapped size for RVA 0x{rva:x}")
	for virtual_address, virtual_size, raw_offset, raw_size in sections:
		delta = rva - virtual_address
		if 0 <= delta < virtual_size and delta <= raw_size and size <= raw_size - delta:
			offset = raw_offset + delta
			if offset <= len(data) and size <= len(data) - offset:
				return offset
	raise ValueError(f"RVA 0x{rva:x} is outside all PE sections")


def _rva_raw_span(
	data: bytes,
	sections: list[tuple[int, int, int, int]],
	rva: int,
) -> tuple[int, int]:
	for virtual_address, virtual_size, raw_offset, raw_size in sections:
		delta = rva - virtual_address
		if 0 <= delta < virtual_size and delta < raw_size:
			offset = raw_offset + delta
			if offset < len(data):
				return offset, min(raw_size - delta, len(data) - offset)
	raise ValueError(f"RVA 0x{rva:x} is outside all PE sections")


def _heap_string(heap: bytes, index: int) -> str:
	if index == 0:
		return ""
	if index < 0 or index >= len(heap):
		raise ValueError(f"CLR #Strings index is out of bounds: {index}")
	end = heap.find(b"\x00", index)
	if end < 0:
		raise ValueError(f"unterminated CLR #Strings value at index {index}")
	try:
		return heap[index:end].decode("utf-8")
	except UnicodeDecodeError as error:
		raise ValueError(f"invalid UTF-8 in CLR #Strings value at index {index}") from error


def _validate_blob_index(heap: bytes, index: int) -> None:
	if index == 0:
		return
	if index < 0 or index >= len(heap):
		raise ValueError(f"CLR #Blob index is out of bounds: {index}")
	length, prefix_size = _compressed_uint(heap, index)
	start = index + prefix_size
	if start > len(heap) or length > len(heap) - start:
		raise ValueError(f"truncated CLR #Blob value at index {index}")


def _parse_user_string_heap(heap: bytes) -> dict[int, str]:
	if not heap or heap[0] != 0:
		raise ValueError("CLR #US heap must start with its reserved zero byte")
	entries = {}
	cursor = 1
	while cursor < len(heap):
		entry_offset = cursor
		if heap[cursor] == 0:
			cursor += 1
			continue
		length, prefix_size = _compressed_uint(heap, cursor)
		cursor += prefix_size
		if cursor > len(heap) or length > len(heap) - cursor:
			raise ValueError("truncated CLR #US entry")
		payload = heap[cursor:cursor + length]
		cursor += length
		if len(payload) < 1 or len(payload[:-1]) % 2:
			raise ValueError("malformed CLR #US entry")
		try:
			entries[entry_offset] = payload[:-1].decode("utf-16le")
		except UnicodeDecodeError as error:
			raise ValueError("invalid UTF-16 in CLR #US heap") from error
	return entries


def _read_index(data: bytes, offset: int, width: int, label: str) -> tuple[int, int]:
	if width not in (2, 4) or offset > len(data) or width > len(data) - offset:
		raise ValueError(f"truncated CLR metadata {label} index")
	if width == 2:
		return struct.unpack_from("<H", data, offset)[0], offset + 2
	return struct.unpack_from("<I", data, offset)[0], offset + 4


_IL_NO_OPERAND = frozenset((
	*range(0x00, 0x0E), *range(0x14, 0x1F), 0x25, 0x26, 0x2A,
	*range(0x46, 0x6F), 0x76, 0x7A, *range(0x82, 0x8C), 0x8E,
	*range(0x90, 0xA3), *range(0xB3, 0xBB), 0xC3,
	*range(0xD1, 0xDD), 0xDF, 0xE0,
))
_IL_ONE_BYTE_OPERAND = frozenset((
	*range(0x0E, 0x14), 0x1F, *range(0x2B, 0x38), 0xDE,
))
_IL_FOUR_BYTE_OPERAND = frozenset((
	0x20, 0x22, *range(0x27, 0x2A), *range(0x38, 0x45),
	*range(0x6F, 0x76), 0x79, *range(0x7B, 0x82), 0x8C, 0x8D, 0x8F,
	0xA3, 0xA4, 0xA5, 0xC2, 0xC6, 0xD0, 0xDD,
))
_IL_EIGHT_BYTE_OPERAND = frozenset((0x21, 0x23))
_IL_TWO_BYTE_NO_OPERAND = frozenset((
	*range(0x00, 0x06), 0x0F, 0x11, 0x13, 0x14, 0x17, 0x18,
	0x1A, 0x1D, 0x1E,
))
_IL_TWO_BYTE_ONE_BYTE_OPERAND = frozenset((0x12, 0x19))
_IL_TWO_BYTE_TWO_BYTE_OPERAND = frozenset(range(0x09, 0x0F))
_IL_TWO_BYTE_FOUR_BYTE_OPERAND = frozenset((0x06, 0x07, 0x15, 0x16, 0x1C))


def _decode_il_user_strings(
	data: bytes,
	sections: list[tuple[int, int, int, int]],
	rva: int,
	user_strings: dict[int, str],
) -> set[str]:
	body_offset, available = _rva_raw_span(data, sections, rva)
	if available < 1:
		raise ValueError(f"empty IL body at RVA 0x{rva:x}")
	first = data[body_offset]
	if first & 0x03 == 0x02:
		header_size = 1
		code_size = first >> 2
	elif first & 0x03 == 0x03:
		if available < 12:
			raise ValueError(f"truncated fat IL header at RVA 0x{rva:x}")
		flags_and_size = struct.unpack_from("<H", data, body_offset)[0]
		header_size = (flags_and_size >> 12) * 4
		if header_size < 12 or header_size > available:
			raise ValueError(f"invalid fat IL header size at RVA 0x{rva:x}")
		code_size = struct.unpack_from("<I", data, body_offset + 4)[0]
	else:
		raise ValueError(f"invalid IL method header at RVA 0x{rva:x}")
	if code_size > available - header_size:
		raise ValueError(f"IL method body exceeds its raw PE section at RVA 0x{rva:x}")
	code = data[body_offset + header_size:body_offset + header_size + code_size]
	strings = set()
	cursor = 0
	while cursor < len(code):
		opcode = code[cursor]
		cursor += 1
		if opcode == 0xFE:
			if cursor >= len(code):
				raise ValueError(f"truncated two-byte IL opcode at RVA 0x{rva:x}")
			second = code[cursor]
			cursor += 1
			if second in _IL_TWO_BYTE_NO_OPERAND:
				operand_size = 0
			elif second in _IL_TWO_BYTE_ONE_BYTE_OPERAND:
				operand_size = 1
			elif second in _IL_TWO_BYTE_TWO_BYTE_OPERAND:
				operand_size = 2
			elif second in _IL_TWO_BYTE_FOUR_BYTE_OPERAND:
				operand_size = 4
			else:
				raise ValueError(f"invalid two-byte IL opcode FE {second:02x}")
		elif opcode == 0x45:
			if cursor > len(code) or 4 > len(code) - cursor:
				raise ValueError(f"truncated IL switch at RVA 0x{rva:x}")
			count = struct.unpack_from("<I", code, cursor)[0]
			operand_size = 4 + count * 4
		elif opcode in _IL_NO_OPERAND:
			operand_size = 0
		elif opcode in _IL_ONE_BYTE_OPERAND:
			operand_size = 1
		elif opcode in _IL_FOUR_BYTE_OPERAND:
			operand_size = 4
		elif opcode in _IL_EIGHT_BYTE_OPERAND:
			operand_size = 8
		else:
			raise ValueError(f"invalid IL opcode {opcode:02x} at RVA 0x{rva:x}")
		if cursor > len(code) or operand_size > len(code) - cursor:
			raise ValueError(f"truncated IL operand at RVA 0x{rva:x}")
		if opcode == 0x72:
			token = struct.unpack_from("<I", code, cursor)[0]
			if token >> 24 != 0x70 or (token & 0xFFFFFF) not in user_strings:
				raise ValueError(f"invalid ldstr token 0x{token:08x} at RVA 0x{rva:x}")
			strings.add(user_strings[token & 0xFFFFFF])
		cursor += operand_size
	return strings


def _parse_metadata_tables(
	tables: bytes,
	strings_heap: bytes,
	blob_heap: bytes,
	guid_heap: bytes,
	user_strings: dict[int, str],
	data: bytes,
	sections: list[tuple[int, int, int, int]],
) -> dict[str, object]:
	if len(tables) < 24:
		raise ValueError("truncated CLR metadata table stream header")
	heap_sizes = tables[6]
	valid = struct.unpack_from("<Q", tables, 8)[0]
	row_counts = {table: 0 for table in range(64)}
	cursor = 24
	for table in range(64):
		if valid & (1 << table):
			if cursor > len(tables) or 4 > len(tables) - cursor:
				raise ValueError("truncated CLR metadata table row counts")
			row_counts[table] = struct.unpack_from("<I", tables, cursor)[0]
			cursor += 4
	if heap_sizes & 0x40:
		if cursor > len(tables) or 4 > len(tables) - cursor:
			raise ValueError("truncated CLR metadata ExtraData word")
		cursor += 4

	string_width = 4 if heap_sizes & 0x01 else 2
	guid_width = 4 if heap_sizes & 0x02 else 2
	blob_width = 4 if heap_sizes & 0x04 else 2

	def simple_width(table: int) -> int:
		return 4 if row_counts[table] >= 0x10000 else 2

	def coded_width(targets: tuple[int, ...], tag_bits: int) -> int:
		limit = 1 << (16 - tag_bits)
		return 4 if max(row_counts[target] for target in targets) >= limit else 2

	resolution_scope = (0, 26, 35, 1)
	type_def_or_ref = (2, 1, 27)
	field_list_table = 3 if valid & (1 << 3) else 4
	method_list_table = 5 if valid & (1 << 5) else 6
	param_list_table = 7 if valid & (1 << 7) else 8
	row_sizes = {
		0: 2 + string_width + guid_width * 3,
		1: coded_width(resolution_scope, 2) + string_width * 2,
		2: 4 + string_width * 2 + coded_width(type_def_or_ref, 2) +
			simple_width(field_list_table) + simple_width(method_list_table),
		3: simple_width(4),
		4: 2 + string_width + blob_width,
		5: simple_width(6),
		6: 8 + string_width + blob_width + simple_width(param_list_table),
	}
	table_offsets = {}
	for table in range(7):
		if not valid & (1 << table):
			continue
		table_offsets[table] = cursor
		size = row_counts[table] * row_sizes[table]
		if cursor > len(tables) or size > len(tables) - cursor:
			raise ValueError(f"truncated CLR metadata table {table}")
		cursor += size

	def row_offset(table: int, rid: int) -> int:
		if rid < 1 or rid > row_counts[table] or table not in table_offsets:
			raise ValueError(f"invalid CLR metadata table {table} RID {rid}")
		return table_offsets[table] + (rid - 1) * row_sizes[table]

	def validate_guid(index: int) -> None:
		if index and (index - 1) * 16 + 16 > len(guid_heap):
			raise ValueError(f"CLR #GUID index is out of bounds: {index}")

	def validate_coded(value: int, targets: tuple[int, ...], tag_bits: int) -> None:
		if value == 0:
			return
		tag = value & ((1 << tag_bits) - 1)
		rid = value >> tag_bits
		if tag >= len(targets) or rid < 1 or rid > row_counts[targets[tag]]:
			raise ValueError(f"invalid CLR coded metadata index: {value}")

	if row_counts[0] != 1:
		raise ValueError(f"CLR Module table must contain exactly one row, found {row_counts[0]}")
	module = row_offset(0, 1)
	module += 2
	module_name_index, module = _read_index(tables, module, string_width, "Module.Name")
	module_name = _heap_string(strings_heap, module_name_index)
	for label in ("Module.Mvid", "Module.EncId", "Module.EncBaseId"):
		guid_index, module = _read_index(tables, module, guid_width, label)
		validate_guid(guid_index)

	for rid in range(1, row_counts[1] + 1):
		position = row_offset(1, rid)
		scope, position = _read_index(
			tables, position, coded_width(resolution_scope, 2), "TypeRef.ResolutionScope"
		)
		validate_coded(scope, resolution_scope, 2)
		name, position = _read_index(tables, position, string_width, "TypeRef.Name")
		namespace, _ = _read_index(tables, position, string_width, "TypeRef.Namespace")
		_heap_string(strings_heap, name)
		_heap_string(strings_heap, namespace)

	field_pointers = []
	for rid in range(1, row_counts[3] + 1):
		value, _ = _read_index(
			tables, row_offset(3, rid), simple_width(4), "FieldPtr.Field"
		)
		if value < 1 or value > row_counts[4]:
			raise ValueError(f"invalid CLR FieldPtr target RID {value}")
		field_pointers.append(value)
	if valid & (1 << 3) and (
		len(field_pointers) != row_counts[4] or
		set(field_pointers) != set(range(1, row_counts[4] + 1))
	):
		raise ValueError("CLR FieldPtr table must be a permutation of the Field table")

	fields = []
	for rid in range(1, row_counts[4] + 1):
		position = row_offset(4, rid) + 2
		name_index, position = _read_index(tables, position, string_width, "Field.Name")
		signature, _ = _read_index(tables, position, blob_width, "Field.Signature")
		_validate_blob_index(blob_heap, signature)
		fields.append(_heap_string(strings_heap, name_index))

	method_pointers = []
	for rid in range(1, row_counts[5] + 1):
		value, _ = _read_index(
			tables, row_offset(5, rid), simple_width(6), "MethodPtr.Method"
		)
		if value < 1 or value > row_counts[6]:
			raise ValueError(f"invalid CLR MethodPtr target RID {value}")
		method_pointers.append(value)
	if valid & (1 << 5) and (
		len(method_pointers) != row_counts[6] or
		set(method_pointers) != set(range(1, row_counts[6] + 1))
	):
		raise ValueError("CLR MethodPtr table must be a permutation of the MethodDef table")

	methods = []
	for rid in range(1, row_counts[6] + 1):
		position = row_offset(6, rid)
		rva, impl_flags, flags = struct.unpack_from("<IHH", tables, position)
		position += 8
		name_index, position = _read_index(tables, position, string_width, "MethodDef.Name")
		signature, position = _read_index(
			tables, position, blob_width, "MethodDef.Signature"
		)
		param_list, _ = _read_index(
			tables, position, simple_width(param_list_table), "MethodDef.ParamList"
		)
		_validate_blob_index(blob_heap, signature)
		if param_list < 1 or param_list > row_counts[param_list_table] + 1:
			raise ValueError(f"invalid MethodDef.ParamList index {param_list}")
		methods.append({
			"rid": rid,
			"rva": rva,
			"impl_flags": impl_flags,
			"flags": flags,
			"name": _heap_string(strings_heap, name_index),
		})

	type_rows = []
	for rid in range(1, row_counts[2] + 1):
		position = row_offset(2, rid) + 4
		name_index, position = _read_index(tables, position, string_width, "TypeDef.Name")
		namespace_index, position = _read_index(
			tables, position, string_width, "TypeDef.Namespace"
		)
		extends, position = _read_index(
			tables, position, coded_width(type_def_or_ref, 2), "TypeDef.Extends"
		)
		validate_coded(extends, type_def_or_ref, 2)
		field_start, position = _read_index(
			tables, position, simple_width(field_list_table), "TypeDef.FieldList"
		)
		method_start, _ = _read_index(
			tables, position, simple_width(method_list_table), "TypeDef.MethodList"
		)
		name = _heap_string(strings_heap, name_index)
		namespace = _heap_string(strings_heap, namespace_index)
		type_rows.append({
			"name": f"{namespace}.{name}" if namespace else name,
			"field_start": field_start,
			"method_start": method_start,
		})
	if not type_rows:
		raise ValueError("CLR metadata contains no TypeDef rows")

	for key, target_table in (("field_start", field_list_table), ("method_start", method_list_table)):
		starts = [row[key] for row in type_rows]
		limit = row_counts[target_table] + 1
		if starts[0] != 1 or any(value < 1 or value > limit for value in starts):
			raise ValueError(f"invalid TypeDef {key} range")
		if any(left > right for left, right in zip(starts, starts[1:])):
			raise ValueError(f"non-monotonic TypeDef {key} range")

	definitions = {}
	definition_counts = {}
	method_owners = {}
	for index, type_row in enumerate(type_rows):
		full_name = type_row["name"]
		field_end = (
			type_rows[index + 1]["field_start"] if index + 1 < len(type_rows)
			else row_counts[field_list_table] + 1
		)
		method_end = (
			type_rows[index + 1]["method_start"] if index + 1 < len(type_rows)
			else row_counts[method_list_table] + 1
		)
		owned_fields = set()
		for logical_rid in range(type_row["field_start"], field_end):
			actual_rid = field_pointers[logical_rid - 1] if field_pointers else logical_rid
			owned_fields.add(fields[actual_rid - 1])
		owned_methods = set()
		for logical_rid in range(type_row["method_start"], method_end):
			actual_rid = method_pointers[logical_rid - 1] if method_pointers else logical_rid
			owned_methods.add(methods[actual_rid - 1]["name"])
			method_owners[actual_rid] = full_name
		definition_counts[full_name] = definition_counts.get(full_name, 0) + 1
		definition = definitions.setdefault(full_name, {"fields": set(), "methods": set()})
		definition["fields"].update(owned_fields)
		definition["methods"].update(owned_methods)

	ldstr_user_strings = set()
	method_ldstr = {}
	for method in methods:
		if not method["rva"] or method["impl_flags"] & 0x0003:
			continue
		try:
			references = _decode_il_user_strings(
				data, sections, method["rva"], user_strings
			)
		except (ValueError, struct.error):
			continue
		owner = method_owners.get(method["rid"])
		if owner is not None:
			method_ldstr.setdefault((owner, method["name"]), set()).update(references)
		ldstr_user_strings.update(references)

	return {
		"module_name": module_name,
		"definitions": definitions,
		"definition_counts": definition_counts,
		"user_strings": set(user_strings.values()),
		"ldstr_user_strings": ldstr_user_strings,
		"method_ldstr": method_ldstr,
	}


def _pe_metadata_evidence(path: Path) -> dict[str, object]:
	data = path.read_bytes()
	if len(data) < 0x40 or data[:2] != b"MZ":
		raise ValueError("missing DOS header")
	pe_offset = struct.unpack_from("<I", data, 0x3C)[0]
	if pe_offset + 24 > len(data) or data[pe_offset:pe_offset + 4] != b"PE\x00\x00":
		raise ValueError("missing PE signature")
	section_count = struct.unpack_from("<H", data, pe_offset + 6)[0]
	optional_size = struct.unpack_from("<H", data, pe_offset + 20)[0]
	optional = pe_offset + 24
	if optional + optional_size > len(data):
		raise ValueError("truncated PE optional header")
	magic = struct.unpack_from("<H", data, optional)[0]
	if magic == 0x10B:
		data_directories = optional + 96
		number_of_directories = optional + 92
	elif magic == 0x20B:
		data_directories = optional + 112
		number_of_directories = optional + 108
	else:
		raise ValueError(f"unsupported PE optional-header magic 0x{magic:x}")
	if number_of_directories + 4 > optional + optional_size:
		raise ValueError("PE optional header has no data-directory count")
	declared_directories = struct.unpack_from("<I", data, number_of_directories)[0]
	if declared_directories < 15:
		raise ValueError(
			f"PE declares only {declared_directories} data directories; CLR requires 15"
		)
	cli_directory = data_directories + 14 * 8
	if cli_directory + 8 > optional + optional_size:
		raise ValueError("PE has no CLR data directory")
	cli_rva, cli_size = struct.unpack_from("<II", data, cli_directory)
	if cli_rva == 0 or cli_size < 72:
		raise ValueError(
			f"PE CLR header is missing or undersized: RVA=0x{cli_rva:x}, size={cli_size}"
		)

	sections = []
	section_table = optional + optional_size
	for index in range(section_count):
		header = section_table + index * 40
		if header + 40 > len(data):
			raise ValueError("truncated PE section table")
		virtual_size, virtual_address, raw_size, raw_offset = struct.unpack_from(
			"<IIII", data, header + 8
		)
		if raw_size and (raw_offset > len(data) or raw_size > len(data) - raw_offset):
			raise ValueError(
				f"PE section {index} raw data is out of bounds: "
				f"offset={raw_offset}, size={raw_size}, file={len(data)}"
			)
		sections.append((virtual_address, virtual_size, raw_offset, raw_size))
	cli_offset = _rva_to_offset(data, sections, cli_rva, cli_size)
	if cli_offset + 72 > len(data):
		raise ValueError("truncated CLR header")
	cli_header_size = struct.unpack_from("<I", data, cli_offset)[0]
	if cli_header_size < 72 or cli_header_size > cli_size:
		raise ValueError(
			f"invalid CLR header size: header={cli_header_size}, directory={cli_size}"
		)
	metadata_rva, metadata_size = struct.unpack_from("<II", data, cli_offset + 8)
	metadata_offset = _rva_to_offset(data, sections, metadata_rva, metadata_size)
	if metadata_size < 20 or metadata_offset + metadata_size > len(data):
		raise ValueError("truncated CLR metadata root")
	metadata = data[metadata_offset:metadata_offset + metadata_size]
	if metadata[:4] != b"BSJB":
		raise ValueError("CLR metadata root signature is missing")
	version_length = struct.unpack_from("<I", metadata, 12)[0]
	cursor = (16 + version_length + 3) & ~3
	if cursor + 4 > len(metadata):
		raise ValueError("truncated CLR metadata stream header")
	stream_count = struct.unpack_from("<H", metadata, cursor + 2)[0]
	cursor += 4
	streams: dict[str, bytes] = {}
	for _ in range(stream_count):
		if cursor + 8 > len(metadata):
			raise ValueError("truncated CLR metadata stream descriptor")
		stream_offset, stream_size = struct.unpack_from("<II", metadata, cursor)
		name_start = cursor + 8
		name_end = metadata.find(b"\x00", name_start, min(len(metadata), name_start + 32))
		if name_end < 0:
			raise ValueError("unterminated CLR metadata stream name")
		try:
			name = metadata[name_start:name_end].decode("ascii")
		except UnicodeDecodeError as error:
			raise ValueError("non-ASCII CLR metadata stream name") from error
		cursor = (name_end + 1 + 3) & ~3
		if stream_offset + stream_size > len(metadata):
			raise ValueError(f"CLR metadata stream {name} is out of bounds")
		if name in streams:
			raise ValueError(f"duplicate CLR metadata stream: {name}")
		streams[name] = metadata[stream_offset:stream_offset + stream_size]
	for required_stream in ("#Strings", "#US", "#Blob", "#GUID"):
		if required_stream not in streams:
			raise ValueError(f"CLR metadata must contain {required_stream} stream")
	table_streams = [name for name in ("#~", "#-") if name in streams]
	if len(table_streams) != 1:
		raise ValueError("CLR metadata must contain exactly one #~ or #- metadata table stream")
	user_strings = _parse_user_string_heap(streams["#US"])
	return _parse_metadata_tables(
		streams[table_streams[0]], streams["#Strings"], streams["#Blob"],
		streams["#GUID"], user_strings, data, sections,
	)


def _pe_metadata_heaps(path: Path) -> tuple[set[str], set[str]]:
	"""Compatibility wrapper for focused parser tests and diagnostics."""
	evidence = _pe_metadata_evidence(path)
	strings = set()
	for full_name, members in evidence["definitions"].items():
		strings.add(full_name)
		strings.update(members["fields"])
		strings.update(members["methods"])
	return strings, set(evidence["user_strings"])


_CSHARP_LEXEMES = re.compile(
	r"(?P<line>//[^\n]*)|(?P<block>/\*.*?\*/)|"
	r"(?P<verbatim>@\"(?:\"\"|[^\"])*\")|"
	r"(?P<string>\"(?:\\.|[^\"\\])*\")|"
	r"(?P<char>'(?:\\.|[^'\\])*')",
	re.DOTALL,
)


def _blank_lexeme(value: str) -> str:
	return "".join("\n" if character == "\n" else " " for character in value)


def _active_csharp(text: str) -> str:
	return _CSHARP_LEXEMES.sub(
		lambda match: _blank_lexeme(match.group(0))
		if match.lastgroup in ("line", "block") else match.group(0),
		text,
	)


def _masked_csharp(text: str) -> str:
	return _CSHARP_LEXEMES.sub(lambda match: _blank_lexeme(match.group(0)), text)


def _normalize_source(text: str) -> str:
	return re.sub(r"\s+", " ", text).strip()


def _balanced_block_span(
	active: str, opening_brace: int
) -> tuple[str, int, int] | None:
	masked = _masked_csharp(active)
	if opening_brace < 0 or opening_brace >= len(masked) or masked[opening_brace] != "{":
		return None
	depth = 0
	for index in range(opening_brace, len(masked)):
		if masked[index] == "{":
			depth += 1
		elif masked[index] == "}":
			depth -= 1
			if depth == 0:
				return active[opening_brace:index + 1], opening_brace, index + 1
	return None


def _balanced_block(active: str, opening_brace: int) -> str | None:
	span = _balanced_block_span(active, opening_brace)
	return None if span is None else span[0]


def _csharp_block(text: str, declaration_pattern: str) -> str | None:
	active = _active_csharp(text)
	masked = _masked_csharp(active)
	match = re.search(declaration_pattern, masked, re.MULTILINE)
	if match is None:
		return None
	opening_brace = masked.find("{", match.end())
	return _balanced_block(active, opening_brace)


def _csharp_block_after_literal(text: str, literal: str) -> str | None:
	active = _active_csharp(text)
	position = active.find(literal)
	if position < 0:
		return None
	masked = _masked_csharp(active)
	opening_brace = masked.find("{", position + len(literal))
	return _balanced_block(active, opening_brace)


def _csharp_direct_block_span(
	text: str, declaration_pattern: str
) -> tuple[str, int, int] | None:
	active = _active_csharp(text)
	masked = _masked_csharp(active)
	match = re.search(declaration_pattern, masked, re.MULTILINE)
	if match is None:
		return None
	cursor = match.end()
	while cursor < len(masked) and masked[cursor].isspace():
		cursor += 1
	if cursor >= len(masked) or masked[cursor] != "{":
		return None
	return _balanced_block_span(active, cursor)


def _csharp_direct_block(text: str, declaration_pattern: str) -> str | None:
	span = _csharp_direct_block_span(text, declaration_pattern)
	return None if span is None else span[0]


def _csharp_adjacent_direct_block(
	text: str, after_offset: int, declaration_pattern: str
) -> str | None:
	active = _active_csharp(text)
	masked = _masked_csharp(active)
	cursor = after_offset
	while cursor < len(masked) and masked[cursor].isspace():
		cursor += 1
	match = re.compile(declaration_pattern, re.MULTILINE).match(masked, cursor)
	if match is None:
		return None
	cursor = match.end()
	while cursor < len(masked) and masked[cursor].isspace():
		cursor += 1
	if cursor >= len(masked) or masked[cursor] != "{":
		return None
	return _balanced_block(active, cursor)


def _csharp_direct_if_block(text: str, condition_pattern: str) -> str | None:
	active = _active_csharp(text)
	masked = _masked_csharp(active)
	for match in re.finditer(r"\bif\s*\(", masked, re.MULTILINE):
		opening_parenthesis = masked.find("(", match.start(), match.end())
		depth = 0
		closing_parenthesis = -1
		for index in range(opening_parenthesis, len(masked)):
			if masked[index] == "(":
				depth += 1
			elif masked[index] == ")":
				depth -= 1
				if depth == 0:
					closing_parenthesis = index
					break
		if closing_parenthesis < 0:
			return None

		condition = _normalize_source(active[opening_parenthesis + 1:closing_parenthesis])
		if re.fullmatch(condition_pattern, condition) is None:
			continue

		cursor = closing_parenthesis + 1
		while cursor < len(masked) and masked[cursor].isspace():
			cursor += 1
		if cursor >= len(masked) or masked[cursor] != "{":
			return None
		return _balanced_block(active, cursor)

	return None


def _enum_members(text: str, name: str) -> tuple[str, ...] | None:
	block = _csharp_block(text, rf"\benum\s+{re.escape(name)}\b")
	if block is None:
		return None
	members = []
	for raw_member in block[1:-1].split(","):
		match = re.match(r"\s*([A-Za-z_]\w*)", raw_member)
		if match is not None:
			members.append(match.group(1))
	return tuple(members)


def _read_ios_lobby_sources(
	source_root: Path, errors: list[str]
) -> dict[str, str]:
	sources = {}
	for name, relative in IOS_LOBBY_SOURCE_PATHS.items():
		path = source_root / relative
		if not path.is_file():
			errors.append(f"iOS lobby runtime source is missing: {relative}")
			continue
		try:
			sources[name] = path.read_text(encoding="utf-8")
		except (OSError, UnicodeError) as error:
			errors.append(f"unable to read iOS lobby runtime source {relative}: {error}")
	return sources


def _has_source_fragments(text: str | None, fragments: tuple[str, ...]) -> bool:
	if text is None:
		return False
	normalized = _normalize_source(_active_csharp(text))
	return all(_normalize_source(fragment) in normalized for fragment in fragments)


def _audit_ios_lobby_source_contracts(source_root: Path, errors: list[str]) -> None:
	sources = _read_ios_lobby_sources(source_root, errors)

	protocol = sources.get("protocol")
	if not _has_source_fragments(protocol, ("public const int Orders = 23;",)):
		errors.append("iOS lobby orders protocol must remain 23")

	safe_start_request = sources.get("safe_start_request")
	request_type = _csharp_block(
		safe_start_request or "", r"\breadonly\s+struct\s+LobbySafeStartRequest\b"
	)
	request_constructor = _csharp_block(
		request_type or "", r"\bLobbySafeStartRequest\s*\(\s*string\s+expectedMapUid\s*,\s*long\s+requestId\s*\)"
	)
	request_command = _csharp_block(
		request_type or "", r"\bstring\s+ToCommand\s*\(\s*\)"
	)
	request_parse = _csharp_block(
		request_type or "", r"\bstatic\s+bool\s+TryParse\s*\("
	)
	if not _has_source_fragments(request_type, (
		"public string ExpectedMapUid { get; }",
		"public long RequestId { get; }",
	)) or not _has_source_fragments(request_constructor, (
		"string.IsNullOrWhiteSpace(expectedMapUid)",
		"expectedMapUid.Any(char.IsWhiteSpace)",
		"requestId <= 0",
		"ExpectedMapUid = expectedMapUid;",
		"RequestId = requestId;",
	)):
		errors.append("LobbySafeStartRequest must reject invalid map tokens and non-positive IDs")
	if not _has_source_fragments(request_command, (
		'"startgame_safe {ExpectedMapUid} {RequestId.ToString(CultureInfo.InvariantCulture)}"',
	)):
		errors.append("safe-start command must canonically serialize map UID and request ID")
	if not _has_source_fragments(request_parse, (
		"value.Split(' ')", "fields.Length != 2", "fields.Any(string.IsNullOrEmpty)",
		"NumberStyles.None", "CultureInfo.InvariantCulture", "requestId <= 0",
		"requestId.ToString(CultureInfo.InvariantCulture) != fields[1]",
	)):
		errors.append("safe-start command parser must accept exactly two canonical tokens")
	request_parse_try_span = _csharp_direct_block_span(
		request_parse or "", r"\btry\b"
	)
	request_parse_try = None if request_parse_try_span is None else request_parse_try_span[0]
	if not _has_source_fragments(request_parse_try, (
		"request = new LobbySafeStartRequest(fields[0], requestId);",
		"return true;",
	)):
		errors.append("safe-start parser must assign the parsed request")
	request_parse_catch = None if request_parse_try_span is None else _csharp_adjacent_direct_block(
		request_parse or "", request_parse_try_span[2],
		r"catch\s*\(\s*ArgumentException\s*\)",
	)
	if request_parse_catch is None:
		errors.append(
			"constructor try must be immediately followed by its argument exception catch"
		)
	if not _has_source_fragments(request_parse_catch, ("return false;",)):
		errors.append("safe-start parser must fail closed on constructor argument errors")

	safe_start_state = sources.get("safe_start_state")
	state_type = _csharp_block(
		safe_start_state or "", r"\bclass\s+LobbySafeStartRequestState\b"
	)
	state_begin = _csharp_block(state_type or "", r"\bLobbySafeStartRequest\s+Begin\s*\(")
	state_accept = _csharp_block(state_type or "", r"\bbool\s+TryAcceptRejection\s*\(")
	state_timeout = _csharp_block(state_type or "", r"\bbool\s+TryTimeout\s*\(")
	state_readiness = _csharp_block(state_type or "", r"\bbool\s+ObserveReadinessChange\s*\(")
	state_sync = _csharp_block(state_type or "", r"\bbool\s+ObserveLobbySync\s*\(")
	state_reset = _csharp_block(state_type or "", r"\bbool\s+Reset\s*\(")
	if not _has_source_fragments(state_type, (
		"public bool IsPending { get; private set; }",
		"public long PendingRequestId { get; private set; }",
		"public LobbyStartRejection Rejection { get; private set; }",
	)):
		errors.append("safe-start request state is missing its pending/rejection state")
	if not _has_source_fragments(state_begin, (
		"if (lastIssuedRequestId == long.MaxValue)",
		"throw new InvalidOperationException",
		"new LobbySafeStartRequest(mapUid, lastIssuedRequestId + 1)",
		"PendingRequestId = request.RequestId;", "IsPending = true;", "Rejection = null;",
	)):
		errors.append("safe-start request IDs must fail closed at long.MaxValue")
	if not _has_source_fragments(state_accept, (
		"!IsPending", "rejection == null", "rejection.RequestId != PendingRequestId",
		"ClearPending();", "Rejection = rejection;", "return true;",
	)):
		errors.append("safe-start state must accept only matching request/map rejections")
	if not _has_source_fragments(state_accept, (
		"!string.Equals(rejection.ExpectedMapUid, pendingMapUid, StringComparison.Ordinal)",
	)):
		errors.append("safe-start rejection must reject a mismatched pending map")
	if not _has_source_fragments(state_accept, (
		"!string.Equals(rejection.ExpectedMapUid, currentMapUid, StringComparison.Ordinal)",
	)):
		errors.append("safe-start rejection must reject a mismatched current map")
	if not _has_source_fragments(state_timeout, (
		"!IsPending || PendingRequestId != requestId", "ClearPending();", "return true;",
	)):
		errors.append("safe-start timeout must clear only the exact pending request")
	if not _has_source_fragments(state_readiness, (
		"Rejection == null || Rejection.ClientIndex != clientIndex",
		"Rejection = null;", "return true;",
	)) or any(fragment in _active_csharp(state_readiness or "") for fragment in (
		"ClearPending(", "IsPending = false", "PendingRequestId = 0",
	)):
		errors.append("readiness changes must preserve the pending request")
	if not _has_source_fragments(state_sync, (
		"Rejection = null;",
		"IsPending && !string.Equals(pendingMapUid, currentMapUid, StringComparison.Ordinal)",
		"ClearPending();", "changed = true;",
	)):
		errors.append("same-map lobby sync must preserve the pending request")
	if not _has_source_fragments(state_reset, (
		"!IsPending && Rejection == null", "ClearPending();", "Rejection = null;",
	)):
		errors.append("safe-start state reset must clear pending and rejection state")

	session = sources.get("session")
	if session is not None and _enum_members(session, "ClientMapPhase") != CLIENT_MAP_PHASES:
		errors.append(
			"ClientMapPhase enum must remain exactly " + ", ".join(CLIENT_MAP_PHASES)
		)
	if not _has_source_fragments(session, (
		"public string MapUid;",
		"public ClientMapPhase MapPhase = ClientMapPhase.Unknown;",
		"public int MapProgress = -1;",
		"ClientMapPhase.Ready",
		"MapProgress == 100",
	)):
		errors.append("Session.Client current-map readiness fields or Ready/100 gate changed")

	readiness = sources.get("readiness")
	validity = _csharp_block(
		readiness or "", r"\bpublic\s+static\s+bool\s+IsValidPhaseProgress\s*\("
	)
	validity_active = _normalize_source(_active_csharp(validity or ""))
	if not re.search(
		r"ClientMapPhase\.Downloading\s*=>\s*progress\s*==\s*-1\s*\|\|\s*"
		r"progress\s+is\s*>=\s*0\s+and\s*<=\s*99",
		validity_active,
	):
		errors.append("Downloading progress must stop at 99 and preserve unknown -1")
	if not re.search(
		r"ClientMapPhase\.Ready\s*=>\s*progress\s*==\s*100",
		validity_active,
	) or not re.search(r"_\s*=>\s*progress\s*==\s*-1", validity_active):
		errors.append("map readiness phase/progress invariants no longer fail closed")

	order_manager = sources.get("order_manager")
	if not _has_source_fragments(order_manager, (
		"public event Action<int> ClientMapReadinessChanged",
		"internal void NotifyClientMapReadinessChanged(int clientIndex)",
		"ClientMapReadinessChanged(clientIndex);",
	)):
		errors.append("network runtime is missing the narrow readiness-changed event")
	if not _has_source_fragments(order_manager, (
		"public event Action<LobbyStartRejection> SafeStartRejected",
		"internal void NotifySafeStartRejected(LobbyStartRejection rejection)",
		"SafeStartRejected(rejection);",
	)):
		errors.append("network runtime is missing the typed safe-start rejection event")

	server = sources.get("server")
	server_delta = _csharp_block(
		server or "", r"\bpublic\s+void\s+SyncClientMapReadiness\s*\("
	)
	if not _has_source_fragments(server_delta, (
		"DispatchServerOrdersToClients",
		'"SyncClientMapReadiness"',
		"update.Serialize()",
	)):
		errors.append("server-to-client SyncClientMapReadiness delta wiring is missing")

	unit_orders = sources.get("unit_orders")
	process_order = _csharp_block(
		unit_orders or "", r"\bProcessOrder\s*\([^)]*\)"
	)
	client_delta = _csharp_block_after_literal(
		process_order or "", 'case "SyncClientMapReadiness":'
	)
	if not _has_source_fragments(client_delta, (
		"ClientMapReadinessUpdate.TryDeserialize",
		"update.ApplyTo(orderManager.LobbyInfo)",
		"orderManager.NotifyClientMapReadinessChanged(update.ClientIndex);",
	)) or "Game.SyncLobbyInfo(" in _active_csharp(client_delta or ""):
		errors.append(
			"client readiness delta must use the narrow readiness-changed event "
			"without Game.SyncLobbyInfo()"
		)
	safe_start_delta = _csharp_block_after_literal(
		process_order or "", 'case "SafeStartRejected":'
	)
	if not _has_source_fragments(safe_start_delta, (
		"LobbyStartRejection.Deserialize(order.TargetString)",
		"if (rejection == null)",
		"orderManager.NotifySafeStartRejected(rejection);",
	)):
		errors.append("SafeStartRejected orders must deserialize before raising the typed event")

	start_availability = sources.get("start_availability")
	evaluate = _csharp_block(
		start_availability or "",
		r"\bpublic\s+static\s+LobbyStartAvailability\s+Evaluate\s*\(",
	)
	if not _has_source_fragments(evaluate, (
		"var mapSafety = EvaluateMapSafety",
		"if (!mapSafety.CanStart)",
		"return mapSafety;",
	)):
		errors.append("shared lobby start policy no longer applies the map-safety gate")
	availability_rejection = _csharp_block(
		start_availability or "", r"\bLobbyStartRejection\s+ToRejection\s*\("
	)
	if not _has_source_fragments(availability_rejection, (
		"if (CanStart)", "throw new InvalidOperationException",
		"request.ExpectedMapUid, request.RequestId",
	)):
		errors.append("availability rejection must echo the safe-start request")

	rejection_type = _csharp_block(
		start_availability or "", r"\bclass\s+LobbyStartRejection\b"
	)
	rejection_active = _active_csharp(rejection_type or "")
	rejection_properties = set(re.findall(
		r"\bpublic\s+[A-Za-z_.][\w.<>]*\s+(\w+)\s*\{\s*get\s*;\s*\}",
		rejection_active,
	))
	rejection_fields = {
		"Reason", "ClientIndex", "ClientMapPhase", "Progress", "ExpectedMapUid", "RequestId",
	}
	if rejection_properties != rejection_fields:
		errors.append("LobbyStartRejection must expose exactly six correlated fields")
	rejection_to_availability = _csharp_block(
		rejection_type or "", r"\bLobbyStartAvailability\s+ToAvailability\s*\("
	)
	if not _has_source_fragments(rejection_to_availability, (
		"new LobbyStartAvailability(Reason, ClientIndex, ClientMapPhase, Progress, 0)",
	)):
		errors.append("LobbyStartRejection.ToAvailability must preserve rejection details")
	rejection_serialize = _csharp_block(
		rejection_type or "", r"\bstring\s+Serialize\s*\("
	)
	serialized_fields = re.findall(
		r"\bnew\s*\(\s*nameof\((\w+)\)",
		_active_csharp(rejection_serialize or ""),
	)
	canonical_rejection_fields = (
		"Reason", "ClientIndex", "ClientMapPhase", "Progress", "ExpectedMapUid", "RequestId",
	)
	if len(serialized_fields) != 6 or set(serialized_fields) != rejection_fields:
		errors.append("rejection serialization must contain exactly six correlated fields")
	elif tuple(serialized_fields) != canonical_rejection_fields:
		errors.append("rejection serialization must use the canonical field order")
	rejection_deserialize = _csharp_block(
		rejection_type or "", r"\bstatic\s+LobbyStartRejection\s+Deserialize\s*\("
	)
	expected_fields = _csharp_block_after_literal(
		rejection_deserialize or "", "var expected = new[]"
	)
	expected_field_names = re.findall(
		r"nameof\((\w+)\)", _active_csharp(expected_fields or "")
	)
	if len(expected_field_names) != 6 or set(expected_field_names) != rejection_fields:
		errors.append("rejection parser must require exactly six field names")
	if not _has_source_fragments(rejection_deserialize, (
		"nodes.Count != expected.Length",
		"expected.Any(key => nodes.Count(node => node.Key == key) != 1)",
		"nodes.Any(node => !expected.Contains(node.Key) || node.Value.Nodes.Any())",
	)):
		errors.append("rejection parser must require exact unique leaf fields")
	if not _has_source_fragments(rejection_deserialize, (
		"!Enum.TryParse(values[nameof(Reason)], false, out Session.LobbyStartBlockReason reason)",
		"values[nameof(Reason)] != reason.ToString()",
		"!Enum.TryParse(values[nameof(ClientMapPhase)], false, out Session.ClientMapPhase phase)",
		"values[nameof(ClientMapPhase)] != phase.ToString()",
		"!int.TryParse(values[nameof(ClientIndex)], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var clientIndex)",
		"values[nameof(ClientIndex)] != clientIndex.ToString(CultureInfo.InvariantCulture)",
		"!int.TryParse(values[nameof(Progress)], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var progress)",
		"values[nameof(Progress)] != progress.ToString(CultureInfo.InvariantCulture)",
	)):
		errors.append("rejection enum and signed integer fields must parse canonically")
	if not _has_source_fragments(rejection_deserialize, (
		"!long.TryParse(values[nameof(RequestId)], NumberStyles.None, CultureInfo.InvariantCulture, out var requestId)",
		"requestId <= 0",
		"values[nameof(RequestId)] != requestId.ToString(CultureInfo.InvariantCulture)",
	)):
		errors.append("rejection request ID must use canonical positive invariant parsing")
	if not _has_source_fragments(rejection_deserialize, (
		"return new LobbyStartRejection(reason, clientIndex, phase, progress, values[nameof(ExpectedMapUid)], requestId);",
		"catch (Exception)", "return null;",
	)):
		errors.append("rejection parser must construct from the parsed request ID")

	server_start = _csharp_block(server or "", r"\bpublic\s+void\s+StartGame\s*\(")
	if not _has_source_fragments(server_start, (
		"if (IsMultiplayer && !LobbyStartAvailability.EvaluateMapSafety",
		"requireCurrentMapReadiness: true",
		".CanStart)",
		"return;",
	)):
		errors.append("server StartGame is missing its central multiplayer map gate")

	lobby_commands = sources.get("lobby_commands")
	evaluate_server = _csharp_block(
		lobby_commands or "", r"\bEvaluateStartAvailability\s*\([^)]*\)"
	)
	legacy_start = _csharp_block(
		lobby_commands or "", r"\bstatic\s+bool\s+StartGame\s*\("
	)
	safe_start = _csharp_block(
		lobby_commands or "", r"\bstatic\s+bool\s+StartGameSafe\s*\("
	)
	auto_start = _csharp_block(
		lobby_commands or "", r"\bstatic\s+void\s+CheckAutoStart\s*\("
	)
	if not _has_source_fragments(evaluate_server, (
		"LobbyStartAvailability.Evaluate",
		"RequireCurrentMapReadiness = server.IsMultiplayer",
	)) or not _has_source_fragments(legacy_start, (
		"LobbyStartMode.ForceConfirmed", "if (!availability.CanStart)",
	)) or not _has_source_fragments(safe_start, (
		"LobbySafeStartRequest.TryParse(value, out var request)",
		"LobbyStartMode.SafeDirect", "if (!availability.CanStart)",
	)) or not _has_source_fragments(auto_start, (
		"LobbyStartMode.Automatic", ".CanStart", "return;",
	)):
		errors.append("legacy, safe, and automatic starts must share the multiplayer map gate")
	non_admin_rejection = _csharp_direct_if_block(
		safe_start or "", re.escape("!client.IsAdmin")
	)
	if non_admin_rejection is None:
		errors.append("non-admin guard must directly own a braced rejection block")
	if not _has_source_fragments(non_admin_rejection, (
		"Session.LobbyStartBlockReason.RequesterNotAdmin",
		"request.ExpectedMapUid, request.RequestId", ".Serialize()",
	)):
		errors.append("explicit safe-start rejection must echo the request")
	if not re.search(
		r"\breturn\s+true\s*;\s*}\s*$", _active_csharp(non_admin_rejection or "")
	):
		errors.append("non-admin safe-start rejection must terminate the branch")
	stale_rejection = _csharp_direct_if_block(
		safe_start or "",
		re.escape(
			"!string.Equals(request.ExpectedMapUid, currentMapUid, StringComparison.Ordinal)"
		),
	)
	if stale_rejection is None:
		errors.append("stale-map guard must directly own a braced rejection block")
	stale_active = _normalize_source(_active_csharp(stale_rejection or ""))
	if not _has_source_fragments(stale_rejection, (
		"Session.LobbyStartBlockReason.StaleMap",
		"request.ExpectedMapUid, request.RequestId", ".Serialize()",
	)) or re.search(r"\bcurrentMapUid\s*,\s*request\.RequestId\b", stale_active):
		errors.append("stale-map rejection must echo the requested map UID")
	if not re.search(
		r"\breturn\s+true\s*;\s*}\s*$", _active_csharp(stale_rejection or "")
	):
		errors.append("stale-map safe-start rejection must terminate the branch")
	availability_rejection_branch = _csharp_direct_if_block(
		safe_start or "", re.escape("!availability.CanStart")
	)
	if availability_rejection_branch is None:
		errors.append("availability guard must directly own a braced rejection block")
	if not _has_source_fragments(availability_rejection_branch, (
		"availability.ToRejection(request).Serialize()",
	)):
		errors.append("blocked safe start must serialize availability with the request ID")
	if not re.search(
		r"\breturn\s+true\s*;\s*}\s*$", _active_csharp(availability_rejection_branch or "")
	):
		errors.append("availability safe-start rejection must terminate the branch")
	if not _has_source_fragments(lobby_commands, (
		'"map_status"', '"startgame_safe"',
		"mapReadinessThrottle.Drain", "server.SyncClientMapReadiness",
	)):
		errors.append("client-to-server map_status or server readiness delta wiring is missing")

	can_sync_lobby = _csharp_block(
		lobby_commands or "", r"\bpublic\s+static\s+bool\s+CanSyncLobby\s*\("
	)
	if not _has_source_fragments(can_sync_lobby, (
		"return serverType is ServerType.Local or ServerType.Skirmish;",
	)):
		errors.append("sync_lobby full snapshots must remain Local/Skirmish-only")
	sync_lobby = _csharp_block(
		lobby_commands or "", r"\bstatic\s+bool\s+SyncLobby\s*\("
	)
	sync_lobby_normalized = _normalize_source(_active_csharp(sync_lobby or ""))
	guard = "if (!CanSyncLobby(server.Type))"
	deserialize = "server.LobbyInfo = Session.Deserialize"
	if (
		guard not in sync_lobby_normalized or
		deserialize not in sync_lobby_normalized or
		sync_lobby_normalized.index(guard) > sync_lobby_normalized.index(deserialize)
	):
		errors.append("sync_lobby must enforce the Local/Skirmish-only guard before deserialization")

	lobby_logic = sources.get("lobby_logic")
	start_click = _csharp_block(
		lobby_logic or "", r"startGameButton\.OnClick\s*=\s*\(\)\s*=>"
	)
	start_click_normalized = _normalize_source(_active_csharp(start_click or ""))
	if (
		"if (Platform.IsIOS) { BeginSafeStart(); return; }" not in start_click_normalized or
		"PanelType.ForceStart" not in start_click_normalized
	):
		errors.append("Start button must keep the iOS-only one-tap safe-start path")
	begin_safe_start = _csharp_block(
		lobby_logic or "", r"\bvoid\s+BeginSafeStart\s*\("
	)
	if not _has_source_fragments(begin_safe_start, (
		"CurrentStartAvailability()", "!availability.CanStart", "safeStartState.IsPending",
		"safeStartState.Begin(currentMapUid)", "Game.RunAfterDelay(5000",
		"safeStartState.TryTimeout(request.RequestId)",
		"Order.Command(request.ToCommand())",
	)):
		errors.append("iOS safe start must issue and timeout the canonical correlated request")
	safe_start_rejected = _csharp_block(
		lobby_logic or "", r"\bvoid\s+SafeStartRejected\s*\("
	)
	if not _has_source_fragments(safe_start_rejected, (
		"safeStartState.TryAcceptRejection(rejection, orderManager.LobbyInfo.GlobalSettings.Map)",
		"InvalidateStartStatus();",
	)):
		errors.append("safe-start rejection handler must accept only correlated rejections")
	current_start_availability = _csharp_block(
		lobby_logic or "", r"\bLobbyStartAvailability\s+CurrentStartAvailability\s*\("
	)
	if not _has_source_fragments(current_start_availability, (
		"safeStartState.Rejection != null",
		"safeStartState.Rejection.ToAvailability()",
	)):
		errors.append("safe-start rejection must drive the displayed availability")
	readiness_changed = _csharp_block(
		lobby_logic or "", r"\bvoid\s+ClientMapReadinessChanged\s*\("
	)
	if not _has_source_fragments(readiness_changed, (
		"safeStartState.ObserveReadinessChange(clientIndex)", "InvalidateStartStatus();",
	)):
		errors.append("readiness events must refresh correlated safe-start rejection state")
	lobby_info_changed = _csharp_block(
		lobby_logic or "", r"\bvoid\s+StartStatusLobbyInfoChanged\s*\("
	)
	if not _has_source_fragments(lobby_info_changed, (
		"safeStartState.ObserveLobbySync(orderManager.LobbyInfo.GlobalSettings.Map)",
		"InvalidateStartStatus();",
	)):
		errors.append("lobby sync must preserve same-map pending safe-start state")
	connection_state_changed = _csharp_block(
		lobby_logic or "", r"\bvoid\s+ConnectionStateChanged\s*\("
	)
	disconnected = _csharp_block_after_literal(
		connection_state_changed or "",
		"if (connection.ConnectionState == ConnectionState.NotConnected)",
	)
	if not _has_source_fragments(disconnected, ("safeStartState.Reset()",)):
		errors.append("connection loss must reset safe-start state")
	dispose = _csharp_block(lobby_logic or "", r"\bDispose\s*\(")
	if not _has_source_fragments(dispose, ("safeStartState.Reset();",)):
		errors.append("lobby dispose must reset safe-start state")
	if not _has_source_fragments(lobby_logic, (
		"new LobbySafeStartRequestState(0)",
		"orderManager.ClientMapReadinessChanged += ClientMapReadinessChanged;",
		"orderManager.ClientMapReadinessChanged -= ClientMapReadinessChanged;",
		"orderManager.SafeStartRejected += SafeStartRejected;",
		"orderManager.SafeStartRejected -= SafeStartRejected;",
	)):
		errors.append("iOS lobby readiness and safe-start event subscriptions are not symmetric")

	lobby_layout = sources.get("lobby_layout")
	if not _has_source_fragments(lobby_layout, (
		"var minimumPlayerWidth = 10 * policy.MinimumTarget;",
		"var playersWidth = skirmishMode ?",
		"2 * Math.Max(0, width - Gap) / 3 :",
		"new[] { 4, 2, 2, 2, 2 }",
		"new[] { 2, 1, 3, 1, 1, 1, 1 }",
	)):
		errors.append("iOS lobby must preserve full multiplayer columns and the text-balanced 2:1 skirmish layout")

	touch_policy = sources.get("touch_policy")
	if not _has_source_fragments(touch_policy, (
		"public static IosDropDownLayout Default { get; } = new(3, 0, 0, default, false);",
		"public static IosDropDownLayout FactionPicker { get; } = new(5, 6, 24, new Size(40, 20), true);",
	)):
		errors.append("IosDropDownLayout.FactionPicker must remain 5/6/24/40x20/singleton-omit")

	dropdown = sources.get("dropdown")
	if not _has_source_fragments(dropdown, (
		"IosDropDownLayout iosLayout",
		"IosDropDownLayout.Default",
		"AdaptIosDropDownPanel(panel, snapshot, iosLayout)",
		"IosTouchWidgetPolicy.ShowDropDownGroupHeader",
		"AdaptIosDropDownItem",
	)):
		errors.append("grouped dropdown no longer threads its iOS layout preset")

	lobby_utils = sources.get("lobby_utils")
	if not _has_source_fragments(lobby_utils, (
		'"FACTION_DROPDOWN_TEMPLATE"',
		"IosDropDownLayout.FactionPicker",
		"label.GetText = () => WidgetUtils.TruncateText",
		"label.Bounds.Width",
	)):
		errors.append("faction dropdown must use FactionPicker with post-layout truncation")

	start_status = sources.get("start_status")
	for message in IOS_LOBBY_FLUENT[Path("mods/common/fluent/chrome.ftl")]:
		if f'"{message}"' not in _active_csharp(start_status or ""):
			errors.append(f"iOS lobby start formatter is missing Fluent key: {message}")
	client_status = _csharp_block(
		start_status or "", r"\bstatic\s+string\s+ClientStatus\s*\("
	)
	if not _has_source_fragments(client_status, (
		"localize(ClientAndOthers, new object[]",
		"localize(ClientsNotReady, new object[]",
		'"status"', '"count"',
	)):
		errors.append("iOS multi-client blocker source wiring is missing")
	start_format = _csharp_block(
		start_status or "", r"\bstatic\s+string\s+Format\s*\("
	)
	if not _has_source_fragments(start_status, (
		'const string RequiresHost = "notification-requires-host";',
	)) or not _has_source_fragments(start_format, (
		"Session.LobbyStartBlockReason.RequesterNotAdmin =>",
		"localize(RequiresHost, Array.Empty<object>())",
	)):
		errors.append("Requester-not-admin must use notification-requires-host")

	smoke = sources.get("smoke")
	smoke_handler = _csharp_block(
		smoke or "", r"\bvoid\s+HandleSafeStartRejected\s*\("
	)
	smoke_poll = _csharp_block(smoke or "", r"\bvoid\s+Poll\s*\(")
	if not _has_source_fragments(smoke_handler, (
		"rejection.RequestId != blockedStartRequest.RequestId",
		"blockedStartRejected = true;",
	)):
		errors.append("smoke blocker handler must validate the blocked request ID and map")
	if not _has_source_fragments(smoke_handler, (
		"!string.Equals(rejection.ExpectedMapUid, currentMapUid, StringComparison.Ordinal)",
	)):
		errors.append("smoke blocker handler must reject a mismatched map")
	if not _has_source_fragments(smoke_poll, (
		"blockedStartRequest = new LobbySafeStartRequest(currentMapUid, 1);",
		"blockedStartRequest.ToCommand()",
		"finalStartRequest = new LobbySafeStartRequest(currentMapUid, 2);",
		"finalStartRequest.ToCommand()",
	)):
		errors.append("smoke final safe-start request must use request ID 2 after blocked ID 1")

	map_status = sources.get("map_status")
	if not _has_source_fragments(map_status, (
		"!isIos", "client.Bot != null", "currentMapUid", "Session.ClientMapPhase.Downloading",
		"client.MapProgress >= 0", "client.MapProgress", '"…"',
	)):
		errors.append("iOS per-player map status no longer shows scoped progress and reasons")


def _audit_assets(
	app_bundle: Path,
	source_root: Path,
	bundle_paths: tuple[Path, ...],
	errors: list[str],
) -> None:
	manifest = _touch_asset_manifest()
	source_uibits = source_root / "mods/ra2/uibits"
	try:
		bundle_uibits = _bundle_member(app_bundle, "mods/ra2/uibits")
	except ValueError as error:
		errors.append(str(error))
		return

	if bundle_uibits.is_dir():
		expected_paths = {
			(Path("mods/ra2/uibits") / name).as_posix() for name in manifest
		}
		bundled_paths = {
			path.relative_to(app_bundle).as_posix()
			for path in bundle_paths
			if path.is_file() and path.name.startswith("ios-touch-") and
			path.suffix.casefold() == ".png"
		}
		if bundled_paths != expected_paths:
			errors.append(
				"bundle must contain exactly 18 production ios-touch PNG assets; "
				f"found {len(bundled_paths)} at {sorted(bundled_paths)}"
			)
	else:
		errors.append(f"bundled RA2 uibits directory is missing: {bundle_uibits}")

	if source_uibits.is_dir():
		source_names = {
			path.name for path in source_uibits.iterdir()
			if path.name.startswith("ios-touch-") and "-v3" in path.name and
			path.suffix.casefold() == ".png"
		}
		if source_names != set(manifest):
			errors.append(
				"source must contain exactly 18 production ios-touch PNG assets; "
				f"found {len(source_names)}"
			)

	for name, expected_size in manifest.items():
		source = source_uibits / name
		try:
			bundled = _bundle_member(app_bundle, Path("mods/ra2/uibits") / name)
		except ValueError as error:
			errors.append(str(error))
			continue
		if not source.is_file():
			errors.append(f"canonical touch asset is missing: {source}")
		if not bundled.is_file():
			errors.append(f"touch asset is missing from the final app: {bundled}")
			continue
		if source.is_file() and _sha256(source) != _sha256(bundled):
			errors.append(f"touch asset hash mismatch for {name}")
		try:
			width, height, bit_depth, color_type = _png_ihdr(bundled)
		except (OSError, ValueError) as error:
			errors.append(f"touch asset has invalid IHDR {name}: {error}")
			continue
		if (width, height) != expected_size:
			errors.append(
				f"touch asset has wrong dimensions {name}: {width}x{height}, "
				f"expected {expected_size[0]}x{expected_size[1]}"
			)
		if not _is_power_of_two(width) or not _is_power_of_two(height):
			errors.append(f"touch asset dimensions are not POT: {name}")
		if bit_depth != 8 or color_type != 6:
			errors.append(
				f"touch asset must use an 8-bit RGBA IHDR: {name} "
				f"(bit depth {bit_depth}, color type {color_type})"
			)


def _audit_configuration_parity(
	app_bundle: Path, source_root: Path, errors: list[str]
) -> dict[Path, str]:
	bundled_text: dict[Path, str] = {}
	for source_relative, bundle_relative in CONFIG_PATHS:
		source = source_root / source_relative
		try:
			bundled = _bundle_member(app_bundle, bundle_relative)
		except ValueError as error:
			errors.append(str(error))
			continue
		if not source.is_file():
			errors.append(f"canonical runtime configuration is missing: {source}")
		if not bundled.is_file():
			errors.append(f"runtime configuration is missing from the final app: {bundled}")
			continue
		if source.is_file() and _sha256(source) != _sha256(bundled):
			errors.append(f"configuration hash mismatch: {bundle_relative}")
		try:
			bundled_text[bundle_relative] = bundled.read_text(encoding="utf-8")
		except (OSError, UnicodeError) as error:
			errors.append(f"unable to read bundled runtime configuration {bundled}: {error}")
	return bundled_text


def _audit_chrome(
	app_bundle: Path, bundled_text: dict[Path, str], errors: list[str]
) -> None:
	chrome = bundled_text.get(Path("mods/ra2/chrome.yaml"))
	if chrome is None:
		return
	collections, parse_errors = _parse_chrome_collections(chrome)
	errors.extend(parse_errors)
	expected = _expected_collections()
	touch_names = {name for name in collections if name.startswith("ios-touch-")}
	if touch_names != set(expected):
		missing = sorted(set(expected) - touch_names)
		extra = sorted(touch_names - set(expected))
		errors.append(
			"final Chrome catalog must define exactly 42 ios-touch collections; "
			f"missing={missing}, extra={extra}"
		)

	cache: dict[str, dict] = {}
	asset_sizes = _touch_asset_manifest()
	for name, filenames in expected.items():
		if name not in collections:
			continue
		try:
			resolved = _resolve_collection(name, collections, cache)
		except ValueError as error:
			errors.append(str(error))
			continue
		if (resolved["Image"], resolved["Image2x"]) != filenames:
			errors.append(
				f"Chrome collection {name} does not resolve to its required Image/Image2x files"
			)
			continue
		if name.startswith("ios-touch-actions-"):
			if name.endswith("-highlighted"):
				missing = HIGHLIGHTED_ACTION_REGIONS - set(
					collections[name]["regions"]
				)
				for region_name in sorted(missing):
					errors.append(
						f"Chrome collection {name} is missing required highlighted "
						f"action region {region_name}"
					)
			else:
				missing = ACTION_REGIONS - set(resolved["regions"])
				for region_name in sorted(missing):
					errors.append(
						f"Chrome collection {name} is missing required action region "
						f"{region_name}"
					)
		elif name.startswith("ios-touch-joystick-"):
			for region_name in sorted(JOYSTICK_REGIONS - set(resolved["regions"])):
				errors.append(
					f"Chrome collection {name} is missing required joystick region "
					f"{region_name}"
				)
		elif name.startswith("ios-touch-quickbar-toggle-"):
			for region_name in sorted(
				QUICKBAR_TOGGLE_REGIONS - set(resolved["regions"])
			):
				errors.append(
					f"Chrome collection {name} is missing required quickbar toggle "
					f"region {region_name}"
				)
		elif (
			name.startswith("ios-touch-quickbar-button-") or
			name.startswith("ios-touch-quickbar-panel-")
		) and resolved["panel"] is None:
			errors.append(f"Chrome collection {name} is missing its required PanelRegion")
		one_x = asset_sizes[filenames[0]]
		two_x = asset_sizes[filenames[1]]
		if not resolved["regions"] and resolved["panel"] is None:
			errors.append(f"Chrome collection has no usable region or panel: {name}")
		for region_name, region in resolved["regions"].items():
			if not _region_fits(region, one_x, 1) or not _region_fits(region, two_x, 2):
				errors.append(
					f"Chrome region overflow in {name}.{region_name}: {region}"
				)
		if resolved["panel"] is not None:
			if not _panel_fits(resolved["panel"], one_x, 1) or not _panel_fits(
				resolved["panel"], two_x, 2
			):
				errors.append(f"Chrome panel region overflow in {name}: {resolved['panel']}")

	for collection, (filename, atlas_size) in SEMANTIC_COLLECTIONS.items():
		if collection not in collections or collections[collection]["Image"] != filename:
			errors.append(
				f"semantic shortcut collection {collection} no longer uses {filename}"
			)
			continue
		try:
			resolved = _resolve_collection(collection, collections, cache)
		except ValueError as error:
			errors.append(str(error))
			continue
		required_regions = {
			image_name
			for image_collection, image_name in SEMANTIC_SHORTCUTS.values()
			if image_collection == collection
		}
		for region_name in sorted(required_regions - set(resolved["regions"])):
			errors.append(
				f"semantic shortcut region is missing from {collection}: {region_name}"
			)
		for region_name in sorted(required_regions & set(resolved["regions"])):
			region = resolved["regions"][region_name]
			if not _region_fits(region, atlas_size, 1):
				errors.append(
					f"semantic shortcut region overflow in {collection}.{region_name}: "
					f"{region}"
				)


def _direct_node_values(text: str, marker: str) -> set[str]:
	block = _find_top_level_node_block(text, marker)
	if block is None:
		return set()
	parent_indent = _indentation(block[0])
	children = [
		line.split("#", 1)[0].strip()
		for line in block[1:]
		if line.split("#", 1)[0].strip() and _indentation(line) > parent_indent
	]
	if not children:
		return set()
	direct_indent = min(
		_indentation(line)
		for line in block[1:]
		if line.split("#", 1)[0].strip() and _indentation(line) > parent_indent
	)
	return {
		line.split("#", 1)[0].strip()
		for line in block[1:]
		if line.split("#", 1)[0].strip() and _indentation(line) == direct_indent
	}


def _direct_node_properties(text: str, marker: str) -> dict[str, str]:
	properties = {}
	for active in _direct_node_values(text, marker):
		if ":" not in active:
			continue
		key, value = (part.strip() for part in active.split(":", 1))
		if key and value:
			properties[key] = value
	return properties


def _fluent_label_values(text: str) -> dict[str, str]:
	labels = {}
	current = None
	for raw_line in text.splitlines():
		line = raw_line.split("#", 1)[0]
		if line and not line[0].isspace() and "=" in line:
			current = line.split("=", 1)[0].strip()
			continue
		stripped = line.strip()
		if current is not None and stripped.startswith(".label") and "=" in stripped:
			labels[current] = stripped.split("=", 1)[1].strip()
	return labels


def _fluent_message_values(text: str) -> tuple[dict[str, str], dict[str, int]]:
	values = {}
	counts = {}
	for raw_line in text.splitlines():
		line = raw_line.split("#", 1)[0].rstrip()
		if not line or line[0].isspace() or "=" not in line:
			continue
		key, value = (part.strip() for part in line.split("=", 1))
		if not key:
			continue
		counts[key] = counts.get(key, 0) + 1
		values[key] = value
	return values, counts


def _audit_lobby_status_widgets(
	bundled_text: dict[Path, str], errors: list[str]
) -> None:
	lobby_players = bundled_text.get(Path("mods/common/chrome/lobby-players.yaml"))
	if lobby_players is None:
		return
	players = _follow_widget_path(
		lobby_players,
		"Container@LOBBY_PLAYER_BIN:",
		("ScrollPanel@LOBBY_PLAYERS:",),
	)
	player_children = _find_direct_child_block(players, "Children:")
	for template_name in ("TEMPLATE_EDITABLE_PLAYER", "TEMPLATE_NONEDITABLE_PLAYER"):
		template = _find_direct_child_block(
			player_children, f"Container@{template_name}:"
		)
		template_children = _find_direct_child_block(template, "Children:")
		for widget_id in ("IOS_MAP_STATUS_TEXT", "IOS_MAP_STATUS_ICON"):
			status = _find_direct_child_block(
				template_children, f"Label@{widget_id}:"
			)
			if status is None or _node_property(status, "Visible") != "false":
				errors.append(
					"default-hidden iOS map status widget is missing or visible: "
					f"{template_name}/{widget_id}"
				)

	for relative, expected_messages in IOS_LOBBY_FLUENT.items():
		fluent = bundled_text.get(relative)
		if fluent is None:
			continue
		values, counts = _fluent_message_values(fluent)
		locale = "zh-CN" if "zh-CN" in relative.parts else "English"
		for key, expected in expected_messages.items():
			if counts.get(key) != 1 or values.get(key) != expected:
				errors.append(
					f"{locale} lobby blocker {key} must be {expected!r}; "
					f"found {values.get(key)!r}"
				)


def _audit_configuration_semantics(
	bundled_text: dict[Path, str], errors: list[str]
) -> None:
	_audit_lobby_status_widgets(bundled_text, errors)
	manifest = bundled_text.get(Path("mods/ra2/mod.yaml"))
	if manifest is not None:
		for section, required in MOD_MANIFEST_WIRING.items():
			active = _direct_node_values(manifest, f"{section}:")
			for value in sorted(required):
				if f"-{value}" in active:
					errors.append(
						f"mod manifest removal disables required {section}: {value}"
					)
			for value in sorted(required - active):
				errors.append(
					f"mod manifest wiring is missing {section}: {value}"
				)
			if section in EXACT_MANIFEST_SECTIONS:
				unexpected = {
					value for value in active
					if not value.startswith("-") and value not in required
				}
				for value in sorted(unexpected):
					errors.append(
						f"unexpected manifest wiring in {section}: {value}"
					)
	metrics = bundled_text.get(Path("mods/ra2/metrics.yaml"))
	if metrics is not None:
		values = _direct_node_properties(metrics, "Metrics:")
		for key, expected in TOUCH_FACTION_METRICS.items():
			if values.get(key) != expected:
				errors.append(
					f"touch faction metric {key} must remain {expected}; "
					f"found {values.get(key)!r}"
				)

	settings = bundled_text.get(Path("mods/common/chrome/settings-input.yaml"))
	if settings is not None:
		container = _follow_widget_path(
			settings,
			"Container@INPUT_PANEL:",
			(
				"ScrollPanel@SETTINGS_SCROLLPANEL:",
				"Container@TOUCH_JOYSTICK_SIZE_CONTAINER:",
			),
		)
		if container is None:
			errors.append(
				"touch-size settings UI is missing its active "
				"Container@TOUCH_JOYSTICK_SIZE_CONTAINER"
			)
		else:
			children = _find_direct_child_block(container, "Children:")
			dropdown = _find_direct_child_block(
				children, "DropDownButton@TOUCH_JOYSTICK_SIZE_DROPDOWN:"
			)
			if dropdown is None:
				errors.append(
					"touch-size settings UI is missing its active "
					"DropDownButton@TOUCH_JOYSTICK_SIZE_DROPDOWN"
				)
	for relative in (
		Path("mods/common/fluent/common.ftl"),
		Path("mods/common/fluent/zh-CN/common.ftl"),
	):
		fluent = bundled_text.get(relative)
		if fluent is None:
			continue
		active_fluent = "\n".join(line.split("#", 1)[0] for line in fluent.splitlines())
		for marker in (
			"label-touch-joystick-size-container =",
			"options-touch-joystick-size =",
			".small =", ".medium =", ".large =", "112 pt", "128 pt", "144 pt",
		):
			if marker not in active_fluent:
				errors.append(f"touch-size settings UI localization is missing {marker}: {relative}")

	for relative, value_index in (
		(Path("mods/ra2/fluent/chrome.ftl"), 1),
		(Path("mods/ra2/fluent/zh-CN/chrome.ftl"), 2),
	):
		fluent = bundled_text.get(relative)
		if fluent is None:
			continue
		labels = _fluent_label_values(fluent)
		for reference, english, chinese in ACTION_LABELS.values():
			message = reference.removesuffix(".label")
			expected = (reference, english, chinese)[value_index]
			if labels.get(message) != expected:
				errors.append(
					f"viewport action localization {message} must be {expected!r}; "
					f"found {labels.get(message)!r}: {relative}"
				)


def _audit_active_layouts(bundled_text: dict[Path, str], errors: list[str]) -> None:
	ingame = bundled_text.get(Path("mods/ra2/chrome/ingame.yaml"))
	chrome = bundled_text.get(Path("mods/ra2/chrome.yaml"))
	action_regions = {}
	if chrome is not None:
		collections, _ = _parse_chrome_collections(chrome)
		if "ios-touch-actions-allies" in collections:
			try:
				action_regions = _resolve_collection(
					"ios-touch-actions-allies", collections, {}
				)["regions"]
			except ValueError:
				pass
	if ingame is not None:
		actions = _follow_widget_path(
			ingame,
			"Container@INGAME_ROOT:",
			(
				"Container@WORLD_ROOT:",
				"Container@IOS_VIEWPORT_ACTIONS:",
			),
		)
		if actions is None:
			errors.append("active action container IOS_VIEWPORT_ACTIONS is missing")
			action_children = None
		else:
			action_children = _find_direct_child_block(actions, "Children:")
		action_buttons = tuple(
			marker.split(":", 1)[0][len("Button@"):]
			for marker in _direct_child_markers(action_children)
			if marker.startswith("Button@")
		)
		if action_buttons != ACTIVE_VIEWPORT_ACTIONS:
			errors.append(
				"active action buttons must be exactly "
				f"{ACTIVE_VIEWPORT_ACTIONS}; found {action_buttons}"
			)
		for button in ACTIVE_VIEWPORT_ACTIONS:
			image_name = ACTION_ATLAS_GLYPHS[button]
			button_block = _find_direct_child_block(
				action_children, f"Button@{button}:"
			)
			children_block = _find_direct_child_block(button_block, "Children:")
			child_markers = _direct_child_markers(children_block)
			if child_markers != ("Image@ICON:", "Label@LABEL:"):
				errors.append(
					f"active action children for {button} must be exactly "
					f"Image@ICON then Label@LABEL; found {child_markers}"
				)
			icon_block = _find_direct_child_block(
				children_block, "Image@ICON:"
			)
			label_block = _find_direct_child_block(
				children_block, "Label@LABEL:"
			)
			collection = _node_property(icon_block, "ImageCollection")
			image = _node_property(icon_block, "ImageName")
			if collection != "ios-touch-actions-allies" or image != image_name:
				errors.append(
					f"active action fallback for {button} must use "
					f"ios-touch-actions-allies/{image_name}"
				)
			if image_name not in action_regions:
				errors.append(
					f"active action fallback region is missing for {button}: {image_name}"
				)

			expected_label = ACTION_LABELS[button][0]
			actual_label = _node_property(label_block, "Text")
			if actual_label != expected_label:
				errors.append(
					f"active action label for {button} must use {expected_label}; "
					f"found {actual_label!r}"
				)
			for property_name, expected in (
				("Align", "Center"),
				("VAlign", "Middle"),
				("Font", "TinyBold"),
				("TextColor", "F8F4E8"),
				("Contrast", "true"),
				("ContrastRadius", "1"),
			):
				actual = _node_property(label_block, property_name)
				if actual != expected:
					errors.append(
					f"active action label {button}.{property_name} must be "
					f"{expected}; found {actual!r}"
				)
			if (
				_node_property(icon_block, "IgnoreMouseOver") != "true" or
				_node_property(label_block, "IgnoreMouseOver") != "true"
			):
				errors.append(
					f"active action input for {button} must keep icon and label "
					"IgnoreMouseOver true"
				)

	player = bundled_text.get(Path("mods/ra2/chrome/ingame-player.yaml"))
	if player is None:
		return
	command_block = _follow_widget_path(
		player,
		"Container@PLAYER_WIDGETS:",
		(
			"CustomCommandBar@COMMAND_BAR_BACKGROUND:",
			"Container@COMMAND_SLOTS:",
		),
	)
	if command_block is None:
		errors.append("COMMAND_SLOTS container is missing from the final app")
		return
	command_children = _find_direct_child_block(command_block, "Children:")
	if command_children is None:
		errors.append("COMMAND_SLOTS Children node is missing from the final app")
		return
	order = tuple(
		marker.split(":", 1)[0][len("Button@"):]
		for marker in _direct_child_markers(command_children)
		if marker.startswith("Button@")
	)
	if order != COMMAND_SLOTS:
		errors.append(
			f"command slot order changed: expected {COMMAND_SLOTS}, found {order}"
		)
	for slot, expected in SEMANTIC_SHORTCUTS.items():
		button_block = _find_direct_child_block(
			command_children, f"Button@{slot}:"
		)
		children_block = _find_direct_child_block(button_block, "Children:")
		icon_block = _find_direct_child_block(
			children_block, "Image@ICON:"
		)
		actual = (
			_node_property(icon_block, "ImageCollection"),
			_node_property(icon_block, "ImageName"),
		)
		if actual != expected:
			errors.append(
				f"semantic shortcut {slot} changed: expected {expected}, found {actual}"
			)


def _audit_semantic_atlases(app_bundle: Path, source_root: Path, errors: list[str]) -> None:
	for name, expected_sha in SEMANTIC_ATLAS_SHA256.items():
		source = source_root / "mods/ra2/uibits" / name
		try:
			bundled = _bundle_member(app_bundle, Path("mods/ra2/uibits") / name)
		except ValueError as error:
			errors.append(str(error))
			continue
		if not source.is_file():
			errors.append(f"canonical semantic shortcut atlas is missing: {source}")
		if source.is_file() and _sha256(source) != expected_sha:
			errors.append(f"frozen semantic atlas hash changed: {name}")
		if not bundled.is_file():
			errors.append(f"semantic shortcut atlas is missing from final app: {bundled}")
		else:
			if _sha256(bundled) != expected_sha:
				errors.append(f"frozen semantic atlas hash changed in final app: {name}")
			if source.is_file() and _sha256(source) != _sha256(bundled):
				errors.append(f"semantic atlas hash mismatch: {name}")


def _audit_metadata_and_aot(
	app_bundle: Path,
	managed_output_root: Path | None,
	errors: list[str],
) -> None:
	for assembly_name, required in ASSEMBLY_METADATA.items():
		try:
			bundled = _bundle_member(app_bundle, assembly_name)
		except ValueError as error:
			errors.append(str(error))
			continue
		if not bundled.is_file():
			errors.append(f"managed assembly is missing from final app: {bundled}")
			continue
		if managed_output_root is not None:
			managed = managed_output_root / assembly_name
			if not managed.is_file():
				errors.append(f"managed build output is missing: {managed}")
			elif _sha256(managed) != _sha256(bundled):
				errors.append(f"managed output hash mismatch: {assembly_name}")
		try:
			evidence = _pe_metadata_evidence(bundled)
		except (OSError, ValueError, struct.error) as error:
			errors.append(f"unable to parse CLR metadata for {assembly_name}: {error}")
			continue
		definitions = evidence["definitions"]
		for type_name, members in required["definitions"].items():
			definition = definitions.get(type_name)
			if definition is None:
				errors.append(
					f"missing TypeDef metadata definition in {assembly_name}: {type_name}"
				)
				continue
			if evidence["definition_counts"].get(type_name) != 1:
				errors.append(
					f"duplicate TypeDef metadata definition in {assembly_name}: {type_name}"
				)
				continue
			for field in sorted(set(members.get("fields", ())) - definition["fields"]):
				errors.append(
					f"missing Field metadata definition in {assembly_name}: "
					f"{type_name}::{field}"
				)
			for method in sorted(set(members.get("methods", ())) - definition["methods"]):
				errors.append(
					f"missing MethodDef metadata definition in {assembly_name}: "
					f"{type_name}::{method}"
				)
		ldstr_user_strings = evidence["ldstr_user_strings"]
		for owner_method, tokens in required.get("user_string_bindings", {}).items():
			method_strings = evidence["method_ldstr"].get(owner_method, set())
			for token in sorted(tokens - method_strings):
				owner, method = owner_method
				if assembly_name == "OpenRA.iOS.dll":
					errors.append(
						f"missing AOT registry entry referenced by valid ldstr in "
						f"{assembly_name} {owner}::{method}: {token}"
					)
				else:
					errors.append(
						f"missing runtime literal ldstr binding in {assembly_name} "
						f"{owner}::{method}: {token}"
					)
		for token in sorted(required["user_strings"] - ldstr_user_strings):
			if assembly_name == "OpenRA.iOS.dll" and token.startswith("OpenRA."):
				errors.append(
					f"missing AOT registry entry referenced by valid ldstr in "
					f"{assembly_name}: {token}"
				)
			else:
				errors.append(
					f"missing #US metadata token referenced by valid ldstr in "
					f"{assembly_name}: {token}"
				)
		if assembly_name == "OpenRA.Mods.Common.dll":
			legacy = sorted(
				LEGACY_COMPILED_JOYSTICK_LITERALS & ldstr_user_strings
			)
			for token in legacy:
				errors.append(f"legacy joystick literal remains compiled as an active default: {token}")

		aot_name = assembly_name.replace(".dll", ".aotdata.arm64")
		try:
			aot = _bundle_member(app_bundle, aot_name)
		except ValueError as error:
			errors.append(str(error))
			continue
		if not aot.is_file():
			errors.append(f"AOT data is missing from final app: {aot_name}")
		elif aot.stat().st_size == 0:
			errors.append(f"AOT data is empty in final app: {aot_name}")


def _audit_native_executable(app_bundle: Path, errors: list[str]) -> None:
	try:
		plist_path = _bundle_member(app_bundle, "Info.plist")
	except ValueError as error:
		errors.append(str(error))
		return
	if not plist_path.is_file():
		errors.append(f"Info.plist is missing from final app: {plist_path}")
		return
	try:
		with plist_path.open("rb") as stream:
			metadata = plistlib.load(stream)
	except (OSError, plistlib.InvalidFileException) as error:
		errors.append(f"unable to read final Info.plist: {error}")
		return
	if not isinstance(metadata, dict):
		errors.append("final Info.plist root must be a dictionary")
		return
	executable_name = metadata.get("CFBundleExecutable")
	if not isinstance(executable_name, str) or not executable_name or Path(executable_name).name != executable_name:
		errors.append("Info.plist has an invalid CFBundleExecutable")
		return
	try:
		executable = _bundle_member(app_bundle, executable_name)
	except ValueError as error:
		errors.append(str(error))
		return
	if not executable.is_file():
		errors.append(f"native executable is missing from final app: {executable}")
		return
	try:
		binary = executable.read_bytes()
	except OSError as error:
		errors.append(f"native executable is not an arm64 Mach-O: {error}")
		return
	if len(binary) < 32:
		errors.append(
			f"truncated arm64 Mach-O header: expected at least 32 bytes, got {len(binary)}"
		)
		return
	try:
		magic, cpu_type, _, file_type, command_count, command_size, _, _ = struct.unpack(
			"<IiiIIIII", binary[:32]
		)
	except struct.error as error:
		errors.append(f"native executable is not an arm64 Mach-O: {error}")
		return
	if magic != 0xFEEDFACF or cpu_type != 0x0100000C:
		errors.append(
			f"native executable is not a thin arm64 Mach-O: magic=0x{magic:x}, cpu={cpu_type}"
		)
		return
	if file_type != 2:
		errors.append(f"native Mach-O filetype must be MH_EXECUTE (2), got {file_type}")
	if command_count == 0 or command_size < 8:
		errors.append(
			"native Mach-O executable must contain at least one load command: "
			f"count={command_count}, size={command_size}"
		)
		return
	if command_size < command_count * 8 or len(binary) < 32 + command_size:
		errors.append(
			f"truncated arm64 Mach-O load commands: count={command_count}, "
			f"size={command_size}, file={len(binary)}"
		)
		return
	commands_end = 32 + command_size
	cursor = 32
	has_segment_64 = False
	has_entrypoint = False
	main_entry_offset = None
	unix_thread_pc = None
	file_segments: list[tuple[int, int]] = []
	executable_file_segments: list[tuple[int, int]] = []
	executable_virtual_segments: list[tuple[int, int]] = []
	for index in range(command_count):
		if cursor + 8 > commands_end:
			errors.append(f"malformed Mach-O load command table at command {index}")
			return
		command, size = struct.unpack_from("<II", binary, cursor)
		if size < 8 or size % 8 or cursor + size > commands_end:
			errors.append(
				f"malformed Mach-O load command {index}: size={size}, "
				f"remaining={commands_end - cursor}"
			)
			return
		if command == 0x19:
			if size < 72:
				errors.append(
					f"malformed LC_SEGMENT_64 command {index}: size={size}, minimum=72"
				)
				return
			virtual_address, virtual_size = struct.unpack_from("<QQ", binary, cursor + 24)
			file_offset, file_size = struct.unpack_from("<QQ", binary, cursor + 40)
			initial_protection = struct.unpack_from("<i", binary, cursor + 60)[0]
			section_count = struct.unpack_from("<I", binary, cursor + 64)[0]
			expected_size = 72 + section_count * 80
			if size != expected_size:
				errors.append(
					f"malformed LC_SEGMENT_64 command {index}: size={size}, "
					f"expected={expected_size} for {section_count} sections"
				)
				return
			if file_offset > len(binary) or file_size > len(binary) - file_offset:
				errors.append(
					f"Mach-O segment file range is out of bounds at command {index}: "
					f"offset={file_offset}, size={file_size}, file={len(binary)}"
				)
				return
			has_segment_64 = True
			if file_size:
				file_segments.append((file_offset, file_offset + file_size))
				if initial_protection & 4:
					executable_file_segments.append(
						(file_offset, file_offset + file_size)
					)
			if virtual_size and initial_protection & 4:
				executable_virtual_segments.append(
					(virtual_address, virtual_address + virtual_size)
				)
		elif command == 0x80000028:
			if size < 24:
				errors.append(
					f"malformed LC_MAIN command {index}: size={size}, minimum=24"
				)
				return
			main_entry_offset = struct.unpack_from("<Q", binary, cursor + 8)[0]
			has_entrypoint = True
		elif command == 0x5:
			if size != 288:
				errors.append(
					f"malformed arm64 LC_UNIXTHREAD command {index}: "
					f"size={size}, expected=288"
				)
				return
			flavor, state_word_count = struct.unpack_from("<II", binary, cursor + 8)
			if flavor != 6 or state_word_count != 68:
				errors.append(
					f"malformed arm64 LC_UNIXTHREAD state at command {index}: "
					f"flavor={flavor}, words={state_word_count}"
				)
				return
			unix_thread_pc = struct.unpack_from("<Q", binary, cursor + 272)[0]
			has_entrypoint = True
		cursor += size
	if cursor != commands_end:
		errors.append(
			f"malformed Mach-O load command table: parsed={cursor - 32}, "
			f"declared={command_size}"
		)
		return
	if not has_segment_64 or not file_segments:
		errors.append(
			"native Mach-O executable must contain a file-backed LC_SEGMENT_64 command"
		)
	if not executable_file_segments:
		errors.append(
			"native Mach-O executable must contain a file-backed executable segment"
		)
	if not has_entrypoint:
		errors.append(
			"native Mach-O executable must contain LC_MAIN or LC_UNIXTHREAD"
		)
	elif main_entry_offset is not None and not any(
		start <= main_entry_offset < end for start, end in executable_file_segments
	):
		errors.append(
			f"Mach-O LC_MAIN entry offset is outside executable file-backed segments: "
			f"{main_entry_offset}"
		)
	elif unix_thread_pc is not None and not any(
		start <= unix_thread_pc < end
		for start, end in executable_virtual_segments
	):
		errors.append(
			f"Mach-O LC_UNIXTHREAD PC is outside mapped segments: {unix_thread_pc}"
		)


def _audit_concept_leaks(
	app_bundle: Path, bundle_paths: tuple[Path, ...], errors: list[str]
) -> None:
	for path in bundle_paths:
		relative = path.relative_to(app_bundle).as_posix().casefold()
		if any(marker in relative for marker in CONCEPT_PATH_MARKERS):
			errors.append(f"concept artifact leaked into final app: {relative}")


def audit_bundle(
	app_bundle: Path | str,
	source_root: Path | str,
	managed_output_root: Path | str | None = None,
) -> tuple[str, ...]:
	"""Return every final-artifact contract violation without modifying the bundle."""
	app_bundle = Path(app_bundle)
	source_root = Path(source_root)
	managed_output = Path(managed_output_root) if managed_output_root is not None else None
	errors: list[str] = []
	path_error = _validate_app_bundle_path(app_bundle)
	if path_error is not None:
		return (path_error,)
	if not source_root.is_dir():
		return (f"source root is missing: {source_root}",)
	if managed_output is not None and not managed_output.is_dir():
		errors.append(f"managed output root is missing: {managed_output}")

	bundle_paths = _enumerate_bundle_paths(app_bundle, errors)
	_audit_assets(app_bundle, source_root, bundle_paths, errors)
	_audit_ios_lobby_source_contracts(source_root, errors)
	bundled_text = _audit_configuration_parity(app_bundle, source_root, errors)
	_audit_configuration_semantics(bundled_text, errors)
	_audit_chrome(app_bundle, bundled_text, errors)
	_audit_active_layouts(bundled_text, errors)
	_audit_semantic_atlases(app_bundle, source_root, errors)
	_audit_metadata_and_aot(app_bundle, managed_output, errors)
	_audit_native_executable(app_bundle, errors)
	_audit_concept_leaks(app_bundle, bundle_paths, errors)
	return tuple(errors)


def _parser() -> argparse.ArgumentParser:
	parser = argparse.ArgumentParser(
		description="Audit final iOS faction touch-control resources and runtime metadata."
	)
	parser.add_argument("app_bundle", type=Path, help="finished .app bundle")
	parser.add_argument("--source-root", type=Path, required=True, help="repository root")
	parser.add_argument(
		"--managed-output-root", type=Path,
		help="directory containing the four managed build-output DLLs",
	)
	return parser


def main(argv: list[str] | None = None) -> int:
	args = _parser().parse_args(argv)
	errors = audit_bundle(args.app_bundle, args.source_root, args.managed_output_root)
	if errors:
		for error in errors:
			print(f"ERROR: {error}", file=sys.stderr)
		return 1
	print(f"iOS touch runtime bundle audit passed: {args.app_bundle}")
	return 0


if __name__ == "__main__":
	raise SystemExit(main())
