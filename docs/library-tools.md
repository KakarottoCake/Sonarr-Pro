# Library tools

The additions preserve existing quality profiles, custom-format scores, series
paths, download clients and compression presets. New automation and import
restrictions start disabled.

| What you want to do | Where to find it |
| --- | --- |
| Select a drive and combine anime, monitored or missing filters | Library: **Filter by path** and **Combine filters** |
| Pause automatic searches, RSS grabs and list additions | **System → Library tools → Pause automation**; use the banner to resume |
| Understand why a search skipped releases | Interactive search score details, and **System → Library tools → Recent searches** |
| Test all indexers and see individual failures | **Settings → Indexers → Test All** |
| Recover genuinely stalled downloads | **Settings → Download Clients → Options → Stalled download timeout**; zero disables recovery |
| Estimate extra space while torrents seed | **System → Library tools → Storage outlook** |
| Require hardlinks rather than falling back to a full copy | **System → Library tools → Hardlink-only imports** |
| Check registered folders and missing files | **System → Library tools → Check library** |
| Preview an import list and add selected shows | **System → Library tools → Preview import lists** |
| Restore a retained deleted file | **System → Library tools → Recycle bin**; choose its show and season |
| Create an expiring key for a companion app | **System → Library tools → Companion app keys** |
| Retain replaced versions and switch between them | A show's **Files, naming and retained versions** panel |
| Set automatic renaming independently for a show | The same show panel; **Preview Rename** still works manually |
| Set optional season folder titles | The same show panel; use `{Season Title}` and `{Season Year}` in Media Management's folder format |
| Preserve modification dates during upgrades | **Settings → Media Management → File Date → Preserve Original** |
| Check indexer show IDs or search anime packs first | Optional switches in **System → Library tools** |
| Search missing episodes in Calendar's displayed dates | Calendar's missing-episode search button |

## Imports and existing files

Original release titles and pack sizes remain attached to imported files, so
compression cannot turn a large release into a preferred small-pack release.
The database upgrade recovers this information only where grab/import history
identifies the exact series and file.

Manual episode selections survive download monitoring and restart. Multi-season
files retain their season boundaries; ambiguous files still require Manual
Import. Anime bundle matching requires explicit episode ranges, a complete aired
catalogue and an exact series or season alias. It does not guess from a similar
name.

Folder moves show a pending destination until the move succeeds. Failures retain
the original registered path and report a failed task. Renames handle colliding
names without overwriting existing files. Subtitle imports inspect subfolders
and preserve language variants, accessibility tags and subtitle labels.

## Retained versions and recycle-bin restore

Enable retention separately for each show. Future upgrades store previous video
files beneath that show's `Plex Versions` folder; they consume additional disk
space. The active file is the version Sonarr tracks. Switching versions retains
the previous active file and keeps the selected archive. Different episode
coverage requires Manual Import instead of an automatic switch.

Restores and version changes run as queued file operations and show their task
status. Existing destination files are never overwritten by recycle-bin
restore. Wait for compression on that show to finish before restoring or
switching versions. Compression itself does not create recycle-bin copies.

## Lists, rules and keys

TMDB account, discovery, list and person providers use a TMDB API Read Access
Token directly. Account lists also require the account ID in advanced settings.
Trakt smart lists use the existing Trakt connection; MDBList needs its list URL
and API key. New providers require configuration before use.

List previews distinguish existing shows. Selected additions use the chosen root
folder and quality profile, are unmonitored by default, and do not initiate a
missing-episode search. Up to 25 shows can be added per selection.

Custom-format conditions with alternative group zero retain the existing shared
logic. Positive groups are alternatives: shared conditions AND (group 1 OR group
2 …). Conditions within each group retain their existing required/optional
behavior. Episode-title exclusion is opt-in.

Companion keys expose only the documented library-read endpoints, optionally
with permission to add shows. They cannot edit or delete existing shows, read
settings or logs, run commands or start compression. Copy the token when created;
only its hash is stored. The existing master API key retains full access.
