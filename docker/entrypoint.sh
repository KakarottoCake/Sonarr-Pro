#!/bin/sh
set -e

# Sonarr Pro writes to /config and to whatever media folders are mounted. If it ran as
# root it would create files the host user cannot edit or delete, which is the usual
# cause of "permission denied" reports after a container is removed. So the app runs as
# a normal user whose ids are set to match the host's, the way the other *arr images do.

PUID=${PUID:-1000}
PGID=${PGID:-1000}

if [ "$(id -u)" = "0" ]; then
    if ! getent group sonarr >/dev/null 2>&1; then
        addgroup --gid "$PGID" sonarr 2>/dev/null || groupadd -g "$PGID" sonarr 2>/dev/null || true
    fi

    if ! getent passwd sonarr >/dev/null 2>&1; then
        adduser --uid "$PUID" --gid "$PGID" --disabled-password --gecos "" sonarr 2>/dev/null \
            || useradd -u "$PUID" -g "$PGID" -M -s /bin/sh sonarr 2>/dev/null || true
    fi

    # Only the config directory is chowned. Media libraries can hold a very large number
    # of files, and walking them on every start would delay startup for minutes; they are
    # expected to already be readable by PUID/PGID.
    mkdir -p /config
    chown -R "$PUID:$PGID" /config 2>/dev/null || true

    echo "Sonarr Pro starting as ${PUID}:${PGID}"
    exec gosu "$PUID:$PGID" "$@"
fi

# Already running as a non-root user, e.g. "docker run --user". Nothing to drop.
echo "Sonarr Pro starting as $(id -u):$(id -g)"
exec "$@"
