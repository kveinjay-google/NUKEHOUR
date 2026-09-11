FROM python:3.11-slim-bookworm AS runtime

RUN useradd --system --uid 10002 --home-dir /opt/nukehour-lobby --shell /usr/sbin/nologin nukehour-lobby \
    && mkdir -p /opt/nukehour-lobby \
    && chown nukehour-lobby:nukehour-lobby /opt/nukehour-lobby

COPY bundle/ /opt/nukehour-lobby/
RUN python3 -m pip install --no-cache-dir --requirement /opt/nukehour-lobby/requirements.txt \
    && chown -R nukehour-lobby:nukehour-lobby /opt/nukehour-lobby

USER nukehour-lobby
WORKDIR /opt/nukehour-lobby
ENV NUKEHOUR_LOBBY_ENVIRONMENT=production
ENV NUKEHOUR_LOBBY_HOST=0.0.0.0
ENV NUKEHOUR_LOBBY_PORT=8080
EXPOSE 8080/tcp
HEALTHCHECK --interval=15s --timeout=3s --start-period=10s --retries=3 \
    CMD ["python3", "-c", "import urllib.request; urllib.request.urlopen('http://127.0.0.1:8080/health', timeout=2).read()"]
ENTRYPOINT ["/opt/nukehour-lobby/run-lobby.sh"]
