import binascii
import copy
import plistlib
import struct
import subprocess
import tempfile
import unittest
import xml.etree.ElementTree as ElementTree
import zlib
from pathlib import Path

from packaging.audit_ios_touch_runtime_bundle import audit_bundle


ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / "packaging" / "audit_ios_touch_runtime_bundle.py"
PROJECT = ROOT / "ios" / "OpenRA.iOS" / "OpenRA.iOS.csproj"

FACTIONS = ("allies", "soviets", "yuri")
ASSET_SIZES = {
	"actions": ((512, 512), (1024, 1024)),
	"joystick": ((256, 256), (512, 512)),
	"quickbar": ((512, 128), (1024, 256)),
}
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
ACTION_LABEL_MESSAGES = {
	"STOP": ("button-ios-viewport-action-stop.label", "Stop", "停止"),
	"DEPLOY": ("button-ios-viewport-action-deploy.label", "Deploy", "部署"),
	"SELECT_TYPE": ("button-ios-viewport-action-select-type.label", "Type", "同类"),
	"FORCE_ATTACK": ("button-ios-viewport-action-force-attack.label", "Force", "强攻"),
	"RETURN_BASE": ("button-ios-viewport-action-return-base.label", "Base", "基地"),
}

def assembly_contract(types, user_string_bindings=None):
	user_string_bindings = user_string_bindings or {}
	strings = set(types)
	for members in types.values():
		strings.update(members.get("fields", ()))
		strings.update(members.get("methods", ()))
	return {
		"definitions": types,
		"strings": strings,
		"user_strings": {
			value for values in user_string_bindings.values() for value in values
		},
		"user_string_bindings": user_string_bindings,
	}


