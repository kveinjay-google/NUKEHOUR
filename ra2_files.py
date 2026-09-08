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
import os
import re
import shutil
import struct
import subprocess
import zipfile

REPO = os.path.dirname(os.path.abspath(__file__))
SUPPORT = os.path.expanduser("~/Library/Application Support/OpenRA")
MOD_ID = "ra2"

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

CORE_PACKAGES = ["ra2.mix", "language.mix", "theme.mix"]


def content_status():
    base = content_dir()
    files = []
    total = 0
    if os.path.isdir(base):
        for fn in sorted(os.listdir(base)):
            full = os.path.join(base, fn)
            if os.path.isfile(full):
                size = os.path.getsize(full)
                total += size
                files.append({"name": fn, "size": _fmt_size(size)})
    present = {f["name"] for f in files}
    missing_core = [p for p in CORE_PACKAGES if p not in present]
    return {
        "dir": base,
        "files": files,
        "count": len(files),
        "total_size": _fmt_size(total),
        "missing_core": missing_core,
        "healthy": not missing_core and len(files) > 0,
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
