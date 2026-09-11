#!/usr/bin/env python3

"""Independent container-build verification for a staged server bundle."""

import fnmatch
import hashlib
import json
import sys
from pathlib import Path, PurePosixPath

ROOT_FILES = {
    "VERSION", "healthcheck.sh", "run-server.sh", "server-build.json",
    "server-config.env.example",
}
LICENSE_FILES = {
    "licenses/NUKE-HOUR-GPL-3.0.txt", "licenses/OpenRA-AUTHORS.txt",
    "licenses/OpenRA-GPL-3.0.txt",
}
BIN_PATTERNS = (
    "AUTHORS", "BeaconLib.dll", "DiscordRPC.dll", "Eluant.dll",
    "Eluant.dll.config", "FuzzyLogicLibrary.dll", "ICSharpCode.SharpZipLib.dll",
    "Linguini.Bundle.dll", "Linguini.Shared.dll", "Linguini.Syntax.dll",
    "MP3Sharp.dll", "Microsoft.*.dll", "Mono.Nat.dll", "NVorbis.dll",
    "Newtonsoft.Json.dll", "OpenRA.Game.dll", "OpenRA.Game.pdb",
    "OpenRA.Mods.Cnc.deps.json", "OpenRA.Mods.Cnc.dll",
    "OpenRA.Mods.Common.deps.json", "OpenRA.Mods.Common.dll",
    "OpenRA.Mods.RA2.deps.json", "OpenRA.Mods.RA2.dll", "OpenRA.Mods.RA2.pdb",
    "OpenRA.Server", "OpenRA.Server.deps.json", "OpenRA.Server.dll",
    "OpenRA.Server.pdb", "OpenRA.Server.runtimeconfig.json", "Pfim.dll",
    "System.*.dll", "System.dll", "TagLibSharp.dll", "WindowsBase.dll", "createdump",
    "libSystem.*.so", "libclrjit.so", "libcoreclr.so",
    "libcoreclrtraceptprovider.so", "libdbgshim.so", "libhostfxr.so",
    "libhostpolicy.so", "libmscordaccore.so", "libmscordbi.so", "lua51.so",
    "mscorlib.dll", "netstandard.dll",
)
FORBIDDEN = ("*.mix", "*.bag", "*.aud", "*.vqa", "*.exe", "*.key", "*.pem")


def allowed(relative):
    path = PurePosixPath(relative)
    if relative in ROOT_FILES or relative in LICENSE_FILES:
        return True
    if len(path.parts) == 2 and path.parts[0] == "bin":
        return any(fnmatch.fnmatchcase(path.name, pattern) for pattern in BIN_PATTERNS)
    if path.parts[:2] in (("mods", "ra2"), ("mods", "common")):
        return (
            "chrome" not in path.parts
            and path.name not in {"chrome.yaml", "metrics.yaml", "cursors.yaml"}
            and (
                path.suffix.lower() in {".yaml", ".ftl", ".lua"}
                or (path.suffix.lower() == ".bin" and "maps" in path.parts)
                or path.name in {"AUTHORS", "COPYING", "LICENSE", "VERSION"}
            )
        )
    return relative == "mods/ts/mod.yaml"


def digest(path):
    value = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            value.update(chunk)
    return value.hexdigest()


def main():
    root = Path(sys.argv[1]).resolve()
    document = json.loads((root / "server-inventory.json").read_text(encoding="utf-8"))
    assert document.get("schema") == 1
    expected = {entry["path"]: entry for entry in document["files"]}
    actual = {
        path.relative_to(root).as_posix(): path
        for path in root.rglob("*")
        if path.is_file() and path.name != "server-inventory.json"
    }
    assert set(actual) == set(expected), "inventory path set mismatch"
    for relative, path in actual.items():
        assert allowed(relative), f"path is not allowlisted: {relative}"
        assert not any(fnmatch.fnmatchcase(path.name.lower(), pattern) for pattern in FORBIDDEN)
        entry = expected[relative]
        assert path.stat().st_size == entry["size"]
        assert digest(path) == entry["sha256"]
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
