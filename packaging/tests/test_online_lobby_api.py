import json
import unittest

from fastapi.testclient import TestClient

from lobby.nukehour_lobby.app import LobbySettings, create_app
from lobby.nukehour_lobby.directory import RoomDirectory
from packaging.tests.test_online_lobby_directory import MutableClock, registration


class OnlineLobbyApiTest(unittest.TestCase):
    def setUp(self):
        self.clock = MutableClock()
        self.codes = iter(("A7K9Q2", "B8M3R4", "C9N4S5"))
        self.directory = RoomDirectory(
            clock=self.clock, code_generator=lambda: next(self.codes)
        )
        self.settings = LobbySettings(
            environment="test",
            registration_token="r" * 48,
            allowed_hosts=("testserver", "localhost"),
            max_body_bytes=4096,
            public_rate_limit=100,
            server_rate_limit=100,
            rate_window_seconds=60,
        )
        self.client = TestClient(
            create_app(self.settings, self.directory, clock=self.clock),
            raise_server_exceptions=False,
        )

    def auth(self, token=None):
        return {"Authorization": f"Bearer {token or self.settings.registration_token}"}

    def register(self, payload=None, token=None):
        value = payload or registration()
        body = value if isinstance(value, dict) else value.model_dump(by_alias=True, mode="json")
        return self.client.post("/v1/servers/register", json=body, headers=self.auth(token))

    def test_health_and_readiness_are_public_and_minimal(self):
        self.assertEqual({"status": "ok"}, self.client.get("/health").json())
        self.assertEqual({"ready": True}, self.client.get("/ready").json())

    def test_registration_requires_exact_bearer_credential(self):
        self.assertEqual(401, self.client.post("/v1/servers/register", json={}).status_code)
        self.assertEqual(401, self.register(token="wrong-registration-token").status_code)

        response = self.register()
        self.assertEqual(201, response.status_code)
        document = response.json()
        self.assertEqual("A7K9Q2", document["room"]["roomCode"])
        self.assertGreaterEqual(len(document["registrationToken"]), 32)

    def test_room_list_details_and_case_insensitive_code_lookup(self):
        created = self.register().json()
        room_id = created["room"]["roomId"]

        listing = self.client.get("/v1/rooms")
        self.assertEqual(200, listing.status_code)
        self.assertEqual([room_id], [r["roomId"] for r in listing.json()["rooms"]])
        self.assertEqual(room_id, self.client.get(f"/v1/rooms/{room_id}").json()["roomId"])
        self.assertEqual(room_id, self.client.get("/v1/rooms/code/a7k9q2").json()["roomId"])
        self.assertEqual(404, self.client.get("/v1/rooms/code/ZZZZZZ").status_code)
        self.assertEqual(422, self.client.get("/v1/rooms/code/bad!").status_code)

    def test_heartbeat_uses_private_session_token_and_updates_public_room(self):
        created = self.register().json()
        server_token = created["registrationToken"]
        response = self.client.post(
            "/v1/servers/server-a/heartbeat",
            json={"status": "IN_GAME", "players": 2, "map": "map-uid-b"},
            headers=self.auth(server_token),
        )

        self.assertEqual(200, response.status_code)
        self.assertEqual("IN_GAME", response.json()["status"])
        self.assertEqual("map-uid-b", response.json()["map"])
        self.assertEqual(2, response.json()["players"])
        self.assertEqual(401, self.client.post(
            "/v1/servers/server-a/heartbeat", json={}, headers=self.auth("x" * 48)
        ).status_code)
        self.assertEqual(404, self.client.post(
            "/v1/servers/missing/heartbeat", json={}, headers=self.auth("x" * 48)
        ).status_code)

    def test_unregister_removes_room(self):
        created = self.register().json()
        response = self.client.post(
            "/v1/servers/server-a/unregister",
            headers=self.auth(created["registrationToken"]),
        )
        self.assertEqual(204, response.status_code)
        self.assertEqual([], self.client.get("/v1/rooms").json()["rooms"])

    def test_public_lookup_prunes_timed_out_registration(self):
        self.register()
        self.clock.advance(46)

        self.assertEqual([], self.client.get("/v1/rooms").json()["rooms"])
        self.assertFalse(self.directory.has_server("server-a"))
        self.assertEqual(404, self.client.post(
            "/v1/servers/server-a/heartbeat",
            json={},
            headers=self.auth("x" * 48),
        ).status_code)

    def test_write_models_reject_extra_fields_controls_and_bad_endpoints(self):
        base = registration().model_dump(by_alias=True, mode="json")
        for changed in (
            {**base, "unexpected": "value"},
            {**base, "serverName": "bad\nname"},
            {**base, "serverEndpoint": "example.com"},
            {**base, "players": 9, "maxPlayers": 8},
        ):
            with self.subTest(changed=changed):
                self.assertEqual(422, self.register(changed).status_code)

    def test_request_body_limit_rejects_oversized_json_before_parsing(self):
        response = self.client.post(
            "/v1/servers/register",
            content=json.dumps({"padding": "x" * 5000}),
            headers={**self.auth(), "Content-Type": "application/json"},
        )
        self.assertEqual(413, response.status_code)
        self.assertEqual("request too large", response.json()["detail"])

        def chunked_body():
            yield b'{"padding":"'
            yield b"x" * 5000
            yield b'"}'

        response = self.client.post(
            "/v1/servers/register",
            content=chunked_body(),
            headers={**self.auth(), "Content-Type": "application/json"},
        )
        self.assertEqual(413, response.status_code)
        self.assertEqual("request too large", response.json()["detail"])

    def test_public_and_server_rate_limits_are_independent(self):
        settings = self.settings.model_copy(update={
            "public_rate_limit": 1,
            "server_rate_limit": 1,
        })
        client = TestClient(
            create_app(settings, RoomDirectory(clock=self.clock), clock=self.clock),
            raise_server_exceptions=False,
        )
        self.assertEqual(200, client.get("/v1/rooms").status_code)
        self.assertEqual(429, client.get("/v1/rooms").status_code)

        body = registration().model_dump(by_alias=True, mode="json")
        headers = self.auth()
        self.assertEqual(201, client.post("/v1/servers/register", json=body, headers=headers).status_code)
        self.assertEqual(429, client.post("/v1/servers/register", json=body, headers=headers).status_code)

    def test_production_disables_docs_cors_debug_and_rejects_unknown_hosts(self):
        production = self.settings.model_copy(update={"environment": "production"})
        client = TestClient(
            create_app(production, RoomDirectory(clock=self.clock), clock=self.clock),
            raise_server_exceptions=False,
        )
        self.assertEqual(404, client.get("/docs").status_code)
        self.assertEqual(404, client.get("/openapi.json").status_code)
        self.assertEqual(400, client.get("/health", headers={"Host": "evil.example"}).status_code)
        self.assertFalse(client.app.debug)
        self.assertNotIn("access-control-allow-origin", client.get("/health").headers)

    def test_responses_set_security_headers_and_never_echo_credentials(self):
        wrong = "never-echo-this-registration-secret"
        response = self.register(token=wrong)
        self.assertNotIn(wrong, response.text)
        self.assertEqual("nosniff", response.headers["x-content-type-options"])
        self.assertEqual("no-referrer", response.headers["referrer-policy"])
        self.assertEqual("DENY", response.headers["x-frame-options"])


if __name__ == "__main__":
    unittest.main()
