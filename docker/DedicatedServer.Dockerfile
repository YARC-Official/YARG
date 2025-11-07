FROM ubuntu:22.04

ENV DEBIAN_FRONTEND=noninteractive
RUN apt-get update && apt-get install -y --no-install-recommends \
    ca-certificates \
    libgl1 \
    libxcursor1 \
    libxrandr2 \
    libxi6 \
    libxinerama1 \
    && rm -rf /var/lib/apt/lists/*

ENV YARG_MAX_PLAYERS=8 \
    YARG_PRIVACY=public \
    YARG_PASSWORD= \
    YARG_LOBBY_NAME="YARG Dedicated Server" \
    YARG_HOST_NAME="Server" \
    YARG_DEDICATED=1 \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1

WORKDIR /app
COPY Build/DedicatedServer/ /app/
RUN useradd --no-log-init --system --home /app yarg \
    && chown -R yarg /app \
    && chmod +x /app/YARGServer
USER yarg

EXPOSE 7777/udp 7777/tcp

ENTRYPOINT ["./YARGServer"]
CMD ["-batchmode", "-nographics", "-dedicated"]
