"""FastAPI entry point for the NUKE HOUR Online Lobby."""

from __future__ import annotations

import logging
import os
import secrets
from collections.abc import Callable
from datetime import datetime, timezone

from fastapi import Depends, FastAPI, HTTPException, Request, Response, status
from fastapi.responses import JSONResponse
from fastapi.security import HTTPAuthorizationCredentials, HTTPBearer
from pydantic import BaseModel, ConfigDict, Field, field_validator
from starlette.middleware.trustedhost import TrustedHostMiddleware

from .directory import InvalidRegistrationToken, RoomCodeExhausted, RoomDirectory
from .models import (
    HeartbeatRequest,
    PrivateRegistration,
    PublicRoom,
    RegistrationRequest,
    RoomListResponse,
    ROOM_CODE_PATTERN,
)
from .rate_limit import RateLimiter


LOG = logging.getLogger("nukehour.lobby")
BEARER = HTTPBearer(auto_error=False)


def _utc_now() -> datetime:
    return datetime.now(timezone.utc)


class LobbySettings(BaseModel):
    model_config = ConfigDict(extra="forbid", frozen=True)

    environment: str = Field(default="development", pattern=r"^(development|test|staging|production)$")
    registration_token: str = Field(min_length=32, max_length=512)
    allowed_hosts: tuple[str, ...] = ("localhost", "127.0.0.1", "testserver")
    max_body_bytes: int = Field(default=16384, ge=1024, le=1_048_576)
    public_rate_limit: int = Field(default=120, ge=1, le=10000)
    server_rate_limit: int = Field(default=240, ge=1, le=10000)
    rate_window_seconds: int = Field(default=60, ge=1, le=3600)

    @field_validator("allowed_hosts")
    @classmethod
    def validate_hosts(cls, hosts: tuple[str, ...]):
        if not hosts or any(not host.strip() for host in hosts):
            raise ValueError("allowed_hosts must contain explicit host names")
        return tuple(host.strip() for host in hosts)

    @classmethod
    def from_environment(cls):
        token = os.environ.get("NUKEHOUR_LOBBY_REGISTRATION_TOKEN", "")
        hosts = tuple(
            host.strip()
            for host in os.environ.get("NUKEHOUR_LOBBY_ALLOWED_HOSTS", "localhost,127.0.0.1").split(",")
            if host.strip()
        )
        return cls(
            environment=os.environ.get("NUKEHOUR_LOBBY_ENVIRONMENT", "production"),
            registration_token=token,
            allowed_hosts=hosts,
            max_body_bytes=int(os.environ.get("NUKEHOUR_LOBBY_MAX_BODY_BYTES", "16384")),
            public_rate_limit=int(os.environ.get("NUKEHOUR_LOBBY_PUBLIC_RATE_LIMIT", "120")),
            server_rate_limit=int(os.environ.get("NUKEHOUR_LOBBY_SERVER_RATE_LIMIT", "240")),
            rate_window_seconds=int(os.environ.get("NUKEHOUR_LOBBY_RATE_WINDOW_SECONDS", "60")),
        )


class SecurityHeadersMiddleware:
    def __init__(self, app):
        self.app = app

    async def __call__(self, scope, receive, send):
        if scope["type"] != "http":
            await self.app(scope, receive, send)
            return

        async def send_with_headers(message):
            if message["type"] == "http.response.start":
                headers = list(message.get("headers", []))
                headers.extend((
                    (b"x-content-type-options", b"nosniff"),
                    (b"x-frame-options", b"DENY"),
                    (b"referrer-policy", b"no-referrer"),
                    (b"permissions-policy", b"camera=(), microphone=(), geolocation=()"),
                    (b"cache-control", b"no-store"),
                ))
                message["headers"] = headers
            await send(message)

        await self.app(scope, receive, send_with_headers)


class RequestSizeLimitMiddleware:
    def __init__(self, app, max_body_bytes: int):
        self.app = app
        self.max_body_bytes = max_body_bytes

    async def __call__(self, scope, receive, send):
        if scope["type"] != "http":
            await self.app(scope, receive, send)
            return

        headers = dict(scope.get("headers", []))
        raw_length = headers.get(b"content-length")
        if raw_length is not None:
            try:
                too_large = int(raw_length) > self.max_body_bytes
            except ValueError:
                too_large = True
            if too_large:
                await self._reject(scope, receive, send)
                return

        body = bytearray()
        while True:
            message = await receive()
            if message["type"] == "http.disconnect":
                return
            body.extend(message.get("body", b""))
            if len(body) > self.max_body_bytes:
                await self._reject(scope, receive, send)
                return
            if not message.get("more_body", False):
                break

        delivered = False

        async def bounded_receive():
            nonlocal delivered
            if delivered:
                return {"type": "http.request", "body": b"", "more_body": False}
            delivered = True
            return {"type": "http.request", "body": bytes(body), "more_body": False}

        await self.app(scope, bounded_receive, send)

    @staticmethod
    async def _reject(scope, receive, send):
        response = JSONResponse(
            status_code=status.HTTP_413_CONTENT_TOO_LARGE,
            content={"detail": "request too large"},
        )
        await response(scope, receive, send)


