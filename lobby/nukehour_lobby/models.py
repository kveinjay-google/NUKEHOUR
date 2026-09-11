"""Validated wire and domain models for the NUKE HOUR room directory."""

from __future__ import annotations

import ipaddress
import re
from datetime import datetime
from enum import Enum

from pydantic import BaseModel, ConfigDict, Field, field_validator, model_validator


CONTROL_CHARACTERS = re.compile(r"[\x00-\x1f\x7f]")
ROOM_CODE_PATTERN = re.compile(r"^[ABCDEFGHJKMNPQRSTUVWXYZ23456789]{6}$")


def _bounded_text(value: str, field_name: str) -> str:
    value = value.strip()
    if not value or CONTROL_CHARACTERS.search(value):
        raise ValueError(f"{field_name} must contain visible text without control characters")
    return value


class LobbyModel(BaseModel):
    model_config = ConfigDict(extra="forbid", populate_by_name=True)


class RoomStatus(str, Enum):
    STARTING = "STARTING"
    WAITING = "WAITING"
    IN_GAME = "IN_GAME"
    FINISHED = "FINISHED"
    OFFLINE = "OFFLINE"


class RegistrationRequest(LobbyModel):
    server_id: str = Field(alias="serverId", min_length=1, max_length=64)
    server_endpoint: str = Field(alias="serverEndpoint", min_length=1, max_length=253)
    server_port: int = Field(alias="serverPort", ge=1, le=65535)
    server_name: str = Field(alias="serverName", min_length=1, max_length=64)
    region: str = Field(min_length=1, max_length=32, pattern=r"^[a-z0-9]+(?:-[a-z0-9]+)*$")
    status: RoomStatus
    map: str = Field(min_length=1, max_length=128)
    mod: str = Field(min_length=1, max_length=32, pattern=r"^[A-Za-z0-9._-]+$")
    mod_version: str = Field(alias="modVersion", min_length=1, max_length=128)
    players: int = Field(ge=0, le=64)
    max_players: int = Field(alias="maxPlayers", ge=1, le=64)
    has_password: bool = Field(alias="hasPassword")
    ready: bool
    engine_compatibility: str = Field(alias="engineCompatibility", min_length=1, max_length=128)
    handshake_schema_version: int = Field(alias="handshakeSchemaVersion", ge=1, le=65535)
    orders_version: int = Field(alias="ordersVersion", ge=1, le=65535)
    runtime_capability: str = Field(alias="runtimeCapability", min_length=1, max_length=128)

    @field_validator(
        "server_id", "server_name", "map", "mod_version",
        "engine_compatibility", "runtime_capability",
    )
    @classmethod
    def validate_visible_text(cls, value: str, info):
        return _bounded_text(value, info.field_name)

    @field_validator("server_endpoint")
    @classmethod
    def validate_endpoint(cls, value: str) -> str:
        value = value.strip()
        try:
            return str(ipaddress.ip_address(value))
        except ValueError as exc:
            raise ValueError("serverEndpoint must be a numeric public endpoint") from exc

    @model_validator(mode="after")
    def validate_capacity(self):
        if self.players > self.max_players:
            raise ValueError("players must not exceed maxPlayers")
        return self


class HeartbeatRequest(LobbyModel):
    status: RoomStatus | None = None
    map: str | None = Field(default=None, min_length=1, max_length=128)
    players: int | None = Field(default=None, ge=0, le=64)
    max_players: int | None = Field(default=None, alias="maxPlayers", ge=1, le=64)
    has_password: bool | None = Field(default=None, alias="hasPassword")
    ready: bool | None = None

    @field_validator("map")
    @classmethod
    def validate_map(cls, value: str | None):
        return None if value is None else _bounded_text(value, "map")


class PublicRoom(LobbyModel):
    room_id: str = Field(alias="roomId")
    room_code: str = Field(alias="roomCode", pattern=ROOM_CODE_PATTERN.pattern)
    server_id: str = Field(alias="serverId")
    server_endpoint: str = Field(alias="serverEndpoint")
    server_port: int = Field(alias="serverPort")
    server_name: str = Field(alias="serverName")
    region: str
    status: RoomStatus
    map: str
    mod: str
    mod_version: str = Field(alias="modVersion")
    players: int
    max_players: int = Field(alias="maxPlayers")
    has_password: bool = Field(alias="hasPassword")
    ready: bool
    engine_compatibility: str = Field(alias="engineCompatibility")
    handshake_schema_version: int = Field(alias="handshakeSchemaVersion")
    orders_version: int = Field(alias="ordersVersion")
    runtime_capability: str = Field(alias="runtimeCapability")
    trust: str = "official"
    created_at: datetime = Field(alias="createdAt")
    last_heartbeat_at: datetime = Field(alias="lastHeartbeatAt")


class PrivateRegistration(LobbyModel):
    room: PublicRoom
    registration_token: str = Field(alias="registrationToken")


class RoomListResponse(LobbyModel):
    rooms: list[PublicRoom]
