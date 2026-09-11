"""Small in-process fixed-window limiter for a single Lobby instance."""

from __future__ import annotations

import threading
from collections import defaultdict, deque
from collections.abc import Callable
from datetime import datetime, timezone


def _utc_now() -> datetime:
    return datetime.now(timezone.utc)


class RateLimiter:
    def __init__(self, *, clock: Callable[[], datetime] = _utc_now):
        self._clock = clock
        self._lock = threading.Lock()
        self._events: dict[tuple[str, str], deque[float]] = defaultdict(deque)

    def allow(self, scope: str, identity: str, limit: int, window_seconds: int) -> bool:
        if limit < 1 or window_seconds < 1:
            raise ValueError("rate limits and windows must be positive")
        now = self._clock().timestamp()
        cutoff = now - window_seconds
        key = (scope, identity)
        with self._lock:
            events = self._events[key]
            while events and events[0] <= cutoff:
                events.popleft()
            if len(events) >= limit:
                return False
            events.append(now)
            return True
