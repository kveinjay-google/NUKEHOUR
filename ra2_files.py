"""红警 2 启动器：回放 / 存档 / 地图 / 内容资产的纯 Python 解析与管理。

文件格式均来自引擎源码（release-20250330）：
- .orarep: 订单帧序列(client:int32, packetLen:int32, packet)，client == -1 后是
  MetaVersion:int32 + 7bit 长度前缀 UTF8 MiniYaml（GameInformation）
- .orasav: 末尾 12 字节 = metadataOffset, traitDataOffset, EOFMarker(-2)；
  metadataOffset 处 = MetadataMarker(-1), LastOrdersFrame, LastSyncFrame,
  syncPacket(13B), 随后是三个长度前缀 MiniYaml：globalSettings / slots / slotClients
- 地图: 目录（内置）或 .oramap zip（玩家），内含 map.yaml
"""

import datetime
import hashlib
import json
import os
import re
import shutil
import stat
import struct
import subprocess
import uuid
import zipfile

REPO = os.path.dirname(os.path.abspath(__file__))
SUPPORT = os.path.expanduser("~/Library/Application Support/Nuke Hour")
LEGACY_SUPPORT = os.path.expanduser("~/Library/Application Support/OpenRA")
MOD_ID = "ra2"

LEGACY_SUPPORT_PATHS = (
    "settings.yaml",
    "ra2-commandbar.yaml",
    "skirmish.ra2.yaml",
    "Content/ra2",
    "maps/ra2",
    "Saves/ra2",
    "Replays/ra2",
    "Screenshots/ra2",
    "Editor/ra2",
)

EOF_MARKER = -2
METADATA_MARKER = -1
SYNC_PACKET_LEN = 13


def mod_version():
    try:
        with open(os.path.join(REPO, "mods", MOD_ID, "mod.yaml"), encoding="utf-8") as f:
            for line in f:
                m = re.match(r"\s*Version:\s*(\S+)", line)
                if m:
                    return m.group(1)
    except OSError:
        pass
    return "{DEV_VERSION}"


def _kind_dir(kind):
    """kind: Replays / Saves / maps —— 返回 SupportDir 下该类的模组目录（含版本子目录的上级）"""
    return os.path.join(SUPPORT, kind, MOD_ID)


def saves_dir():
    return os.path.join(_kind_dir("Saves"), mod_version())


def replays_dir():
    return os.path.join(_kind_dir("Replays"), mod_version())


def user_maps_dir():
    return os.path.join(_kind_dir("maps"), mod_version())


def content_dir():
    return os.path.join(SUPPORT, "Content", MOD_ID)


# ------------------------------------------------------------ 二进制读取

def _read_lp_string(data, pos):
    """BinaryReader.ReadString 风格：7bit 变长长度前缀 + UTF-8 字节。"""
    shift = 0
    length = 0
    while True:
        if pos >= len(data):
            raise ValueError("长度前缀越界")
        b = data[pos]
        pos += 1
        length |= (b & 0x7F) << shift
        if not (b & 0x80):
            break
        shift += 7
    text = data[pos:pos + length].decode("utf-8", "replace")
    return text.lstrip("\x00"), pos + length


def _miniyaml_blocks(text):
    """解析两层 MiniYaml：返回 (顶层键值dict, [(块名, {子键:值}), ...])。

    块名不含 @ 后缀（如 Player@0 -> Player）。仅支持单层缩进，足够
    GameInformation / globalSettings / slotClients / map.yaml 顶层使用。
    """
    top = {}
    blocks = []
    cur = None
    for raw in text.splitlines():
        if raw.startswith("\t") or raw.startswith("    "):
            if cur is not None:
                line = raw.strip()
                if ":" in line:
                    k, v = line.split(":", 1)
                    cur[k.strip()] = v.strip()
            continue
        line = raw.rstrip()
        if not line:
            continue
        if line.endswith(":"):
            name = line[:-1].strip().split("@")[0]
            cur = {}
            blocks.append((name, cur))
        elif ":" in line:
            k, v = line.split(":", 1)
            top[k.strip()] = v.strip()
            cur = None
    return top, blocks


def _fmt_dt(text):
    """'2026-07-29 14-46-31' -> '2026-07-29 14:46'"""
    m = re.match(r"(\d{4}-\d{2}-\d{2}) (\d{2})-(\d{2})(?:-(\d{2}))?", text or "")
    if not m:
        return text or "未知"
    return f"{m.group(1)} {m.group(2)}:{m.group(3)}"


