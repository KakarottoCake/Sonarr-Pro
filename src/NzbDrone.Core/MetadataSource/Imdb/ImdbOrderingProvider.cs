using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.MetadataSource.Imdb.Commands;
using NzbDrone.Core.MetadataSource.Ordering;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.MetadataSource.Imdb
{
    public interface IProvideImdbOrdering
    {
        EpisodeOrdering GetOrdering(string imdbId);
        List<Episode> ApplyOrdering(string imdbId, List<Episode> episodes);
    }

    /// <summary>
    /// Offers IMDb's episode numbering as an ordering alongside TMDB's episode groups.
    /// <para>
    /// IMDb sells API access commercially, so the numbering comes from the dumps it
    /// publishes for non-commercial use rather than from a live service. That makes this the
    /// one ordering that works with no API key at all, at the cost of a periodic download.
    /// </para>
    /// </summary>
    public class ImdbOrderingProvider : IProvideImdbOrdering, IExecute<RefreshImdbDatasetCommand>
    {
        public const string OrderingId = "imdb";

        private readonly IImdbIndex _index;
        private readonly IImdbDatasetService _datasetService;
        private readonly Logger _logger;

        public ImdbOrderingProvider(IImdbIndex index, IImdbDatasetService datasetService, Logger logger)
        {
            _index = index;
            _datasetService = datasetService;
            _logger = logger;
        }

        /// <summary>
        /// Null when the series has no IMDb id or the index holds nothing for it, so the
        /// caller offers no choice rather than an empty one.
        /// </summary>
        public EpisodeOrdering GetOrdering(string imdbId)
        {
            var tconst = ImdbTsvReader.ParseTconst(imdbId);

            if (tconst == 0)
            {
                return null;
            }

            var episodes = _index.GetEpisodes(tconst);

            if (episodes.Count == 0)
            {
                return null;
            }

            return new EpisodeOrdering
            {
                Id = OrderingId,
                Name = "IMDb Order",
                Description = "Season and episode numbering as published by IMDb.",
                EpisodeCount = episodes.Count,
                SeasonCount = episodes.Select(e => e.SeasonNumber).Distinct().Count(),
                IsAbsolute = false
            };
        }

        /// <summary>
        /// Renumbers episodes matched by IMDb id. Episodes IMDb does not list keep their
        /// existing numbering rather than being dropped, so nothing already matched to a
        /// file is lost.
        /// </summary>
        public List<Episode> ApplyOrdering(string imdbId, List<Episode> episodes)
        {
            var tconst = ImdbTsvReader.ParseTconst(imdbId);

            if (tconst == 0)
            {
                return episodes;
            }

            var ordering = _index.GetEpisodes(tconst);

            if (ordering.Count == 0)
            {
                _logger.Debug("No IMDb ordering is held for {0}, keeping the existing numbering", imdbId);

                return episodes;
            }

            var byImdbId = ordering.ToDictionary(e => e.Tconst);

            foreach (var episode in episodes)
            {
                if (episode.ForeignId.IsNullOrWhiteSpace())
                {
                    continue;
                }

                var episodeTconst = ImdbTsvReader.ParseTconst(episode.ForeignId);

                if (episodeTconst != 0 && byImdbId.TryGetValue(episodeTconst, out var entry))
                {
                    episode.SeasonNumber = entry.SeasonNumber;
                    episode.EpisodeNumber = entry.EpisodeNumber;
                }
            }

            return episodes;
        }

        public void Execute(RefreshImdbDatasetCommand message)
        {
            // Run from System, Tasks or the API, the user asked for it, so build regardless.
            // On the daily schedule, only refresh an index that already exists and has gone
            // stale: nobody should discover this feature by way of an unexplained fifty
            // megabyte download.
            if (message.Trigger != CommandTrigger.Manual && !_datasetService.ShouldRefreshOnSchedule())
            {
                return;
            }

            _datasetService.BuildIndex();
        }
    }
}