ASSEMBLY_METADATA = {
	"OpenRA.Game.dll": assembly_contract({
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
	}),
	"OpenRA.Mods.Common.dll": assembly_contract({
		"OpenRA.Mods.Common.Widgets.TouchFactionSkin": {"methods": {
			"Resolve", "ResolveForPlayer", "ActionCollection", "JoystickCollection",
			"QuickbarPanelCollection", "QuickbarButtonCollection",
			"QuickbarToggleCollection",
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
		"OpenRA.Mods.Common.Widgets.Logic.Ingame.IosSupportPowerCellPolicy": {"methods": {
			"TryGetCells", "ShouldHandleMouseDown", "CenterArt",
		}},
		"OpenRA.Mods.Common.Widgets.Logic.Ingame.IosSupportPowerTooltipPolicy": {"methods": {
			"ClampOrigin", "MaximumSize", "FitText",
		}},
		"OpenRA.Mods.Common.Widgets.Logic.Ingame.IosViewportActionsLogic": {},
		"OpenRA.Mods.Common.Widgets.Logic.SupportPowerBinLogic": {},
		"OpenRA.Mods.Common.Widgets.Logic.SupportPowerTooltipLogic": {},
		"OpenRA.Mods.Common.Widgets.SupportPowersWidget": {},
		"OpenRA.Mods.Common.Widgets.TooltipContainerWidget": {},
		"OpenRA.Mods.Common.Widgets.VirtualViewportJoystickWidget": {
			"methods": {".ctor"},
		},
	}, {
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
		("OpenRA.Mods.Common.Widgets.VirtualViewportJoystickWidget", ".ctor"):
			{"ios-touch-joystick-allies"},
		(
			"OpenRA.Mods.Common.Widgets.Logic.InputSettingsLogic",
			"BindIosVirtualJoystickSizeDropdown",
		): {"TOUCH_JOYSTICK_SIZE_DROPDOWN"},
	}),
	"OpenRA.Mods.RA2.dll": assembly_contract({
		"OpenRA.Mods.RA2.Widgets.CustomCommandBarWidget": {"methods": {
			"PanelBackgroundFor", "ButtonBackgroundFor", "ToggleCollectionFor",
			"ShouldRefreshTouchChrome", "ApplyTouchChrome",
		}},
	}),
	"OpenRA.iOS.dll": assembly_contract({
		"OpenRA.iOS.GeneratedAotObjectRegistryRegistration": {
			"fields": {"RegisteredTypeNames"},
			"methods": {".cctor"},
		},
	}, {
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
	}),
}

USER_STRING_METHODS = {
	value: owner_method
	for metadata in ASSEMBLY_METADATA.values()
	for owner_method, values in metadata["user_string_bindings"].items()
	for value in values
}


def _align(value, alignment=4):
	return (value + alignment - 1) & ~(alignment - 1)


def _png_chunk(kind, payload):
	return (
		struct.pack(">I", len(payload)) + kind + payload +
		struct.pack(">I", binascii.crc32(kind + payload) & 0xFFFFFFFF)
	)


def png_bytes(width, height, seed=0, bit_depth=8, color_type=6):
	pixel = bytes(((seed * 31 + 17) & 0xFF, (seed * 47 + 23) & 0xFF,
		(seed * 59 + 29) & 0xFF, 0xFF))
	raw = (b"\x00" + pixel * width) * height
	ihdr = struct.pack(">IIBBBBB", width, height, bit_depth, color_type, 0, 0, 0)
	return (
		b"\x89PNG\r\n\x1a\n" + _png_chunk(b"IHDR", ihdr) +
		_png_chunk(b"IDAT", zlib.compress(raw, 9)) + _png_chunk(b"IEND", b"")
	)


def _compressed_uint(value):
	if value < 0x80:
		return bytes((value,))
	if value < 0x4000:
		return bytes((0x80 | (value >> 8), value & 0xFF))
	return bytes((
		0xC0 | (value >> 24), (value >> 16) & 0xFF,
		(value >> 8) & 0xFF, value & 0xFF,
	))


def _string_heap(values):
	heap = bytearray(b"\x00")
	indices = {}
	for value in sorted(values):
		indices[value] = len(heap)
		heap.extend(value.encode("utf-8") + b"\x00")
	return bytes(heap), indices


def _user_string_heap(values):
	user_heap = bytearray(b"\x00")
	indices = {}
	for value in sorted(values):
		indices[value] = len(user_heap)
		payload = value.encode("utf-16le") + b"\x00"
		user_heap.extend(_compressed_uint(len(payload)))
		user_heap.extend(payload)
	return bytes(user_heap), indices


def _definition_rows(definitions, include_reference_method):
	fields = []
	methods = []
	types = []
	for index, (full_name, members) in enumerate(sorted(definitions.items())):
		namespace, _, type_name = full_name.rpartition(".")
		field_start = len(fields) + 1
		method_start = len(methods) + 1
		fields.extend((full_name, name) for name in sorted(members.get("fields", ())))
		methods.extend((full_name, name) for name in sorted(members.get("methods", ())))
		if include_reference_method and index == 0:
			methods.append((full_name, "ContractUserStrings"))
		types.append((full_name, type_name, namespace, field_start, method_start))
	return types, fields, methods


def _tables_stream(
	definitions,
	string_indices,
	method_rvas,
	include_reference_method,
	type_references=(),
	unoptimized=False,
	type_list_overrides=None,
	field_pointer_overrides=None,
	method_pointer_overrides=None,
):
	type_list_overrides = type_list_overrides or {}
	field_pointer_overrides = field_pointer_overrides or {}
	method_pointer_overrides = method_pointer_overrides or {}
	types, fields, methods = _definition_rows(definitions, include_reference_method)
	valid_tables = [0, 1, 2, 4, 6]
	if unoptimized:
		valid_tables.extend((3, 5))
	valid = sum(1 << table for table in valid_tables)
	stream = bytearray(struct.pack("<IBBBBQQ", 0, 2, 0, 0, 1, valid, 0))
	counts = {
		0: 1,
		1: 1 + len(type_references),
		2: len(types) + 1,
		3: len(fields),
		4: len(fields),
		5: len(methods),
		6: len(methods),
	}
	for table in sorted(valid_tables):
		stream.extend(struct.pack("<I", counts[table]))

	stream.extend(struct.pack(
		"<HHHHH", 0, string_indices["Synthetic.dll"], 1, 0, 0
	))
	stream.extend(struct.pack(
		"<HHH", 4, string_indices["Object"], string_indices["System"]
	))
	for full_name in sorted(type_references):
		namespace, _, type_name = full_name.rpartition(".")
		stream.extend(struct.pack(
			"<HHH", 4, string_indices[type_name], string_indices[namespace]
		))
	stream.extend(struct.pack(
		"<IHHHHH", 0, string_indices["<Module>"], 0, 0, 1, 1
	))
	for full_name, type_name, namespace, field_start, method_start in types:
		field_start, method_start = type_list_overrides.get(
			full_name, (field_start, method_start)
		)
		stream.extend(struct.pack(
			"<IHHHHH", 0x00100001, string_indices.get(type_name, 0),
			string_indices.get(namespace, 0), 5, field_start, method_start,
		))
	if unoptimized:
		for rid in range(1, len(fields) + 1):
			stream.extend(struct.pack("<H", field_pointer_overrides.get(rid, rid)))
	for _, field_name in fields:
		stream.extend(struct.pack(
			"<HHH", 0x0006, string_indices.get(field_name, 0), 1
		))
	if unoptimized:
		for rid in range(1, len(methods) + 1):
			stream.extend(struct.pack("<H", method_pointer_overrides.get(rid, rid)))
	for rva, (_, method_name) in zip(method_rvas, methods):
		stream.extend(struct.pack(
			"<IHHHHH", rva, 0, 0x0016, string_indices.get(method_name, 0), 4, 1
		))
	return bytes(stream), methods


def _metadata_root(
	strings,
	user_strings,
	definitions,
	method_rvas,
	include_tables=True,
	type_references=(),
	unoptimized=False,
	type_list_overrides=None,
	field_pointer_overrides=None,
	method_pointer_overrides=None,
):
	types, fields, methods = _definition_rows(definitions, bool(user_strings))
	all_strings = set(strings) | {
		"<Module>", "Synthetic.dll", "Object", "System", "Synthetic",
	}
	for full_name in definitions:
		namespace, _, type_name = full_name.rpartition(".")
		all_strings.update((namespace, type_name))
	for full_name in type_references:
		namespace, _, type_name = full_name.rpartition(".")
		all_strings.update((namespace, type_name))
	if user_strings:
		all_strings.add("ContractUserStrings")
	strings_heap, string_indices = _string_heap(all_strings)
	user_heap, user_indices = _user_string_heap(user_strings)
	blob_heap = b"\x00\x02\x06\x08\x03\x00\x00\x01"
	guid_heap = bytes(range(16))
	streams = [
		("#Strings", strings_heap),
		("#US", user_heap),
		("#Blob", blob_heap),
		("#GUID", guid_heap),
	]
	if include_tables:
		tables, parsed_methods = _tables_stream(
			definitions, string_indices, method_rvas, bool(user_strings),
			type_references, unoptimized, type_list_overrides,
			field_pointer_overrides, method_pointer_overrides,
		)
		self_check_methods = [name for _, name in methods]
		if [name for _, name in parsed_methods] != self_check_methods:
			raise AssertionError("synthetic MethodDef order changed")
		streams.append(("#-" if unoptimized else "#~", tables))

	version = b"v4.0.30319\x00"
	prefix = bytearray(struct.pack("<IHHII", 0x424A5342, 1, 1, 0, len(version)))
	prefix.extend(version)
	prefix.extend(b"\x00" * (_align(len(prefix)) - len(prefix)))
	prefix.extend(struct.pack("<HH", 0, len(streams)))
	header_length = sum(8 + _align(len(name.encode("ascii")) + 1) for name, _ in streams)
	cursor = _align(len(prefix) + header_length)
	stream_offsets = []
	for _, payload in streams:
		stream_offsets.append(cursor)
		cursor = _align(cursor + len(payload))

	root = bytearray(prefix)
	for (name, payload), offset in zip(streams, stream_offsets):
		root.extend(struct.pack("<II", offset, len(payload)))
		encoded = name.encode("ascii") + b"\x00"
		root.extend(encoded)
		root.extend(b"\x00" * (_align(len(encoded)) - len(encoded)))
	root.extend(b"\x00" * (stream_offsets[0] - len(root)))
	for (_, payload), offset in zip(streams, stream_offsets):
		root.extend(b"\x00" * (offset - len(root)))
		root.extend(payload)
	return bytes(root), user_indices, methods


def managed_pe(
	strings,
	user_strings,
	raw_suffix=b"",
	definitions=None,
	referenced_user_strings=None,
	reference_overrides=None,
	operand_decoys=None,
	malformed_methods=(),
	fat_methods=None,
	include_tables=True,
	type_references=(),
	unoptimized=False,
	type_list_overrides=None,
	field_pointer_overrides=None,
	method_pointer_overrides=None,
):
	definitions = definitions or {}
	references = (
		set(user_strings) if referenced_user_strings is None
		else set(referenced_user_strings)
	)
	reference_overrides = reference_overrides or {}
	operand_decoys = operand_decoys or {}
	malformed_methods = set(malformed_methods)
	fat_methods = set(fat_methods or ())
	_, user_indices, methods = _metadata_root(
		strings, user_strings, definitions, [0] * len(_definition_rows(
			definitions, bool(user_strings)
		)[2]), include_tables, type_references, unoptimized,
		type_list_overrides, field_pointer_overrides, method_pointer_overrides,
	)
	method_bodies = []
	for owner, method_name in methods:
		code = bytearray()
		for value in sorted(references & set(user_strings)):
			target = reference_overrides.get(value, USER_STRING_METHODS.get(value))
			if target is None or target[0] not in definitions:
				target = (methods[0][0], "ContractUserStrings")
			if (owner, method_name) == target:
				code.extend(b"\x72" + struct.pack("<I", 0x70000000 | user_indices[value]))
				code.append(0x26)
		for value, target in operand_decoys.items():
			if (owner, method_name) == target:
				trap = b"\x72" + struct.pack(
					"<I", 0x70000000 | user_indices[value]
				) + b"\x00\x00\x00"
				code.extend(b"\x21" + trap + b"\x26")
		if (owner, method_name) in malformed_methods:
			code.append(0x24)
		code.append(0x2A)
		if len(code) >= 64 and (owner, method_name) not in fat_methods:
			raise AssertionError("synthetic tiny IL body overflow")
		if (owner, method_name) in fat_methods:
			method_bodies.append(
				struct.pack("<HHII", 0x3003, 8, len(code), 0) + bytes(code)
			)
		else:
			method_bodies.append(bytes(((len(code) << 2) | 2,)) + bytes(code))

	headers_size = 0x200
	section_raw = 0x200
	section_rva = 0x2000
	metadata_delta = 0x100
	metadata, _, methods = _metadata_root(
		strings, user_strings, definitions, [0] * len(methods), include_tables,
		type_references, unoptimized, type_list_overrides,
		field_pointer_overrides, method_pointer_overrides,
	)
	il_delta = _align(metadata_delta + len(metadata), 4)
	method_rvas = []
	il_blob = bytearray()
	for body in method_bodies:
		aligned = _align(len(il_blob), 4)
		il_blob.extend(b"\x00" * (aligned - len(il_blob)))
		method_rvas.append(section_rva + il_delta + len(il_blob))
		il_blob.extend(body)
	metadata, _, _ = _metadata_root(
		strings, user_strings, definitions, method_rvas, include_tables,
		type_references, unoptimized, type_list_overrides,
		field_pointer_overrides, method_pointer_overrides,
	)
	metadata_raw = section_raw + metadata_delta
	metadata_rva = section_rva + metadata_delta
	section_size = _align(il_delta + len(il_blob), 0x200)
	image = bytearray(headers_size + section_size)

	image[0:2] = b"MZ"
	struct.pack_into("<I", image, 0x3C, 0x80)
	pe = 0x80
	image[pe:pe + 4] = b"PE\x00\x00"
	struct.pack_into("<HHIIIHH", image, pe + 4, 0xAA64, 1, 0, 0, 0, 0xF0, 0x2022)
	optional = pe + 24
	struct.pack_into("<H", image, optional, 0x20B)
	struct.pack_into("<I", image, optional + 56, _align(section_rva + section_size, 0x1000))
	struct.pack_into("<I", image, optional + 60, headers_size)
	struct.pack_into("<I", image, optional + 108, 16)
	cli_directory = optional + 112 + 14 * 8
	struct.pack_into("<II", image, cli_directory, section_rva, 72)
	section = optional + 0xF0
	image[section:section + 8] = b".text\x00\x00\x00"
	struct.pack_into("<IIII", image, section + 8, section_size, section_rva,
		section_size, section_raw)
	struct.pack_into("<I", image, section + 36, 0x60000020)
	struct.pack_into("<IHHII", image, section_raw, 72, 2, 5,
		metadata_rva, len(metadata))
	struct.pack_into("<I", image, section_raw + 16, 1)
	image[metadata_raw:metadata_raw + len(metadata)] = metadata
	image[section_raw + il_delta:section_raw + il_delta + len(il_blob)] = il_blob
	return bytes(image) + raw_suffix


def arm64_macho():
	command_size = 72 + 24
	file_size = 32 + command_size + 4
	segment = struct.pack(
		"<II16sQQQQiiII", 0x19, 72, b"__TEXT", 0, file_size,
		0, file_size, 7, 5, 0, 0,
	)
	main = struct.pack("<IIQQ", 0x80000028, 24, 32 + command_size, 0)
	return struct.pack(
		"<IiiIIIII", 0xFEEDFACF, 0x0100000C, 0, 2, 2, command_size, 0, 0
	) + segment + main + b"\x00\x00\x00\x00"


def touch_chrome_text():
	lines = [
		"ios-commandbar-icons-v2:",
		"\tImage: ios-commandbar-icons-v2.png",
		"\tRegions:",
	]
	for index, name in enumerate((
		"group-1", "group-2", "group-3", "group-4", "group-5",
		"attack-move", "scatter", "queue-orders",
	)):
		lines.append(f"\t\t{name}: {index * 32 + 3}, 3, 26, 26")
	lines.extend((
		"", "production-x5-icon:", "\tImage: production-x5-icon.png",
		"\tRegions:", "\t\tproduction-x5: 3, 3, 26, 26", "",
	))

	action_names = tuple(ACTION_ATLAS_GLYPHS.values())
	rows = ("", "-disabled", "-hover", "-pressed", "-active", "-active-hover", "-active-pressed")
	button_states = (
		"", "-hover", "-pressed", "-highlighted", "-highlighted-hover",
		"-highlighted-pressed", "-disabled", "-highlighted-disabled",
	)
	for faction in FACTIONS:
		lines.extend((
			f"ios-touch-actions-{faction}:",
			f"\tImage: ios-touch-actions-{faction}.png",
			f"\tImage2x: ios-touch-actions-{faction}-2x.png",
			"\tRegions:",
		))
		for column, action in enumerate(action_names):
			for row, suffix in enumerate(rows):
				lines.append(f"\t\t{action}{suffix}: {column * 64}, {row * 64}, 56, 56")
		lines.extend((
			"", f"ios-touch-actions-{faction}-highlighted:",
			f"\tInherits: ios-touch-actions-{faction}", "\tRegions:",
		))
		for column, action in enumerate(action_names):
			for row, suffix in ((4, ""), (5, "-hover"), (6, "-pressed")):
				lines.append(f"\t\t{action}{suffix}: {column * 64}, {row * 64}, 56, 56")
		lines.extend((
			"", f"ios-touch-joystick-{faction}:",
			f"\tImage: ios-touch-joystick-{faction}.png",
			f"\tImage2x: ios-touch-joystick-{faction}-2x.png",
			"\tRegions:", "\t\tbase: 0, 0, 144, 144",
			"\t\tthumb: 160, 0, 64, 64", "\t\tthumb-active: 160, 72, 64, 64", "",
		))
		for index, suffix in enumerate(button_states):
			lines.extend((
				f"ios-touch-quickbar-button-{faction}{suffix}:",
				f"\tImage: ios-touch-quickbar-{faction}.png",
				f"\tImage2x: ios-touch-quickbar-{faction}-2x.png",
				f"\tPanelRegion: {index * 64}, 0, 0, 0, 48, 52, 0, 0",
				"\tPanelSides: Center", "",
			))
		lines.extend((
			f"ios-touch-quickbar-panel-{faction}:",
			f"\tImage: ios-touch-quickbar-{faction}.png",
			f"\tImage2x: ios-touch-quickbar-{faction}-2x.png",
			"\tPanelRegion: 0, 64, 12, 12, 104, 40, 12, 12", "",
			f"ios-touch-quickbar-panel-compact-{faction}:",
			f"\tImage: ios-touch-quickbar-{faction}.png",
			f"\tImage2x: ios-touch-quickbar-{faction}-2x.png",
			"\tPanelRegion: 128, 64, 12, 12, 104, 40, 12, 12", "",
			f"ios-touch-quickbar-toggle-{faction}:",
			f"\tImage: ios-touch-quickbar-{faction}.png",
			f"\tImage2x: ios-touch-quickbar-{faction}-2x.png",
			"\tRegions:", "\t\tcollapse: 259, 67, 26, 26",
			"\t\texpand: 291, 67, 26, 26", "",
		))
	return "\n".join(lines)


def active_action_button_lines(button):
	image = ACTION_ATLAS_GLYPHS[button]
	label, _, _ = ACTION_LABEL_MESSAGES[button]
	return (
		f"\t\t\t\t\t\tButton@{button}:",
		"\t\t\t\t\t\t\tChildren:",
		"\t\t\t\t\t\t\t\tImage@ICON:",
		"\t\t\t\t\t\t\t\t\tImageCollection: ios-touch-actions-allies",
		f"\t\t\t\t\t\t\t\t\tImageName: {image}",
		"\t\t\t\t\t\t\t\t\tIgnoreMouseOver: true",
		"\t\t\t\t\t\t\t\tLabel@LABEL:",
		f"\t\t\t\t\t\t\t\t\tText: {label}",
		"\t\t\t\t\t\t\t\t\tAlign: Center",
		"\t\t\t\t\t\t\t\t\tVAlign: Middle",
		"\t\t\t\t\t\t\t\t\tFont: TinyBold",
		"\t\t\t\t\t\t\t\t\tTextColor: F8F4E8",
		"\t\t\t\t\t\t\t\t\tContrast: true",
		"\t\t\t\t\t\t\t\t\tContrastRadius: 1",
		"\t\t\t\t\t\t\t\t\tIgnoreMouseOver: true",
	)


def ingame_text():
	lines = [
		"Container@INGAME_ROOT:", "\tChildren:",
		"\t\tContainer@WORLD_ROOT:", "\t\t\tChildren:",
		"\t\t\t\tVirtualViewportJoystick@IOS_VIEWPORT_JOYSTICK:",
		"\t\t\t\tContainer@IOS_VIEWPORT_ACTIONS:",
		"\t\t\t\t\tChildren:",
	]
	for button in ACTIVE_VIEWPORT_ACTIONS:
		lines.extend(active_action_button_lines(button))
	return "\n".join(lines) + "\n"


def action_fluent_text(locale):
	value_index = 1 if locale == "zh-CN" else 0
	return "\n".join(
		f"{reference.removesuffix('.label')} =\n    .label = {values[value_index]}"
		for reference, *values in ACTION_LABEL_MESSAGES.values()
	) + "\n"


def ingame_player_text():
	lines = [
		"Container@PLAYER_WIDGETS:", "\tChildren:",
		"\t\tCustomCommandBar@COMMAND_BAR_BACKGROUND:", "\t\t\tChildren:",
		"\t\t\t\tContainer@COMMAND_SLOTS:", "\t\t\t\t\tChildren:",
	]
	for slot in COMMAND_SLOTS:
		collection, image = SEMANTIC_SHORTCUTS.get(
			slot, ("commandbar-icons", slot.casefold().replace("_", "-")))
		lines.extend((
			f"\t\t\t\t\t\tButton@{slot}:",
			"\t\t\t\t\t\t\tChildren:",
			"\t\t\t\t\t\t\t\tImage@ICON:",
			f"\t\t\t\t\t\t\t\t\tImageCollection: {collection}",
			f"\t\t\t\t\t\t\t\t\tImageName: {image}",
		))
	return "\n".join(lines) + "\n"


IOS_LOBBY_FLUENT = {
	"default": {
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
	"zh-CN": {
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


def lobby_fluent_text(locale):
	return "\n".join(
		f"{key} = {value}" for key, value in IOS_LOBBY_FLUENT[locale].items()
	) + "\n"


def lobby_players_text():
	return (
		"Container@LOBBY_PLAYER_BIN:\n"
		"\tChildren:\n"
		"\t\tScrollPanel@LOBBY_PLAYERS:\n"
		"\t\t\tChildren:\n"
		"\t\t\t\tContainer@TEMPLATE_EDITABLE_PLAYER:\n"
		"\t\t\t\t\tChildren:\n"
		"\t\t\t\t\t\tLabel@IOS_MAP_STATUS_TEXT:\n"
		"\t\t\t\t\t\t\tVisible: false\n"
		"\t\t\t\t\t\tLabel@IOS_MAP_STATUS_ICON:\n"
		"\t\t\t\t\t\t\tVisible: false\n"
		"\t\t\t\tContainer@TEMPLATE_NONEDITABLE_PLAYER:\n"
		"\t\t\t\t\tChildren:\n"
		"\t\t\t\t\t\tLabel@IOS_MAP_STATUS_TEXT:\n"
		"\t\t\t\t\t\t\tVisible: false\n"
		"\t\t\t\t\t\tLabel@IOS_MAP_STATUS_ICON:\n"
		"\t\t\t\t\t\t\tVisible: false\n"
	)


IOS_LOBBY_SOURCE_CONTRACTS = {
	Path("engine/OpenRA.Game/Server/ProtocolVersion.cs"): """
namespace OpenRA.Server
{
	public static class ProtocolVersion
	{
		public const int Orders = 23;
	}
}
""",
	Path("engine/OpenRA.Game/Network/LobbySafeStartRequest.cs"): """
public readonly struct LobbySafeStartRequest
{
	public string ExpectedMapUid { get; }
	public long RequestId { get; }

	public LobbySafeStartRequest(string expectedMapUid, long requestId)
	{
		if (string.IsNullOrWhiteSpace(expectedMapUid) || expectedMapUid.Any(char.IsWhiteSpace))
			throw new ArgumentException();
		if (requestId <= 0)
			throw new ArgumentOutOfRangeException();
		ExpectedMapUid = expectedMapUid;
		RequestId = requestId;
	}

	public string ToCommand()
	{
		return $"startgame_safe {ExpectedMapUid} {RequestId.ToString(CultureInfo.InvariantCulture)}";
	}

	public static bool TryParse(string value, out LobbySafeStartRequest request)
	{
		request = default;
		if (value == null)
			return false;
		var fields = value.Split(' ');
		if (fields.Length != 2 || fields.Any(string.IsNullOrEmpty) ||
			!long.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out var requestId) ||
			requestId <= 0 || requestId.ToString(CultureInfo.InvariantCulture) != fields[1])
			return false;
		try
		{
			request = new LobbySafeStartRequest(fields[0], requestId);
			return true;
		}
		catch (ArgumentException)
		{
			return false;
		}
	}
}
""",
	Path("engine/OpenRA.Game/Network/LobbySafeStartRequestState.cs"): """
public sealed class LobbySafeStartRequestState
{
	long lastIssuedRequestId;
	string pendingMapUid;
	public bool IsPending { get; private set; }
	public long PendingRequestId { get; private set; }
	public LobbyStartRejection Rejection { get; private set; }

	public LobbySafeStartRequestState(long lastIssuedRequestId)
	{
		if (lastIssuedRequestId < 0)
			throw new ArgumentOutOfRangeException();
		this.lastIssuedRequestId = lastIssuedRequestId;
	}

	public LobbySafeStartRequest Begin(string mapUid)
	{
		if (lastIssuedRequestId == long.MaxValue)
			throw new InvalidOperationException();
		var request = new LobbySafeStartRequest(mapUid, lastIssuedRequestId + 1);
		lastIssuedRequestId = request.RequestId;
		pendingMapUid = request.ExpectedMapUid;
		PendingRequestId = request.RequestId;
		IsPending = true;
		Rejection = null;
		return request;
	}

	public bool TryAcceptRejection(LobbyStartRejection rejection, string currentMapUid)
	{
		if (!IsPending || rejection == null || rejection.RequestId != PendingRequestId ||
			!string.Equals(rejection.ExpectedMapUid, pendingMapUid, StringComparison.Ordinal) ||
			!string.Equals(rejection.ExpectedMapUid, currentMapUid, StringComparison.Ordinal))
			return false;
		ClearPending();
		Rejection = rejection;
		return true;
	}

	public bool TryTimeout(long requestId)
	{
		if (!IsPending || PendingRequestId != requestId)
			return false;
		ClearPending();
		return true;
	}

	public bool ObserveReadinessChange(int clientIndex)
	{
		if (Rejection == null || Rejection.ClientIndex != clientIndex)
			return false;
		Rejection = null;
		return true;
	}

	public bool ObserveLobbySync(string currentMapUid)
	{
		var changed = Rejection != null;
		Rejection = null;
		if (IsPending && !string.Equals(pendingMapUid, currentMapUid, StringComparison.Ordinal))
		{
			ClearPending();
			changed = true;
		}
		return changed;
	}

	public bool Reset()
	{
		if (!IsPending && Rejection == null)
			return false;
		ClearPending();
		Rejection = null;
		return true;
	}

	void ClearPending()
	{
		IsPending = false;
		PendingRequestId = 0;
		pendingMapUid = null;
	}
}
""",
	Path("engine/OpenRA.Game/Network/Session.cs"): """
public sealed class Session
{
	public enum ClientMapPhase
	{
		Unknown,
		Searching,
		WaitingForDownload,
		Downloading,
		InstallingOrVerifying,
		Ready,
		Unavailable,
		Error
	}

	public sealed class Client
	{
		public string MapUid;
		public ClientMapPhase MapPhase = ClientMapPhase.Unknown;
		public int MapProgress = -1;
		public bool IsMapReadyFor(string uid) =>
			MapUid == uid && MapPhase == ClientMapPhase.Ready && MapProgress == 100;
	}
}
""",
	Path("engine/OpenRA.Game/Network/ClientMapReadiness.cs"): """
public static class ClientMapReadinessState
{
	public static bool IsValidPhaseProgress(Session.ClientMapPhase phase, int progress)
	{
		return phase switch
		{
			Session.ClientMapPhase.Downloading => progress == -1 || progress is >= 0 and <= 99,
			Session.ClientMapPhase.Ready => progress == 100,
			_ => progress == -1
		};
	}
}

public sealed class ClientMapReadinessUpdate
{
	public static bool TryDeserialize(string data, string name, out ClientMapReadinessUpdate update) => true;
	public bool ApplyTo(Session session) => true;
}
""",
	Path("engine/OpenRA.Game/Network/OrderManager.cs"): """
public sealed class OrderManager
{
	public event Action<int> ClientMapReadinessChanged = _ => { };
	public event Action<LobbyStartRejection> SafeStartRejected = _ => { };
	internal void NotifyClientMapReadinessChanged(int clientIndex)
	{
		ClientMapReadinessChanged(clientIndex);
	}
	internal void NotifySafeStartRejected(LobbyStartRejection rejection)
	{
		SafeStartRejected(rejection);
	}
}
""",
	Path("engine/OpenRA.Game/Network/UnitOrders.cs"): """
public static class UnitOrders
{
	internal static void ProcessOrder(OrderManager orderManager, World world, int clientId, Order order)
	{
		switch (order.OrderString)
		{
			case "SyncClientMapReadiness":
			{
				if (!ClientMapReadinessUpdate.TryDeserialize(
					order.TargetString, order.OrderString, out var update))
					break;

				if (update.ApplyTo(orderManager.LobbyInfo))
					orderManager.NotifyClientMapReadinessChanged(update.ClientIndex);
				break;
			}
			case "SafeStartRejected":
			{
				var rejection = LobbyStartRejection.Deserialize(order.TargetString);
				if (rejection == null)
					Debug.WriteLine("Ignoring malformed SafeStartRejected order.");
				else
					orderManager.NotifySafeStartRejected(rejection);
				break;
			}
		}
	}
}
""",
	Path("engine/OpenRA.Game/Network/LobbyStartAvailability.cs"): """
public readonly struct LobbyStartAvailability
{
	public bool CanStart { get; }
	public static LobbyStartAvailability EvaluateMapSafety(Session lobbyInfo, bool requireCurrentMapReadiness)
	{
		if ((lobbyInfo.GlobalSettings.MapStatus & Session.MapStatus.Playable) == 0)
			return Blocked(Session.LobbyStartBlockReason.ServerMapNotPlayable);
		if (!requireCurrentMapReadiness)
			return Allowed;
		return lobbyInfo.NonBotClients.All(client => client.IsMapReadyFor(lobbyInfo.GlobalSettings.Map)) ?
			Allowed : Blocked(Session.LobbyStartBlockReason.ClientMapUnknown);
	}

	public static LobbyStartAvailability Evaluate(LobbyStartInputs inputs)
	{
		var mapSafety = EvaluateMapSafety(inputs.LobbyInfo, inputs.RequireCurrentMapReadiness);
		if (!mapSafety.CanStart)
			return mapSafety;
		return Allowed;
	}

	public LobbyStartRejection ToRejection(LobbySafeStartRequest request)
	{
		if (CanStart)
			throw new InvalidOperationException();
		return new LobbyStartRejection(Reason, ClientIndex, ClientMapPhase, Progress,
			request.ExpectedMapUid, request.RequestId);
	}
}

public sealed class LobbyStartRejection
{
	public Session.LobbyStartBlockReason Reason { get; }
	public int ClientIndex { get; }
	public Session.ClientMapPhase ClientMapPhase { get; }
	public int Progress { get; }
	public string ExpectedMapUid { get; }
	public long RequestId { get; }

	public LobbyStartRejection(Session.LobbyStartBlockReason reason, int clientIndex,
		Session.ClientMapPhase clientMapPhase, int progress, string expectedMapUid, long requestId)
	{
		Reason = reason;
		ClientIndex = clientIndex;
		ClientMapPhase = clientMapPhase;
		Progress = progress;
		ExpectedMapUid = expectedMapUid;
		RequestId = requestId;
	}

	public LobbyStartAvailability ToAvailability()
	{
		return new LobbyStartAvailability(Reason, ClientIndex, ClientMapPhase, Progress, 0);
	}

	public string Serialize()
	{
		return new List<MiniYamlNode>
		{
			new(nameof(Reason), Reason.ToString()),
			new(nameof(ClientIndex), ClientIndex.ToString(CultureInfo.InvariantCulture)),
			new(nameof(ClientMapPhase), ClientMapPhase.ToString()),
			new(nameof(Progress), Progress.ToString(CultureInfo.InvariantCulture)),
			new(nameof(ExpectedMapUid), ExpectedMapUid),
			new(nameof(RequestId), RequestId.ToString(CultureInfo.InvariantCulture))
		}.WriteToString();
	}

	public static LobbyStartRejection Deserialize(string data)
	{
		try
		{
			var nodes = MiniYaml.FromString(data, "lobby-start-rejection");
			var expected = new[]
			{
				nameof(Reason), nameof(ClientIndex), nameof(ClientMapPhase), nameof(Progress),
				nameof(ExpectedMapUid), nameof(RequestId)
			};
			if (nodes.Count != expected.Length || expected.Any(key => nodes.Count(node => node.Key == key) != 1) ||
				nodes.Any(node => !expected.Contains(node.Key) || node.Value.Nodes.Any()))
				return null;

			var values = nodes.ToDictionary(node => node.Key, node => node.Value.Value);
			if (!Enum.TryParse(values[nameof(Reason)], false, out Session.LobbyStartBlockReason reason) ||
				values[nameof(Reason)] != reason.ToString() ||
				!int.TryParse(values[nameof(ClientIndex)], NumberStyles.AllowLeadingSign,
					CultureInfo.InvariantCulture, out var clientIndex) ||
				values[nameof(ClientIndex)] != clientIndex.ToString(CultureInfo.InvariantCulture) ||
				!Enum.TryParse(values[nameof(ClientMapPhase)], false, out Session.ClientMapPhase phase) ||
				values[nameof(ClientMapPhase)] != phase.ToString() ||
				!int.TryParse(values[nameof(Progress)], NumberStyles.AllowLeadingSign,
					CultureInfo.InvariantCulture, out var progress) ||
				values[nameof(Progress)] != progress.ToString(CultureInfo.InvariantCulture) ||
				!long.TryParse(values[nameof(RequestId)], NumberStyles.None,
					CultureInfo.InvariantCulture, out var requestId) || requestId <= 0 ||
				values[nameof(RequestId)] != requestId.ToString(CultureInfo.InvariantCulture))
				return null;

			return new LobbyStartRejection(reason, clientIndex, phase, progress,
				values[nameof(ExpectedMapUid)], requestId);
		}
		catch (Exception)
		{
			return null;
		}
	}
}
""",
	Path("engine/OpenRA.Game/Server/Server.cs"): """
public sealed class Server
{
	public void SyncClientMapReadiness(ClientMapReadinessUpdate update)
	{
		DispatchServerOrdersToClients(Order.FromTargetString(
			"SyncClientMapReadiness", new[] { update.Serialize() }.WriteToString(), true));
	}

	public void StartGame()
	{
		if (IsMultiplayer && !LobbyStartAvailability.EvaluateMapSafety(
			LobbyInfo, requireCurrentMapReadiness: true).CanStart)
			return;
		WriteLineWithTimeStamp("started");
	}
}
""",
	Path("engine/OpenRA.Mods.Common/ServerTraits/LobbyCommands.cs"): """
public sealed class LobbyCommands
{
	void Configure()
	{
		commandHandlers.Add("map_status", MapReadiness);
		commandHandlers.Add("startgame_safe", StartGameSafe);
	}

	static LobbyStartAvailability EvaluateStartAvailability(
		Server server, LobbyStartMode mode, bool requesterIsAdmin)
	{
		return LobbyStartAvailability.Evaluate(new LobbyStartInputs
		{
			Mode = mode,
			RequireCurrentMapReadiness = server.IsMultiplayer
		});
	}

	static bool StartGame(Server server, Client client)
	{
		var availability = EvaluateStartAvailability(
			server, LobbyStartMode.ForceConfirmed, client.IsAdmin);
		if (!availability.CanStart)
			return true;
		server.StartGame();
		return true;
	}

	static bool StartGameSafe(Server server, Client client, string value)
	{
		var currentMapUid = server.LobbyInfo.GlobalSettings.Map;
		if (!LobbySafeStartRequest.TryParse(value, out var request))
			return true;
		if (!client.IsAdmin)
		{
			server.SendOrderTo(client, "SafeStartRejected", new LobbyStartRejection(
				Session.LobbyStartBlockReason.RequesterNotAdmin, -1,
				Session.ClientMapPhase.Unknown, -1,
				request.ExpectedMapUid, request.RequestId).Serialize());
			return true;
		}
		if (!string.Equals(request.ExpectedMapUid, currentMapUid, StringComparison.Ordinal))
		{
			server.SendOrderTo(client, "SafeStartRejected", new LobbyStartRejection(
				Session.LobbyStartBlockReason.StaleMap, -1,
				Session.ClientMapPhase.Unknown, -1,
				request.ExpectedMapUid, request.RequestId).Serialize());
			return true;
		}
		var availability = EvaluateStartAvailability(server, LobbyStartMode.SafeDirect, true);
		if (!availability.CanStart)
		{
			server.SendOrderTo(client, "SafeStartRejected",
				availability.ToRejection(request).Serialize());
			return true;
		}
		server.StartGame();
		return true;
	}

	static void CheckAutoStart(Server server)
	{
		if (!EvaluateStartAvailability(server, LobbyStartMode.Automatic, true).CanStart)
			return;
		server.StartGame();
	}

	public static bool CanSyncLobby(ServerType serverType)
	{
		return serverType is ServerType.Local or ServerType.Skirmish;
	}

	static bool SyncLobby(Server server)
	{
		if (!CanSyncLobby(server.Type))
			return true;
		server.LobbyInfo = Session.Deserialize(snapshot, nameof(SyncLobby));
		server.SyncLobbyInfo();
		return true;
	}

	void Tick(Server server)
	{
		foreach (var pending in mapReadinessThrottle.Drain(now))
			server.SyncClientMapReadiness(ReadinessSnapshot(client));
	}
}
""",
	Path("engine/OpenRA.Mods.Common/Widgets/Logic/Lobby/LobbyLogic.cs"): """
public sealed class LobbyLogic
{
	readonly LobbySafeStartRequestState safeStartState = new LobbySafeStartRequestState(0);

	void Subscribe()
	{
		orderManager.ClientMapReadinessChanged += ClientMapReadinessChanged;
		orderManager.SafeStartRejected += SafeStartRejected;
	}

	void Dispose()
	{
		safeStartState.Reset();
		orderManager.ClientMapReadinessChanged -= ClientMapReadinessChanged;
		orderManager.SafeStartRejected -= SafeStartRejected;
	}

	void ConnectionStateChanged(Connection connection)
	{
		if (connection.ConnectionState == ConnectionState.NotConnected)
		{
			safeStartState.Reset();
			CloseWindow();
		}
	}

	void ClientMapReadinessChanged(int clientIndex)
	{
		safeStartState.ObserveReadinessChange(clientIndex);
		InvalidateStartStatus();
	}

	void SafeStartRejected(LobbyStartRejection rejection)
	{
		if (safeStartState.TryAcceptRejection(rejection, orderManager.LobbyInfo.GlobalSettings.Map))
			InvalidateStartStatus();
	}

	void StartStatusLobbyInfoChanged()
	{
		safeStartState.ObserveLobbySync(orderManager.LobbyInfo.GlobalSettings.Map);
		InvalidateStartStatus();
	}

	LobbyStartAvailability CurrentStartAvailability()
	{
		return safeStartState.Rejection != null ?
			safeStartState.Rejection.ToAvailability() :
			LobbyStartAvailability.Evaluate(default);
	}

	void BeginSafeStart()
	{
		var availability = CurrentStartAvailability();
		if (!availability.CanStart || safeStartState.IsPending)
			return;
		var currentMapUid = orderManager.LobbyInfo.GlobalSettings.Map;
		var request = safeStartState.Begin(currentMapUid);
		InvalidateStartStatus();
		Game.RunAfterDelay(5000, () =>
		{
			if (!disposed && safeStartState.TryTimeout(request.RequestId))
				InvalidateStartStatus();
		});
		orderManager.IssueOrder(Order.Command(request.ToCommand()));
	}

	void BindStartButton()
	{
		startGameButton.OnClick = () =>
		{
			if (Platform.IsIOS)
			{
				BeginSafeStart();
				return;
			}

			if (ordinaryPlayerNotReady)
				panel = PanelType.ForceStart;
			else
				StartGame();
		};
	}
}
""",
	Path("engine/OpenRA.Mods.Common/Widgets/Logic/Lobby/IosLobbyLayout.cs"): """
public sealed class IosLobbyLayout
{
	void CreateColumns(IosMenuLayoutPolicy policy)
	{
		var playersWidth = skirmishMode ?
			2 * Math.Max(0, width - Gap) / 3 :
			10 * policy.MinimumTarget;
		var minimumUnits = skirmishMode ? new[] { 4, 2, 2, 2, 2 } : new[] { 2, 1, 3, 1, 1, 1, 1 };
		var totalMinimumUnits = minimumUnits.Sum();
	}
}
""",
	Path("engine/OpenRA.Mods.Common/Widgets/Logic/IosTouchWidgetPolicy.cs"): """
public readonly struct IosDropDownLayout
{
	public static IosDropDownLayout Default { get; } = new(3, 0, 0, default, false);
	public static IosDropDownLayout FactionPicker { get; } =
		new(5, 6, 24, new Size(40, 20), true);
}
""",
	Path("engine/OpenRA.Mods.Common/Widgets/Logic/Lobby/LobbyUtils.cs"): """
public static class LobbyUtils
{
	public static void ShowFactionDropDown(DropDownButtonWidget dropdown)
	{
		label.GetText = () => WidgetUtils.TruncateText(
			fullName, label.Bounds.Width, Game.Renderer.Fonts[label.Font]);
		dropdown.ShowDropDown(
			"FACTION_DROPDOWN_TEMPLATE", 154, options, SetupItem,
			IosDropDownLayout.FactionPicker);
	}
}
""",
	Path("engine/OpenRA.Mods.Common/Widgets/DropDownButtonWidget.cs"): """
public class DropDownButtonWidget
{
	public void ShowDropDown<T>(string template, int height,
		Dictionary<string, IEnumerable<T>> groups, Func<T, Widget, Widget> setupItem)
	{
		ShowDropDown(template, height, groups, setupItem, IosDropDownLayout.Default);
	}

	public void ShowDropDown<T>(string template, int height,
		Dictionary<string, IEnumerable<T>> groups, Func<T, Widget, Widget> setupItem,
		IosDropDownLayout iosLayout)
	{
		AdaptIosDropDownPanel(panel, snapshot, iosLayout);
		IosTouchWidgetPolicy.ShowDropDownGroupHeader(options.Length, Platform.IsIOS, iosLayout);
		AdaptIosDropDownItem(item, panel, snapshot, false, iosLayout);
	}
}
""",
	Path("engine/OpenRA.Mods.Common/Widgets/Logic/Lobby/IosLobbyStartStatus.cs"): """
public static class IosLobbyStartStatus
{
	const string Validating = "button-ios-lobby-start-validating";
	const string MapUnavailable = "button-ios-lobby-start-map-unavailable";
	const string ClientFailed = "button-ios-lobby-start-client-failed";
	const string ClientsFailed = "button-ios-lobby-start-clients-failed";
	const string ClientVerifying = "button-ios-lobby-start-client-verifying";
	const string ClientsVerifying = "button-ios-lobby-start-clients-verifying";
	const string ClientDownloading = "button-ios-lobby-start-client-downloading";
	const string ClientsDownloading = "button-ios-lobby-start-clients-downloading";
	const string ClientWaiting = "button-ios-lobby-start-client-waiting";
	const string ClientsWaiting = "button-ios-lobby-start-clients-waiting";
	const string ClientAndOthers = "button-ios-lobby-start-client-and-others";
	const string ClientsNotReady = "button-ios-lobby-start-clients-not-ready";
	const string RequiredSlot = "button-ios-lobby-start-required-slot";
	const string NoPlayers = "button-ios-lobby-start-no-players";
	const string TwoPlayers = "button-ios-lobby-start-two-players";
	const string Spawns = "button-ios-lobby-start-spawns";
	const string Waiting = "button-ios-lobby-start-waiting";
	const string RequiresHost = "notification-requires-host";
	public static string Format(LobbyStartAvailability availability)
	{
		return availability.Reason switch
		{
			Session.LobbyStartBlockReason.RequesterNotAdmin =>
				localize(RequiresHost, Array.Empty<object>()),
			_ => availability.Progress >= 0 ? ClientDownloading : ClientsDownloading
		};
	}
	static string ClientStatus(int count, string named, Func<string, object[], string> localize)
	{
		var namedAndOthers = localize(ClientAndOthers, new object[]
		{
			"status", named,
			"count", count - 1
		});
		return count > 1 ? namedAndOthers :
			localize(ClientsNotReady, new object[] { "count", count });
	}
}
""",
	Path("engine/OpenRA.Mods.Common/Widgets/Logic/Lobby/IosLobbyMapStatusPresentation.cs"): """
public readonly struct IosLobbyMapStatusPresentation
{
	public static IosLobbyMapStatusPresentation For(
		Session.Client client, string currentMapUid, bool isIos)
	{
		if (!isIos || client.Bot != null || client.MapUid != currentMapUid)
			return default;
		return client.MapPhase == Session.ClientMapPhase.Downloading && client.MapProgress >= 0 ?
			new(true, $"{client.MapProgress}%", "") : new(true, "…", "");
	}
}
""",
	Path("engine/OpenRA.Game/Support/IosMultiplayerSmoke.cs"): """
public sealed class IosMultiplayerSmokeCoordinator
{
	LobbySafeStartRequest blockedStartRequest;
	LobbySafeStartRequest finalStartRequest;

	void HandleSafeStartRejected(LobbyStartRejection rejection)
	{
		if (blockedStartRejected ||
			rejection.RequestId != blockedStartRequest.RequestId ||
			!string.Equals(rejection.ExpectedMapUid, currentMapUid, StringComparison.Ordinal))
			Fail();
		blockedStartRejected = true;
	}

	void Poll()
	{
		blockedStartRequest = new LobbySafeStartRequest(currentMapUid, 1);
		orderManager.IssueOrder(Order.Command(blockedStartRequest.ToCommand()));
		finalStartRequest = new LobbySafeStartRequest(currentMapUid, 2);
		orderManager.IssueOrder(Order.Command(finalStartRequest.ToCommand()));
	}
}
""",
}


class IosTouchRuntimeBundleAuditTest(unittest.TestCase):
	def setUp(self):
		self.temporary_directory = tempfile.TemporaryDirectory()
		self.addCleanup(self.temporary_directory.cleanup)
		root = Path(self.temporary_directory.name)
		self.app = root / "NUCLEAR CRISIS.app"
		self.source = root / "source"
		self.managed = root / "managed"
		self.app.mkdir()
		self.source.mkdir()
		self.managed.mkdir()

		configs = {
			Path("mods/ra2/mod.yaml"): (
				"Chrome:\n"
				"\tra2|chrome.yaml\n"
				"ChromeLayout:\n"
				"\tra2|chrome/ingame-player.yaml\n"
				"\tra2|chrome/ingame.yaml\n"
				"\tcommon|chrome/settings-input.yaml\n"
				"FluentMessages:\n"
				"\tcommon|fluent/common.ftl\n"
				"ChromeMetrics:\n"
				"\tcommon|metrics.yaml\n"
				"\tra2|metrics.yaml\n"
			),
			Path("mods/ra2/chrome.yaml"): touch_chrome_text(),
			Path("mods/ra2/metrics.yaml"): (
				"Metrics:\n"
				"\tFactionSuffix-yuri: soviets\n"
				"\tTouchFactionSuffix-america: allies\n"
				"\tTouchFactionSuffix-korea: allies\n"
				"\tTouchFactionSuffix-france: allies\n"
				"\tTouchFactionSuffix-germany: allies\n"
				"\tTouchFactionSuffix-england: allies\n"
				"\tTouchFactionSuffix-libya: soviets\n"
				"\tTouchFactionSuffix-cuba: soviets\n"
				"\tTouchFactionSuffix-iraq: soviets\n"
				"\tTouchFactionSuffix-russia: soviets\n"
				"\tTouchFactionSuffix-yuri: yuri\n"
			),
			Path("mods/ra2/chrome/ingame.yaml"): ingame_text(),
			Path("mods/ra2/chrome/ingame-player.yaml"): ingame_player_text(),
			Path("mods/ra2/fluent/chrome.ftl"): action_fluent_text("default"),
			Path("mods/ra2/fluent/zh-CN/chrome.ftl"): action_fluent_text("zh-CN"),
			Path("engine/mods/common/chrome/settings-input.yaml"): (
				"Container@INPUT_PANEL:\n"
				"\tChildren:\n"
				"\t\tScrollPanel@SETTINGS_SCROLLPANEL:\n"
				"\t\t\tChildren:\n"
				"\t\t\t\tContainer@TOUCH_JOYSTICK_SIZE_CONTAINER:\n"
				"\t\t\t\t\tChildren:\n"
				"\t\t\t\t\t\tDropDownButton@TOUCH_JOYSTICK_SIZE_DROPDOWN:\n"
			),
			Path("engine/mods/common/chrome/lobby-players.yaml"):
				lobby_players_text(),
			Path("engine/mods/common/fluent/chrome.ftl"):
				lobby_fluent_text("default"),
			Path("engine/mods/common/fluent/zh-CN/chrome.ftl"):
				lobby_fluent_text("zh-CN"),
			Path("engine/mods/common/fluent/common.ftl"): (
				"label-touch-joystick-size-container = Virtual joystick size\n"
				"options-touch-joystick-size =\n"
				"    .small = Small (112 pt)\n"
				"    .medium = Medium (128 pt)\n"
				"    .large = Large (144 pt)\n"
			),
			Path("engine/mods/common/fluent/zh-CN/common.ftl"): (
				"label-touch-joystick-size-container = 虚拟摇杆尺寸\n"
				"options-touch-joystick-size =\n"
				"    .small = 小（112 pt）\n"
				"    .medium = 中（128 pt）\n"
				"    .large = 大（144 pt）\n"
			),
		}
		for source_relative, text in configs.items():
			bundle_relative = source_relative
			if source_relative.parts[0] == "engine":
				bundle_relative = Path(*source_relative.parts[1:])
			self._write_pair(source_relative, bundle_relative, text.encode("utf-8"))

		for source_relative, text in IOS_LOBBY_SOURCE_CONTRACTS.items():
			source_path = self.source / source_relative
			source_path.parent.mkdir(parents=True, exist_ok=True)
			source_path.write_text(text, encoding="utf-8")

		seed = 1
		for faction in FACTIONS:
			for kind, sizes in ASSET_SIZES.items():
				for scale, size in (("", sizes[0]), ("-2x", sizes[1])):
					name = f"ios-touch-{kind}-{faction}{scale}.png"
					data = png_bytes(*size, seed=seed)
					seed += 1
					self._write_pair(
						Path("mods/ra2/uibits") / name,
						Path("mods/ra2/uibits") / name,
						data,
					)

		for name in ("ios-commandbar-icons-v2.png", "production-x5-icon.png"):
			data = (ROOT / "mods/ra2/uibits" / name).read_bytes()
			self._write_pair(
				Path("mods/ra2/uibits") / name,
				Path("mods/ra2/uibits") / name,
				data,
			)

		for name, metadata in ASSEMBLY_METADATA.items():
			data = managed_pe(
				metadata["strings"], metadata["user_strings"],
				definitions=metadata["definitions"],
			)
			(self.app / name).write_bytes(data)
			(self.managed / name).write_bytes(data)
			(self.app / name.replace(".dll", ".aotdata.arm64")).write_bytes(
				b"aot-" + name.encode("ascii")
			)

		with (self.app / "Info.plist").open("wb") as stream:
			plistlib.dump({"CFBundleExecutable": "NUCLEAR-CRISIS"}, stream)
		(self.app / "NUCLEAR-CRISIS").write_bytes(arm64_macho())

	def _write_pair(self, source_relative, bundle_relative, data):
		source_path = self.source / source_relative
		bundle_path = self.app / bundle_relative
		source_path.parent.mkdir(parents=True, exist_ok=True)
		bundle_path.parent.mkdir(parents=True, exist_ok=True)
		source_path.write_bytes(data)
		bundle_path.write_bytes(data)

	def _rewrite_pair(self, source_relative, bundle_relative, transform):
		source_path = self.source / source_relative
		bundle_path = self.app / bundle_relative
		value = transform(source_path.read_text(encoding="utf-8"))
		source_path.write_text(value, encoding="utf-8")
		bundle_path.write_text(value, encoding="utf-8")

	def _rewrite_source(self, source_relative, transform):
		source_path = self.source / source_relative
		value = transform(source_path.read_text(encoding="utf-8"))
		source_path.write_text(value, encoding="utf-8")

	def errors(self):
		return audit_bundle(self.app, self.source, self.managed)

	def assert_error(self, text):
		errors = self.errors()
		self.assertTrue(errors, "tampered bundle unexpectedly passed")
		self.assertIn(text.casefold(), "\n".join(errors).casefold())

	def test_accepts_a_complete_synthetic_final_app(self):
		self.assertEqual((), self.errors())

	def test_rejects_a_protocol_23_downgrade(self):
		self._rewrite_source(
			Path("engine/OpenRA.Game/Server/ProtocolVersion.cs"),
			lambda text: text.replace("Orders = 23", "Orders = 22"),
		)
		self.assert_error("orders protocol must remain 23")

	def test_rejects_rejection_serialization_without_request_id(self):
		self._rewrite_source(
			Path("engine/OpenRA.Game/Network/LobbyStartAvailability.cs"),
			lambda text: text.replace(
				"\t\t\tnew(nameof(RequestId), RequestId.ToString(CultureInfo.InvariantCulture))\n",
				"",
			),
		)
		self.assert_error("rejection serialization must contain exactly six correlated fields")

	def test_rejects_reordered_rejection_serialization_fields(self):
		self._rewrite_source(
			Path("engine/OpenRA.Game/Network/LobbyStartAvailability.cs"),
			lambda text: text.replace(
				"new(nameof(Reason), Reason.ToString()),\n"
				"\t\t\tnew(nameof(ClientIndex), ClientIndex.ToString(CultureInfo.InvariantCulture)),",
				"new(nameof(ClientIndex), ClientIndex.ToString(CultureInfo.InvariantCulture)),\n"
				"\t\t\tnew(nameof(Reason), Reason.ToString()),",
			),
		)
		self.assert_error("rejection serialization must use the canonical field order")

	def test_rejects_rejection_deserialization_with_nested_values(self):
		self._rewrite_source(
			Path("engine/OpenRA.Game/Network/LobbyStartAvailability.cs"),
			lambda text: text.replace(" || node.Value.Nodes.Any()", ""),
		)
		self.assert_error("rejection parser must require exact unique leaf fields")

	def test_rejects_rejection_deserialization_with_an_extra_allowed_key(self):
		self._rewrite_source(
			Path("engine/OpenRA.Game/Network/LobbyStartAvailability.cs"),
			lambda text: text.replace(
				"nameof(ExpectedMapUid), nameof(RequestId)\n",
				"nameof(ExpectedMapUid), nameof(RequestId), nameof(ExtraField)\n",
				1,
			),
		)
		self.assert_error("rejection parser must require exactly six field names")

	def test_accepts_reordered_rejection_deserializer_expected_fields(self):
		self._rewrite_source(
			Path("engine/OpenRA.Game/Network/LobbyStartAvailability.cs"),
			lambda text: text.replace(
				"nameof(Reason), nameof(ClientIndex), nameof(ClientMapPhase), nameof(Progress),",
				"nameof(ClientIndex), nameof(Reason), nameof(ClientMapPhase), nameof(Progress),",
				1,
			),
		)
		self.assertEqual((), self.errors())

	def test_rejects_noncanonical_rejection_request_id_parsing(self):
		self._rewrite_source(
			Path("engine/OpenRA.Game/Network/LobbyStartAvailability.cs"),
			lambda text: text.replace(
				"!long.TryParse(values[nameof(RequestId)], NumberStyles.None,",
				"!long.TryParse(values[nameof(RequestId)], NumberStyles.Integer,",
			),
		)
		self.assert_error("rejection request ID must use canonical positive invariant parsing")

	def test_rejects_rejection_deserialization_that_ignores_the_parsed_request_id(self):
		def constantize_request_id(text):
			return text.replace(
				"out var requestId) || requestId <= 0 ||\n"
				"\t\t\t\tvalues[nameof(RequestId)] != requestId.ToString(CultureInfo.InvariantCulture)",
				"out var parsedRequestId) || parsedRequestId <= 0 ||\n"
				"\t\t\t\tvalues[nameof(RequestId)] != parsedRequestId.ToString(CultureInfo.InvariantCulture)",
			).replace(
				"\t\t\treturn new LobbyStartRejection(reason, clientIndex, phase, progress,\n",
				"\t\t\tvar requestId = 1L;\n"
				"\t\t\treturn new LobbyStartRejection(reason, clientIndex, phase, progress,\n",
			)

		self._rewrite_source(
			Path("engine/OpenRA.Game/Network/LobbyStartAvailability.cs"),
			constantize_request_id,
		)
		self.assert_error("rejection request ID must use canonical positive invariant parsing")

	def test_rejects_availability_rejection_without_the_request_id(self):
		self._rewrite_source(
			Path("engine/OpenRA.Game/Network/LobbyStartAvailability.cs"),
			lambda text: text.replace(
				"request.ExpectedMapUid, request.RequestId);",
				"request.ExpectedMapUid, 1);",
				1,
			),
		)
		self.assert_error("availability rejection must echo the safe-start request")

	def test_rejects_explicit_server_rejection_without_the_request_id(self):
		self._rewrite_source(
			Path("engine/OpenRA.Mods.Common/ServerTraits/LobbyCommands.cs"),
			lambda text: text.replace(
				"request.ExpectedMapUid, request.RequestId).Serialize());",
				"request.ExpectedMapUid, 1).Serialize());",
				1,
			),
		)
		self.assert_error("explicit safe-start rejection must echo the request")

	def test_rejects_non_admin_rejection_that_falls_through(self):
		self._rewrite_source(
			Path("engine/OpenRA.Mods.Common/ServerTraits/LobbyCommands.cs"),
			lambda text: text.replace(
				"request.ExpectedMapUid, request.RequestId).Serialize());\n"
				"\t\t\treturn true;",
				"request.ExpectedMapUid, request.RequestId).Serialize());",
				1,
			),
		)
		self.assert_error("non-admin safe-start rejection must terminate the branch")

	def test_rejects_non_admin_rejection_hidden_in_a_nested_inverse_if(self):
		self._rewrite_source(
			Path("engine/OpenRA.Mods.Common/ServerTraits/LobbyCommands.cs"),
			lambda text: text.replace(
				"if (!client.IsAdmin)\n\t\t{",
				"if (!client.IsAdmin)\n\t\t\tif (client.IsAdmin)\n\t\t{",
			),
		)
		self.assert_error("non-admin guard must directly own a braced rejection block")

	def test_rejects_server_availability_rejection_without_the_request_id(self):
		self._rewrite_source(
			Path("engine/OpenRA.Mods.Common/ServerTraits/LobbyCommands.cs"),
			lambda text: text.replace(
				"availability.ToRejection(request).Serialize()",
				"availability.ToRejection(new LobbySafeStartRequest(request.ExpectedMapUid, 1)).Serialize()",
			),
		)
		self.assert_error("blocked safe start must serialize availability with the request ID")

	def test_rejects_availability_rejection_that_falls_through(self):
		self._rewrite_source(
			Path("engine/OpenRA.Mods.Common/ServerTraits/LobbyCommands.cs"),
			lambda text: text.replace(
				"availability.ToRejection(request).Serialize());\n"
				"\t\t\treturn true;",
				"availability.ToRejection(request).Serialize());",
			),
		)
		self.assert_error("availability safe-start rejection must terminate the branch")

	def test_rejects_availability_rejection_hidden_in_a_nested_inverse_if(self):
		self._rewrite_source(
			Path("engine/OpenRA.Mods.Common/ServerTraits/LobbyCommands.cs"),
			lambda text: text.replace(
				"if (!availability.CanStart)\n\t\t{",
				"if (!availability.CanStart)\n\t\t\tif (availability.CanStart)\n\t\t{",
				1,
			),
		)
		self.assert_error("availability guard must directly own a braced rejection block")

	def test_rejects_stale_rejection_that_substitutes_the_current_map(self):
		self._rewrite_source(
			Path("engine/OpenRA.Mods.Common/ServerTraits/LobbyCommands.cs"),
			lambda text: text.replace(
				"Session.LobbyStartBlockReason.StaleMap, -1,\n"
				"\t\t\t\tSession.ClientMapPhase.Unknown, -1,\n"
				"\t\t\t\trequest.ExpectedMapUid, request.RequestId).Serialize());",
				"Session.LobbyStartBlockReason.StaleMap, -1,\n"
				"\t\t\t\tSession.ClientMapPhase.Unknown, -1,\n"
				"\t\t\t\tcurrentMapUid, request.RequestId).Serialize());",
			),
		)
		self.assert_error("stale-map rejection must echo the requested map UID")

	def test_rejects_stale_map_rejection_that_falls_through(self):
		self._rewrite_source(
			Path("engine/OpenRA.Mods.Common/ServerTraits/LobbyCommands.cs"),
			lambda text: text.replace(
				"Session.LobbyStartBlockReason.StaleMap, -1,\n"
				"\t\t\t\tSession.ClientMapPhase.Unknown, -1,\n"
				"\t\t\t\trequest.ExpectedMapUid, request.RequestId).Serialize());\n"
				"\t\t\treturn true;",
				"Session.LobbyStartBlockReason.StaleMap, -1,\n"
				"\t\t\t\tSession.ClientMapPhase.Unknown, -1,\n"
				"\t\t\t\trequest.ExpectedMapUid, request.RequestId).Serialize());",
			),
		)
		self.assert_error("stale-map safe-start rejection must terminate the branch")

	def test_rejects_stale_rejection_hidden_in_a_nested_inverse_if(self):
		self._rewrite_source(
			Path("engine/OpenRA.Mods.Common/ServerTraits/LobbyCommands.cs"),
			lambda text: text.replace(
				"if (!string.Equals(request.ExpectedMapUid, currentMapUid, StringComparison.Ordinal))\n"
				"\t\t{",
				"if (!string.Equals(request.ExpectedMapUid, currentMapUid, StringComparison.Ordinal))\n"
				"\t\t\tif (string.Equals(request.ExpectedMapUid, currentMapUid, StringComparison.Ordinal))\n"
				"\t\t{",
			),
		)
		self.assert_error("stale-map guard must directly own a braced rejection block")

	def test_rejects_ui_rejection_handler_that_bypasses_correlation(self):
		self._rewrite_source(
			Path("engine/OpenRA.Mods.Common/Widgets/Logic/Lobby/LobbyLogic.cs"),
			lambda text: text.replace(
				"safeStartState.TryAcceptRejection(rejection, orderManager.LobbyInfo.GlobalSettings.Map)",
				"safeStartState.Reset()",
			),
		)
		self.assert_error("safe-start rejection handler must accept only correlated rejections")

	def test_rejects_ui_that_ignores_the_correlated_rejection_availability(self):
		self._rewrite_source(
			Path("engine/OpenRA.Mods.Common/Widgets/Logic/Lobby/LobbyLogic.cs"),
			lambda text: text.replace(
				"safeStartState.Rejection.ToAvailability()",
				"LobbyStartAvailability.Evaluate(default)",
			),
		)
		self.assert_error("safe-start rejection must drive the displayed availability")

	def test_rejects_connection_loss_without_safe_start_reset(self):
		self._rewrite_source(
			Path("engine/OpenRA.Mods.Common/Widgets/Logic/Lobby/LobbyLogic.cs"),
			lambda text: text.replace(
				"\t\t\tsafeStartState.Reset();\n\t\t\tCloseWindow();",
				"\t\t\tCloseWindow();",
			),
		)
		self.assert_error("connection loss must reset safe-start state")

	def test_rejects_dispose_without_safe_start_reset(self):
		self._rewrite_source(
			Path("engine/OpenRA.Mods.Common/Widgets/Logic/Lobby/LobbyLogic.cs"),
			lambda text: text.replace(
				"\t\tsafeStartState.Reset();\n"
				"\t\torderManager.ClientMapReadinessChanged -= ClientMapReadinessChanged;",
				"\t\torderManager.ClientMapReadinessChanged -= ClientMapReadinessChanged;",
			),
		)
		self.assert_error("lobby dispose must reset safe-start state")

	def test_rejects_smoke_that_reuses_the_blocked_request_id(self):
		self._rewrite_source(
			Path("engine/OpenRA.Game/Support/IosMultiplayerSmoke.cs"),
			lambda text: text.replace(
				"finalStartRequest = new LobbySafeStartRequest(currentMapUid, 2);",
				"finalStartRequest = new LobbySafeStartRequest(currentMapUid, 1);",
			),
		)
		self.assert_error("smoke final safe-start request must use request ID 2")

	def test_rejects_smoke_rejection_with_inverted_map_correlation(self):
		self._rewrite_source(
			Path("engine/OpenRA.Game/Support/IosMultiplayerSmoke.cs"),
			lambda text: text.replace(
				"!string.Equals(rejection.ExpectedMapUid, currentMapUid, StringComparison.Ordinal)",
				"string.Equals(rejection.ExpectedMapUid, currentMapUid, StringComparison.Ordinal)",
			),
		)
		self.assert_error("smoke blocker handler must reject a mismatched map")

	def test_rejects_requester_not_admin_generic_waiting_fallback(self):
		self._rewrite_source(
			Path("engine/OpenRA.Mods.Common/Widgets/Logic/Lobby/IosLobbyStartStatus.cs"),
			lambda text: text.replace(
				"localize(RequiresHost, Array.Empty<object>())",
				"localize(Waiting, Array.Empty<object>())",
			),
		)
		self.assert_error("requester-not-admin must use notification-requires-host")

	def test_rejects_same_map_sync_that_clears_a_pending_request(self):
		self._rewrite_source(
			Path("engine/OpenRA.Game/Network/LobbySafeStartRequestState.cs"),
			lambda text: text.replace(
				"if (IsPending && !string.Equals(pendingMapUid, currentMapUid, StringComparison.Ordinal))",
				"if (IsPending && string.Equals(pendingMapUid, currentMapUid, StringComparison.Ordinal))",
			),
		)
		self.assert_error("same-map lobby sync must preserve the pending request")

	def test_rejects_rejection_with_inverted_pending_map_correlation(self):
		self._rewrite_source(
			Path("engine/OpenRA.Game/Network/LobbySafeStartRequestState.cs"),
			lambda text: text.replace(
				"!string.Equals(rejection.ExpectedMapUid, pendingMapUid, StringComparison.Ordinal)",
				"string.Equals(rejection.ExpectedMapUid, pendingMapUid, StringComparison.Ordinal)",
			),
		)
		self.assert_error("safe-start rejection must reject a mismatched pending map")

	def test_rejects_rejection_with_inverted_current_map_correlation(self):
		self._rewrite_source(
			Path("engine/OpenRA.Game/Network/LobbySafeStartRequestState.cs"),
			lambda text: text.replace(
				"!string.Equals(rejection.ExpectedMapUid, currentMapUid, StringComparison.Ordinal)",
				"string.Equals(rejection.ExpectedMapUid, currentMapUid, StringComparison.Ordinal)",
			),
		)
		self.assert_error("safe-start rejection must reject a mismatched current map")

	def test_rejects_readiness_change_that_clears_a_pending_request(self):
		self._rewrite_source(
			Path("engine/OpenRA.Game/Network/LobbySafeStartRequestState.cs"),
			lambda text: text.replace(
				"\t\tRejection = null;\n\t\treturn true;",
				"\t\tClearPending();\n\t\tRejection = null;\n\t\treturn true;",
				1,
			),
		)
		self.assert_error("readiness changes must preserve the pending request")

	def test_rejects_safe_start_request_id_wraparound(self):
		self._rewrite_source(
			Path("engine/OpenRA.Game/Network/LobbySafeStartRequestState.cs"),
			lambda text: text.replace(
				"if (lastIssuedRequestId == long.MaxValue)",
				"if (false)",
			),
		)
		self.assert_error("safe-start request IDs must fail closed at long.MaxValue")

	def test_rejects_request_parser_that_never_assigns_the_parsed_request(self):
		self._rewrite_source(
			Path("engine/OpenRA.Game/Network/LobbySafeStartRequest.cs"),
			lambda text: text.replace(
				"request = new LobbySafeStartRequest(fields[0], requestId);",
				"_ = new LobbySafeStartRequest(fields[0], requestId);",
			),
		)
		self.assert_error("safe-start parser must assign the parsed request")

	def test_rejects_request_parser_without_argument_exception_catch(self):
		self._rewrite_source(
			Path("engine/OpenRA.Game/Network/LobbySafeStartRequest.cs"),
			lambda text: text.replace(
				"\t\tcatch (ArgumentException)\n"
				"\t\t{\n"
				"\t\t\treturn false;\n"
				"\t\t}\n",
				"",
			),
		)
		self.assert_error("safe-start parser must fail closed on constructor argument errors")

	def test_rejects_request_parser_with_the_wrong_catch_type(self):
		self._rewrite_source(
			Path("engine/OpenRA.Game/Network/LobbySafeStartRequest.cs"),
			lambda text: text.replace(
				"catch (ArgumentException)", "catch (InvalidOperationException)"
			),
		)
		self.assert_error("safe-start parser must fail closed on constructor argument errors")

	def test_rejects_decoy_argument_exception_catch_after_the_constructor_try(self):
		def add_decoy_catch(text):
			return text.replace(
				"catch (ArgumentException)\n"
				"\t\t{\n"
				"\t\t\treturn false;\n"
				"\t\t}\n",
				"catch (InvalidOperationException)\n"
				"\t\t{\n"
				"\t\t\treturn false;\n"
				"\t\t}\n"
				"\t\ttry\n"
				"\t\t{\n"
				"\t\t\treturn false;\n"
				"\t\t}\n"
				"\t\tcatch (ArgumentException)\n"
				"\t\t{\n"
				"\t\t\treturn false;\n"
				"\t\t}\n",
			)

		self._rewrite_source(
			Path("engine/OpenRA.Game/Network/LobbySafeStartRequest.cs"),
			add_decoy_catch,
		)
		self.assert_error("constructor try must be immediately followed by its argument exception catch")

	def test_rejects_network_or_dedicated_sync_lobby_full_snapshots(self):
		self._rewrite_source(
			Path("engine/OpenRA.Mods.Common/ServerTraits/LobbyCommands.cs"),
			lambda text: text.replace(
				"serverType is ServerType.Local or ServerType.Skirmish",
				"serverType is ServerType.Local or ServerType.Skirmish or ServerType.Dedicated",
			),
		)
		self.assert_error("sync_lobby full snapshots must remain local/skirmish-only")

	def test_rejects_sync_lobby_without_the_server_type_guard(self):
		self._rewrite_source(
			Path("engine/OpenRA.Mods.Common/ServerTraits/LobbyCommands.cs"),
			lambda text: text.replace(
				"\t\tif (!CanSyncLobby(server.Type))\n\t\t\treturn true;\n",
				"",
			),
		)
		self.assert_error("sync_lobby must enforce the local/skirmish-only guard")

	def test_rejects_a_removed_central_multiplayer_map_gate(self):
		self._rewrite_source(
			Path("engine/OpenRA.Game/Server/Server.cs"),
			lambda text: text.replace(
				"if (IsMultiplayer && !LobbyStartAvailability.EvaluateMapSafety(\n"
				"\t\t\tLobbyInfo, requireCurrentMapReadiness: true).CanStart)\n"
				"\t\t\treturn;",
				"if (false)\n\t\t\treturn;",
			),
		)
		self.assert_error("central multiplayer map gate")

	def test_rejects_a_readiness_delta_that_rebuilds_the_full_lobby(self):
		self._rewrite_source(
			Path("engine/OpenRA.Game/Network/UnitOrders.cs"),
			lambda text: text.replace(
				"orderManager.NotifyClientMapReadinessChanged(update.ClientIndex);",
				"Game.SyncLobbyInfo();",
			),
		)
		self.assert_error("narrow readiness-changed event")

	def test_rejects_a_missing_chinese_lobby_blocker(self):
		self._rewrite_pair(
			Path("engine/mods/common/fluent/zh-CN/chrome.ftl"),
			Path("mods/common/fluent/zh-CN/chrome.ftl"),
			lambda text: text.replace(
				"button-ios-lobby-start-client-downloading = "
				"{ $player } 下载 { $progress }%\n",
				"",
			),
		)
		self.assert_error("zh-cn lobby blocker")

	def test_rejects_a_missing_english_client_and_others_blocker(self):
		self._rewrite_pair(
			Path("engine/mods/common/fluent/chrome.ftl"),
			Path("mods/common/fluent/chrome.ftl"),
			lambda text: text.replace(
				"button-ios-lobby-start-client-and-others = { $status } · +{ $count }\n",
				"",
			),
		)
		self.assert_error("english lobby blocker button-ios-lobby-start-client-and-others")

	def test_rejects_a_missing_chinese_clients_not_ready_blocker(self):
		self._rewrite_pair(
			Path("engine/mods/common/fluent/zh-CN/chrome.ftl"),
			Path("mods/common/fluent/zh-CN/chrome.ftl"),
			lambda text: text.replace(
				"button-ios-lobby-start-clients-not-ready = { $count } 人地图未就绪\n",
				"",
			),
		)
		self.assert_error("zh-cn lobby blocker button-ios-lobby-start-clients-not-ready")

	def test_rejects_missing_multi_client_blocker_source_wiring(self):
		self._rewrite_source(
			Path("engine/OpenRA.Mods.Common/Widgets/Logic/Lobby/IosLobbyStartStatus.cs"),
			lambda text: text.replace(
				"localize(ClientAndOthers, new object[]",
				"localize(ClientsNotReady, new object[]",
			),
		)
		self.assert_error("multi-client blocker source wiring")

	def test_rejects_a_desktop_visible_map_status_widget(self):
		self._rewrite_pair(
			Path("engine/mods/common/chrome/lobby-players.yaml"),
			Path("mods/common/chrome/lobby-players.yaml"),
			lambda text: text.replace(
				"Label@IOS_MAP_STATUS_TEXT:\n"
				"\t\t\t\t\t\t\tVisible: false",
				"Label@IOS_MAP_STATUS_TEXT:\n"
				"\t\t\t\t\t\t\tVisible: true",
				1,
			),
		)
		self.assert_error("default-hidden ios map status")

	def test_rejects_downloading_100_as_a_valid_readiness_state(self):
		self._rewrite_source(
			Path("engine/OpenRA.Game/Network/ClientMapReadiness.cs"),
			lambda text: text.replace("progress is >= 0 and <= 99", "progress is >= 0 and <= 100"),
		)
		self.assert_error("downloading progress must stop at 99")

	def test_rejects_a_faction_picker_replaced_by_the_default_preset(self):
		self._rewrite_source(
			Path("engine/OpenRA.Mods.Common/Widgets/Logic/Lobby/LobbyUtils.cs"),
			lambda text: text.replace(
				"IosDropDownLayout.FactionPicker", "IosDropDownLayout.Default"
			),
		)
		self.assert_error("faction dropdown must use factionpicker")

	def test_rejects_a_missing_or_extra_touch_asset(self):
		(self.app / "mods/ra2/uibits/ios-touch-actions-allies.png").unlink()
		self.assert_error("touch asset is missing")

		data = png_bytes(32, 32, seed=222)
		(self.app / "mods/ra2/uibits/ios-touch-actions-extra.png").write_bytes(data)
		self.assert_error("exactly 18")

	def test_rejects_an_extra_touch_asset_outside_the_canonical_directory(self):
		extra = self.app / "nested/concepts/ios-touch-actions-allies.png"
		extra.parent.mkdir(parents=True)
		extra.write_bytes(png_bytes(512, 512, seed=223))
		self.assert_error("exactly 18")

	def test_rejects_a_touch_asset_hash_mismatch(self):
		(self.app / "mods/ra2/uibits/ios-touch-actions-allies.png").write_bytes(b"tampered")
		self.assert_error("touch asset hash mismatch")

	def test_rejects_wrong_ihdr_mode_or_dimensions(self):
		name = Path("mods/ra2/uibits/ios-touch-joystick-yuri.png")
		bad = png_bytes(256, 256, seed=2, bit_depth=16, color_type=2)
		(self.source / name).write_bytes(bad)
		(self.app / name).write_bytes(bad)
		self.assert_error("8-bit RGBA")

	def test_rejects_a_chrome_region_that_overflows_the_atlas(self):
		self._rewrite_pair(
			Path("mods/ra2/chrome.yaml"), Path("mods/ra2/chrome.yaml"),
			lambda text: text.replace("base: 0, 0, 144, 144", "base: 250, 250, 144, 144"),
		)
		self.assert_error("region overflow")

	def test_rejects_a_missing_unused_compatibility_action_atlas_region(self):
		self._rewrite_pair(
			Path("mods/ra2/chrome.yaml"), Path("mods/ra2/chrome.yaml"),
			lambda text: text.replace("\t\tios-stop: 0, 0, 56, 56\n", "", 1),
		)
		self.assert_error("required action region")

	def test_rejects_a_missing_semantic_shortcut_region(self):
		self._rewrite_pair(
			Path("mods/ra2/chrome.yaml"), Path("mods/ra2/chrome.yaml"),
			lambda text: text.replace("\t\tgroup-1: 3, 3, 26, 26\n", "", 1),
		)
		self.assert_error("semantic shortcut region")

	def test_rejects_an_overflowing_semantic_shortcut_region(self):
		self._rewrite_pair(
			Path("mods/ra2/chrome.yaml"), Path("mods/ra2/chrome.yaml"),
			lambda text: text.replace(
				"\t\tgroup-1: 3, 3, 26, 26\n",
				"\t\tgroup-1: 250, 120, 26, 26\n",
				1,
			),
		)
		self.assert_error("semantic shortcut region overflow")

	def test_rejects_a_missing_non_default_faction_action_region(self):
		def remove_yuri_stop(text):
			return text.replace(
				"ios-touch-actions-yuri:\n"
				"\tImage: ios-touch-actions-yuri.png\n"
				"\tImage2x: ios-touch-actions-yuri-2x.png\n"
				"\tRegions:\n"
				"\t\tios-stop: 0, 0, 56, 56\n",
				"ios-touch-actions-yuri:\n"
				"\tImage: ios-touch-actions-yuri.png\n"
				"\tImage2x: ios-touch-actions-yuri-2x.png\n"
				"\tRegions:\n",
				1,
			)

		self._rewrite_pair(
			Path("mods/ra2/chrome.yaml"), Path("mods/ra2/chrome.yaml"),
			remove_yuri_stop,
		)
		self.assert_error("required action region")

	def test_rejects_a_missing_joystick_region(self):
		def remove_active_thumb(text):
			return text.replace(
				"ios-touch-joystick-yuri:\n"
				"\tImage: ios-touch-joystick-yuri.png\n"
				"\tImage2x: ios-touch-joystick-yuri-2x.png\n"
				"\tRegions:\n"
				"\t\tbase: 0, 0, 144, 144\n"
				"\t\tthumb: 160, 0, 64, 64\n"
				"\t\tthumb-active: 160, 72, 64, 64\n",
				"ios-touch-joystick-yuri:\n"
				"\tImage: ios-touch-joystick-yuri.png\n"
				"\tImage2x: ios-touch-joystick-yuri-2x.png\n"
				"\tRegions:\n"
				"\t\tbase: 0, 0, 144, 144\n"
				"\t\tthumb: 160, 0, 64, 64\n",
				1,
			)

		self._rewrite_pair(
			Path("mods/ra2/chrome.yaml"), Path("mods/ra2/chrome.yaml"),
			remove_active_thumb,
		)
		self.assert_error("required joystick region")

	def test_rejects_a_missing_quickbar_toggle_region(self):
		def remove_expand(text):
			return text.replace(
				"ios-touch-quickbar-toggle-yuri:\n"
				"\tImage: ios-touch-quickbar-yuri.png\n"
				"\tImage2x: ios-touch-quickbar-yuri-2x.png\n"
				"\tRegions:\n"
				"\t\tcollapse: 259, 67, 26, 26\n"
				"\t\texpand: 291, 67, 26, 26\n",
				"ios-touch-quickbar-toggle-yuri:\n"
				"\tImage: ios-touch-quickbar-yuri.png\n"
				"\tImage2x: ios-touch-quickbar-yuri-2x.png\n"
				"\tRegions:\n"
				"\t\tcollapse: 259, 67, 26, 26\n",
				1,
			)

		self._rewrite_pair(
			Path("mods/ra2/chrome.yaml"), Path("mods/ra2/chrome.yaml"),
			remove_expand,
		)
		self.assert_error("required quickbar toggle region")

	def test_rejects_a_stale_bundled_configuration(self):
		(self.app / "mods/ra2/metrics.yaml").write_text("stale\n", encoding="utf-8")
		self.assert_error("configuration hash mismatch")

	def test_rejects_stale_default_viewport_action_localization(self):
		(self.app / "mods/ra2/fluent/chrome.ftl").write_text("stale\n", encoding="utf-8")
		self.assert_error("configuration hash mismatch")

	def test_rejects_stale_zh_cn_viewport_action_localization(self):
		(self.app / "mods/ra2/fluent/zh-CN/chrome.ftl").write_text("stale\n", encoding="utf-8")
		self.assert_error("configuration hash mismatch")

	def test_rejects_wrong_default_viewport_action_label_even_when_source_matches(self):
		self._rewrite_pair(
			Path("mods/ra2/fluent/chrome.ftl"), Path("mods/ra2/fluent/chrome.ftl"),
			lambda text: text.replace("    .label = Type", "    .label = Same Type", 1),
		)
		self.assert_error("viewport action localization")

	def test_rejects_wrong_zh_cn_viewport_action_label_even_when_source_matches(self):
		self._rewrite_pair(
			Path("mods/ra2/fluent/zh-CN/chrome.ftl"),
			Path("mods/ra2/fluent/zh-CN/chrome.ftl"),
			lambda text: text.replace("    .label = 基地", "    .label = 返回", 1),
		)
		self.assert_error("viewport action localization")

	def test_rejects_a_manifest_that_does_not_activate_touch_runtime_files(self):
		self._rewrite_pair(
			Path("mods/ra2/mod.yaml"), Path("mods/ra2/mod.yaml"), lambda _: "",
		)
		self.assert_error("manifest wiring")

	def test_rejects_a_manifest_removal_of_required_wiring(self):
		self._rewrite_pair(
			Path("mods/ra2/mod.yaml"), Path("mods/ra2/mod.yaml"),
			lambda text: text.replace(
				"\tra2|chrome.yaml\n",
				"\tra2|chrome.yaml\n\t-ra2|chrome.yaml\n",
				1,
			),
		)
		self.assert_error("manifest removal")

	def test_rejects_a_duplicate_manifest_section_that_removes_wiring(self):
		self._rewrite_pair(
			Path("mods/ra2/mod.yaml"), Path("mods/ra2/mod.yaml"),
			lambda text: text + "\nChrome:\n\t-ra2|chrome.yaml\n",
		)
		self.assert_error("manifest wiring")

	def test_rejects_unexpected_root_chrome_or_metrics_sources(self):
		self._rewrite_pair(
			Path("mods/ra2/mod.yaml"), Path("mods/ra2/mod.yaml"),
			lambda text: text.replace(
				"\tra2|chrome.yaml\n",
				"\tra2|chrome.yaml\n\tra2|evil-chrome.yaml\n",
				1,
			).replace(
				"\tra2|metrics.yaml\n",
				"\tra2|metrics.yaml\n\tra2|evil-metrics.yaml\n",
				1,
			),
		)
		self.assert_error("unexpected manifest wiring")

	def test_collection_files_must_be_direct_properties(self):
		def nest_files_under_a_bogus_node(text):
			return text.replace(
				"ios-touch-actions-allies:\n"
				"\tImage: ios-touch-actions-allies.png\n"
				"\tImage2x: ios-touch-actions-allies-2x.png\n",
				"ios-touch-actions-allies:\n"
				"\tBogus:\n"
				"\t\tImage: ios-touch-actions-allies.png\n"
				"\t\tImage2x: ios-touch-actions-allies-2x.png\n",
				1,
			)

		self._rewrite_pair(
			Path("mods/ra2/chrome.yaml"), Path("mods/ra2/chrome.yaml"),
			nest_files_under_a_bogus_node,
		)
		self.assert_error("Image/Image2x")

	def test_collection_regions_must_be_a_direct_property(self):
		def nest_regions_under_a_bogus_node(text):
			return text.replace(
				"ios-touch-joystick-allies:\n"
				"\tImage: ios-touch-joystick-allies.png\n"
				"\tImage2x: ios-touch-joystick-allies-2x.png\n"
				"\tRegions:\n"
				"\t\tbase: 0, 0, 144, 144\n"
				"\t\tthumb: 160, 0, 64, 64\n"
				"\t\tthumb-active: 160, 72, 64, 64\n",
				"ios-touch-joystick-allies:\n"
				"\tImage: ios-touch-joystick-allies.png\n"
				"\tImage2x: ios-touch-joystick-allies-2x.png\n"
				"\tBogus:\n"
				"\t\tRegions:\n"
				"\t\t\tbase: 0, 0, 144, 144\n"
				"\t\t\tthumb: 160, 0, 64, 64\n"
				"\t\t\tthumb-active: 160, 72, 64, 64\n",
				1,
			)

		self._rewrite_pair(
			Path("mods/ra2/chrome.yaml"), Path("mods/ra2/chrome.yaml"),
			nest_regions_under_a_bogus_node,
		)
		self.assert_error("required joystick region")

	def test_collection_inheritance_must_be_a_direct_property(self):
		self._rewrite_pair(
			Path("mods/ra2/chrome.yaml"), Path("mods/ra2/chrome.yaml"),
			lambda text: text.replace(
				"ios-touch-actions-yuri-highlighted:\n"
				"\tInherits: ios-touch-actions-yuri\n",
				"ios-touch-actions-yuri-highlighted:\n"
				"\tBogus:\n"
				"\t\tInherits: ios-touch-actions-yuri\n",
				1,
			),
		)
		self.assert_error("Image/Image2x")

	def test_collection_panel_region_must_be_a_direct_property(self):
		self._rewrite_pair(
			Path("mods/ra2/chrome.yaml"), Path("mods/ra2/chrome.yaml"),
			lambda text: text.replace(
				"ios-touch-quickbar-panel-yuri:\n"
				"\tImage: ios-touch-quickbar-yuri.png\n"
				"\tImage2x: ios-touch-quickbar-yuri-2x.png\n"
				"\tPanelRegion: 0, 64, 12, 12, 104, 40, 12, 12\n",
				"ios-touch-quickbar-panel-yuri:\n"
				"\tImage: ios-touch-quickbar-yuri.png\n"
				"\tImage2x: ios-touch-quickbar-yuri-2x.png\n"
				"\tBogus:\n"
				"\t\tPanelRegion: 0, 64, 12, 12, 104, 40, 12, 12\n",
				1,
			),
		)
		self.assert_error("required PanelRegion")

	def test_rejects_a_removal_of_a_required_touch_collection(self):
		self._rewrite_pair(
			Path("mods/ra2/chrome.yaml"), Path("mods/ra2/chrome.yaml"),
			lambda text: text + "\n-ios-touch-actions-yuri:\n",
		)
		self.assert_error("required Chrome removal")

	def test_rejects_missing_touch_faction_metrics_even_when_source_matches(self):
		self._rewrite_pair(
			Path("mods/ra2/metrics.yaml"), Path("mods/ra2/metrics.yaml"),
			lambda text: text.replace("\tTouchFactionSuffix-yuri: yuri\n", ""),
		)
		self.assert_error("touch faction metric")

	def test_touch_faction_metrics_must_be_direct_children(self):
		self._rewrite_pair(
			Path("mods/ra2/metrics.yaml"), Path("mods/ra2/metrics.yaml"),
			lambda text: text.replace(
				"\tTouchFactionSuffix-yuri: yuri\n",
				"\tNestedDecoy:\n\t\tTouchFactionSuffix-yuri: yuri\n",
				1,
			),
		)
		self.assert_error("touch faction metric")

	def test_rejects_a_duplicate_metrics_section_that_overrides_a_value(self):
		self._rewrite_pair(
			Path("mods/ra2/metrics.yaml"), Path("mods/ra2/metrics.yaml"),
			lambda text: text + "\nMetrics:\n\tTouchFactionSuffix-yuri: soviets\n",
		)
		self.assert_error("touch faction metric")

	def test_rejects_missing_touch_size_ui_even_when_source_matches(self):
		self._rewrite_pair(
			Path("engine/mods/common/chrome/settings-input.yaml"),
			Path("mods/common/chrome/settings-input.yaml"),
			lambda text: text.replace(
				"DropDownButton@TOUCH_JOYSTICK_SIZE_DROPDOWN",
				"DropDownButton@REMOVED_TOUCH_SIZE",
			),
		)
		self.assert_error("touch-size settings UI")

	def test_touch_size_dropdown_cannot_be_spoofed_by_a_comment(self):
		def replace_with_comment_decoy(text):
			return text.replace(
				"DropDownButton@TOUCH_JOYSTICK_SIZE_DROPDOWN:",
				"DropDownButton@REMOVED_TOUCH_SIZE:\n"
				"# DropDownButton@TOUCH_JOYSTICK_SIZE_DROPDOWN:",
				1,
			)

		self._rewrite_pair(
			Path("engine/mods/common/chrome/settings-input.yaml"),
			Path("mods/common/chrome/settings-input.yaml"), replace_with_comment_decoy,
		)
		self.assert_error("touch-size settings UI")

	def test_rejects_old_active_action_fallbacks(self):
		self._rewrite_pair(
			Path("mods/ra2/chrome/ingame.yaml"),
			Path("mods/ra2/chrome/ingame.yaml"),
			lambda text: text.replace(
				"ImageCollection: ios-touch-actions-allies",
				"ImageCollection: ios-joystick-command-icons",
				1,
			),
		)
		self.assert_error("active action fallback")

	def test_rejects_wrong_active_action_label_mapping(self):
		self._rewrite_pair(
			Path("mods/ra2/chrome/ingame.yaml"), Path("mods/ra2/chrome/ingame.yaml"),
			lambda text: text.replace(
				"Text: button-ios-viewport-action-deploy.label",
				"Text: button-command-bar-stop.label",
				1,
			),
		)
		self.assert_error("active action label")

	def test_rejects_input_intercepting_active_action_label_children(self):
		self._rewrite_pair(
			Path("mods/ra2/chrome/ingame.yaml"), Path("mods/ra2/chrome/ingame.yaml"),
			lambda text: text.replace(
				"\t\t\t\t\t\t\t\tLabel@LABEL:",
				"\t\t\t\t\t\t\t\tColorBlock@LABEL_PLATE:\n"
				"\t\t\t\t\t\t\t\tLabel@LABEL:",
				1,
			),
		)
		self.assert_error("active action children")

	def test_rejects_active_action_labels_that_participate_in_mouse_over(self):
		self._rewrite_pair(
			Path("mods/ra2/chrome/ingame.yaml"), Path("mods/ra2/chrome/ingame.yaml"),
			lambda text: text.replace(
				"\t\t\t\t\t\t\t\t\tIgnoreMouseOver: true\n",
				"\t\t\t\t\t\t\t\t\tIgnoreMouseOver: false\n",
				2,
			),
		)
		self.assert_error("active action input")

	def test_action_fallbacks_must_come_from_the_active_widget_chain(self):
		def move_actions_to_dead_layout(text):
			marker = "\t\t\t\tContainer@IOS_VIEWPORT_ACTIONS:"
			start = text.index(marker)
			block = text[start:].rstrip().splitlines()
			dead = ["Container@DEAD_LAYOUT:", "\tChildren:"]
			dead.extend("\t\t" + line[4:] for line in block)
			return (
				text.replace(
					marker,
					"\t\t\t\tContainer@REMOVED_IOS_VIEWPORT_ACTIONS:",
					1,
				).rstrip() + "\n\n" + "\n".join(dead) + "\n"
			)

		self._rewrite_pair(
			Path("mods/ra2/chrome/ingame.yaml"),
			Path("mods/ra2/chrome/ingame.yaml"), move_actions_to_dead_layout,
		)
		self.assert_error("active action")

	def test_action_fallback_reads_only_the_direct_icon_child(self):
		def add_decoy(text):
			return text.replace(
				"\t\t\t\t\t\t\t\tImage@ICON:\n"
				"\t\t\t\t\t\t\t\t\tImageCollection: ios-touch-actions-allies\n"
				"\t\t\t\t\t\t\t\t\tImageName: ios-deploy",
				"\t\t\t\t\t\t\t\tImage@DECOY:\n"
				"\t\t\t\t\t\t\t\t\tImageCollection: ios-touch-actions-allies\n"
				"\t\t\t\t\t\t\t\t\tImageName: ios-deploy\n"
				"\t\t\t\t\t\t\t\tImage@ICON:\n"
				"\t\t\t\t\t\t\t\t\tImageCollection: ios-joystick-command-icons\n"
				"\t\t\t\t\t\t\t\t\tImageName: ios-deploy",
				1,
			)

		self._rewrite_pair(
			Path("mods/ra2/chrome/ingame.yaml"),
			Path("mods/ra2/chrome/ingame.yaml"), add_decoy,
		)
		self.assert_error("active action fallback")

	def test_action_fallback_ignores_a_nested_icon_decoy(self):
		def add_nested_decoy(text):
			return text.replace(
				"\t\t\t\t\t\t\t\tImage@ICON:\n"
				"\t\t\t\t\t\t\t\t\tImageCollection: ios-touch-actions-allies\n"
				"\t\t\t\t\t\t\t\t\tImageName: ios-deploy",
				"\t\t\t\t\t\t\t\tContainer@DECOY:\n"
				"\t\t\t\t\t\t\t\t\tChildren:\n"
				"\t\t\t\t\t\t\t\t\t\tImage@ICON:\n"
				"\t\t\t\t\t\t\t\t\t\t\tImageCollection: ios-touch-actions-allies\n"
				"\t\t\t\t\t\t\t\t\t\t\tImageName: ios-deploy\n"
				"\t\t\t\t\t\t\t\tImage@ICON:\n"
				"\t\t\t\t\t\t\t\t\tImageCollection: ios-joystick-command-icons\n"
				"\t\t\t\t\t\t\t\t\tImageName: ios-deploy",
				1,
			)

		self._rewrite_pair(
			Path("mods/ra2/chrome/ingame.yaml"),
			Path("mods/ra2/chrome/ingame.yaml"), add_nested_decoy,
		)
		self.assert_error("active action fallback")

	def test_rejects_readded_compatibility_actions_from_the_active_viewport(self):
		for button in ("STOP", "RETURN_BASE"):
			with self.subTest(button=button):
				extra = "\n".join(active_action_button_lines(button)) + "\n"
				self._rewrite_pair(
					Path("mods/ra2/chrome/ingame.yaml"),
					Path("mods/ra2/chrome/ingame.yaml"),
					lambda _, extra=extra: ingame_text().replace(
						"\t\t\t\t\t\tButton@DEPLOY:\n",
						extra + "\t\t\t\t\t\tButton@DEPLOY:\n",
						1,
					),
				)
				self.assert_error("active action buttons")

	def test_rejects_a_missing_active_viewport_action(self):
		for button in ACTIVE_VIEWPORT_ACTIONS:
			with self.subTest(button=button):
				block = "\n".join(active_action_button_lines(button)) + "\n"
				self._rewrite_pair(
					Path("mods/ra2/chrome/ingame.yaml"),
					Path("mods/ra2/chrome/ingame.yaml"),
					lambda _, block=block: ingame_text().replace(block, "", 1),
				)
				self.assert_error("active action buttons")

	def test_rejects_reordered_active_viewport_actions(self):
		deploy = "\n".join(active_action_button_lines("DEPLOY")) + "\n"
		select_type = "\n".join(active_action_button_lines("SELECT_TYPE")) + "\n"
		self._rewrite_pair(
			Path("mods/ra2/chrome/ingame.yaml"),
			Path("mods/ra2/chrome/ingame.yaml"),
			lambda _: ingame_text().replace(
				deploy + select_type,
				select_type + deploy,
				1,
			),
		)
		self.assert_error("active action buttons")

	def test_rejects_a_duplicate_active_action_button_override(self):
		duplicate = "\n".join(active_action_button_lines("DEPLOY")) + "\n"
		self._rewrite_pair(
			Path("mods/ra2/chrome/ingame.yaml"),
			Path("mods/ra2/chrome/ingame.yaml"),
			lambda text: text.replace(
				"\t\t\t\t\t\tButton@SELECT_TYPE:\n",
				duplicate + "\t\t\t\t\t\tButton@SELECT_TYPE:\n",
				1,
			),
		)
		self.assert_error("active action buttons")

	def test_rejects_a_reordered_command_slot_catalog(self):
		def swap(text):
			return text.replace("Button@GROUP_01", "Button@TEMP").replace(
				"Button@GROUP_02", "Button@GROUP_01").replace("Button@TEMP", "Button@GROUP_02")

		self._rewrite_pair(
			Path("mods/ra2/chrome/ingame-player.yaml"),
			Path("mods/ra2/chrome/ingame-player.yaml"), swap,
		)
		self.assert_error("command slot order")

	def test_command_slots_must_come_from_the_active_widget_chain(self):
		def move_slots_to_dead_layout(text):
			marker = "\t\t\t\tContainer@COMMAND_SLOTS:"
			start = text.index(marker)
			block = text[start:].rstrip().splitlines()
			dead = ["Container@DEAD_LAYOUT:", "\tChildren:"]
			dead.extend("\t\t" + line[4:] for line in block)
			return (
				text.replace(
					marker, "\t\t\t\tContainer@REMOVED_COMMAND_SLOTS:", 1
				).rstrip() + "\n\n" + "\n".join(dead) + "\n"
			)

		self._rewrite_pair(
			Path("mods/ra2/chrome/ingame-player.yaml"),
			Path("mods/ra2/chrome/ingame-player.yaml"), move_slots_to_dead_layout,
		)
		self.assert_error("COMMAND_SLOTS")

	def test_rejects_changed_semantic_shortcut_icons_or_atlas_bytes(self):
		self._rewrite_pair(
			Path("mods/ra2/chrome/ingame-player.yaml"),
			Path("mods/ra2/chrome/ingame-player.yaml"),
			lambda text: text.replace(
				"ImageCollection: production-x5-icon",
				"ImageCollection: ios-commandbar-icons-v2",
			),
		)
		self.assert_error("semantic shortcut")

		(self.app / "mods/ra2/uibits/production-x5-icon.png").write_bytes(b"stale")
		self.assert_error("semantic atlas hash mismatch")

	def test_semantic_shortcut_reads_only_the_direct_icon_child(self):
		def add_decoy(text):
			return text.replace(
				"\t\t\t\t\t\t\t\tImage@ICON:\n"
				"\t\t\t\t\t\t\t\t\tImageCollection: production-x5-icon\n"
				"\t\t\t\t\t\t\t\t\tImageName: production-x5",
				"\t\t\t\t\t\t\t\tImage@DECOY:\n"
				"\t\t\t\t\t\t\t\t\tImageCollection: production-x5-icon\n"
				"\t\t\t\t\t\t\t\t\tImageName: production-x5\n"
				"\t\t\t\t\t\t\t\tImage@ICON:\n"
				"\t\t\t\t\t\t\t\t\tImageCollection: commandbar-icons\n"
				"\t\t\t\t\t\t\t\t\tImageName: production-x5",
				1,
			)

		self._rewrite_pair(
			Path("mods/ra2/chrome/ingame-player.yaml"),
			Path("mods/ra2/chrome/ingame-player.yaml"), add_decoy,
		)
		self.assert_error("semantic shortcut")

	def test_rejects_a_duplicate_semantic_icon_override(self):
		self._rewrite_pair(
			Path("mods/ra2/chrome/ingame-player.yaml"),
			Path("mods/ra2/chrome/ingame-player.yaml"),
			lambda text: text.replace(
				"\t\t\t\t\t\t\t\t\tImageName: production-x5\n",
				"\t\t\t\t\t\t\t\t\tImageName: production-x5\n"
				"\t\t\t\t\t\t\t\tImage@ICON:\n"
				"\t\t\t\t\t\t\t\t\tImageCollection: commandbar-icons\n"
				"\t\t\t\t\t\t\t\t\tImageName: production-x5\n",
				1,
			),
		)
		self.assert_error("semantic shortcut")

	def test_rejects_a_synchronized_semantic_atlas_replacement(self):
		name = Path("mods/ra2/uibits/production-x5-icon.png")
		replacement = png_bytes(512, 128, seed=231)
		(self.source / name).write_bytes(replacement)
		(self.app / name).write_bytes(replacement)
		self.assert_error("frozen semantic atlas hash")

	def test_rejects_missing_clr_metadata_even_if_raw_bytes_contain_the_token(self):
		metadata = ASSEMBLY_METADATA["OpenRA.Mods.Common.dll"]
		missing = set(metadata["strings"])
		missing.remove(
			"OpenRA.Mods.Common.Widgets.Logic.Ingame.IosSupportPowerLayoutPolicy"
		)
		definitions = copy.deepcopy(metadata["definitions"])
		del definitions[
			"OpenRA.Mods.Common.Widgets.Logic.Ingame.IosSupportPowerLayoutPolicy"
		]
		data = managed_pe(
			missing, metadata["user_strings"], raw_suffix=b"IosSupportPowerLayoutPolicy",
			definitions=definitions,
		)
		(self.app / "OpenRA.Mods.Common.dll").write_bytes(data)
		(self.managed / "OpenRA.Mods.Common.dll").write_bytes(data)
		self.assert_error("metadata definition")

	def test_rejects_clr_heaps_without_a_metadata_table_stream(self):
		metadata = ASSEMBLY_METADATA["OpenRA.Mods.Common.dll"]
		data = managed_pe(
			metadata["strings"], metadata["user_strings"],
			definitions=metadata["definitions"], include_tables=False,
		)
		(self.app / "OpenRA.Mods.Common.dll").write_bytes(data)
		(self.managed / "OpenRA.Mods.Common.dll").write_bytes(data)
		self.assert_error("metadata table stream")

	def test_rejects_a_required_type_name_that_is_only_heap_garbage(self):
		metadata = ASSEMBLY_METADATA["OpenRA.Mods.Common.dll"]
		definitions = copy.deepcopy(metadata["definitions"])
		del definitions[
			"OpenRA.Mods.Common.Widgets.Logic.Ingame.IosSupportPowerLayoutPolicy"
		]
		data = managed_pe(
			metadata["strings"], metadata["user_strings"], definitions=definitions,
		)
		(self.app / "OpenRA.Mods.Common.dll").write_bytes(data)
		(self.managed / "OpenRA.Mods.Common.dll").write_bytes(data)
		self.assert_error("TypeDef")

	def test_rejects_a_required_type_that_exists_only_as_a_typeref(self):
		metadata = ASSEMBLY_METADATA["OpenRA.Mods.Common.dll"]
		type_name = (
			"OpenRA.Mods.Common.Widgets.Logic.Ingame.IosSupportPowerLayoutPolicy"
		)
		definitions = copy.deepcopy(metadata["definitions"])
		del definitions[type_name]
		data = managed_pe(
			metadata["strings"], metadata["user_strings"], definitions=definitions,
			type_references={type_name},
		)
		(self.app / "OpenRA.Mods.Common.dll").write_bytes(data)
		(self.managed / "OpenRA.Mods.Common.dll").write_bytes(data)
		self.assert_error("TypeDef")

	def test_rejects_a_required_field_owned_by_the_wrong_type(self):
		metadata = ASSEMBLY_METADATA["OpenRA.Game.dll"]
		definitions = copy.deepcopy(metadata["definitions"])
		definitions["OpenRA.GameSettings"]["fields"].remove("IosVirtualJoystickSize")
		definitions["OpenRA.IosScreenSnapshot"]["fields"] = {
			"IosVirtualJoystickSize"
		}
		data = managed_pe(
			metadata["strings"], metadata["user_strings"], definitions=definitions,
		)
		(self.app / "OpenRA.Game.dll").write_bytes(data)
		(self.managed / "OpenRA.Game.dll").write_bytes(data)
		self.assert_error("Field")

	def test_rejects_a_required_method_owned_by_the_wrong_type(self):
		metadata = ASSEMBLY_METADATA["OpenRA.Game.dll"]
		definitions = copy.deepcopy(metadata["definitions"])
		definitions["OpenRA.VirtualViewportJoystickState"]["methods"].remove(
			"SetRadius"
		)
		definitions["OpenRA.IosScreenSnapshot"]["methods"].add("SetRadius")
		data = managed_pe(
			metadata["strings"], metadata["user_strings"], definitions=definitions,
		)
		(self.app / "OpenRA.Game.dll").write_bytes(data)
		(self.managed / "OpenRA.Game.dll").write_bytes(data)
		self.assert_error("MethodDef")

	def test_rejects_missing_compiled_safe_start_rejection_notification(self):
		metadata = ASSEMBLY_METADATA["OpenRA.Game.dll"]
		definitions = copy.deepcopy(metadata["definitions"])
		definitions["OpenRA.Network.OrderManager"]["methods"].remove(
			"NotifySafeStartRejected"
		)
		data = managed_pe(
			metadata["strings"], metadata["user_strings"], definitions=definitions,
		)
		(self.app / "OpenRA.Game.dll").write_bytes(data)
		(self.managed / "OpenRA.Game.dll").write_bytes(data)
		self.assert_error("NotifySafeStartRejected")

	def test_rejects_a_bundle_without_the_compiled_action_label_layout_policy(self):
		metadata = ASSEMBLY_METADATA["OpenRA.Mods.Common.dll"]
		definitions = copy.deepcopy(metadata["definitions"])
		del definitions["OpenRA.Mods.Common.Widgets.IosViewportActionLabelLayout"]
		data = managed_pe(
			metadata["strings"], metadata["user_strings"], definitions=definitions,
		)
		(self.app / "OpenRA.Mods.Common.dll").write_bytes(data)
		(self.managed / "OpenRA.Mods.Common.dll").write_bytes(data)
		self.assert_error("IosViewportActionLabelLayout")

	def test_rejects_a_bundle_without_the_compiled_action_label_application_seam(self):
		metadata = ASSEMBLY_METADATA["OpenRA.Mods.Common.dll"]
		definitions = copy.deepcopy(metadata["definitions"])
		definitions["OpenRA.Mods.Common.Widgets.IosViewportActionLabelLayout"]["methods"].remove(
			"ApplyTo"
		)
		data = managed_pe(
			metadata["strings"], metadata["user_strings"], definitions=definitions,
		)
		(self.app / "OpenRA.Mods.Common.dll").write_bytes(data)
		(self.managed / "OpenRA.Mods.Common.dll").write_bytes(data)
		self.assert_error("ApplyTo")

	def test_rejects_a_bundle_without_the_responsive_lobby_layout_policy(self):
		metadata = ASSEMBLY_METADATA["OpenRA.Mods.Common.dll"]
		definitions = copy.deepcopy(metadata["definitions"])
		del definitions["OpenRA.Mods.Common.Widgets.Logic.IosMapPreviewLayout"]
		data = managed_pe(
			metadata["strings"], metadata["user_strings"], definitions=definitions,
		)
		(self.app / "OpenRA.Mods.Common.dll").write_bytes(data)
		(self.managed / "OpenRA.Mods.Common.dll").write_bytes(data)
		self.assert_error("IosMapPreviewLayout")

	def test_rejects_a_bundle_without_the_scroll_arrow_centering_policy(self):
		metadata = ASSEMBLY_METADATA["OpenRA.Mods.Common.dll"]
		definitions = copy.deepcopy(metadata["definitions"])
		definitions["OpenRA.Mods.Common.Widgets.Logic.IosTouchWidgetPolicy"]["methods"].remove(
			"CenteredDecorationOrigin"
		)
		data = managed_pe(
			metadata["strings"], metadata["user_strings"], definitions=definitions,
		)
		(self.app / "OpenRA.Mods.Common.dll").write_bytes(data)
		(self.managed / "OpenRA.Mods.Common.dll").write_bytes(data)
		self.assert_error("CenteredDecorationOrigin")

	def test_rejects_a_user_string_that_is_not_referenced_by_ldstr(self):
		metadata = ASSEMBLY_METADATA["OpenRA.Mods.Common.dll"]
		references = set(metadata["user_strings"])
		references.remove("ios-touch-actions-")
		data = managed_pe(
			metadata["strings"], metadata["user_strings"],
			definitions=metadata["definitions"],
			referenced_user_strings=references,
		)
		(self.app / "OpenRA.Mods.Common.dll").write_bytes(data)
		(self.managed / "OpenRA.Mods.Common.dll").write_bytes(data)
		self.assert_error("ldstr")

	def test_rejects_a_runtime_literal_referenced_by_the_wrong_method(self):
		metadata = ASSEMBLY_METADATA["OpenRA.Mods.Common.dll"]
		data = managed_pe(
			metadata["strings"], metadata["user_strings"],
			definitions=metadata["definitions"],
			reference_overrides={
				"ios-touch-actions-": (
					"OpenRA.Mods.Common.Widgets.TouchFactionSkin", "Resolve"
				),
			},
		)
		(self.app / "OpenRA.Mods.Common.dll").write_bytes(data)
		(self.managed / "OpenRA.Mods.Common.dll").write_bytes(data)
		self.assert_error("ActionCollection")

	def test_does_not_mistake_an_ldstr_token_hidden_inside_an_operand(self):
		metadata = ASSEMBLY_METADATA["OpenRA.Mods.Common.dll"]
		literal = "ios-touch-actions-"
		references = set(metadata["user_strings"])
		references.remove(literal)
		target = (
			"OpenRA.Mods.Common.Widgets.TouchFactionSkin", "ActionCollection"
		)
		data = managed_pe(
			metadata["strings"], metadata["user_strings"],
			definitions=metadata["definitions"],
			referenced_user_strings=references,
			operand_decoys={literal: target},
		)
		(self.app / "OpenRA.Mods.Common.dll").write_bytes(data)
		(self.managed / "OpenRA.Mods.Common.dll").write_bytes(data)
		self.assert_error("ActionCollection")

	def test_rejects_ldstr_evidence_from_a_malformed_il_body(self):
		metadata = ASSEMBLY_METADATA["OpenRA.Mods.Common.dll"]
		target = (
			"OpenRA.Mods.Common.Widgets.TouchFactionSkin", "ActionCollection"
		)
		data = managed_pe(
			metadata["strings"], metadata["user_strings"],
			definitions=metadata["definitions"], malformed_methods={target},
		)
		(self.app / "OpenRA.Mods.Common.dll").write_bytes(data)
		(self.managed / "OpenRA.Mods.Common.dll").write_bytes(data)
		self.assert_error("ActionCollection")

	def test_accepts_valid_fat_il_and_unoptimized_pointer_tables(self):
		metadata = ASSEMBLY_METADATA["OpenRA.Mods.Common.dll"]
		fat_method = (
			"OpenRA.Mods.Common.Widgets.TouchFactionSkin", "QuickbarPanelCollection"
		)
		data = managed_pe(
			metadata["strings"], metadata["user_strings"],
			definitions=metadata["definitions"], fat_methods={fat_method},
			unoptimized=True,
		)
		(self.app / "OpenRA.Mods.Common.dll").write_bytes(data)
		(self.managed / "OpenRA.Mods.Common.dll").write_bytes(data)
		self.assertEqual((), self.errors())

	def test_rejects_an_out_of_range_typedef_member_list(self):
		metadata = ASSEMBLY_METADATA["OpenRA.Game.dll"]
		type_name = "OpenRA.GameSettings"
		data = managed_pe(
			metadata["strings"], metadata["user_strings"],
			definitions=metadata["definitions"],
			type_list_overrides={type_name: (0, 1)},
		)
		(self.app / "OpenRA.Game.dll").write_bytes(data)
		(self.managed / "OpenRA.Game.dll").write_bytes(data)
		self.assert_error("TypeDef field_start range")

	def test_rejects_an_invalid_unoptimized_field_pointer(self):
		metadata = ASSEMBLY_METADATA["OpenRA.Game.dll"]
		data = managed_pe(
			metadata["strings"], metadata["user_strings"],
			definitions=metadata["definitions"], unoptimized=True,
			field_pointer_overrides={1: 2},
		)
		(self.app / "OpenRA.Game.dll").write_bytes(data)
		(self.managed / "OpenRA.Game.dll").write_bytes(data)
		self.assert_error("FieldPtr")

	def test_rejects_a_clr_metadata_rva_in_a_virtual_only_section_tail(self):
		metadata = ASSEMBLY_METADATA["OpenRA.Mods.Common.dll"]
		data = bytearray(managed_pe(
			metadata["strings"], metadata["user_strings"],
			definitions=metadata["definitions"],
		))
		section = 0x80 + 24 + 0xF0
		struct.pack_into("<I", data, section + 16, 16)
		(self.app / "OpenRA.Mods.Common.dll").write_bytes(data)
		(self.managed / "OpenRA.Mods.Common.dll").write_bytes(data)
		self.assert_error("outside all PE sections")

	def test_rejects_an_undersized_clr_header_directory(self):
		metadata = ASSEMBLY_METADATA["OpenRA.Mods.Common.dll"]
		data = bytearray(managed_pe(
			metadata["strings"], metadata["user_strings"],
			definitions=metadata["definitions"],
		))
		optional = 0x80 + 24
		cli_directory = optional + 112 + 14 * 8
		struct.pack_into("<I", data, cli_directory + 4, 16)
		(self.app / "OpenRA.Mods.Common.dll").write_bytes(data)
		(self.managed / "OpenRA.Mods.Common.dll").write_bytes(data)
		self.assert_error("CLR header")

	def test_rejects_an_undeclared_clr_data_directory(self):
		metadata = ASSEMBLY_METADATA["OpenRA.Mods.Common.dll"]
		data = bytearray(managed_pe(
			metadata["strings"], metadata["user_strings"],
			definitions=metadata["definitions"],
		))
		optional = 0x80 + 24
		struct.pack_into("<I", data, optional + 108, 0)
		(self.app / "OpenRA.Mods.Common.dll").write_bytes(data)
		(self.managed / "OpenRA.Mods.Common.dll").write_bytes(data)
		self.assert_error("data directories")

	def test_rejects_a_managed_output_hash_mismatch(self):
		(self.managed / "OpenRA.Mods.RA2.dll").write_bytes(b"stale-build-output")
		self.assert_error("managed output hash mismatch")

	def test_rejects_legacy_compiled_joystick_defaults(self):
		metadata = ASSEMBLY_METADATA["OpenRA.Mods.Common.dll"]
		user_strings = set(metadata["user_strings"])
		user_strings.add("ios-viewport-joystick")
		data = managed_pe(
			metadata["strings"], user_strings,
			definitions=metadata["definitions"],
		)
		(self.app / "OpenRA.Mods.Common.dll").write_bytes(data)
		(self.managed / "OpenRA.Mods.Common.dll").write_bytes(data)
		self.assert_error("legacy joystick literal")

	def test_rejects_a_missing_new_compiled_joystick_fallback(self):
		metadata = ASSEMBLY_METADATA["OpenRA.Mods.Common.dll"]
		user_strings = set(metadata["user_strings"])
		user_strings.remove("ios-touch-joystick-allies")
		data = managed_pe(
			metadata["strings"], user_strings,
			definitions=metadata["definitions"],
		)
		(self.app / "OpenRA.Mods.Common.dll").write_bytes(data)
		(self.managed / "OpenRA.Mods.Common.dll").write_bytes(data)
		self.assert_error("#US metadata token")

	def test_rejects_a_missing_aot_payload_or_arm64_native_executable(self):
		(self.app / "OpenRA.Mods.RA2.aotdata.arm64").unlink()
		self.assert_error("AOT data is missing")

		(self.app / "NUCLEAR-CRISIS").write_bytes(b"not-macho")
		self.assert_error("arm64 Mach-O")

	def test_rejects_an_aot_module_bound_to_a_stale_managed_mvid(self):
		(self.app / "OpenRA.Mods.RA2.aotdata.arm64").write_bytes(
			b"\x00OpenRA.Game\x00FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF\x00"
		)
		self.assert_error("AOT dependency MVID mismatch")

	def test_rejects_a_truncated_macho_with_valid_magic_and_cpu(self):
		(self.app / "NUCLEAR-CRISIS").write_bytes(
			struct.pack("<Ii", 0xFEEDFACF, 0x0100000C)
		)
		self.assert_error("truncated arm64 Mach-O")

	def test_rejects_a_non_executable_macho_filetype(self):
		(self.app / "NUCLEAR-CRISIS").write_bytes(
			struct.pack("<IiiIIIII", 0xFEEDFACF, 0x0100000C, 0, 6, 0, 0, 0, 0)
		)
		self.assert_error("Mach-O filetype")

	def test_rejects_an_executable_without_a_segment_or_entrypoint_command(self):
		load_command = struct.pack("<II", 0x1B, 24) + b"\x00" * 16
		(self.app / "NUCLEAR-CRISIS").write_bytes(
			struct.pack(
				"<IiiIIIII", 0xFEEDFACF, 0x0100000C, 0, 2, 1, 24, 0, 0
			) + load_command
		)
		self.assert_error("LC_SEGMENT_64")

	def test_rejects_an_out_of_bounds_macho_segment(self):
		segment = struct.pack(
			"<II16sQQQQiiII", 0x19, 72, b"__TEXT", 0, 4096,
			0, 4096, 7, 5, 0, 0,
		)
		main = struct.pack("<IIQQ", 0x80000028, 24, 128, 0)
		binary = struct.pack(
			"<IiiIIIII", 0xFEEDFACF, 0x0100000C, 0, 2, 2, 96, 0, 0
		) + segment + main + b"\x00\x00\x00\x00"
		(self.app / "NUCLEAR-CRISIS").write_bytes(binary)
		self.assert_error("segment file range")

	def test_rejects_an_entrypoint_in_a_non_executable_segment(self):
		binary = bytearray((self.app / "NUCLEAR-CRISIS").read_bytes())
		struct.pack_into("<i", binary, 32 + 60, 0)
		(self.app / "NUCLEAR-CRISIS").write_bytes(binary)
		self.assert_error("executable segment")

	def test_rejects_an_empty_arm64_unixthread_command(self):
		command_size = 72 + 16
		file_size = 32 + command_size + 4
		segment = struct.pack(
			"<II16sQQQQiiII", 0x19, 72, b"__TEXT", 0, file_size,
			0, file_size, 7, 5, 0, 0,
		)
		unixthread = struct.pack("<IIII", 0x5, 16, 6, 0)
		binary = struct.pack(
			"<IiiIIIII", 0xFEEDFACF, 0x0100000C, 0, 2, 2, command_size, 0, 0
		) + segment + unixthread + b"\x00\x00\x00\x00"
		(self.app / "NUCLEAR-CRISIS").write_bytes(binary)
		self.assert_error("LC_UNIXTHREAD")

	def test_rejects_an_executable_macho_with_no_load_commands(self):
		(self.app / "NUCLEAR-CRISIS").write_bytes(
			struct.pack("<IiiIIIII", 0xFEEDFACF, 0x0100000C, 0, 2, 0, 0, 0, 0)
		)
		self.assert_error("at least one load command")

	def test_rejects_a_malformed_macho_load_command(self):
		header = struct.pack(
			"<IiiIIIII", 0xFEEDFACF, 0x0100000C, 0, 2, 1, 8, 0, 0
		)
		malformed_command = struct.pack("<II", 1, 4)
		(self.app / "NUCLEAR-CRISIS").write_bytes(header + malformed_command)
		self.assert_error("Mach-O load command")

	def test_rejects_a_non_dictionary_info_plist_without_crashing(self):
		with (self.app / "Info.plist").open("wb") as stream:
			plistlib.dump([], stream)
		errors = audit_bundle(self.app, self.source, self.managed)
		self.assertIsInstance(errors, tuple)
		self.assertIn("dictionary", "\n".join(errors).casefold())

	def test_rejects_a_missing_generated_registry_entry(self):
		metadata = ASSEMBLY_METADATA["OpenRA.iOS.dll"]
		user_strings = set(metadata["user_strings"])
		user_strings.remove("OpenRA.Mods.RA2.Widgets.CustomCommandBarWidget")
		data = managed_pe(
			metadata["strings"], user_strings,
			definitions=metadata["definitions"],
		)
		(self.app / "OpenRA.iOS.dll").write_bytes(data)
		(self.managed / "OpenRA.iOS.dll").write_bytes(data)
		self.assert_error("AOT registry entry")

	def test_rejects_a_missing_support_power_tracker_registry_entry(self):
		metadata = ASSEMBLY_METADATA["OpenRA.iOS.dll"]
		user_strings = set(metadata["user_strings"])
		user_strings.remove(
			"OpenRA.Mods.Common.Widgets.Logic.Ingame.IosSupportPowerLayoutTracker"
		)
		data = managed_pe(
			metadata["strings"], user_strings,
			definitions=metadata["definitions"],
		)
		(self.app / "OpenRA.iOS.dll").write_bytes(data)
		(self.managed / "OpenRA.iOS.dll").write_bytes(data)
		self.assert_error("AOT registry entry")

	def test_rejects_concept_artifacts_anywhere_in_the_app(self):
		leak = self.app / "design-demos/faction-touch-controls-control-kit-screenshot.png"
		leak.parent.mkdir()
		leak.write_bytes(b"concept")
		self.assert_error("concept artifact")

	def test_returns_errors_instead_of_crashing_on_a_bundle_symlink_loop(self):
		mods = self.app / "mods"
		mods.rename(self.app / "real-mods")
		mods.symlink_to("mods", target_is_directory=True)
		errors = audit_bundle(self.app, self.source, self.managed)
		self.assertIsInstance(errors, tuple)
		self.assertIn("symlink", "\n".join(errors).casefold())

	def test_cli_returns_nonzero_for_a_failed_audit(self):
		(self.app / "mods/ra2/uibits/ios-touch-actions-allies.png").unlink()
		result = subprocess.run(
			(
				"python3", str(SCRIPT), str(self.app), "--source-root", str(self.source),
				"--managed-output-root", str(self.managed),
			),
			check=False, capture_output=True, text=True,
		)
		self.assertNotEqual(0, result.returncode)
		self.assertIn("touch asset is missing", result.stderr.casefold())


class IosTouchRuntimeBundleProjectTargetTest(unittest.TestCase):
	def test_incremental_aot_invalidates_the_complete_hard_bound_closure(self):
		root = ElementTree.parse(PROJECT).getroot()
		target = root.find("./Target[@Name='InvalidateIosAotDependencyClosure']")
		self.assertIsNotNone(target)
		self.assertEqual("_AOTCompile", target.get("BeforeTargets"))
		self.assertEqual("_LoadLinkerOutput", target.get("DependsOnTargets"))
		self.assertEqual("@(_AssembliesToAOT)", target.get("Inputs"))
		self.assertEqual(
			"$(DeviceSpecificIntermediateOutputPath)aot-dependency-closure.stamp",
			target.get("Outputs"),
		)
		self.assertIn("$(RuntimeIdentifier)", target.get("Condition", ""))
		self.assertNotIn("$(PublicClean)", target.get("Condition", ""))

		delete = target.find("Delete")
		self.assertIsNotNone(delete)
		self.assertCountEqual(
			(
				"@(_AssembliesToAOT->'%(AOTData)')",
				"@(_AssembliesToAOT->'%(AOTAssembly)')",
				"@(_AssembliesToAOT->'%(LLVMFile)')",
				"@(_AssembliesToAOT->'%(ObjectFile)')",
			),
			delete.get("Files", "").split(";"),
		)

		stamp_target = root.find("./Target[@Name='StampIosAotDependencyClosure']")
		self.assertIsNotNone(stamp_target)
		self.assertEqual("_AOTCompile", stamp_target.get("AfterTargets"))
		stamp_condition = stamp_target.get("Condition", "")
		for required in (
			"$(RuntimeIdentifier)", "$(_RunAotCompiler)", "$(IsMacEnabled)",
			"@(_AssembliesToAOT)",
		):
			self.assertIn(required, stamp_condition)
		self.assertNotIn("_IosAotDependencyClosureInvalidated", stamp_condition)
		touch = stamp_target.find("Touch")
		self.assertIsNotNone(touch)
		self.assertEqual(target.get("Outputs"), touch.get("Files"))
		self.assertEqual("true", touch.get("AlwaysCreate"))

	def test_target_runs_after_bundle_and_loading_audit_but_before_codesign(self):
		root = ElementTree.parse(PROJECT).getroot()
		target = root.find("./Target[@Name='AuditPersonalIosTouchRuntimeBundle']")
		self.assertIsNotNone(target)
		self.assertEqual("_CreateAppBundle", target.get("AfterTargets"))
		self.assertEqual("_CodesignAppBundle", target.get("BeforeTargets"))
		self.assertEqual(
			"AuditPersonalIosLoadingBrandBundle", target.get("DependsOnTargets")
		)
		self.assertEqual(
			"'$(PublicClean)' != 'true' and '$(RuntimeIdentifier)' == 'ios-arm64'",
			target.get("Condition"),
		)

		exec_node = target.find("Exec")
		self.assertIsNotNone(exec_node)
		command = exec_node.get("Command", "")
		self.assertIn("packaging/audit_ios_touch_runtime_bundle.py", command)
		self.assertIn("$(AppBundleDir)", command)
		self.assertIn("--source-root", command)
		self.assertIn("$(MSBuildProjectDirectory)/../..", command)
		self.assertIn("--managed-output-root", command)
		self.assertIn("$(TargetDir)", command)
		self.assertNotIn("prepare", command.casefold())
		self.assertNotIn("delete", command.casefold())
		self.assertNotIn("rm ", command.casefold())

	def test_audit_script_has_no_destructive_cleanup_primitives(self):
		text = SCRIPT.read_text(encoding="utf-8")
		for forbidden in (".unlink(", "rmtree(", "os.remove(", "shutil.rmtree("):
			self.assertNotIn(forbidden, text)


if __name__ == "__main__":
	unittest.main()
