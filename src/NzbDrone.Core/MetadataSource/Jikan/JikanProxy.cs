using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Core.Exceptions;
using NzbDrone.Core.Languages;
using NzbDrone.Core.MediaCover;
using NzbDrone.Core.MetadataSource.Jikan.Resource;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.MetadataSource.Jikan
{
    /// <summary>
    /// MyAnimeList metadata via the Jikan mirror.
    /// <para>
    /// Complements AniList rather than duplicating it: Jikan publishes per-episode titles
    /// and air dates, which AniList does not, so a series whose episode titles matter is
    /// better owned here. It needs no API key, but it is a free shared service and is
    /// rate limited accordingly.
    /// </para>
    /// </summary>
    public class JikanProxy : IMetadataProvider
    {
        private const string BaseUrl = "https://api.jikan.moe/v4";
        private const int MaxEpisodePages = 30;
        private const int MaxSearchResults = 20;

        private static readonly Regex DurationRegex = new Regex(@"(?<minutes>\d+)\s*min", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private readonly IHttpClient _httpClient;
        private readonly IJikanRateLimiter _rateLimiter;
        private readonly Logger _logger;

        public JikanProxy(IHttpClient httpClient, IJikanRateLimiter rateLimiter, Logger logger)
        {
            _httpClient = httpClient;
            _rateLimiter = rateLimiter;
            _logger = logger;
        }

        public MetadataSourceType Source => MetadataSourceType.MyAnimeList;

        public Tuple<Series, List<Episode>> GetSeriesInfo(Series series)
        {
            var foreignId = series.ForeignId.IsNotNullOrWhiteSpace()
                ? series.ForeignId
                : series.MalIds?.FirstOrDefault().ToString();

            if (!int.TryParse(foreignId, out var malId))
            {
                throw new SeriesNotFoundException(series.TvdbId, "Series has no MyAnimeList id");
            }

            return GetSeriesInfo(malId);
        }

        public Tuple<Series, List<Episode>> GetSeriesInfo(int malId)
        {
            var anime = Get<JikanAnimeResponse>($"/anime/{malId}/full")?.Data;

            if (anime == null)
            {
                throw new SeriesNotFoundException(0, "Series not found on MyAnimeList: {0}", malId);
            }

            var series = MapSeries(anime);
            var episodes = GetEpisodes(malId, anime);

            return new Tuple<Series, List<Episode>>(series, episodes);
        }

        private List<Episode> GetEpisodes(int malId, JikanAnimeResource anime)
        {
            var episodes = new List<Episode>();
            var runtime = ParseDuration(anime.Duration);
            var page = 1;

            while (page <= MaxEpisodePages)
            {
                JikanEpisodeListResponse response;

                try
                {
                    response = Get<JikanEpisodeListResponse>($"/anime/{malId}/episodes?page={page}");
                }
                catch (HttpException ex)
                {
                    // Jikan answers the episode list from MyAnimeList rather than its own
                    // cache, and that call fails far more often than the series call does.
                    // Refusing to add the series over it would make the source unusable
                    // whenever MyAnimeList is unwell, so what was read so far is kept and
                    // the rest is filled in from the declared count below.
                    _logger.Warn(ex, "Unable to read the episode list for MyAnimeList series {0}, falling back to the episode count", malId);

                    break;
                }

                if (response?.Data == null || response.Data.Count == 0)
                {
                    break;
                }

                foreach (var resource in response.Data)
                {
                    episodes.Add(MapEpisode(resource, episodes.Count + 1, runtime));
                }

                if (response.Pagination?.HasNextPage != true)
                {
                    break;
                }

                page++;
            }

            return FillMissingEpisodes(episodes, anime, runtime);
        }

        /// <summary>
        /// Adds placeholders up to the count the series declares, for episodes the list did
        /// not cover. They carry no title, which is the one thing this source is chosen for,
        /// but numbering is what searching and importing rely on, and the titles arrive on a
        /// later refresh once MyAnimeList answers again.
        /// </summary>
        private List<Episode> FillMissingEpisodes(List<Episode> episodes, JikanAnimeResource anime, int runtime)
        {
            var declared = anime.Episodes ?? 0;

            if (declared <= episodes.Count)
            {
                return episodes;
            }

            _logger.Debug("Filling {0} episodes of {1} that the episode list did not cover", declared - episodes.Count, anime.MalId);

            for (var number = episodes.Count + 1; number <= declared; number++)
            {
                episodes.Add(new Episode
                {
                    ForeignId = string.Format("{0}:{1}", anime.MalId, number),
                    SeasonNumber = 1,
                    EpisodeNumber = number,
                    AbsoluteEpisodeNumber = number,
                    Runtime = runtime,
                    Monitored = true,
                    Images = new List<MediaCover.MediaCover>()
                });
            }

            return episodes;
        }

        private static Episode MapEpisode(JikanEpisodeResource resource, int number, int runtime)
        {
            var episode = new Episode
            {
                // Jikan numbers episodes by mal_id within the series, which is the episode
                // number itself for almost all entries. The position is used as a fallback
                // so a gap in mal_id cannot shift the whole run.
                ForeignId = resource.MalId > 0 ? resource.MalId.ToString() : number.ToString(),
                SeasonNumber = 1,
                EpisodeNumber = resource.MalId > 0 ? resource.MalId : number,
                AbsoluteEpisodeNumber = resource.MalId > 0 ? resource.MalId : number,
                Title = resource.Title,
                Runtime = runtime,
                Monitored = true,
                Images = new List<MediaCover.MediaCover>()
            };

            if (resource.Aired.HasValue)
            {
                var aired = resource.Aired.Value.ToUniversalTime();

                episode.AirDate = aired.ToString(Episode.AIR_DATE_FORMAT);
                episode.AirDateUtc = aired;
            }

            return episode;
        }

        private Series MapSeries(JikanAnimeResource anime)
        {
            var series = new Series
            {
                MetadataSource = MetadataSourceType.MyAnimeList,
                ForeignId = anime.MalId.ToString(),
                MalIds = new HashSet<int> { anime.MalId },
                Title = anime.TitleEnglish.IsNotNullOrWhiteSpace() ? anime.TitleEnglish : anime.Title,
                Overview = anime.Synopsis,
                SeriesType = SeriesTypes.Anime,
                Status = MapStatus(anime.Status),
                Genres = anime.Genres?.Select(g => g.Name).ToList() ?? new List<string>(),
                Runtime = ParseDuration(anime.Duration),
                Network = anime.Studios?.FirstOrDefault()?.Name,
                OriginalLanguage = Language.Japanese,
                OriginalCountry = "JP",
                Monitored = true,
                Images = new List<MediaCover.MediaCover>(),
                Seasons = new List<Season> { new Season { SeasonNumber = 1, Monitored = true } }
            };

            series.CleanTitle = Parser.Parser.CleanSeriesTitle(series.Title);
            series.TitleSlug = anime.MalId.ToString();
            series.SortTitle = SeriesTitleNormalizer.Normalize(series.Title, 0);

            if (anime.Aired?.From != null)
            {
                series.FirstAired = anime.Aired.From.Value.ToUniversalTime();
                series.Year = series.FirstAired.Value.Year;
            }

            if (anime.Aired?.To != null)
            {
                series.LastAired = anime.Aired.To.Value.ToUniversalTime();
            }

            if (anime.Score.HasValue)
            {
                series.Ratings = new Ratings { Value = anime.Score.Value, Votes = anime.ScoredBy ?? 0 };
            }

            if (anime.Rating.IsNotNullOrWhiteSpace())
            {
                series.Certification = anime.Rating.ToUpperInvariant();
            }

            var poster = anime.Images?.Jpg?.LargeImageUrl ?? anime.Images?.Jpg?.ImageUrl;

            if (poster.IsNotNullOrWhiteSpace())
            {
                series.Images.Add(new MediaCover.MediaCover(MediaCoverTypes.Poster, poster));
            }

            return series;
        }

        /// <summary>
        /// Excludes adult entries, which the sfw query parameter used to do before it was
        /// dropped for causing cache misses. MyAnimeList marks these "Rx - Hentai".
        /// </summary>
        private static bool IsSuitable(JikanAnimeResource anime)
        {
            return anime.Rating.IsNullOrWhiteSpace() ||
                   !anime.Rating.StartsWith("Rx", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Jikan reports runtime as prose, for example "24 min per ep".
        /// </summary>
        private static int ParseDuration(string duration)
        {
            if (duration.IsNullOrWhiteSpace())
            {
                return 0;
            }

            var match = DurationRegex.Match(duration);

            return match.Success && int.TryParse(match.Groups["minutes"].Value, out var minutes) ? minutes : 0;
        }

        private static SeriesStatusType MapStatus(string status)
        {
            return status switch
            {
                "Finished Airing" => SeriesStatusType.Ended,
                "Currently Airing" => SeriesStatusType.Continuing,
                "Not yet aired" => SeriesStatusType.Upcoming,
                _ => SeriesStatusType.Continuing
            };
        }

        private T Get<T>(string resource)
            where T : new()
        {
            _rateLimiter.WaitForSlot();

            var request = new HttpRequestBuilder(BaseUrl + resource).Build();

            request.AllowAutoRedirect = true;
            request.SuppressHttpError = true;

            var response = _httpClient.Get<T>(request);

            if (response.HasHttpError)
            {
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    throw new SeriesNotFoundException(0, "Series not found on MyAnimeList: {0}", resource);
                }

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    // The limiter should prevent this; if it happens the service is under
                    // load and retrying immediately would make it worse.
                    _logger.Warn("Jikan rate limit hit despite throttling, backing off");
                }

                throw new HttpException(request, response);
            }

            return response.Resource;
        }

        public List<Series> SearchForNewSeriesByMyAnimeListId(int malId)
        {
            try
            {
                return new List<Series> { GetSeriesInfo(malId).Item1 };
            }
            catch (SeriesNotFoundException)
            {
                return new List<Series>();
            }
        }

        public List<Series> SearchForNewSeries(string title)
        {
            if (title.IsNullOrWhiteSpace())
            {
                return new List<Series>();
            }

            try
            {
                // Only the search term. Jikan caches by the whole query string and proxies a
                // miss through to MyAnimeList, which frequently times out and answers 504,
                // so every additional parameter measurably reduces the chance of an answer:
                // "?q=naruto" is served while "?q=naruto&limit=20" is not. Trimming and
                // filtering are done below instead, where they cost nothing.
                var response = Get<JikanSearchResponse>($"/anime?q={Uri.EscapeDataString(title.Trim())}");

                var results = response?.Data ?? new List<JikanAnimeResource>();

                var mapped = results.Where(IsSuitable)
                                    .Take(MaxSearchResults)
                                    .Select(MapSeries)
                                    .ToList();

                mapped.Sort(new SearchSeriesComparer(title));

                return mapped;
            }
            catch (HttpException ex)
            {
                // Jikan is a free shared service and does go down. An empty list leaves the
                // other sources usable rather than failing the whole search.
                _logger.Warn(ex, "MyAnimeList search for '{0}' failed", title);

                return new List<Series>();
            }
        }
    }
}