def _parse_dt(text):
    m = re.match(r"(\d{4})-(\d{2})-(\d{2}) (\d{2})-(\d{2})-(\d{2})", text or "")
    if not m:
        return None
    try:
        return datetime.datetime(*[int(x) for x in m.groups()])
    except ValueError:
        return None


def _fmt_size(n):
    if n >= 1 << 20:
        return f"{n / (1 << 20):.1f} MB"
    if n >= 1 << 10:
        return f"{n / (1 << 10):.0f} KB"
    return f"{n} B"


# ------------------------------------------------------------ 回放

def parse_replay(path):
    info = {"path": path, "name": os.path.splitext(os.path.basename(path))[0],
            "size": os.path.getsize(path), "mtime": os.path.getmtime(path),
            "ok": False, "error": None}
    try:
        with open(path, "rb") as f:
            data = f.read()
        pos = 0
        while pos + 8 <= len(data):
            client, = struct.unpack_from("<i", data, pos)
            pos += 4
            if client == -1:
                _ver, = struct.unpack_from("<i", data, pos)
                pos += 4
                text, _ = _read_lp_string(data, pos)
                top, blocks = _miniyaml_blocks(text)
                root = next((b for n, b in blocks if n == "Root"), top)
                info["map_title"] = root.get("MapTitle", "未知地图")
                info["map_uid"] = root.get("MapUid", "")
                info["start"] = _fmt_dt(root.get("StartTimeUtc"))
                start_dt = _parse_dt(root.get("StartTimeUtc"))
                end_dt = _parse_dt(root.get("EndTimeUtc"))
                if start_dt and end_dt and end_dt > start_dt:
                    secs = int((end_dt - start_dt).total_seconds())
                    info["duration"] = f"{secs // 60} 分 {secs % 60} 秒"
                else:
                    info["duration"] = "未知"
                players = []
                for name, blk in blocks:
                    if name != "Player":
                        continue
                    players.append({
                        "name": blk.get("Name", "?"),
                        "faction": blk.get("DisplayFactionName") or blk.get("FactionName", "?"),
                        "is_bot": blk.get("IsBot") == "True",
                        "outcome": blk.get("Outcome", ""),
                    })
                info["players"] = players
                info["ok"] = True
                return info
            plen, = struct.unpack_from("<i", data, pos)
            pos += 4 + plen
        info["error"] = "未找到元数据"
    except Exception as exc:  # 防御：任何坏文件都不能让启动器崩
        info["error"] = str(exc)
    return info


def list_replays():
    base = _kind_dir("Replays")
    out = []
    if os.path.isdir(base):
        for root, _dirs, files in os.walk(base):
            for fn in files:
                if fn.endswith(".orarep"):
                    out.append(parse_replay(os.path.join(root, fn)))
    out.sort(key=lambda r: r["mtime"], reverse=True)
    return out


# ------------------------------------------------------------ 存档

def parse_save(path):
    info = {"path": path, "name": os.path.splitext(os.path.basename(path))[0],
            "size": os.path.getsize(path), "mtime": os.path.getmtime(path),
            "ok": False, "error": None}
    try:
        with open(path, "rb") as f:
            data = f.read()
        if len(data) < 12:
            raise ValueError("文件过小")
        metadata_offset, _trait_offset, eof = struct.unpack_from("<iii", data, len(data) - 12)
        if eof != EOF_MARKER:
            raise ValueError("无效的 orasav 结尾标记")
        pos = metadata_offset
        marker, = struct.unpack_from("<i", data, pos)
        if marker != METADATA_MARKER:
            raise ValueError("无效的元数据标记")
        pos += 4 + 4 + 4 + SYNC_PACKET_LEN  # marker + 两个帧号 + syncPacket
        gs_text, pos = _read_lp_string(data, pos)
        _slots_text, pos = _read_lp_string(data, pos)
        sc_text, pos = _read_lp_string(data, pos)

        _top, gs_blocks = _miniyaml_blocks(gs_text)
        gs = gs_blocks[0][1] if gs_blocks else {}
        info["map_uid"] = gs.get("Map", "")

        players = []
        _top, sc_blocks = _miniyaml_blocks(sc_text)
        for name, blk in sc_blocks:
            if name == "SlotClient" and blk.get("Name"):
                players.append(blk["Name"])
        info["players"] = players
        info["ok"] = True
    except Exception as exc:
        info["error"] = str(exc)
    return info


