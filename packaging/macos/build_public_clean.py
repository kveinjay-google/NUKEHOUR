#!/usr/bin/env python3
"""Build the self-contained NUKE HOUR macOS PublicClean release."""

from __future__ import annotations

import argparse
import datetime
import hashlib
import json
import os
import plistlib
import re
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path, PurePosixPath


PROHIBITED_SOURCE_EXTENSIONS = frozenset({
    ".aud", ".bag", ".bik", ".hva", ".idx", ".map", ".mix", ".mpr",
    ".oramap", ".shp", ".vqa", ".vxl", ".wsa", ".yrm",
})
PUBLIC_CATEGORIES = frozenset({
    "engine-source", "project-original", "redistributable-third-party",
})


def sha256_bytes(data):
    return hashlib.sha256(data).hexdigest()


def sha256_file(path):
    digest = hashlib.sha256()
    with Path(path).open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def load_source_manifest(path):
    with Path(path).open(encoding="utf-8") as stream:
        manifest = json.load(stream)
    if manifest.get("schemaVersion") != 1:
        raise ValueError("PublicClean source manifest must use schemaVersion 1")
    destinations = set()
    for entry in manifest.get("files", []):
        for key in ("source", "destination", "sha256", "size", "category", "license"):
            if key not in entry:
                raise ValueError(f"manifest entry is missing {key}: {entry!r}")
        source = PurePosixPath(entry["source"]).as_posix()
        destination = PurePosixPath(entry["destination"]).as_posix()
        if source.startswith("../") or source.startswith("/"):
            raise ValueError(f"source path escapes checkout: {source}")
        if destination.startswith("../") or destination.startswith("/"):
            raise ValueError(f"destination path escapes runtime: {destination}")
        if destination in destinations:
            raise ValueError(f"duplicate destination: {destination}")
        destinations.add(destination)
        if entry["category"] not in PUBLIC_CATEGORIES:
            raise ValueError(f"unreviewed category: {entry['category']}")
        if Path(destination).suffix.lower() in PROHIBITED_SOURCE_EXTENSIONS:
            raise ValueError(f"prohibited public destination: {destination}")
    return manifest


def public_source_paths(root, manifest):
    root = Path(root).resolve()
    paths = []
    for entry in manifest["files"]:
        path = root / entry["source"]
        try:
            path.resolve(strict=True).relative_to(root)
        except (FileNotFoundError, ValueError) as exc:
            raise ValueError(f"unsafe or missing public source: {entry['source']}") from exc
        if path.is_symlink() or not path.is_file():
            raise ValueError(f"public source must be a regular file: {entry['source']}")
        paths.append(path)
    return tuple(paths)


def verify_source_manifest(root, manifest):
    errors = []
    for path, entry in zip(public_source_paths(root, manifest), manifest["files"]):
        expected_size = entry.get("sourceSize", entry["size"])
        expected_hash = entry.get("sourceSha256", entry["sha256"])
        actual_size = path.stat().st_size
        actual_hash = sha256_file(path)
        if actual_size != expected_size:
            errors.append(
                f"source size drift: {entry['source']} expected {expected_size}, got {actual_size}")
        if actual_hash != expected_hash:
            errors.append(f"source hash drift: {entry['source']}")
    return tuple(errors)


def _public_clean_mod(data):
    text = data.decode("utf-8")
    excluded_lines = {
        "\t\t$ts: ts",
        "\t\tra2|bits",
        "\t\tra2|bits/cameos",
        "\t\tra2|bits/structures",
        "\t\tra2|bits/animations",
        "\t\tra2|bits/projectiles",
        "\tra2|maps: System",
    }
    lines = [line for line in text.splitlines() if line not in excluded_lines]
    lines = [
        "\tcommon|chrome/assetbrowser.yaml" if line == "\tts|chrome/assetbrowser.yaml" else line
        for line in lines
    ]
    return ("\n".join(lines) + "\n").encode("utf-8")


def _public_clean_cursors(data):
    text = data.decode("utf-8")
    marker = "\n\tundeploy.shp: mouse\n"
    head, separator, tail = text.partition(marker)
    if not separator or "\n\tassaultmove.shp: mouse\n" not in tail:
        raise ValueError("unexpected RA2 cursor layout")

    retail_aliases = (
        "\n\t\tundeploy:\n"
        "\t\t\tStart: 120\n"
        "\t\t\tLength: 9\n"
        "\t\tassaultmove:\n"
        "\t\t\tStart: 404\n"
        "\t\t\tLength: 9\n"
        "\t\tassaultmove-minimap:\n"
        "\t\t\tStart: 434\n"
        "\t\tassaultmove-blocked:\n"
        "\t\t\tStart: 384\n"
        "\t\tassaultmove-blocked-minimap:\n"
        "\t\t\tStart: 384\n"
    )
    return (head + retail_aliases).encode("utf-8")


