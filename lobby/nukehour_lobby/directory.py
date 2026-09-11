"""Thread-safe, short-lived official room directory."""

from __future__ import annotations

import secrets
import threading
import uuid
from collections.abc import Callable
from dataclasses import dataclass
from datetime import datetime, timedelta, timezone

from .models import (
    HeartbeatRequest,
    PrivateRegistration,
    PublicRoom,
    RegistrationRequest,
    RoomStatus,
)


ROOM_CODE_ALPHABET = "ABCDEFGHJKMNPQRSTUVWXYZ23456789"


class InvalidRegistrationToken(PermissionError):
    pass


class RoomCodeExhausted(RuntimeError):
    pass


@dataclass(frozen=True)
class _RoomRecord:
    room: PublicRoom
    registration_token: str


def _utc_now() -> datetime:
    return datetime.now(timezone.utc)


def _random_room_code() -> str:
    return "".join(secrets.choice(ROOM_CODE_ALPHABET) for _ in range(6))


class RoomDirectory:
    def __init__(
        self,
        *,
        clock: Callable[[], datetime] = _utc_now,
        code_generator: Callable[[], str] = _random_room_code,
        stale_after_seconds: int = 45,
        code_attempts: int = 32,
    ):
        if stale_after_seconds < 1:
            raise ValueError("stale_after_seconds must be positive")
        if code_attempts < 1:
            raise ValueError("code_attempts must be positive")
        self._clock = clock
        self._code_generator = code_generator
        self._stale_after = timedelta(seconds=stale_after_seconds)
        self._code_attempts = code_attempts
        self._lock = threading.RLock()
        self._by_server: dict[str, _RoomRecord] = {}

    def _now(self) -> datetime:
        now = self._clock()
        if now.tzinfo is None:
            raise ValueError("clock must return a timezone-aware datetime")
        return now

    def _active_records(self, now: datetime) -> list[_RoomRecord]:
        return [
            record for record in self._by_server.values()
            if now - record.room.last_heartbeat_at <= self._stale_after
        ]

    def _new_code(self, excluded_server_id: str | None = None) -> str:
        used = {
            record.room.room_code
            for server_id, record in self._by_server.items()
            if server_id != excluded_server_id
        }
        for _ in range(self._code_attempts):
            code = self._code_generator().upper()
            if code not in used:
                return code
        raise RoomCodeExhausted("unable to allocate a unique room code")

    def register(self, request: RegistrationRequest) -> PrivateRegistration:
        with self._lock:
            now = self._now()
            existing = self._by_server.get(request.server_id)
            room_id = existing.room.room_id if existing else str(uuid.uuid4())
            room_code = existing.room.room_code if existing else self._new_code()
            created_at = existing.room.created_at if existing else now
            token = secrets.token_urlsafe(32)
            room = PublicRoom(
                roomId=room_id,
                roomCode=room_code,
                serverId=request.server_id,
                serverEndpoint=request.server_endpoint,
                serverPort=request.server_port,
                serverName=request.server_name,
                region=request.region,
                status=request.status,
                map=request.map,
                mod=request.mod,
                modVersion=request.mod_version,
                players=request.players,
                maxPlayers=request.max_players,
                hasPassword=request.has_password,
                ready=request.ready,
                engineCompatibility=request.engine_compatibility,
                handshakeSchemaVersion=request.handshake_schema_version,
                ordersVersion=request.orders_version,
                runtimeCapability=request.runtime_capability,
                createdAt=created_at,
                lastHeartbeatAt=now,
            )
            self._by_server[request.server_id] = _RoomRecord(room, token)
            return PrivateRegistration(room=room, registrationToken=token)

    @staticmethod
    def _authorized(record: _RoomRecord | None, token: str) -> _RoomRecord:
        if record is None or not secrets.compare_digest(record.registration_token, token):
            raise InvalidRegistrationToken("invalid registration token")
        return record

    def has_server(self, server_id: str) -> bool:
        with self._lock:
            return server_id in self._by_server

    def authorize(self, server_id: str, token: str) -> None:
        with self._lock:
            self._authorized(self._by_server.get(server_id), token)

    def heartbeat(self, server_id: str, token: str, request: HeartbeatRequest) -> PublicRoom:
        with self._lock:
            record = self._authorized(self._by_server.get(server_id), token)
            updates = request.model_dump(exclude_none=True)
            max_players = updates.get("max_players", record.room.max_players)
            players = updates.get("players", record.room.players)
            if players > max_players:
                raise ValueError("players must not exceed maxPlayers")
            room = record.room.model_copy(update={**updates, "last_heartbeat_at": self._now()})
            self._by_server[server_id] = _RoomRecord(room, record.registration_token)
            return room

    def unregister(self, server_id: str, token: str) -> bool:
        with self._lock:
            self._authorized(self._by_server.get(server_id), token)
            del self._by_server[server_id]
            return True

    @staticmethod
    def _public(record: _RoomRecord, now: datetime, stale_after: timedelta) -> bool:
        room = record.room
        return (
            now - room.last_heartbeat_at <= stale_after
            and room.ready
            and room.status not in {RoomStatus.FINISHED, RoomStatus.OFFLINE, RoomStatus.STARTING}
        )

    @staticmethod
    def _sort_key(room: PublicRoom):
        if room.status == RoomStatus.WAITING and room.players < room.max_players:
            group = 0
        elif room.status == RoomStatus.WAITING:
            group = 1
        elif room.status == RoomStatus.IN_GAME:
            group = 2
        else:
            group = 3
        return group, room.created_at, room.room_id

    def list_rooms(self) -> list[PublicRoom]:
        with self._lock:
            now = self._now()
            rooms = [
                record.room for record in self._active_records(now)
                if self._public(record, now, self._stale_after)
            ]
            return sorted(rooms, key=self._sort_key)

    def get_by_id(self, room_id: str) -> PublicRoom | None:
        return next((room for room in self.list_rooms() if room.room_id == room_id), None)

    def get_by_code(self, room_code: str) -> PublicRoom | None:
        normalized = room_code.strip().upper()
        return next((room for room in self.list_rooms() if room.room_code == normalized), None)

    def remove_stale(self) -> int:
        with self._lock:
            now = self._now()
            stale = [
                server_id for server_id, record in self._by_server.items()
                if now - record.room.last_heartbeat_at > self._stale_after
            ]
            for server_id in stale:
                del self._by_server[server_id]
            return len(stale)