def list_saves():
    base = _kind_dir("Saves")
    out = []
    if os.path.isdir(base):
        for root, _dirs, files in os.walk(base):
            for fn in files:
                if fn.endswith(".orasav"):
                    out.append(parse_save(os.path.join(root, fn)))
    out.sort(key=lambda r: r["mtime"], reverse=True)
    return out


# ------------------------------------------------------------ 地图

def _parse_map_yaml_text(text):
    top, blocks = _miniyaml_blocks(text)
    players_blk = next((b for n, b in blocks if n == "Players"), {})
    slots = [k.split("@", 1)[1] for k in players_blk if k.startswith("PlayerReference@")]
    size = top.get("MapSize", "")
    wh = ""
    if "," in size:
        parts = [p.strip() for p in size.split(",")]
        if len(parts) == 2:
            wh = f"{parts[0]}×{parts[1]}"
    return {
        "title": top.get("Title", ""),
        "author": top.get("Author", ""),
        "tileset": top.get("Tileset", ""),
        "size": wh or size,
        "players": len(slots),
        "slots": slots,
        "categories": top.get("Categories", ""),
    }


def _parse_map_dir(path):
    yaml_path = os.path.join(path, "map.yaml")
    if not os.path.isfile(yaml_path):
        raise ValueError("缺少 map.yaml")
    with open(yaml_path, encoding="utf-8", errors="replace") as f:
        return _parse_map_yaml_text(f.read())


def _parse_map_zip(path):
    with zipfile.ZipFile(path) as zf:
        with zf.open("map.yaml") as f:
            return _parse_map_yaml_text(f.read().decode("utf-8", "replace"))


def _collect_maps(base, builtin):
    out = []
    if not os.path.isdir(base):
        return out
    for entry in sorted(os.listdir(base)):
        full = os.path.join(base, entry)
        pkg = entry if os.path.isdir(full) else os.path.splitext(entry)[0]
        rec = {"path": full, "builtin": builtin, "ok": False, "pkg": pkg}
        try:
            if os.path.isdir(full):
                meta = _parse_map_dir(full)
            elif entry.endswith(".oramap") or entry.endswith(".zip"):
                meta = _parse_map_zip(full)
            else:
                continue
            rec.update(meta)
            rec["ok"] = True
            rec["name"] = meta.get("title") or entry
        except Exception as exc:
            rec["name"] = entry
            rec["error"] = str(exc)
        out.append(rec)
    return out


def list_maps():
    official = _collect_maps(os.path.join(REPO, "mods", MOD_ID, "maps"), builtin=True)
    custom = []
    mbase = _kind_dir("maps")
    if os.path.isdir(mbase):
        for root, dirs, files in os.walk(mbase):
            for d in dirs:
                full = os.path.join(root, d)
                if os.path.isfile(os.path.join(full, "map.yaml")):
                    rec = {"path": full, "builtin": False, "ok": False, "pkg": d}
                    try:
                        meta = _parse_map_dir(full)
                        rec.update(meta)
                        rec["ok"] = True
                        rec["name"] = meta.get("title") or d
                    except Exception as exc:
                        rec["name"] = d
                        rec["error"] = str(exc)
                    custom.append(rec)
            for fn in files:
                if fn.endswith(".oramap") or fn.endswith(".zip"):
                    full = os.path.join(root, fn)
                    rec = {"path": full, "builtin": False, "ok": False,
                           "pkg": os.path.splitext(fn)[0]}
                    try:
                        meta = _parse_map_zip(full)
                        rec.update(meta)
                        rec["ok"] = True
                        rec["name"] = meta.get("title") or fn
                    except Exception as exc:
                        rec["name"] = fn
                        rec["error"] = str(exc)
                    custom.append(rec)
    return official, custom


def import_map(src):
    """把 .oramap/.zip 复制到玩家地图目录，返回目标路径。"""
    dst_dir = user_maps_dir()
    os.makedirs(dst_dir, exist_ok=True)
    dst = os.path.join(dst_dir, os.path.basename(src))
    shutil.copy2(src, dst)
    return dst


def delete_file(path):
    """只允许删除 SupportDir 下的文件，防止误删游戏本体内容。"""
    real = os.path.realpath(path)
    if not real.startswith(os.path.realpath(SUPPORT) + os.sep):
        raise ValueError("只允许删除用户目录下的文件")
    os.remove(real)


def reveal_in_finder(path):
    subprocess.Popen(["open", "-R", path])


