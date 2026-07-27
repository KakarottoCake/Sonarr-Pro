# Sonarr Pro

A fork of [Sonarr](https://github.com/Sonarr/Sonarr) that adds the metadata and downloading features long-running and anime libraries need, and ships as a single Docker image.

> **Unofficial.** Sonarr Pro is not affiliated with, endorsed by, or supported by the Sonarr project. Please don't raise Sonarr Pro problems on Sonarr's issue tracker, forums, or Discord — they can't help with code they didn't write. Report them [here](https://github.com/KakarottoCake/Sonarr-Pro/issues) instead.

## Built with AI, and not quiet about it

This fork was written with **Claude Opus 5**. Not "AI-assisted" as a hedge — written with it, most of it, and I'd rather say so up front than have you find out from the commit trailers.

I'm pro-AI and this account is pro-AI. Not AI *art* — artists get to keep that one. But AI as a tool for building things? Absolutely. (AI video gets a pass strictly when it's funny stupid shit.)

**This is also why it isn't a pull request.** Sonarr's maintainers have their own position on AI-assisted contributions, and I'd guess they wouldn't want most of what's in here regardless of who or what wrote it. That's entirely their call — it's their project, their review time, and their support burden when something breaks at 3am. Forking is the polite option: nobody upstream has to review this, maintain it, or answer for it.

None of which is a complaint about them. Sonarr is excellent, this is built entirely on their work, and the only reason a fork like this is even possible is that they made it GPL and wrote it well.

**I'll do my best to merge upstream in at least once a month**, so this doesn't rot into an unmergeable mess. Where a bug turns out to be upstream's rather than mine, the plan is to report it there rather than patch around it here — that keeps the diff small and the merges cheap.

---

## Why this exists

Sonarr resolves every series through TheTVDB. That works well for weekly Western television and poorly for everything else — a 1,100-episode anime becomes twenty-odd invented seasons, a recut that TheTVDB folds into its parent can't be added at all, and an `S01-S05` pack is rejected outright.

Sonarr Pro keeps everything upstream does and adds a way around each of those.

## What's added

**Four metadata sources, picked per series** — TheTVDB, TMDB, AniList, and MyAnimeList (via Jikan), chosen from a dropdown above the search box when adding a series. Only TMDB needs a key; the rest need no account.

**Selectable episode orderings** — TMDB publishes alternate numbering schemes per series, and IMDb's dumps carry another. Where a series has more than one, you pick at add time. The one that motivates this is TMDB's *Absolute* order: One Piece becomes season 1, episodes 1–1181, instead of twenty-odd seasons. DVD order, story arcs and streaming orders come along for free.

**Series TheTVDB won't list separately** — recuts and spin-offs that TheTVDB folds into a parent series can be added from TMDB or AniList. Upstream can't add these at all.

**Absolute numbering that still matches season releases** — a series on ordinary season ordering accepts absolute-numbered releases too. `Tensei Shitara Slime Datta Ken - 88` resolves to S04E16.

**Multi-season packs** — `Series.S01-S05` is accepted and mapped to every season it contains, rather than refused. On by default.

**Fake release filtering** — the `.torrent` file is inspected before it reaches your download client, rejecting `Episode.mkv.exe`, bare executables and installers, and password-protected archives containing no video. Split RAR sets pass, because those are normal and legitimate. On by default, and every rejection is logged with a specific reason.

**Season pack trimming** — skip episodes you deliberately left unmonitored instead of importing the whole pack. Off by default.

**Anime-aware search matching** — `2nd Season`, `S2`, `Part 2` and `II` all read as "season 2"; accents and articles are stripped; scoring uses Jaro-Winkler rather than upstream's single-character tolerance. Release-to-series matching also compares against every title a provider knows, so a release using a series' English title still matches when it's stored under the romaji one.

**AniList custom lists** — import lists can target one of your own custom lists instead of only Planning and Watching. ([Sonarr#6772](https://github.com/Sonarr/Sonarr/issues/6772))

Full detail, including the reasoning and the trade-offs behind each, is in [FORK.md](FORK.md).

## What's removed

Sonarr Pro doesn't contact Sonarr's servers for updates, crash reports, or announcements. Those endpoints describe upstream Sonarr builds, so an update offered there would replace this program with a different one — and the Sonarr team shouldn't receive crash reports or install metrics for a fork they don't maintain. Updates happen through Docker instead.

## A word on Usenet

NZBGet, SABnzbd and NZBVortex are all still in here, completely untouched. They'll work exactly as they do upstream.

None of the new downloading features apply to them, though. Fake release filtering reads the file list inside a `.torrent` before the grab, and Usenet has no equivalent — so it's off there, and multi-season packs and pack trimming have only ever been tested against torrents. Consider Usenet support inherited rather than maintained.

But nobody uses that shit anyways.

---

## Getting started

You need [Docker](https://docs.docker.com/get-docker/). That's the only prerequisite.

### Quick start

```bash
docker run -d --name sonarr-pro -p 8989:8989 -e PUID=1000 -e PGID=1000 -e TZ=Etc/UTC -v ./config:/config -v /path/to/media:/media --restart unless-stopped ghcr.io/kakarottocake/sonarr-pro:latest
```

Then open **http://localhost:8989**.

### Or with Compose

Grab [`docker-compose.yml`](docker-compose.yml), edit the media path, and run:

```bash
docker compose up -d
```

### Updating

```bash
docker compose pull && docker compose up -d
```

Or without Compose:

```bash
docker pull ghcr.io/kakarottocake/sonarr-pro:latest && docker restart sonarr-pro
```

Your database and settings live in the `/config` volume and survive the upgrade. There's no in-app updater to wait on and nothing to reinstall.

### Settings that matter

| Variable | Default | What it does |
|---|---|---|
| `PUID` / `PGID` | `1000` | The user files are written as. Run `id` on the host and use those numbers, or downloads arrive owned by root and you won't be able to edit them. |
| `TZ` | `Etc/UTC` | Timezone, so scheduled tasks and log timestamps match your clock. |

Mount your download client's completed folder and your library under a **single** `/media` parent. If they're separate mounts, every import becomes a full file copy instead of an instant move.

### Tags

| Tag | What you get |
|---|---|
| `latest` | The newest release. Use this one. |
| `edge` | Every commit to `main`. Untested; expect breakage. |
| `v1.2.3` | A specific release, pinned. |

Built for `linux/amd64` and `linux/arm64`.

### Coming from Sonarr

The config directory layout is unchanged, so pointing Sonarr Pro's `/config` at a copy of an existing Sonarr config works. **Copy it, don't move it** — the database is migrated on first start and isn't readable by upstream Sonarr afterwards. Keep the original until you're satisfied.

---

## First-run setup

**Add a TMDB API key** if you want TMDB metadata or episode groups — Settings → Metadata Source. It's free from [themoviedb.org](https://www.themoviedb.org/settings/api). Nothing else needs a key, and the other three sources work without it.

**Build the IMDb index** if you want IMDb orderings — System → Tasks → *Refresh Imdb Dataset*. It downloads about 52 MB once, then keeps itself current monthly. Until it runs, IMDb simply doesn't appear in the ordering dropdown.

One thing worth knowing before you add a long series: **the episode ordering is fixed when the series is added.** Season and episode numbers go into file and folder names, so changing it afterwards would rename your library on disk. To change it, remove the series and add it again.

---

## Contributing

Contributions are welcome, **including AI-assisted ones**. Upstream's tooling automatically flags and converts PRs from AI agents to drafts; that's been removed here. There's no CLA and no template to fill in.

What's asked instead:

- **Explain the reasoning, not just the change.** A PR that says what broke and why this is the right fix is worth several that only say what changed.
- **Say what you actually verified.** "Tests pass, ran it against a real library" and "compiles, untested" are both fine answers. Claiming the first while meaning the second is not.
- Run `dotnet build src/Sonarr.sln` and `yarn lint` first. CI runs both anyway.

Suggestions and bug reports are equally welcome — [open an issue](https://github.com/KakarottoCake/Sonarr-Pro/issues).

### Building locally

```bash
dotnet build src/Sonarr.sln -c Debug
```

Build the **solution**, not individual project files. `Directory.Build.props` loads `stylecop.json` through `$(SolutionDir)`, which is only defined for a solution build — building a bare `.csproj` silently drops the StyleCop config and then fails on every `using` directive in the repo.

```bash
yarn install && yarn build --env production
```

`--env production` is not optional. Without it webpack emits eval-source-map output and the UI loads as a blank page.

---

## License

[GPL-3.0](LICENSE.md), the same as upstream Sonarr.

Sonarr Pro is a derivative work of [Sonarr](https://github.com/Sonarr/Sonarr), copyright the Sonarr contributors, whose copyright notices are retained. Credit for the several hundred thousand lines this is built on belongs to them; the changes listed above are the only part that's new here.
