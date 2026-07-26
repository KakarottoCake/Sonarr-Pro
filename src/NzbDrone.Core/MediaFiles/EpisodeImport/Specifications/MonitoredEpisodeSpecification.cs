using System.Linq;
using NLog;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Download;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.MediaFiles.EpisodeImport.Specifications
{
    /// <summary>
    /// Skips episodes inside a pack that were not asked for.
    /// <para>
    /// A season pack arrives whole, including episodes that are unmonitored because they are
    /// unwanted. Sonarr otherwise imports all of them, so grabbing a pack for the two
    /// episodes that are missing also pulls in the rest.
    /// </para>
    /// <para>
    /// Files already held at equal or better quality are handled separately by
    /// <see cref="UpgradeSpecification"/>; this covers only the "did not want it" half.
    /// </para>
    /// </summary>
    public class MonitoredEpisodeSpecification : IImportDecisionEngineSpecification
    {
        private readonly IConfigService _configService;
        private readonly Logger _logger;

        public MonitoredEpisodeSpecification(IConfigService configService, Logger logger)
        {
            _configService = configService;
            _logger = logger;
        }

        public ImportSpecDecision IsSatisfiedBy(LocalEpisode localEpisode, DownloadClientItem downloadClientItem)
        {
            if (!_configService.SkipUnmonitoredEpisodesFromPacks)
            {
                return ImportSpecDecision.Accept();
            }

            // A manual import has no download client item. The user picked the file, so their
            // choice outranks the monitored flag.
            if (downloadClientItem == null)
            {
                return ImportSpecDecision.Accept();
            }

            if (localEpisode.Series is { Monitored: false })
            {
                return ImportSpecDecision.Accept();
            }

            if (localEpisode.Episodes == null || localEpisode.Episodes.Count == 0)
            {
                return ImportSpecDecision.Accept();
            }

            // Only when every episode the file covers is unmonitored. A multi-episode file
            // that includes one wanted episode still has to be imported to get it.
            if (localEpisode.Episodes.All(e => !e.Monitored))
            {
                _logger.Debug("Skipping {0}, no episode it contains is monitored", localEpisode.Path);

                return ImportSpecDecision.Reject(ImportRejectionReason.UnmonitoredEpisode,
                                                 "Episode is not monitored");
            }

            return ImportSpecDecision.Accept();
        }
    }
}
