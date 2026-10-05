using System.Linq;
using NLog;
using NzbDrone.Core.Download;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.MediaFiles.EpisodeImport.Specifications
{
    public class FullSeasonSpecification : IImportDecisionEngineSpecification
    {
        private readonly Logger _logger;

        public FullSeasonSpecification(Logger logger)
        {
            _logger = logger;
        }

        public ImportSpecDecision IsSatisfiedBy(LocalEpisode localEpisode, DownloadClientItem downloadClientItem)
        {
            if (localEpisode.FileEpisodeInfo == null)
            {
                return ImportSpecDecision.Accept();
            }

            if (localEpisode.FileEpisodeInfo.FullSeason)
            {
                // Some complete packs contain one video per season. Only accept a named season
                // when grab history covers every mapped episode and its runtime supports that mapping.
                var expectedRuntime = localEpisode.Episodes.Sum(episode => episode.Runtime > 0 ? episode.Runtime : localEpisode.Series.Runtime);
                if (localEpisode.DownloadClientEpisodeInfo is { IsMultiSeason: true } &&
                    localEpisode.FileEpisodeInfo.SeasonNumbers.Length == 1 &&
                    localEpisode.Episodes.Count > 1 &&
                    localEpisode.Episodes.Select(episode => episode.SeasonNumber).Distinct().Count() == 1 &&
                    localEpisode.Release?.EpisodeIds != null &&
                    localEpisode.Episodes.All(episode => localEpisode.Release.EpisodeIds.Contains(episode.Id)) &&
                    expectedRuntime > 0 &&
                    localEpisode.MediaInfo?.RunTime.TotalMinutes >= expectedRuntime * 0.8)
                {
                    return ImportSpecDecision.Accept();
                }

                _logger.Debug("Single episode file detected as containing all episodes in the season due to no episode parsed from the file name.");
                return ImportSpecDecision.Reject(ImportRejectionReason.FullSeason, "Single episode file contains all episodes in seasons. Review file name or manually import");
            }

            return ImportSpecDecision.Accept();
        }
    }
}
