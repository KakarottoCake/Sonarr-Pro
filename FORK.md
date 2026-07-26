# What this fork changes

A personal fork of Sonarr. Everything below is additive: an untouched install
behaves exactly as upstream does, because every new behaviour is either off by
default or defaults to the path that already existed.

Forked from upstream `v5-develop` at `7e627f6`.

---

## Turn-on checklist

Three things need doing before the new features do anything.

1. **TMDB API key** — Settings → Metadata Source. Free from
   <https://www.themoviedb.org/settings/api>. Without it, TMDB metadata and
   episode groups are silently unavailable; everything else works.
2. **IMDb index** — run the `RefreshImdbDataset` command once. Downloads ~52MB
   and builds a ~45MB local index. Until then IMDb ordering does not appear.
3. **Skip Unmonitored Episodes In Packs** — Settings → Media Management, off by
   default. See below for why.

Nothing else needs configuring.

---

## Metadata sources

Upstream resolves every series through SkyHook, which is a proxy in front of
TheTVDB. This fork adds three more sources and lets a series be owned by any of
them, chosen from the dropdown above the search box on the Add New Series page.

| Source | Good for | Needs |
|---|---|---|
| TheTVDB | Default. Scene mappings, which the indexer ecosystem is built on | nothing |
| TMDB | Artwork, episode groups, IMDb ids | API key |
| AniList | Anime. Lists recuts, OVAs and specials separately, and knows every alternative title | nothing |
| Jikan (MyAnimeList) | Anime with per-episode titles, which AniList does not publish | nothing |

**A series is owned by exactly one source.** They are selected, not merged. This
is deliberate and worth keeping: if two providers disagreed about how many
episodes a series has, episode rows would be created and deleted on every
refresh, which detaches episode files and makes Sonarr re-download content it
already has. Silent, gradual, and hard to reverse.

### Series that TheTVDB does not list

TheTVDB's editors sometimes decline to list content separately, folding a recut
or spin-off into its parent series, while TMDB and AniList list it in its own
right. Upstream cannot add such a series at all.

Here it is added with a placeholder TVDB id, allocated as a negative number.
Every TVDB-specific behaviour tests for a positive id, so those paths skip
themselves: the series is searched by title rather than by id, gets no XEM scene
mapping, and shows no TheTVDB link. All three are fallbacks Sonarr already uses.

---

## Episode ordering

TMDB publishes alternate numbering schemes per series, and IMDb's dumps carry
another. When a series has more than one, a dropdown appears on the add dialog.

The one that motivates this: **TMDB group type 2 is "Absolute"** — one
continuous run with no seasons, which is how long-running anime is released and
discussed. Selecting it for One Piece gives season 1 with episodes 1 to 1181,
rather than twenty-four seasons. Every other ordering TMDB publishes appears in
the same list, so DVD order, story arcs and streaming-service orders come along
at no extra cost.

**The ordering is chosen when the series is added and cannot be changed
afterwards.** Season and episode numbers are written into file and folder names,
so changing it later would rename the library on disk. Fixing it at add time
means there are no episode files linked yet and nothing to rename. To change it,
remove the series and add it again.

Episodes an ordering does not mention keep their original numbering rather than
being dropped, so nothing already matched to a file is lost. For One Piece that
is 39 specials, kept in season 0.

---

## Downloading

### Fake release filtering — on by default

Sonarr fetches the `.torrent` file before handing it to the download client, and
that file lists everything inside. This inspects it and rejects four shapes
before anything reaches the client:

- a media extension followed by an executable one, such as `Episode.mkv.exe`
- executables, installers or scripts
- archives with no video alongside a file advertising a password
- neither video nor archives, so nothing to import

Split RAR sets with no loose video are common and legitimate, so they pass; the
password rule only fires when video is absent too. If the file list cannot be
read the grab proceeds — this is a safety net, not a gate.

Rejections are recorded with a specific reason. Silent rejections are what make
this kind of filter get switched off.

*Usenet is unaffected: it has no equivalent pre-grab file list. This fork is
torrent-focused.*

### Multi-season packs — on by default

Upstream rejects `Series.S01-S05` outright. The parser already recognised every
season and discarded all but the first so the release could be refused; this
keeps them, and maps the pack to the episodes of all its seasons.

A pack still has to match at least one wanted episode, so a Seasons 1-9 pack is
not accepted for a Season 3 search on the strength of its title.

Switch off under Settings → Indexers → Options if it misbehaves.

### Season pack trimming — off by default

A pack arrives whole, including episodes left unmonitored because they are
unwanted. With this on, those are skipped instead of imported.

Off by default because a file silently not appearing is harder to diagnose than
one that arrived and is unwanted. Manual imports are exempt: picking a file by
hand is a deliberate choice that outranks the monitored flag. A file covering
several episodes still imports if any one of them is wanted.

Files already held at equal or better quality were already protected upstream.

---

## Search

Series lookup ranking now folds the many ways a season is written — `2nd
Season`, `S2`, `Part 2`, `II` all read as "season 2" — strips accents and
articles, and scores with Jaro-Winkler or word overlap. Upstream allowed a
single character of difference, which is useless for anime.

This ranks lookup results only, where a person picks from a list. **Deciding
which series a downloaded release belongs to is untouched**, because that runs
unattended and a loose match there files episodes under the wrong series.

---

## Import lists

AniList import lists can be restricted to one of your own custom lists, rather
than the standard Planning and Watching lists which cover everything you have
ever added. Leave the field blank for the previous behaviour.
(Sonarr/Sonarr#6772)

---

## Notes for whoever works on this next

- **The HTTP client deserializes with Newtonsoft, not System.Text.Json.**
  Provider resource classes must use `[JsonProperty("snake_case")]`.
  `[JsonPropertyName]` is ignored silently and every field defaults to zero or
  null with no error. This made episode groups entirely non-functional once, and
  no unit test caught it because they built resource objects in C# and never
  deserialized. `TmdbResourceDeserializationFixture` guards it now.
- **Services are auto-registered against every interface they implement.**
  `IProvideSeriesInfo` and `ISearchForNewSeries` are injected as single
  instances, so only `SkyHookProxy` may implement them.
  `MetadataProviderRegistrationFixture` pins this.
- **Anything assigning `Series.TvdbId` must guard `> 0`**, and anything sending
  it to an indexer likewise. Two bugs came from `!= 0`: one zeroed the id for
  TMDB-added series, the other wiped the placeholder id on the first refresh.
- **Build the whole solution.** Building `Sonarr.Core.Test.csproj` alone emits
  thousands of spurious StyleCop errors. Run tests with `--no-build` afterwards,
  and never build while a test run is in flight.
- Unit tests did not catch the serializer bug, the registration bugs, or the
  refresh bug. Starting the application and calling the API found all of them.
