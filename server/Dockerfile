FROM python:3.11-slim-bookworm AS verify

COPY bundle/ /opt/nukehour/
COPY verify-bundle.py /tmp/verify-bundle.py
RUN test -x /opt/nukehour/run-server.sh \
    && test -x /opt/nukehour/healthcheck.sh \
    && python3 /tmp/verify-bundle.py /opt/nukehour

FROM debian:bookworm-slim AS runtime

RUN apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates libicu72 zlib1g \
    && rm -rf /var/lib/apt/lists/* \
    && useradd --system --uid 10001 --home-dir /opt/nukehour --shell /usr/sbin/nologin nukehour \
    && mkdir -p /data /opt/nukehour/support \
    && chown -R nukehour:nukehour /data /opt/nukehour/support

COPY --from=verify --chown=nukehour:nukehour /opt/nukehour/ /opt/nukehour/

USER nukehour
WORKDIR /opt/nukehour
ENV SupportDir=/data
ENV ListenAddress=0.0.0.0
ENV ListenPort=1234
EXPOSE 1234/tcp
VOLUME ["/data"]
HEALTHCHECK --interval=10s --timeout=3s --start-period=20s --retries=3 CMD ["/opt/nukehour/healthcheck.sh"]
ENTRYPOINT ["/opt/nukehour/run-server.sh"]