TRANSFORMS = {
    "public-clean-cursors": _public_clean_cursors,
    "public-clean-mod": _public_clean_mod,
}


def stage_public_runtime(root, destination, manifest):
    root = Path(root).resolve()
    destination = Path(destination)
    errors = verify_source_manifest(root, manifest)
    if errors:
        raise ValueError("; ".join(errors))
    destination.mkdir(parents=True, exist_ok=True)
    for source_path, entry in zip(public_source_paths(root, manifest), manifest["files"]):
        target = destination / entry["destination"]
        target.parent.mkdir(parents=True, exist_ok=True)
        transform = entry.get("transform")
        if transform:
            try:
                data = TRANSFORMS[transform](source_path.read_bytes())
            except KeyError as exc:
                raise ValueError(f"unknown public transform: {transform}") from exc
            if len(data) != entry["size"] or sha256_bytes(data) != entry["sha256"]:
                raise ValueError(f"transformed output drift: {entry['destination']}")
            target.write_bytes(data)
            shutil.copymode(source_path, target)
        else:
            shutil.copy2(source_path, target, follow_symlinks=False)
    return destination


def public_app_plist(version, build):
    return {
        "CFBundleDisplayName": "NUKE HOUR",
        "CFBundleExecutable": "NUKE HOUR",
        "CFBundleIconFile": "NUKE-HOUR.icns",
        "CFBundleIdentifier": "com.nukehour.macos.publicclean",
        "CFBundleInfoDictionaryVersion": "6.0",
        "CFBundleName": "NUKE HOUR",
        "CFBundlePackageType": "APPL",
        "CFBundleShortVersionString": str(version),
        "CFBundleVersion": str(build),
        "LSApplicationCategoryType": "public.app-category.games",
        "LSMinimumSystemVersion": "12.0",
        "NSHighResolutionCapable": True,
    }


def public_dmg_name(version):
    return f"NUKE-HOUR-PublicClean-{version}-arm64.dmg"


def build_sbom(root, manifest):
    root = Path(root)
    by_destination = {entry["destination"]: entry for entry in manifest["files"]}
    files = []
    for path in sorted(root.rglob("*"), key=lambda item: item.as_posix()):
        if not path.is_file() or path.is_symlink():
            continue
        relative = path.relative_to(root).as_posix()
        entry = by_destination.get(relative)
        files.append({
            "path": relative,
            "size": path.stat().st_size,
            "sha256": sha256_file(path),
            "category": entry["category"] if entry else "generated-runtime",
            "license": entry.get("license", "") if entry else "See bundled notices",
        })
    return {"schemaVersion": 1, "product": "NUKE HOUR", "files": files}


def pyinstaller_command(pyinstaller, root, work):
    root = Path(root)
    work = Path(work)
    return [
        str(pyinstaller), "--noconfirm", "--clean", "--windowed", "--onedir",
        "--target-architecture", "arm64", "--name", "NUKE HOUR",
        "--icon", str(root / "launcher_assets/icon.icns"),
        "--distpath", str(work / "dist"),
        "--workpath", str(work / "pyinstaller-work"),
        "--specpath", str(work / "spec"),
        str(root / "launcher.py"),
    ]


def release_metadata(version, build, signing_identity=None, notarized=False):
    return {
        "schemaVersion": 1,
        "product": "NUKE HOUR",
        "profile": "macOS PublicClean",
        "version": str(version),
        "build": int(build),
        "architecture": "arm64",
        "signing": "signed" if signing_identity else "ad-hoc",
        "notarization": "accepted-and-stapled" if notarized else "not-submitted",
        "builtAt": datetime.datetime.now(datetime.timezone.utc).isoformat(),
    }


def _run(command, **kwargs):
    print("+", " ".join(str(item) for item in command))
    return subprocess.run([str(item) for item in command], check=True, **kwargs)


def _find_pyinstaller(explicit=None):
    candidates = [
        explicit,
        os.environ.get("PYINSTALLER"),
        shutil.which("pyinstaller"),
        Path(sys.executable).with_name("pyinstaller"),
        Path.home() / "hermes-agent/.venv/bin/pyinstaller",
    ]
    for candidate in candidates:
        if candidate and Path(candidate).is_file() and os.access(candidate, os.X_OK):
            return Path(candidate)
    raise RuntimeError("PyInstaller 6 is required; set PYINSTALLER to its executable")


