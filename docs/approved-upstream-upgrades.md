# Approved upstream additions

Approved on 2026-10-06. Preserve the existing library, quality preferences,
metadata providers, torrent reuse, multi-season import, compression worker,
mobile controls and path picker. Add settings with compatible defaults and
provide understandable controls; do not start downloads, move media or delete
files merely to validate a feature.

Implementation baseline: `5a756d49f4a630a37355f0fa77665c9185eaf548`.

## Delivery checklist

- [x] Consistent source-release scores after import (#5598).
- [x] Expired search/cache/download cleanup (#9023, #9028, #9029).
- [x] Mobile popovers, scroll locks, suggestions and language sorting (#9016, #8924, #9018).
- [x] Configurable stalled-download recovery (#8859).
- [x] Confirmed folder moves and collision-safe renames (#8799, #8835).
- [x] Search explanations, score breakdown and provider diagnostics (#8523, #6960, #6836).
- [x] Conservative anime packs and efficient season searches (#9007, #6495).
- [x] Persistent manual episode overrides (#6147).
- [x] Hardlink-only imports, copy explanations and queued-space estimates (#6260, #1906, #2233).
- [x] Database, backup and library health (#8975, #1118).
- [x] Combinable library filters (#4987).
- [x] Manual renaming independent of automatic naming (#6484, #926).
- [x] Subtitle language preservation and subfolder imports (#8505, #6821, #4931).
- [x] Recycle-bin browser and guarded restore (#1984).
- [x] Calendar missing-episode search (#8831).
- [x] Configurable external-ID mismatch protection (#8939).
- [x] Import-list preview and additional list providers (#5481, #8997, #9027, #7603).
- [x] Season folder titles/year naming (#6007, #7667).
- [x] Original file dates retained on upgrades (#8838).
- [x] Flexible format logic and episode-title exclusions (#7806, #7456).
- [x] Pause/resume automation (#1194).
- [x] Companion-app keys with limited permissions (#7549; approved in linked review).
- [x] Multiple retained episode versions (#4551).

## Validation and deployment

- [x] Whole-solution build and relevant backend tests.
- [x] Frontend typecheck, lint, CSS lint and production build.
- [x] Compression safety tests on Linux (worker unchanged; generated-video test not repeated).
- [x] Mobile and desktop UI verification.
Deployment uses a consistent stopped-app SQLite backup, an isolated startup and
key-permission smoke check, a digest-pinned image, and library/settings
fingerprints. Its verified outcome is recorded privately on the host in
`deployment.json` and the timestamped backup receipt.

Upstream PR code is adapted and reviewed against this fork, rather than assumed
to work unchanged. Historical investigation is saved outside the repository in
`../_ops/sonarr-upstream-review.md` and `../_ops/sonarr-upstream-index.md`.

### Test evidence

- Whole-solution Debug build: zero warnings and errors.
- Offline Core suite: 5,625 passed, 17 skipped; final targeted run including
  provider fixes: 118 passed.
- Cache tests: 18 passed.
- Linux compression worker: 25 passed, one optional generated-video test skipped.
- Frontend typecheck, ESLint, CSS lint and Vite production build passed.
- Isolated browser checks: 390-pixel phone and 1280-pixel desktop layouts;
  native path picker, combined filters, list selection and pause banner.
- Real-library SQLite migration rehearsal: 167 shows and 5,288 episode-file
  records preserved; 224 exact source-release contexts recovered.

The broader Common suite had three environment failures on this Windows host:
certificate-store import, system Python discovery and unwritable ProgramData.
The modified cache tests passed separately. Online metadata integration tests
are classified and excluded from the offline suite. New list providers have
literal-JSON parser tests; account credentials are required for live use.
