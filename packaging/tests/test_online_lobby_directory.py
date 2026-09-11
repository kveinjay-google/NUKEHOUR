import unittest
from datetime import datetime, timedelta, timezone

from lobby.nukehour_lobby.directory import (
    InvalidRegistrationToken,
    RoomCodeExhausted,
    RoomDirectory,
)
from lobby.nukehour_lobby.models import (
    HeartbeatRequest,
    RegistrationRequest,
    RoomStatus,
)


class MutableClock:
    def __init__(self):
        self.now = datetime(2026, 9, 9, 12, 0, tzinfo=timezone.utc)

    def __call__(self):
        return self.now

    def advance(self, seconds):
        self.now += timedelta(seconds=seconds)


def registration(server_id="server-a", server_name="Alpha", ready=True):
    return RegistrationRequest(
        serverId=server_id,
        serverEndpoint="203.0.113.10",
        serverPort=4567,
        serverName=server_name,
        region="asia-east",
        status=RoomStatus.WAITING,
        map="map-uid-a",
        mod="ra2",
        modVersion="nukehour-core-sha256-example",
        players=0,
        maxPlayers=8,
        hasPassword=False,
        ready=ready,
        engineCompatibility="release-20250330",
        handshakeSchemaVersion=1,
        ordersVersion=23,
        runtimeCapability="ra2-presentation-capability-v2",
    )