def _load_version(root):
    with (Path(root) / "packaging/nukehour-version.json").open(encoding="utf-8") as stream:
        payload = json.load(stream)
    return str(payload["version"]), int(payload["build"])


def _latest_version_directory(parent):
    directories = [path for path in Path(parent).iterdir() if path.is_dir()]
    if not directories:
        raise RuntimeError(f"no runtime version found under {parent}")
    return sorted(directories, key=lambda path: tuple(
        int(part) if part.isdigit() else part for part in re.split(r"[.-]", path.name)),
                  reverse=True)[0]


def copy_dotnet_runtime(dotnet_source, destination):
    dotnet_source = Path(dotnet_source)
    destination = Path(destination)
    fxr = _latest_version_directory(dotnet_source / "host/fxr")
    shared = _latest_version_directory(dotnet_source / "shared/Microsoft.NETCore.App")
    destination.mkdir(parents=True, exist_ok=True)
    shutil.copy2(dotnet_source / "dotnet", destination / "dotnet")
    shutil.copytree(fxr, destination / "host/fxr" / fxr.name, symlinks=True)
    shutil.copytree(shared, destination / "shared/Microsoft.NETCore.App" / shared.name,
                    symlinks=True)
    for notice in ("LICENSE.txt", "ThirdPartyNotices.txt"):
        source = dotnet_source / notice
        if source.is_file():
            shutil.copy2(source, destination / notice)


def build_game_app(root, runtime, version, build):
    root = Path(root)
    app = Path(runtime) / "NUKE HOUR GAME.app"
    macos = app / "Contents/MacOS"
    resources = app / "Contents/Resources"
    macos.mkdir(parents=True)
    resources.mkdir(parents=True)
    _run([
        "clang", str(root / "packaging/macos/apphost_ra2.c"),
        "-o", str(macos / "NuclearCrisis"), "-target", "arm64-apple-macos12",
    ])
    plist = public_app_plist(version, build)
    plist.update({
        "CFBundleExecutable": "NuclearCrisis",
        "CFBundleIdentifier": "com.nukehour.macos.publicclean.game",
    })
    with (app / "Contents/Info.plist").open("wb") as stream:
        plistlib.dump(plist, stream, sort_keys=True)
    shutil.copy2(root / "launcher_assets/icon.icns", resources / "NUKE-HOUR.icns")
    return app


def _copy_engine_bin(root, runtime):
    source = Path(root) / "engine/bin"
    required = (
        "OpenRA.dll",
        "OpenRA.Mods.RA2.dll",
        "OpenRA.Platforms.Default.dll",
        "global mix database.dat",
    )
    missing = [name for name in required if not (source / name).is_file()]
    if missing:
        raise RuntimeError("missing built OpenRA files: " + ", ".join(missing))
    shutil.copytree(
        source,
        Path(runtime) / "engine/bin",
        symlinks=True,
        ignore=lambda _directory, names: {"mods"} if "mods" in names else set(),
    )
    shutil.copy2(
        source / "global mix database.dat",
        Path(runtime) / "engine/global mix database.dat",
    )


def _find_developer_identity():
    configured = os.environ.get("MACOS_DEVELOPER_IDENTITY")
    if configured:
        return configured
    result = subprocess.run(
        ["security", "find-identity", "-v", "-p", "codesigning"],
        capture_output=True, text=True, check=False)
    match = re.search(r'"(Developer ID Application: [^"]+)"', result.stdout)
    return match.group(1) if match else None


def _sign_app(app, identity):
    if identity:
        _run([
            "codesign", "--force", "--deep", "--options", "runtime", "--timestamp",
            "--sign", identity, str(app),
        ])
    else:
        _run(["codesign", "--force", "--deep", "--sign", "-", str(app)])
    _run(["codesign", "--verify", "--deep", "--strict", "--verbose=2", str(app)])


def _notarize_if_configured(dmg):
    apple_id = os.environ.get("MACOS_DEVELOPER_USERNAME")
    password = os.environ.get("MACOS_DEVELOPER_PASSWORD")
    team_id = os.environ.get("MACOS_DEVELOPER_TEAM_ID")
    profile = os.environ.get("MACOS_NOTARY_PROFILE")
    if profile:
        _run(["xcrun", "notarytool", "submit", str(dmg), "--keychain-profile", profile,
              "--wait"])
    elif apple_id and password and team_id:
        _run(["xcrun", "notarytool", "submit", str(dmg), "--apple-id", apple_id,
              "--password", password, "--team-id", team_id, "--wait"])
    else:
        return False
    _run(["xcrun", "stapler", "staple", str(dmg)])
    _run(["xcrun", "stapler", "validate", str(dmg)])
    return True