def open_dir(path):
    os.makedirs(path, exist_ok=True)
    subprocess.Popen(["open", path])


# ------------------------------------------------------------ 内容资产

MINIMUM_PACKAGES = ("ra2.mix", "language.mix")
CORE_PACKAGES = list(MINIMUM_PACKAGES)
OPTIONAL_PACKAGES = {
    "music": ("theme.mix", "thememd.mix"),
    "yuris_revenge": ("ra2md.mix", "langmd.mix"),
    "audio": ("audio.bag", "audio.idx"),
}
RETAIL_CONTENT_EXTENSIONS = frozenset({".mix", ".bag", ".idx"})
MAP_CONTENT_EXTENSIONS = frozenset({".map", ".mpr", ".yrm", ".oramap"})
REJECTED_EXECUTABLE_EXTENSIONS = frozenset({
    ".app", ".bat", ".cmd", ".com", ".dll", ".dylib", ".exe", ".js",
    ".msi", ".ps1", ".py", ".sh", ".so", ".vbs",
})
EXECUTABLE_MAGICS = (
    b"MZ",
    b"\x7fELF",
    b"\xca\xfe\xba\xbe",
    b"\xce\xfa\xed\xfe",
    b"\xcf\xfa\xed\xfe",
    b"\xfe\xed\xfa\xce",
    b"\xfe\xed\xfa\xcf",
)


class RetailImportError(ValueError):
    """The selected retail content cannot be imported safely."""