class OnlineLobbyDirectoryTest(unittest.TestCase):
    def setUp(self):
        self.clock = MutableClock()

    def test_registration_returns_private_token_and_public_random_identity(self):
        directory = RoomDirectory(clock=self.clock, code_generator=lambda: "A7K9Q2")

        result = directory.register(registration())

        self.assertEqual("A7K9Q2", result.room.room_code)
        self.assertRegex(result.room.room_id, r"^[0-9a-f-]{36}$")
        self.assertGreaterEqual(len(result.registration_token), 32)
        public = result.room.model_dump(by_alias=True)
        self.assertNotIn("registrationToken", public)
        self.assertNotIn("runtimeContract", public)
        self.assertEqual("official", public["trust"])

    def test_room_code_collision_retries_without_overwriting(self):
        codes = iter(("A7K9Q2", "A7K9Q2", "B8M3R4"))
        directory = RoomDirectory(clock=self.clock, code_generator=lambda: next(codes))
        first = directory.register(registration("server-a", "Alpha"))
        second = directory.register(registration("server-b", "Bravo"))

        self.assertEqual("A7K9Q2", first.room.room_code)
        self.assertEqual("B8M3R4", second.room.room_code)
        self.assertEqual("Alpha", directory.get_by_code("a7k9q2").server_name)
        self.assertEqual("Bravo", directory.get_by_code("b8m3r4").server_name)

    def test_room_code_collision_has_a_bounded_failure(self):
        directory = RoomDirectory(
            clock=self.clock, code_generator=lambda: "A7K9Q2", code_attempts=2
        )
        directory.register(registration("server-a"))
        with self.assertRaises(RoomCodeExhausted):
            directory.register(registration("server-b"))

    def test_duplicate_server_registration_rotates_session_without_duplication(self):
        codes = iter(("A7K9Q2", "B8M3R4"))
        directory = RoomDirectory(clock=self.clock, code_generator=lambda: next(codes))
        first = directory.register(registration("server-a", "Alpha"))
        replacement = directory.register(registration("server-a", "Alpha 2"))

        self.assertEqual(first.room.room_id, replacement.room.room_id)
        self.assertEqual(first.room.room_code, replacement.room.room_code)
        self.assertNotEqual(first.registration_token, replacement.registration_token)
        self.assertEqual(1, len(directory.list_rooms()))
        self.assertEqual("Alpha 2", directory.list_rooms()[0].server_name)
        with self.assertRaises(InvalidRegistrationToken):
            directory.heartbeat(
                "server-a", first.registration_token, HeartbeatRequest()
            )

    def test_heartbeat_updates_authoritative_session_fields(self):
        directory = RoomDirectory(clock=self.clock, code_generator=lambda: "A7K9Q2")
        created = directory.register(registration())
        self.clock.advance(10)

        room = directory.heartbeat(
            "server-a",
            created.registration_token,
            HeartbeatRequest(
                status=RoomStatus.IN_GAME,
                map="map-uid-b",
                players=3,
                maxPlayers=6,
                hasPassword=True,
                ready=True,
            ),
        )

        self.assertEqual(RoomStatus.IN_GAME, room.status)
        self.assertEqual("map-uid-b", room.map)
        self.assertEqual(3, room.players)
        self.assertEqual(6, room.max_players)
        self.assertTrue(room.has_password)
        self.assertEqual(self.clock.now, room.last_heartbeat_at)

    def test_invalid_token_cannot_heartbeat_or_unregister(self):
        directory = RoomDirectory(clock=self.clock, code_generator=lambda: "A7K9Q2")
        directory.register(registration())

        with self.assertRaises(InvalidRegistrationToken):
            directory.heartbeat("server-a", "wrong", HeartbeatRequest())
        with self.assertRaises(InvalidRegistrationToken):
            directory.unregister("server-a", "wrong")
        self.assertEqual(1, len(directory.list_rooms()))

    def test_unregister_removes_room_from_public_lookup(self):
        directory = RoomDirectory(clock=self.clock, code_generator=lambda: "A7K9Q2")
        created = directory.register(registration())

        self.assertTrue(directory.unregister("server-a", created.registration_token))
        self.assertEqual([], directory.list_rooms())
        self.assertIsNone(directory.get_by_code("A7K9Q2"))
        self.assertIsNone(directory.get_by_id(created.room.room_id))

    def test_stale_or_not_ready_rooms_are_not_public(self):
        codes = iter(("A7K9Q2", "B8M3R4"))
        directory = RoomDirectory(
            clock=self.clock, code_generator=lambda: next(codes), stale_after_seconds=31
        )
        visible = directory.register(registration("server-a", ready=True))
        directory.register(registration("server-b", ready=False))
        self.assertEqual([visible.room.room_id], [r.room_id for r in directory.list_rooms()])

        self.clock.advance(32)
        self.assertEqual([], directory.list_rooms())
        self.assertIsNone(directory.get_by_code("A7K9Q2"))

    def test_re_registration_recovers_after_lobby_restart(self):
        first = RoomDirectory(clock=self.clock, code_generator=lambda: "A7K9Q2")
        first.register(registration())

        restarted = RoomDirectory(clock=self.clock, code_generator=lambda: "B8M3R4")
        recovered = restarted.register(registration())

        self.assertEqual("B8M3R4", recovered.room.room_code)
        self.assertEqual("server-a", recovered.room.server_id)

    def test_sorting_prefers_waiting_capacity_then_stable_creation_order(self):
        codes = iter(("A7K9Q2", "B8M3R4", "C9N4S5"))
        directory = RoomDirectory(clock=self.clock, code_generator=lambda: next(codes))
        first = directory.register(registration("first", "First"))
        self.clock.advance(1)
        second = directory.register(registration("second", "Second"))
        self.clock.advance(1)
        third = directory.register(registration("third", "Third"))

        directory.heartbeat(
            "first", first.registration_token,
            HeartbeatRequest(status=RoomStatus.IN_GAME, players=2),
        )
        directory.heartbeat(
            "second", second.registration_token,
            HeartbeatRequest(status=RoomStatus.WAITING, players=8, maxPlayers=8),
        )
        directory.heartbeat(
            "third", third.registration_token,
            HeartbeatRequest(status=RoomStatus.WAITING, players=2),
        )

        self.assertEqual(
            ["third", "second", "first"],
            [room.server_id for room in directory.list_rooms()],
        )


if __name__ == "__main__":
    unittest.main()
