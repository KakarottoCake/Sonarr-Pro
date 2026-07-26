# syntax=docker/dockerfile:1

# Sonarr Pro
#
# Three stages: the React UI, the .NET backend, then a runtime image holding only
# the published output. Building the UI separately means a backend-only change does
# not reinstall node_modules, and vice versa.

# ---------------------------------------------------------------------------
# UI
# ---------------------------------------------------------------------------
FROM node:20-bookworm AS ui

WORKDIR /src

# Copy the manifests alone first so the dependency layer is reused whenever
# application source changes but dependencies do not.
COPY package.json yarn.lock .yarnrc ./
RUN yarn install --frozen-lockfile --network-timeout 600000

# tsconfig.json lives at the root and is resolved from there by the TypeScript loader.
COPY tsconfig.json ./
COPY frontend/ ./frontend/

# --env production is what switches webpack out of eval-source-map. Without it the
# bundle ships as tens of megabytes of dev output and the UI renders as a blank page.
RUN yarn build --env production

# ---------------------------------------------------------------------------
# Backend
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS backend

ARG TARGETARCH
WORKDIR /src

COPY . .

# Map Docker's architecture names onto the .NET runtime identifiers, then build the
# way the upstream CI does. SelfContained keeps the runtime with the app so the final
# image needs no SDK.
#
# Platform is Posix for every Linux target. It names the OS family, not the CPU: the
# solution only declares "Any CPU", "Posix" and "Windows", so passing an architecture
# here fails with MSB4126 before anything compiles. The architecture is carried by
# RuntimeIdentifiers instead.
RUN set -eux; \
    case "${TARGETARCH}" in \
      amd64) RID=linux-x64   ;; \
      arm64) RID=linux-arm64 ;; \
      *) echo "Unsupported architecture: ${TARGETARCH}" >&2; exit 1 ;; \
    esac; \
    dotnet msbuild -restore src/Sonarr.sln \
      -p:SelfContained=true \
      -p:Configuration=Release \
      -p:Platform=Posix \
      -p:RuntimeIdentifiers="${RID}" \
      -t:PublishAllRids; \
    mkdir -p /app; \
    cp -r "_output/net10.0/${RID}/publish/." /app/

# The UI is built separately and is not produced by the .NET build.
COPY --from=ui /src/_output/UI /app/UI

# The publish output is platform-agnostic, so it carries assemblies this image can
# never use. Upstream's packaging drops the same ones for its Linux builds: the
# Windows platform assembly, and the Windows service installers. Sonarr.Update goes
# too, because updating happens through Docker here.
RUN set -eux; \
    rm -rf /app/Sonarr.Update; \
    rm -f /app/Sonarr.Windows.*; \
    rm -f /app/ServiceInstall.* /app/ServiceUninstall.*; \
    chmod +x /app/Sonarr; \
    find /app -name ffprobe -exec chmod +x {} \;

# ---------------------------------------------------------------------------
# Runtime
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-noble AS runtime

# runtime-deps already carries what a self-contained .NET app needs to start —
# ICU for globalization, ca-certificates for outbound HTTPS, tzdata — so only the
# extras go here. gosu drops privileges in the entrypoint; libsqlite3-0 backs the
# database.
RUN set -eux; \
    apt-get update; \
    apt-get install -y --no-install-recommends \
      gosu \
      libsqlite3-0; \
    rm -rf /var/lib/apt/lists/*

COPY --from=backend /app /app
COPY docker/entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh

# This image is a binary distribution of GPLv3 software, so it carries its licence
# and a pointer to the corresponding source.
COPY LICENSE.md /app/LICENSE.md

# /config holds the database, config.xml and logs. Everything else is media.
VOLUME ["/config"]
EXPOSE 8989

ENV XDG_CONFIG_HOME=/config \
    PUID=1000 \
    PGID=1000 \
    TZ=Etc/UTC \
    COMPlus_EnableDiagnostics=0

# Docker restarts the container on failure, so the app does not need to fork or
# manage its own lifetime.
ENTRYPOINT ["/entrypoint.sh"]
CMD ["/app/Sonarr", "-nobrowser", "-data=/config"]

LABEL org.opencontainers.image.title="Sonarr Pro" \
      org.opencontainers.image.description="A fork of Sonarr with TMDB, IMDb, AniList and MyAnimeList metadata, selectable episode orderings, absolute numbering, multi-season packs and fake release filtering." \
      org.opencontainers.image.source="https://github.com/KakarottoCake/Sonarr-Pro" \
      org.opencontainers.image.licenses="GPL-3.0-only"
