using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.DecisionEngine.Specifications
{
    public class MultiSeasonSpecification : IDownloadDecisionEngineSpecification
    {
        private readonly IConfigService _configService;
        private readonly Logger _logger;

        public MultiSeasonSpecification(IConfigService configService, Logger logger)
        {
            _configService = configService;
            _logger = logger;
        }

        public SpecificationPriority Priority => SpecificationPriority.Default;
        public RejectionType Type => RejectionType.Permanent;

        public virtual DownloadSpecDecision IsSatisfiedBy(RemoteEpisode subject, ReleaseDecisionInformation information)
        {
            if (!subject.ParsedEpisodeInfo.IsMultiSeason)
            {
                return DownloadSpecDecision.Accept();
            }

            if (!_configService.EnableMultiSeasonReleases)
            {
                _logger.Debug("Multi-season release {0} rejected. Not enabled", subject.Release.Title);
                return DownloadSpecDecision.Reject(DownloadRejectionReason.MultiSeason, "Multi-season releases are not enabled");
            }

            // The pack has to actually contain episodes that were searched for. Without this
            // a pack spanning seasons 1-9 would be accepted for a season 3 search on the
            // strength of the title alone.
            if (subject.Episodes == null || subject.Episodes.Empty())
            {
                _logger.Debug("Multi-season release {0} rejected. No matching episodes", subject.Release.Title);
                return DownloadSpecDecision.Reject(DownloadRejectionReason.MultiSeason, "Multi-season release does not match any wanted episodes");
            }

            _logger.Debug("Multi-season release {0} accepted, spanning seasons {1}",
                          subject.Release.Title,
                          string.Join(", ", subject.ParsedEpisodeInfo.SeasonNumbers));

            return DownloadSpecDecision.Accept();
        }
    }
}