def create_app(
    settings: LobbySettings,
    directory: RoomDirectory | None = None,
    *,
    clock: Callable[[], datetime] = _utc_now,
) -> FastAPI:
    production = settings.environment in {"staging", "production"}
    app = FastAPI(
        title="NUKE HOUR Online Lobby",
        debug=False,
        docs_url=None if production else "/docs",
        redoc_url=None,
        openapi_url=None if production else "/openapi.json",
    )
    rooms = directory or RoomDirectory(clock=clock)
    limiter = RateLimiter(clock=clock)
    app.state.directory = rooms
    app.state.settings = settings
    app.add_middleware(TrustedHostMiddleware, allowed_hosts=list(settings.allowed_hosts))
    app.add_middleware(RequestSizeLimitMiddleware, max_body_bytes=settings.max_body_bytes)
    app.add_middleware(SecurityHeadersMiddleware)

    def cleanup_stale_rooms():
        expired = rooms.remove_stale()
        if expired:
            LOG.info("Expired official rooms after heartbeat timeout count=%s", expired)

    def remote_identity(request: Request) -> str:
        return request.client.host if request.client else "unknown"

    def public_rate_limit(request: Request):
        if not limiter.allow(
            "public", remote_identity(request), settings.public_rate_limit,
            settings.rate_window_seconds,
        ):
            raise HTTPException(status_code=429, detail="rate limit exceeded")

    def server_rate_limit(request: Request):
        if not limiter.allow(
            "server", remote_identity(request), settings.server_rate_limit,
            settings.rate_window_seconds,
        ):
            raise HTTPException(status_code=429, detail="rate limit exceeded")

    def bearer_value(credentials: HTTPAuthorizationCredentials | None) -> str:
        if credentials is None or credentials.scheme.lower() != "bearer":
            raise HTTPException(status_code=401, detail="authentication required")
        return credentials.credentials

    def require_registration_credential(
        credentials: HTTPAuthorizationCredentials | None = Depends(BEARER),
    ):
        provided = bearer_value(credentials)
        if not secrets.compare_digest(provided, settings.registration_token):
            raise HTTPException(status_code=401, detail="invalid credential")

    def require_room_credential(
        server_id: str,
        credentials: HTTPAuthorizationCredentials | None = Depends(BEARER),
    ) -> str:
        provided = bearer_value(credentials)
        if not rooms.has_server(server_id):
            raise HTTPException(status_code=404, detail="server registration not found")
        try:
            rooms.authorize(server_id, provided)
        except InvalidRegistrationToken as exc:
            raise HTTPException(status_code=401, detail="invalid credential") from exc
        return provided

    @app.exception_handler(RoomCodeExhausted)
    async def room_code_exhausted(_request: Request, _exception: RoomCodeExhausted):
        LOG.error("Room code allocation exhausted")
        return JSONResponse(status_code=503, content={"detail": "room code unavailable"})

    @app.get("/health", response_model=dict[str, str])
    def health():
        return {"status": "ok"}

    @app.get("/ready", response_model=dict[str, bool])
    def ready():
        return {"ready": True}

    @app.get(
        "/v1/rooms", response_model=RoomListResponse,
        dependencies=[Depends(public_rate_limit)],
    )
    def list_rooms():
        cleanup_stale_rooms()
        result = rooms.list_rooms()
        LOG.info("Listed official rooms count=%s", len(result))
        return RoomListResponse(rooms=result)

    @app.get(
        "/v1/rooms/code/{room_code}", response_model=PublicRoom,
        dependencies=[Depends(public_rate_limit)],
    )
    def room_by_code(room_code: str):
        cleanup_stale_rooms()
        normalized = room_code.upper()
        if not ROOM_CODE_PATTERN.fullmatch(normalized):
            raise HTTPException(status_code=422, detail="invalid room code")
        room = rooms.get_by_code(normalized)
        if room is None:
            LOG.info("Resolved official room code found=false")
            raise HTTPException(status_code=404, detail="room not found")
        LOG.info("Resolved official room code found=true")
        return room

    @app.get(
        "/v1/rooms/{room_id}", response_model=PublicRoom,
        dependencies=[Depends(public_rate_limit)],
    )
    def room_by_id(room_id: str):
        cleanup_stale_rooms()
        room = rooms.get_by_id(room_id)
        if room is None:
            LOG.info("Resolved official room id found=false")
            raise HTTPException(status_code=404, detail="room not found")
        LOG.info("Resolved official room id found=true")
        return room

    @app.post(
        "/v1/servers/register", response_model=PrivateRegistration,
        status_code=201,
        dependencies=[Depends(server_rate_limit), Depends(require_registration_credential)],
    )
    def register(request: RegistrationRequest):
        cleanup_stale_rooms()
        result = rooms.register(request)
        LOG.info("Registered official room serverId=%s roomId=%s", request.server_id, result.room.room_id)
        return result

    @app.post(
        "/v1/servers/{server_id}/heartbeat", response_model=PublicRoom,
        dependencies=[Depends(server_rate_limit)],
    )
    def heartbeat(
        server_id: str,
        request: HeartbeatRequest,
        token: str = Depends(require_room_credential),
    ):
        try:
            return rooms.heartbeat(server_id, token, request)
        except ValueError as exc:
            raise HTTPException(status_code=422, detail=str(exc)) from exc

    @app.post(
        "/v1/servers/{server_id}/unregister", status_code=204,
        dependencies=[Depends(server_rate_limit)],
    )
    def unregister(server_id: str, token: str = Depends(require_room_credential)):
        rooms.unregister(server_id, token)
        LOG.info("Unregistered official room serverId=%s", server_id)
        return Response(status_code=204)

    return app


def app_from_environment() -> FastAPI:
    return create_app(LobbySettings.from_environment())
