#!/usr/bin/env python3
"""Verify that the two NUKE HOUR repositories ship one protocol implementation."""

from __future__ import annotations

import argparse
import hashlib
from pathlib import Path


PROTOCOL_FILES = (
    "engine/OpenRA.Game/Network/Handshake.cs",
    "engine/OpenRA.Game/Network/NukeHourNetworkCompatibility.cs",
    "engine/OpenRA.Game/Network/UnitOrders.cs",
    "engine/OpenRA.Game/Server/ProtocolVersion.cs",
    "engine/OpenRA.Game/Server/Server.cs",
    "engine/OpenRA.Game/ModData.cs",
    "engine/OpenRA.Mods.Common/ServerTraits/LobbyCommands.cs",
    "engine/OpenRA.Test/NukeHourNetworkProtocolTest.cs",
    "engine/OpenRA.Test/NukeHourDirectTcpHandshakeTest.cs",
    "docs/network-protocol.md",
)


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("left", type=Path)
    parser.add_argument("right", type=Path)
    args = parser.parse_args()

    failures: list[str] = []
    for relative in PROTOCOL_FILES:
        left = args.left / relative
        right = args.right / relative
        if not left.is_file() or not right.is_file():
            failures.append(f"missing: {relative}")
        elif digest(left) != digest(right):
            failures.append(f"different: {relative}")

    if failures:
        for failure in failures:
            print(failure)
        return 1

    print(f"NUKE HOUR network protocol parity passed: {len(PROTOCOL_FILES)} files")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
