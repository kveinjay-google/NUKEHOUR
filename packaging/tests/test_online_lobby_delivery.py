import importlib.util
import json
import os
import tempfile
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
LOBBY = ROOT / "lobby"
BUILDER = ROOT / "packaging" / "lobby" / "build_lobby.py"
AUDITOR = ROOT / "packaging" / "lobby" / "audit_lobby_bundle.py"


def load_module(path, name):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class OnlineLobbyDeliveryTest(unittest.TestCase):
    def test_dependencies_are_exactly_pinned(self):
        requirements = (LOBBY / "requirements.txt").read_text(encoding="utf-8").splitlines()
        self.assertEqual(
            [
                "annotated-doc==0.0.5",
                "annotated-types==0.8.0",
                "anyio==4.15.1",
                "click==8.5.0",
                "fastapi==0.136.0",
                "h11==0.16.0",
                "idna==3.19",
                "packaging==26.3",
                "pydantic==2.13.3",
                "pydantic_core==2.46.3",
                "starlette==1.6.0",
                "typing-inspection==0.4.4",
                "typing_extensions==4.16.0",
                "uvicorn==0.45.0",
            ],
            requirements,
        )
        self.assertTrue(all("==" in line for line in requirements))

    def test_config_example_has_no_credential_value(self):
        config = (LOBBY / "lobby-config.env.example").read_text(encoding="utf-8")
        self.assertIn("NUKEHOUR_LOBBY_REGISTRATION_TOKEN=", config)
        token_line = next(
            line for line in config.splitlines()
            if line.startswith("NUKEHOUR_LOBBY_REGISTRATION_TOKEN=")
        )
        self.assertEqual("NUKEHOUR_LOBBY_REGISTRATION_TOKEN=", token_line)
        self.assertIn("NUKEHOUR_LOBBY_ENVIRONMENT=production", config)
        self.assertIn("NUKEHOUR_LOBBY_ALLOWED_HOSTS=", config)

    def test_runtime_launcher_is_production_only_and_never_reloads(self):
        launcher = (LOBBY / "run-lobby.sh").read_text(encoding="utf-8")
        self.assertIn("app_from_environment", launcher)
        self.assertIn("--factory", launcher)
        self.assertIn("--no-access-log", launcher)
        self.assertIn("--proxy-headers", launcher)
        self.assertIn("--forwarded-allow-ips=127.0.0.1", launcher)
        self.assertNotIn("--reload", launcher)
        self.assertNotIn("eval ", launcher)

    def test_container_is_non_root_minimal_and_health_checked(self):
        dockerfile = (LOBBY / "Dockerfile").read_text(encoding="utf-8")
        self.assertIn("FROM python:3.11-slim-bookworm", dockerfile)
        self.assertIn("--uid 10002", dockerfile)
        self.assertIn("USER nukehour-lobby", dockerfile)
        self.assertIn("HEALTHCHECK", dockerfile)
        self.assertIn("COPY bundle/ /opt/nukehour-lobby/", dockerfile)
        self.assertNotIn("COPY . ", dockerfile)
        self.assertNotIn("--reload", dockerfile)
        self.assertNotIn("NUKEHOUR_LOBBY_REGISTRATION_TOKEN=", dockerfile)

        ignored = (LOBBY / ".dockerignore").read_text(encoding="utf-8").splitlines()
        self.assertEqual(["*", "!bundle", "!bundle/**"], ignored)

    def test_builder_validates_version_target_and_paths(self):
        builder = load_module(BUILDER, "nukehour_lobby_builder")
        for valid in ("linux-x64", "linux-arm64"):
            self.assertEqual(valid, builder.validate_rid(valid))
        for invalid in ("osx-arm64", "../linux-x64", ""):
            with self.assertRaises(builder.BuildError):
                builder.validate_rid(invalid)
        for invalid in ("../bad", "bad name", "", "x" * 65):
            with self.assertRaises(builder.BuildError):
                builder.validate_version(invalid)
        for invalid in ("../secret", "/absolute", "a\\b", "a/./b", ""):
            with self.assertRaises(builder.BuildError):
                builder.validate_relative_path(invalid)

    def test_staged_bundle_is_allowlisted_deterministic_and_audited(self):
        builder = load_module(BUILDER, "nukehour_lobby_builder_stage")
        auditor = load_module(AUDITOR, "nukehour_lobby_auditor")
        with tempfile.TemporaryDirectory() as temporary:
            first = Path(temporary) / "first"
            second = Path(temporary) / "second"
            builder.stage_bundle(ROOT, first, "linux-x64", "1.0.3-phase3", "a" * 40)
            builder.stage_bundle(ROOT, second, "linux-x64", "1.0.3-phase3", "a" * 40)

            first_inventory = json.loads((first / "lobby-inventory.json").read_text())
            second_inventory = json.loads((second / "lobby-inventory.json").read_text())
            self.assertEqual(first_inventory, second_inventory)
            evidence = auditor.audit_bundle(first)
            self.assertEqual("PASS", evidence["result"])
            self.assertEqual("NONE", evidence["commercialResourcesIncluded"])
            self.assertTrue(os.access(first / "run-lobby.sh", os.X_OK))
            paths = {item["path"] for item in first_inventory["files"]}
            self.assertIn("src/nukehour_lobby/app.py", paths)
            self.assertNotIn(".git", "\n".join(paths))
            self.assertFalse(any("test" in path.lower() for path in paths))

    def test_audit_rejects_secret_commercial_extra_and_world_writable_files(self):
        builder = load_module(BUILDER, "nukehour_lobby_builder_reject")
        auditor = load_module(AUDITOR, "nukehour_lobby_auditor_reject")
        cases = (
            ("registration.secret", b"s"),
            ("ra2.mix", b"commercial"),
            ("unexpected.txt", b"extra"),
        )
        for name, content in cases:
            with self.subTest(name=name), tempfile.TemporaryDirectory() as temporary:
                bundle = Path(temporary) / "bundle"
                builder.stage_bundle(ROOT, bundle, "linux-x64", "1.0.3-phase3", "a" * 40)
                (bundle / name).write_bytes(content)
                with self.assertRaises(auditor.AuditError):
                    auditor.audit_bundle(bundle)

        with tempfile.TemporaryDirectory() as temporary:
            bundle = Path(temporary) / "bundle"
            builder.stage_bundle(ROOT, bundle, "linux-x64", "1.0.3-phase3", "a" * 40)
            target = bundle / "run-lobby.sh"
            target.chmod(0o777)
            inventory = auditor.create_inventory(bundle, exclude={"lobby-inventory.json"})
            (bundle / "lobby-inventory.json").write_text(
                json.dumps({"schema": 1, "files": inventory}, indent=2) + "\n"
            )
            with self.assertRaises(auditor.AuditError):
                auditor.audit_bundle(bundle)

    def test_makefile_exposes_lobby_targets(self):
        makefile = (ROOT / "Makefile").read_text(encoding="utf-8")
        for target in ("lobby-test:", "lobby-build:", "lobby-audit:"):
            self.assertIn(target, makefile)


if __name__ == "__main__":
    unittest.main()
