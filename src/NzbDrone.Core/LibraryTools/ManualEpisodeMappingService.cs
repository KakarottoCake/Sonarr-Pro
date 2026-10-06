using System.Collections.Generic;
using System.Linq;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.History;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.LibraryTools
{
    public class ManualEpisodeMapping
    {
        public EpisodeHistory History { get; set; }
        public List<Episode> Episodes { get; set; } = new();
    }

    public interface IManualEpisodeMappingService
    {
        ManualEpisodeMapping Find(LocalEpisode episode, string downloadId, bool multipleFiles);
    }

    public class ManualEpisodeMappingService : IManualEpisodeMappingService
    {
        private readonly IHistoryService _history;
        private readonly IEpisodeService _episodes;

        public ManualEpisodeMappingService(IHistoryService history, IEpisodeService episodes)
        {
            _history = history;
            _episodes = episodes;
        }

        public ManualEpisodeMapping Find(LocalEpisode episode, string downloadId, bool multipleFiles)
        {
            if (string.IsNullOrWhiteSpace(downloadId))
            {
                return null;
            }

            var history = _history.FindByDownloadId(downloadId).Where(h => h.EventType == EpisodeHistoryEventType.Grabbed && h.SeriesId == episode.Series.Id && h.Data.GetValueOrDefault("ManualEpisodeMapping") == "true")
                .OrderByDescending(h => h.Date).FirstOrDefault();
            if (history == null)
            {
                return null;
            }

            var result = new ManualEpisodeMapping { History = history };
            var ids = Json.Deserialize<List<int>>(history.Data["ManualEpisodeIds"]);
            var selected = _episodes.GetEpisodes(ids).Where(e => e.SeriesId == episode.Series.Id).ToList();
            if (selected.Count != ids.Distinct().Count())
            {
                return result;
            }

            if (!multipleFiles)
            {
                result.Episodes = selected;
                return result;
            }

            var fileInfo = episode.FileEpisodeInfo;
            var releaseInfo = Parser.Parser.ParseTitle(history.SourceTitle);
            if (fileInfo == null || fileInfo.FullSeason || fileInfo.IsMultiSeason)
            {
                return result;
            }

            if (fileInfo.AbsoluteEpisodeNumbers.Any())
            {
                result.Episodes = selected.Where(e => e.AbsoluteEpisodeNumber.HasValue && fileInfo.AbsoluteEpisodeNumbers.Contains(e.AbsoluteEpisodeNumber.Value)).ToList();
                if (result.Episodes.Count != fileInfo.AbsoluteEpisodeNumbers.Distinct().Count())
                {
                    result.Episodes.Clear();
                }
            }
            else if (fileInfo.EpisodeNumbers.Any())
            {
                // A single source season may be remapped to a single chosen season.
                // Multi-season bundles keep their explicit season boundaries.
                var targetSeasons = selected.Select(e => e.SeasonNumber).Distinct().ToList();
                var remapSeason = releaseInfo?.SeasonNumbers.Length == 1 && targetSeasons.Count == 1 && fileInfo.SeasonNumber == releaseInfo.SeasonNumber;
                result.Episodes = selected.Where(e => (remapSeason || e.SeasonNumber == fileInfo.SeasonNumber) && fileInfo.EpisodeNumbers.Contains(e.EpisodeNumber)).ToList();
                if (result.Episodes.Count != fileInfo.EpisodeNumbers.Distinct().Count())
                {
                    result.Episodes.Clear();
                }
            }

            return result;
        }
    }
}
