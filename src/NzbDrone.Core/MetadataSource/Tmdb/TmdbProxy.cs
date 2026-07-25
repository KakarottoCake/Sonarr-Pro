using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Exceptions;
using NzbDrone.Core.Languages;
using NzbDrone.Core.MediaCover;
using NzbDrone.Core.MetadataSource.Ordering;
using NzbDrone.Core.MetadataSource.Tmdb.Resource;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.MetadataSource.Tmdb
{
    public interface IProvideEpisodeOrderings
    {
        List<EpisodeOrdering> GetOrderings(string foreignId);
    }

    public class TmdbProxy : IMetadataProvider, IProvideEpisodeOrderings
    {
        private const string BaseUrl = "https://api.themoviedb.org/3";
        private const string ImageBaseUrl = "https://image.tmdb.org/t/p";

        private readonly IHttpClient _httpClient;
        private readonly IConfigService _configService;
        private readonly Logger _logger;

        public TmdbProxy(IHttpClient httpClient, IConfigService configService, Logger logger)
        {
            _httpClient = httpClient;
            _configService = configService;
            _logger = logger;
        }

        public MetadataSourceType Source => MetadataSourceType.Tmdb;

        public Tuple<Series, List<Episode>> GetSeriesInfo(Series series)
        {
            var foreignId = series.ForeignId.IsNotNullOrWhiteSpace()
                ? series.ForeignId
                : series.TmdbId.ToString();

            return GetSeriesInfo(foreignId, series.OrderingId);
        }

        public Tuple<Series, List<Episode>> GetSeriesInfo(int tmdbId)
        {
            return GetSeriesInfo(tmdbId.ToString(), null);
        }

        public Tuple<Series, List<Episode>> GetSeriesInfo(string foreignId, string orderingId)
        {
            var resource = Get<TmdbSeriesResource>($"/tv/{foreignId}", new Dictionary<string, string>
            {
                { "append_to_response", "external_ids,content_ratings" }
            });

            var series = MapSeries(resource);
            series.OrderingId = orderingId;

            var episodes = GetEpisodes(foreignId, resource);

            if (orderingId.IsNotNullOrWhiteSpace())
            {
                episodes = ApplyOrdering(orderingId, episodes);
            }

            SetAbsoluteNumbering(series, episodes);

            return new Tuple<Series, List<Episode>>(series, episodes);
        }

        public List<EpisodeOrdering> GetOrderings(string foreignId)
        {
            var orderings = new List<EpisodeOrdering>
            {
                new EpisodeOrdering
                {
                    Id = null,
                    Name = "Default (Aired order)",
                    Description = "The season and episode numbering TMDB lists by default."
                }
            };

            try
            {
                var groups = Get<TmdbEpisodeGroupListResource>($"/tv/{foreignId}/episode_groups", null);

                if (groups?.Results != null)
                {
                    orderings.AddRange(groups.Results
                        .Where(g => g.EpisodeCount > 0)
                        .Select(TmdbEpisodeGroupProjector.ToOrdering));
                }
            }
            catch (Exception ex)
            {
                // A series without published groups is normal, and an unavailable list should
                // not stop the series being added with the default ordering.
                _logger.Debug(ex, "Unable to fetch episode groups for TMDB series {0}", foreignId);
            }

            return orderings;
        }

        private List<Episode> ApplyOrdering(string orderingId, List<Episode> episodes)
        {
            try
            {
                var group = Get<TmdbEpisodeGroupDetailResource>($"/tv/episode_group/{orderingId}", null);

                return TmdbEpisodeGroupProjector.Project(group, episodes);
            }
            catch (Exception ex)
            {
                // Renumbering against a half-fetched ordering would scramble the library, so
                // keep the numbering already on disk and let the refresh report the failure.
                _logger.Error(ex, "Unable to apply episode ordering {0}, keeping existing numbering", orderingId);

                throw;
            }
        }

        private List<Episode> GetEpisodes(string foreignId, TmdbSeriesResource resource)
        {
            var episodes = new List<Episode>();

            if (resource.Seasons == null)
            {
                return episodes;
            }

            foreach (var season in resource.Seasons.OrderBy(s => s.SeasonNumber))
            {
                var detail = Get<TmdbSeasonDetailResource>($"/tv/{foreignId}/season/{season.SeasonNumber}", null);

                if (detail?.Episodes == null)
                {
                    continue;
                }

                episodes.AddRange(detail.Episodes.Select(MapEpisode));
            }

            return episodes;
        }

        /// <summary>
        /// Absolute numbers are what anime release groups put in filenames, so they are
        /// derived for every series rather than only those using an absolute ordering.
        /// </summary>
        private static void SetAbsoluteNumbering(Series series, List<Episode> episodes)
        {
            var absolute = 1;

            foreach (var episode in episodes.Where(e => e.SeasonNumber > 0)
                                            .OrderBy(e => e.SeasonNumber)
                                            .ThenBy(e => e.EpisodeNumber))
            {
                episode.AbsoluteEpisodeNumber = absolute++;
            }
        }

        private Series MapSeries(TmdbSeriesResource resource)
        {
            var series = new Series
            {
                MetadataSource = MetadataSourceType.Tmdb,
                ForeignId = resource.Id.ToString(),
                TmdbId = resource.Id,
                Title = resource.Name,
                Overview = resource.Overview,
                Network = resource.Networks?.FirstOrDefault()?.Name,
                Status = MapStatus(resource.Status),
                Genres = resource.Genres?.Select(g => g.Name).ToList() ?? new List<string>(),
                Runtime = resource.EpisodeRunTime?.FirstOrDefault() ?? 0,
                Monitored = true
            };

            series.CleanTitle = Parser.Parser.CleanSeriesTitle(series.Title);
            series.TitleSlug = resource.Id.ToString();

            series.ImdbId = resource.ExternalIds?.ImdbId;

            // TMDB knows the TVDB id for most series. Carrying it over keeps id-based
            // indexer search and scene mappings working.
            if (resource.ExternalIds?.TvdbId is > 0)
            {
                series.TvdbId = resource.ExternalIds.TvdbId.Value;
            }

            series.SortTitle = SeriesTitleNormalizer.Normalize(series.Title, series.TvdbId);

            series.OriginalLanguage = resource.OriginalLanguage.IsNotNullOrWhiteSpace()
                ? IsoLanguages.Find(resource.OriginalLanguage.ToLowerInvariant())?.Language ?? Language.English
                : Language.English;

            series.OriginalCountry = resource.OriginCountry?.FirstOrDefault();

            if (resource.FirstAirDate.IsNotNullOrWhiteSpace() && TryParseDate(resource.FirstAirDate, out var firstAired))
            {
                series.FirstAired = firstAired;
                series.Year = firstAired.Year;
            }

            if (resource.LastAirDate.IsNotNullOrWhiteSpace() && TryParseDate(resource.LastAirDate, out var lastAired))
            {
                series.LastAired = lastAired;
            }

            series.Ratings = new Ratings { Votes = resource.VoteCount, Value = resource.VoteAverage };

            var rating = resource.ContentRatings?.Results?
                .FirstOrDefault(r => r.Country.Equals("US", StringComparison.OrdinalIgnoreCase));

            if (rating?.Rating != null)
            {
                series.Certification = rating.Rating.ToUpperInvariant();
            }

            series.Images = new List<MediaCover.MediaCover>();

            if (resource.PosterPath.IsNotNullOrWhiteSpace())
            {
                series.Images.Add(new MediaCover.MediaCover(MediaCoverTypes.Poster, $"{ImageBaseUrl}/original{resource.PosterPath}"));
            }

            if (resource.BackdropPath.IsNotNullOrWhiteSpace())
            {
                series.Images.Add(new MediaCover.MediaCover(MediaCoverTypes.Fanart, $"{ImageBaseUrl}/original{resource.BackdropPath}"));
            }

            series.Seasons = resource.Seasons?
                .Select(s => new Season { SeasonNumber = s.SeasonNumber, Monitored = s.SeasonNumber > 0 })
                .ToList() ?? new List<Season>();

            return series;
        }

        private static Episode MapEpisode(TmdbEpisodeResource resource)
        {
            var episode = new Episode
            {
                ForeignId = resource.Id.ToString(),
                SeasonNumber = resource.SeasonNumber,
                EpisodeNumber = resource.EpisodeNumber,
                Title = resource.Name,
                Overview = resource.Overview,
                Runtime = resource.Runtime ?? 0,
                Ratings = new Ratings { Votes = resource.VoteCount, Value = resource.VoteAverage },
                Images = new List<MediaCover.MediaCover>()
            };

            if (resource.AirDate.IsNotNullOrWhiteSpace() && TryParseDate(resource.AirDate, out var airDate))
            {
                episode.AirDate = airDate.ToString(Episode.AIR_DATE_FORMAT);
                episode.AirDateUtc = airDate;
            }

            if (resource.StillPath.IsNotNullOrWhiteSpace())
            {
                episode.Images.Add(new MediaCover.MediaCover(MediaCoverTypes.Screenshot, $"{ImageBaseUrl}/original{resource.StillPath}"));
            }

            return episode;
        }

        private static bool TryParseDate(string value, out DateTime result)
        {
            return DateTime.TryParseExact(value,
                                          "yyyy-MM-dd",
                                          DateTimeFormatInfo.InvariantInfo,
                                          DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                                          out result);
        }

        private static SeriesStatusType MapStatus(string status)
        {
            return status switch
            {
                "Ended" => SeriesStatusType.Ended,
                "Canceled" => SeriesStatusType.Ended,
                "Returning Series" => SeriesStatusType.Continuing,
                "In Production" => SeriesStatusType.Upcoming,
                "Planned" => SeriesStatusType.Upcoming,
                "Pilot" => SeriesStatusType.Upcoming,
                _ => SeriesStatusType.Continuing
            };
        }

        private T Get<T>(string resource, Dictionary<string, string> query)
            where T : new()
        {
            var apiKey = _configService.TmdbApiKey;

            if (apiKey.IsNullOrWhiteSpace())
            {
                throw new TmdbApiKeyMissingException();
            }

            var builder = new HttpRequestBuilder(BaseUrl + resource)
                .AddQueryParam("api_key", apiKey);

            if (query != null)
            {
                foreach (var pair in query)
                {
                    builder.AddQueryParam(pair.Key, pair.Value);
                }
            }

            var request = builder.Build();
            request.AllowAutoRedirect = true;
            request.SuppressHttpError = true;

            var response = _httpClient.Get<T>(request);

            if (response.HasHttpError)
            {
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    throw new SeriesNotFoundException(0, "Series not found on TMDB: {0}", resource);
                }

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    throw new TmdbApiKeyMissingException("The configured TMDB API key was rejected");
                }

                throw new HttpException(request, response);
            }

            return response.Resource;
        }

        // Lookup is not implemented yet; series are still discovered through the default
        // provider, which is what the add-series UI calls today.
        public List<Series> SearchForNewSeries(string title) => new List<Series>();
        public List<Series> SearchForNewSeriesByImdbId(string imdbId) => new List<Series>();
        public List<Series> SearchForNewSeriesByAniListId(int aniListId) => new List<Series>();
        public List<Series> SearchForNewSeriesByTmdbId(int tmdbId) => new List<Series>();
        public List<Series> SearchForNewSeriesByMyAnimeListId(int malId) => new List<Series>();
    }
}