def _sha256_file(path):
    digest = hashlib.sha256()
    with open(path, "rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def _legacy_support_candidates(legacy_root):
    candidates = []
    skipped = []
    for owned_path in LEGACY_SUPPORT_PATHS:
        source = os.path.join(legacy_root, *owned_path.split("/"))
        if not os.path.lexists(source):
            continue
        if os.path.islink(source):
            skipped.append(owned_path)
            continue
        if os.path.isfile(source):
            candidates.append((owned_path, source))
            continue
        if not os.path.isdir(source):
            skipped.append(owned_path)
            continue

        for root, dirs, files in os.walk(source, followlinks=False):
            dirs.sort()
            files.sort()
            linked_dirs = [name for name in dirs if os.path.islink(os.path.join(root, name))]
            dirs[:] = [name for name in dirs if name not in linked_dirs]
            for name in linked_dirs:
                skipped.append(os.path.relpath(os.path.join(root, name), legacy_root))
            for name in files:
                path = os.path.join(root, name)
                relative = os.path.relpath(path, legacy_root)
                if os.path.islink(path) or not os.path.isfile(path):
                    skipped.append(relative)
                else:
                    candidates.append((relative, path))
    return candidates, skipped


def _safe_migration_parent(support_root, relative):
    parent = support_root
    for part in relative.split(os.sep)[:-1]:
        parent = os.path.join(parent, part)
        if os.path.lexists(parent):
            if os.path.islink(parent) or not os.path.isdir(parent):
                return None
        else:
            os.mkdir(parent)
    return parent


def _copy_migration_file(source, destination):
    source_before = os.stat(source, follow_symlinks=False)
    digest = hashlib.sha256()
    created = False
    try:
        with open(source, "rb") as input_stream, open(destination, "xb") as output_stream:
            created = True
            for block in iter(lambda: input_stream.read(1024 * 1024), b""):
                digest.update(block)
                output_stream.write(block)

        source_after = os.stat(source, follow_symlinks=False)
        if (source_before.st_size != source_after.st_size
                or source_before.st_mtime_ns != source_after.st_mtime_ns
                or source_before.st_ino != source_after.st_ino
                or os.path.getsize(destination) != source_after.st_size
                or _sha256_file(destination) != digest.hexdigest()):
            raise OSError(f"legacy support file changed during migration: {source}")
        shutil.copystat(source, destination, follow_symlinks=False)
    except Exception:
        if created and os.path.isfile(destination) and not os.path.islink(destination):
            os.remove(destination)
        raise


def migrate_legacy_support(legacy_root=LEGACY_SUPPORT, support_root=SUPPORT):
    """Copy NUKE HOUR-owned legacy data without deleting or overwriting files."""
    legacy_root = os.path.abspath(os.fspath(legacy_root))
    support_root = os.path.abspath(os.fspath(support_root))
    result = {"copied": [], "conflicts": [], "skipped": []}
    if not os.path.isdir(legacy_root) or os.path.islink(legacy_root):
        return {key: tuple(value) for key, value in result.items()}
    if os.path.realpath(legacy_root) == os.path.realpath(support_root):
        raise ValueError("legacy and current support directories must be different")

    if os.path.lexists(support_root):
        if os.path.islink(support_root):
            raise ValueError("current support directory must not be a symbolic link")
        if not os.path.isdir(support_root):
            raise ValueError("current support path must be a directory")
    else:
        os.makedirs(support_root)
    candidates, skipped = _legacy_support_candidates(legacy_root)
    result["skipped"].extend(skipped)
    for relative, source in candidates:
        destination = os.path.join(support_root, *relative.split(os.sep))
        parent = _safe_migration_parent(support_root, relative)
        if parent is None or os.path.lexists(destination):
            if (parent is not None and os.path.isfile(destination)
                    and not os.path.islink(destination)
                    and os.path.getsize(source) == os.path.getsize(destination)
                    and _sha256_file(source) == _sha256_file(destination)):
                result["skipped"].append(relative)
            else:
                result["conflicts"].append(relative)
            continue

        _copy_migration_file(source, destination)
        result["copied"].append(relative)

    return {key: tuple(sorted(value)) for key, value in result.items()}


def _has_executable_magic(path):
    try:
        with open(path, "rb") as stream:
            prefix = stream.read(4)
    except OSError as exc:
        raise RetailImportError(f"cannot read selected file: {os.path.basename(path)}") from exc
    return any(prefix.startswith(magic) for magic in EXECUTABLE_MAGICS)


def _selected_candidates(paths):
    """Yield regular-looking entries without following directory symlinks."""
    for selected in paths:
        selected = os.fspath(selected)
        if os.path.islink(selected):
            yield selected
        elif os.path.isdir(selected):
            for root, dirs, files in os.walk(selected, followlinks=False):
                dirs.sort()
                files.sort()
                linked_dirs = [name for name in dirs if os.path.islink(os.path.join(root, name))]
                dirs[:] = [name for name in dirs if name not in linked_dirs]
                for name in linked_dirs:
                    yield os.path.join(root, name)
                for name in files:
                    yield os.path.join(root, name)
        else:
            yield selected


def discover_retail_content(paths):
    """Return safe retail/map files found under *paths* without copying them."""
    accepted = {}
    rejected = []
    supported = RETAIL_CONTENT_EXTENSIONS | MAP_CONTENT_EXTENSIONS
    for candidate in _selected_candidates(paths):
        name = os.path.basename(candidate)
        normalized = name.lower()
        extension = os.path.splitext(normalized)[1]

        if os.path.islink(candidate):
            rejected.append({"name": name, "reason": "symlink"})
            continue
        if extension in REJECTED_EXECUTABLE_EXTENSIONS:
            rejected.append({"name": name, "reason": "executable-extension"})
            continue
        if extension not in supported:
            continue
        try:
            mode = os.stat(candidate, follow_symlinks=False).st_mode
        except OSError:
            rejected.append({"name": name, "reason": "unreadable"})
            continue
        if not stat.S_ISREG(mode):
            rejected.append({"name": name, "reason": "not-regular-file"})
            continue
        if _has_executable_magic(candidate):
            rejected.append({"name": name, "reason": "executable-magic"})
            continue

        digest = _sha256_file(candidate)
        existing = accepted.get(normalized)
        if existing is not None:
            if existing["sha256"] != digest:
                raise RetailImportError(f"conflicting duplicate filename: {normalized}")
            continue
        accepted[normalized] = {
            "name": normalized,
            "path": os.path.abspath(candidate),
            "size": os.path.getsize(candidate),
            "sha256": digest,
        }

    return {
        "files": [accepted[name] for name in sorted(accepted)],
        "rejected": sorted(rejected, key=lambda item: (item["name"].lower(), item["reason"])),
    }


def _safe_import_tree(path):
    resolved = os.path.abspath(os.fspath(path))
    parent = os.path.dirname(resolved)
    name = os.path.basename(resolved)
    token = uuid.uuid4().hex
    return (
        resolved,
        os.path.join(parent, f".{name}.import-staging-{token}"),
        os.path.join(parent, f".{name}.import-previous-{token}"),
    )


def import_retail_content(paths, destination=None, maps_destination=None, imported_at=None):
    """Validate, stage, and atomically publish locally selected retail files."""
    discovery = discover_retail_content(paths)
    if not discovery["files"]:
        raise RetailImportError("no supported retail content was selected")

    destination_was_default = destination is None
    destination, staging, previous = _safe_import_tree(destination or content_dir())
    maps_destination = os.fspath(maps_destination or user_maps_dir())
    package_files = [
        item for item in discovery["files"]
        if os.path.splitext(item["name"])[1] in RETAIL_CONTENT_EXTENSIONS
    ]
    map_files = [
        item for item in discovery["files"]
        if os.path.splitext(item["name"])[1] in MAP_CONTENT_EXTENSIONS
    ]
    parent = os.path.dirname(destination)
    os.makedirs(parent, exist_ok=True)
    if package_files:
        os.makedirs(staging)
    moved_previous = False
    try:
        if package_files and os.path.isdir(destination):
            existing = discover_retail_content([destination])
            for item in existing["files"]:
                if os.path.splitext(item["name"])[1] in RETAIL_CONTENT_EXTENSIONS:
                    shutil.copy2(item["path"], os.path.join(staging, item["name"]))

        for item in package_files:
            shutil.copy2(item["path"], os.path.join(staging, item["name"]))

        if package_files:
            staged = discover_retail_content([staging])
            receipt = {
                "schemaVersion": 1,
                "importedAt": imported_at or datetime.datetime.now(datetime.timezone.utc).isoformat(),
                "files": [
                    {key: item[key] for key in ("name", "size", "sha256")}
                    for item in staged["files"]
                ],
                "rejected": discovery["rejected"],
            }
            with open(os.path.join(staging, "import-receipt.json"), "w", encoding="utf-8") as stream:
                json.dump(receipt, stream, ensure_ascii=False, indent=2, sort_keys=True)
                stream.write("\n")

            if os.path.exists(destination):
                os.replace(destination, previous)
                moved_previous = True
            os.replace(staging, destination)

        if map_files:
            os.makedirs(maps_destination, exist_ok=True)
            for item in map_files:
                shutil.copy2(item["path"], os.path.join(maps_destination, item["name"]))

        status = content_status(
            destination,
            maps_directory=maps_destination if map_files or destination_was_default else None)
        if moved_previous:
            shutil.rmtree(previous)
        return status
    except Exception:
        if os.path.isdir(staging):
            shutil.rmtree(staging)
        if moved_previous and not os.path.exists(destination) and os.path.exists(previous):
            os.replace(previous, destination)
        raise


def content_status(directory=None, maps_directory=None):
    if directory is None and maps_directory is None:
        maps_directory = user_maps_dir()
    base = os.fspath(directory or content_dir())
    files = []
    total = 0
    if os.path.isdir(base):
        for fn in sorted(os.listdir(base)):
            full = os.path.join(base, fn)
            if os.path.isfile(full):
                size = os.path.getsize(full)
                total += size
                files.append({"name": fn, "size": _fmt_size(size)})
    present = {f["name"].lower() for f in files}
    missing_core = [p for p in MINIMUM_PACKAGES if p not in present]
    capabilities = {
        "base_game": not missing_core,
        "music": any(name in present for name in OPTIONAL_PACKAGES["music"]),
        "yuris_revenge": all(name in present for name in OPTIONAL_PACKAGES["yuris_revenge"]),
        "audio": all(name in present for name in OPTIONAL_PACKAGES["audio"]),
        "maps": bool(maps_directory and os.path.isdir(maps_directory) and any(
            os.path.splitext(name.lower())[1] in MAP_CONTENT_EXTENSIONS
            for name in os.listdir(maps_directory)
        )),
    }
    return {
        "dir": base,
        "files": files,
        "count": len(files),
        "total_size": _fmt_size(total),
        "missing_core": missing_core,
        "healthy": not missing_core,
        "capabilities": capabilities,
    }


# ------------------------------------------------------------ 制作名单

def credits_text():
    parts = []
    mod_authors = os.path.join(REPO, "mods", MOD_ID, "AUTHORS")
    engine_authors = os.path.join(REPO, "engine", "AUTHORS")
    try:
        with open(mod_authors, encoding="utf-8", errors="replace") as f:
            parts.append("【Red Alert 2 模组】\n\n" + f.read().strip())
    except OSError:
        pass
    try:
        with open(engine_authors, encoding="utf-8", errors="replace") as f:
            parts.append("【OpenRA 引擎】\n\n" + f.read().strip())
    except OSError:
        pass
    return "\n\n\n".join(parts) if parts else "未找到制作名单文件"


fmt_size = _fmt_size
