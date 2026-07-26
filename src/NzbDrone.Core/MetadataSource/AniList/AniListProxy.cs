using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.Exceptions;
using NzbDrone.Core.Languages;
using NzbDrone.Core.MediaCover;
using NzbDrone.Core.MetadataSource.AniList.Resource;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.MetadataSource.AniList
{
    /// <summary>
    /// Anime metadata from AniList.
    /// <para>
    /// AniList lists anime far more granularly than TheTVDB or TMDB do: recuts, OVAs and
    /// specials get their own entries rather than being folded into a parent series. That
    /// is the point of having it, since content those two refuse to split out cannot
    /// otherwise be added at all.
    /// </para>
    /// </summary>
    public class AniListProxy : IMetadataProvider
    {
        // AniList allows 90 requests per minute. Long series need several schedule pages,
        // so the page count is capped to keep one refresh well inside that.
        private const int MaxAiringSchedulePages = 20;

        // AniList descriptions carry a little HTML even with asHtml: false.
        private static readonly Regex HtmlTagRegex = new Regex(@"<[^>]+>", RegexOptions.Compiled);

        private readonly IHttpClient _httpClient;
        private readonly Logger _logger;

        public AniListProxy(IHttpClient httpClient, Logger logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public MetadataSourceType Source => MetadataSourceType.AniList;

        public Tuple<Series, List<Episode>> GetSeriesInfo(Series series)
        {
            var foreignId = series.ForeignId.IsNotNullOrWhiteSpace()
                ? series.ForeignId
                : series.AniListIds?.FirstOrDefault().ToString();

            if (!int.TryParse(foreignId, out var aniListId))
            {
                throw new SeriesNotFoundException(series.TvdbId, "Series has no AniList id");
            }

            return GetSeriesInfo(aniListId);
        }

        public Tuple<Series, List<Episode>> GetSeriesInfo(int aniListId)
        {
            var media = FetchMedia(aniListId);

            if (media == null)
            {
                throw new SeriesNotFoundException(0, "Series not found on AniList: {0}", aniListId);
            }

            var series = MapSeries(media);
            var episodes = AniListEpisodeMapper.MapEpisodes(media);

            return new Tuple<Series, List<Episode>>(series, episodes);
        }

        private AniListMediaResource FetchMedia(int aniListId)
        {
            var media = Query<AniListMediaResponse>(AniListQueries.MediaById, new { id = aniListId, page = 1 })?.Data?.Media;

            if (media == null)
            {
                return null;
            }

            // The first page of the airing schedule covers 50 episodes. Anything longer,
            // which is the case this provider exists for, needs the remaining pages.
            var page = 2;

            while (media.AiringSchedule?.Nodes != null &&
                   media.AiringSchedule.Nodes.Count >= 50 * (page - 1) &&
                   page <= MaxAiringSchedulePages)
            {
                var next = Query<AniListMediaResponse>(AniListQueries.MediaById, new { id = aniListId, page })?.Data?.Media;

                var nodes = next?.AiringSchedule?.Nodes;

                if (nodes == null || nodes.Count == 0)
                {
                    break;
                }

                media.AiringSchedule.Nodes.AddRange(nodes);
                page++;
            }

            return media;
        }

        private Series MapSeries(AniListMediaResource media)
        {
            var titles = AniListEpisodeMapper.GetAllTitles(media);

            var series = new Series
            {
                MetadataSource = MetadataSourceType.AniList,
                ForeignId = media.Id.ToString(),
                AniListIds = new HashSet<int> { media.Id },
                Title = titles.FirstOrDefault() ?? "Unknown",

                // AniList entries are single-run anime, which is what SeriesTypes.Anime
                // means to the rest of Sonarr: absolute numbering, anime release parsing.
                SeriesType = SeriesTypes.Anime,
                Status = MapStatus(media.Status),
                Genres = media.Genres ?? new List<string>(),
                Runtime = media.Duration ?? 0,
                Monitored = true,
                Overview = CleanDescription(media.Description),
                Network = media.Studios?.Nodes?.FirstOrDefault(s => s.IsAnimationStudio)?.Name,
                OriginalCountry = media.CountryOfOrigin,
                Images = new List<MediaCover.MediaCover>(),
                Seasons = new List<Season> { new Season { SeasonNumber = 1, Monitored = true } }
            };

            if (media.IdMal.HasValue)
            {
                series.MalIds = new HashSet<int> { media.IdMal.Value };
            }

            series.CleanTitle = Parser.Parser.CleanSeriesTitle(series.Title);
            series.TitleSlug = media.Id.ToString();
            series.SortTitle = SeriesTitleNormalizer.Normalize(series.Title, 0);

            // Almost all AniList content is Japanese; CountryOfOrigin is the best signal
            // available since AniList does not expose an explicit language.
            series.OriginalLanguage = media.CountryOfOrigin switch
            {
                "CN" => Language.Chinese,
                "KR" => Language.Korean,
                "TW" => Language.Chinese,
                _ => Language.Japanese
            };

            var startDate = ToDateTime(media.StartDate);

            if (startDate.HasValue)
            {
                series.FirstAired = startDate;
                series.Year = startDate.Value.Year;
            }

            series.LastAired = ToDateTime(media.EndDate);

            if (media.AverageScore.HasValue)
            {
                // AniList scores out of 100; Sonarr's ratings are out of 10.
                series.Ratings = new Ratings
                {
                    Value = media.AverageScore.Value / 10m,
                    Votes = media.Popularity ?? 0
                };
            }

            var poster = media.CoverImage?.ExtraLarge ?? media.CoverImage?.Large;

            if (poster.IsNotNullOrWhiteSpace())
            {
                series.Images.Add(new MediaCover.MediaCover(MediaCoverTypes.Poster, poster));
            }

            if (media.BannerImage.IsNotNullOrWhiteSpace())
            {
                series.Images.Add(new MediaCover.MediaCover(MediaCoverTypes.Fanart, media.BannerImage));
            }

            return series;
        }

        private static string CleanDescription(string description)
        {
            return description.IsNullOrWhiteSpace()
                ? description
                : HtmlTagRegex.Replace(description.Replace("<br>", "\n"), string.Empty).Trim();
        }

        private static DateTime? ToDateTime(AniListDateResource date)
        {
            if (date?.Year == null)
            {
                return null;
            }

            return new DateTime(date.Year.Value, date.Month ?? 1, date.Day ?? 1, 0, 0, 0, DateTimeKind.Utc);
        }

        private static SeriesStatusType MapStatus(string status)
        {
            return status switch
            {
                "FINISHED" => SeriesStatusType.Ended,
                "RELEASING" => SeriesStatusType.Continuing,
                "NOT_YET_RELEASED" => SeriesStatusType.Upcoming,
                "CANCELLED" => SeriesStatusType.Ended,
                "HIATUS" => SeriesStatusType.Continuing,
                _ => SeriesStatusType.Continuing
            };
        }

        private T Query<T>(string query, object variables)
            where T : new()
        {
            var request = new HttpRequestBuilder(AniListQueries.BaseUrl).Build();

            request.Method = HttpMethod.Post;
            request.Headers.ContentType = "application/json";
            request.SuppressHttpError = true;
            request.SetContent(Json.ToJson(new { query, variables }));

            var response = _httpClient.Post<T>(request);

            if (response.HasHttpError)
            {
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    throw new SeriesNotFoundException(0, "Series not found on AniList");
                }

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    _logger.Warn("AniList rate limit reached, backing off");
                }

                throw new HttpException(request, response);
            }

            return response.Resource;
        }

        public List<Series> SearchForNewSeries(string title)
        {
            try
            {
                var response = Query<AniListSearchResponse>(AniListQueries.SearchByTitle, new { search = title });

                var results = response?.Data?.Page?.Media;

                if (results == null)
                {
                    return new List<Series>();
                }

                var mapped = results.Select(MapSeries).ToList();

                // AniList orders by its own popularity weighting, which buries an exact title
                // match under better-known entries in the same franchise.
                mapped.Sort(new SearchSeriesComparer(title));

                return mapped;
            }
            catch (HttpException ex)
            {
                _logger.Warn(ex, "AniList search for '{0}' failed", title);

                return new List<Series>();
            }
        }

        public List<Series> SearchForNewSeriesByAniListId(int aniListId)
        {
            try
            {
                return new List<Series> { GetSeriesInfo(aniListId).Item1 };
            }
            catch (SeriesNotFoundException)
            {
                return new List<Series>();
            }
        }
    }
}
