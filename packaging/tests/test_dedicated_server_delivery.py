import json
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
SERVER_ROOT = ROOT / "server"
DELIVERY_MANIFEST = SERVER_ROOT / "server-files.json"
CONFIG_EXAMPLE = SERVER_ROOT / "server-config.env.example"
DOCKERFILE = SERVER_ROOT / "Dockerfile"
DOCKERIGNORE = SERVER_ROOT / ".dockerignore"
CONTAINER_VERIFIER = SERVER_ROOT / "verify-bundle.py"
COMPOSE = SERVER_ROOT / "compose.test.yaml"
MAKEFILE = ROOT / "Makefile"
LEGACY_LAUNCHER = ROOT / "launch-dedicated.sh"
HEALTHCHECK = ROOT / "packaging" / "server" / "healthcheck.sh"


class DedicatedServerDeliveryContractTest(unittest.TestCase):
    def load_manifest(self):
        return json.loads(DELIVERY_MANIFEST.read_text(encoding="utf-8"))

    def test_delivery_manifest_freezes_shared_protocol(self):
        manifest = self.load_manifest()

        self.assertEqual(1, manifest["schema"])
        self.assertEqual("NUKE HOUR Dedicated Server", manifest["product"])
        self.assertEqual(
            {
                "transportHandshake": 7,
                "handshakeSchema": 1,
                "orders": 23,
                "runtimeContractSchema": 1,
                "resourceCapability": "ra2-presentation-capability-v2",
            },
            manifest["protocol"],
        )

    def test_linux_targets_and_dynamic_port_contract_are_explicit(self):
        manifest = self.load_manifest()

        self.assertEqual(["linux-x64", "linux-arm64"], manifest["targets"])
        self.assertEqual("0.0.0.0", manifest["defaults"]["listenAddress"])
        self.assertEqual(1234, manifest["defaults"]["listenPort"])
        self.assertEqual([1, 65535], manifest["constraints"]["listenPortRange"])
        self.assertEqual("one-process-one-room", manifest["instanceModel"])

    def test_manifest_has_an_explicit_commercial_resource_denylist(self):
        forbidden = set(self.load_manifest()["forbiddenCommercialResources"])

        for required in (
            "ra2.mix",
            "language.mix",
            "ra2md.mix",
            "langmd.mix",
            "*.mix",
            "*.bag",
            "*.aud",
            "*.vqa",
            "*.exe",
        ):
            self.assertIn(required, forbidden)

    def test_manifest_requires_every_reflection_loaded_mod_assembly(self):
        self.assertEqual(
            [
                "OpenRA.Mods.Common.dll",
                "OpenRA.Mods.Cnc.dll",
                "OpenRA.Mods.RA2.dll",
            ],
            self.load_manifest()["requiredModAssemblies"],
        )
        self.assertEqual(
            [
                "OpenRA.Mods.Common.deps.json",
                "OpenRA.Mods.Cnc.deps.json",
                "OpenRA.Mods.RA2.deps.json",
            ],
            self.load_manifest()["requiredModDependencyManifests"],
        )

    def test_server_config_uses_supported_openra_settings_and_safe_defaults(self):
        values = {}
        for raw_line in CONFIG_EXAMPLE.read_text(encoding="utf-8").splitlines():
            line = raw_line.strip()
            if not line or line.startswith("#"):
                continue
            key, value = line.split("=", 1)
            values[key] = value

        self.assertEqual("NUKE HOUR Dedicated Server", values["Name"])
        self.assertEqual("0.0.0.0", values["ListenAddress"])
        self.assertEqual("1234", values["ListenPort"])
        self.assertNotIn("AdvertiseOnline", values)
        self.assertEqual("False", values["AdvertiseLAN"])
        self.assertEqual("True", values["EnableSyncReports"])
        self.assertEqual("False", values["RecordReplays"])
        self.assertIn("Map", values)
        self.assertIn("Password", values)
        self.assertIn("MaxPlayers", values)
        self.assertEqual("", values["OnlineLobbyUrl"])
        self.assertEqual("", values["OnlineLobbyServerId"])
        self.assertEqual("", values["NUKEHOUR_LOBBY_REGISTRATION_CREDENTIAL"])
        self.assertNotIn("OnlineLobbyRegistrationCredential", values)
        self.assertEqual("", values["OnlineLobbyPublicEndpoint"])
        self.assertEqual("0", values["OnlineLobbyPublicPort"])
        self.assertEqual("", values["OnlineLobbyRegion"])
        self.assertEqual("15", values["OnlineLobbyHeartbeatSeconds"])

    def test_server_launcher_forwards_lobby_settings_without_default_secret(self):
        source = (ROOT / "packaging" / "server" / "run-server.sh").read_text(encoding="utf-8")
        for key in (
            "OnlineLobbyUrl",
            "OnlineLobbyServerId",
            "OnlineLobbyPublicEndpoint",
            "OnlineLobbyPublicPort",
            "OnlineLobbyRegion",
            "OnlineLobbyHeartbeatSeconds",
        ):
            self.assertIn(f'"Server.{key}=${{{key}}}"', source)
        self.assertIn("NUKEHOUR_LOBBY_REGISTRATION_CREDENTIAL", source)
        self.assertNotIn("Server.OnlineLobbyRegistrationCredential", source)
        self.assertNotIn("OnlineLobbyRegistrationCredential=", source)

    def test_container_contract_is_multistage_non_root_and_artifact_only(self):
        dockerfile = DOCKERFILE.read_text(encoding="utf-8")
        self.assertGreaterEqual(dockerfile.upper().count("FROM "), 2)
        self.assertIn(" AS verify", dockerfile)
        self.assertIn(" AS runtime", dockerfile)
        self.assertIn("USER nukehour", dockerfile)
        self.assertIn("EXPOSE 1234/tcp", dockerfile)
        self.assertIn("COPY bundle/ /opt/nukehour/", dockerfile)
        self.assertNotIn("COPY . ", dockerfile)
        self.assertNotIn("--privileged", dockerfile)
        self.assertNotIn("network_mode: host", dockerfile)

        ignored = DOCKERIGNORE.read_text(encoding="utf-8").splitlines()
        self.assertEqual(["*", "!bundle", "!bundle/**", "!verify-bundle.py"], ignored)
        self.assertIn("COPY verify-bundle.py", dockerfile)
        verifier = CONTAINER_VERIFIER.read_text(encoding="utf-8")
        self.assertIn("assert set(actual) == set(expected)", verifier)
        self.assertIn("assert allowed(relative)", verifier)
        self.assertIn('"*.mix"', verifier)

        compose = COMPOSE.read_text(encoding="utf-8")
        self.assertIn("12340:1234/tcp", compose)
        self.assertIn("read_only: true", compose)
        self.assertIn("no-new-privileges:true", compose)
        self.assertNotIn("privileged: true", compose)
        self.assertNotIn("network_mode: host", compose)

    def test_makefile_exposes_server_build_audit_and_test_targets(self):
        makefile = MAKEFILE.read_text(encoding="utf-8")
        self.assertIn("server-build:", makefile)
        self.assertIn("server-audit:", makefile)
        self.assertIn("server-test:", makefile)

    def test_legacy_launcher_obeys_one_process_one_room_lifecycle(self):
        source = LEGACY_LAUNCHER.read_text(encoding="utf-8")

        self.assertNotIn("while true", source)
        self.assertIn('Server.ListenAddress="${LISTEN_ADDRESS}"', source)
        self.assertIn('Server.MaxPlayers="${MAX_PLAYERS}"', source)
        self.assertIn('Server.IdleTimeoutSeconds="${IDLE_TIMEOUT_SECONDS}"', source)
        self.assertIn('Server.StatusFile="${STATUS_FILE}"', source)
        self.assertNotIn("AdvertiseOnline", source)
        self.assertNotIn("Server.AdvertiseOnline", source)
        self.assertIn('mkdir -p "${SUPPORT_DIR}"', source)

    def test_healthcheck_uses_the_same_runtime_status_location(self):
        source = HEALTHCHECK.read_text(encoding="utf-8")

        self.assertIn('SUPPORT_DIR=${SupportDir:-"${SERVER_ROOT}/support"}', source)
        self.assertIn('STATUS_FILE=${StatusFile:-"status/server-status.json"}', source)
        self.assertIn('STATUS_PATH=${SUPPORT_DIR}/${STATUS_FILE}', source)


if __name__ == "__main__":
    unittest.main()
