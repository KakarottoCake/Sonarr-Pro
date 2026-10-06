# Per-show compression worker

Sonarr exposes authenticated compression controls on each series page. The
optional worker runs as the media owner on the Linux host and uses FFmpeg and
ffprobe already installed there. A private Unix socket connects it to Sonarr;
there is no open TCP port, SSH credential, shell endpoint or Docker socket.

Example worker configuration (keep outside the public repository):

```json
{
  "socket": "/home/media/sonarr-pro/compression-shared/worker.sock",
  "stateDirectory": "/home/media/sonarr-pro/compression-state",
  "sonarrConfig": "/home/media/sonarr-pro/config/config.xml",
  "sonarrUrl": "http://127.0.0.1:6970",
  "mediaRoots": ["/mnt/media", "/mnt/media2"],
  "scriptIndex": "/home/media/.ghost_index"
}
```

Run `python3 worker.py --config /path/to/worker-config.json` as an unprivileged
media user. For Haswell VAAPI, add that user to the render group and install the
i965 VAAPI driver. Mount `compression-shared` into Sonarr at `/compression`.
The socket is mode 660; Sonarr's media UID/GID must match the worker. Optionally
set `SONARR_COMPRESSION_SOCKET` to another container-side socket path.

Use a systemd service with `KillMode=control-group`, `UMask=0007`,
`Restart=on-failure`, and the media user/group. Keep the worker state and
configuration private. Only one worker instance should use a socket/state
directory. Restarted jobs become interrupted; starting again skips files
already processed. A shared FileSaver/Phantom lock avoids concurrent script
encodes. Sonarr queues up to ten shows and compresses only registered files.

Software presets use x265 CRF 24 and fast/slow, at low process priority with at
most two encoding threads. Hardware fast/slow use tested VAAPI H.264 quality
controls. Hardware HEVC is not advertised on Haswell. FileSaver and Phantom
choices read the installed scripts' CRF/preset/QP and minimum-size settings and
use those settings through the same safer runner. Their CLI scripts remain
untouched, and their existing processed-file index is respected and updated.
These options do not invoke the scripts' Discord notifications.

Files retain their original filename/container. Audio, subtitles, attachments,
chapters and stream dispositions are copied; unsupported containers, HDR,
high bit-depth, HEVC/AV1 and previously processed files are skipped. Output must
save at least 2%, retain stream metadata/duration/resolution, and fully decode
before an atomic replacement. The source and its live Sonarr association are
rechecked at replacement. Hard-linked torrent originals are preserved.
Cancellation keeps the active original; completed encodes remain. Completion
requests a Sonarr rescan to refresh file sizes and codec information.

Safety tests: `python3 docker/compression/test_worker.py`. Generated-video
integration tests: `SONARR_COMPRESSION_INTEGRATION=1 python3 docker/compression/test_worker.py`.