def _write_json(path, payload):
    Path(path).write_text(json.dumps(payload, ensure_ascii=False, indent=2) + "\n",
                          encoding="utf-8")


def build_release(root, output, manifest, pyinstaller=None, dotnet_root=None):
    try:
        from . import public_clean_audit
    except ImportError:
        import public_clean_audit

    root = Path(root).resolve()
    output = Path(output).resolve()
    output.mkdir(parents=True, exist_ok=True)
    version, build = _load_version(root)
    pyinstaller = _find_pyinstaller(pyinstaller)
    dotnet_root = Path(dotnet_root or os.environ.get("DOTNET_ROOT", "/usr/local/share/dotnet"))
    if not (dotnet_root / "dotnet").is_file():
        raise RuntimeError(f".NET runtime not found: {dotnet_root}")

    manifest_errors = verify_source_manifest(root, manifest)
    if manifest_errors:
        raise RuntimeError("; ".join(manifest_errors))

    with tempfile.TemporaryDirectory(prefix="nukehour-public-clean-") as temporary:
        work = Path(temporary)
        _run(pyinstaller_command(pyinstaller, root, work), cwd=root)
        app = work / "dist/NUKE HOUR.app"
        if not app.is_dir():
            raise RuntimeError("PyInstaller did not produce NUKE HOUR.app")

        contents = app / "Contents"
        resources = contents / "Resources"
        runtime = resources / "runtime"
        resources.mkdir(parents=True, exist_ok=True)
        with (contents / "Info.plist").open("wb") as stream:
            plistlib.dump(public_app_plist(version, build), stream, sort_keys=True)
        shutil.copy2(root / "launcher_assets/icon.icns", resources / "NUKE-HOUR.icns")

        stage_public_runtime(root, runtime, manifest)
        _copy_engine_bin(root, runtime)
        copy_dotnet_runtime(dotnet_root, runtime / "dotnet")
        build_game_app(root, runtime, version, build)

        result = public_clean_audit.audit_app(app, manifest, version)
        if not result.passed:
            public_clean_audit.write_reports(
                result, output / "public-clean-audit.json", output / "public-clean-audit.md")
            raise RuntimeError(f"staged app audit failed with {len(result.errors)} errors")

        identity = _find_developer_identity()
        _sign_app(app, identity)
        signed_result = public_clean_audit.audit_app(app, manifest, version)
        if not signed_result.passed:
            raise RuntimeError("signed app failed PublicClean audit")

        dmg_root = work / "dmg"
        dmg_root.mkdir()
        shutil.copytree(app, dmg_root / "NUKE HOUR.app", symlinks=True)
        os.symlink("/Applications", dmg_root / "Applications")
        dmg = output / public_dmg_name(version)
        _run([
            "hdiutil", "create", "-ov", "-format", "UDZO", "-fs", "HFS+",
            "-volname", "NUKE HOUR", "-srcfolder", str(dmg_root), str(dmg),
        ])
        notarized = _notarize_if_configured(dmg)
        final_result = public_clean_audit._audit_dmg(dmg, manifest, version)
        public_clean_audit.write_reports(
            final_result, output / "public-clean-audit.json", output / "public-clean-audit.md")
        if not final_result.passed:
            raise RuntimeError(f"DMG audit failed with {len(final_result.errors)} errors")

        sbom = build_sbom(runtime, manifest)
        _write_json(output / "public-clean-sbom.json", sbom)
        _write_json(output / "build-metadata.json", release_metadata(
            version, build, signing_identity=identity, notarized=notarized))
        digest = sha256_file(dmg)
        (output / f"{dmg.name}.sha256").write_text(
            f"{digest}  {dmg.name}\n", encoding="utf-8")
        return dmg


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--manifest", type=Path)
    parser.add_argument("--pyinstaller", type=Path)
    parser.add_argument("--dotnet-root", type=Path)
    arguments = parser.parse_args(argv)
    root = Path(__file__).resolve().parents[2]
    manifest_path = arguments.manifest or root / "packaging/macos/public_clean_manifest.json"
    manifest = load_source_manifest(manifest_path)
    try:
        dmg = build_release(
            root, arguments.output, manifest,
            pyinstaller=arguments.pyinstaller, dotnet_root=arguments.dotnet_root)
    except (OSError, RuntimeError, subprocess.CalledProcessError, ValueError) as exc:
        print(f"PublicClean build failed: {exc}", file=sys.stderr)
        return 2
    print(f"PublicClean build complete: {dmg}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
