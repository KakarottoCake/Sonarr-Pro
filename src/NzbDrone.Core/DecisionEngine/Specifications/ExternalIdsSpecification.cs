using NzbDrone.Core.LibraryTools;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.DecisionEngine.Specifications
{
    public class ExternalIdsSpecification : IDownloadDecisionEngineSpecification
    {
        private readonly IProOptionsService _options;

        public ExternalIdsSpecification(IProOptionsService options)
        {
            _options = options;
        }

        public SpecificationPriority Priority => SpecificationPriority.Default;
        public RejectionType Type => RejectionType.Permanent;

        public DownloadSpecDecision IsSatisfiedBy(RemoteEpisode subject, ReleaseDecisionInformation information)
        {
            if (_options.Read()?.MatchExternalIds != true || subject.Series == null || subject.Release == null)
            {
                return DownloadSpecDecision.Accept();
            }

            if (subject.Release.TvdbId > 0 && subject.Series.TvdbId > 0 && subject.Release.TvdbId != subject.Series.TvdbId)
            {
                return DownloadSpecDecision.Reject(DownloadRejectionReason.WrongSeries, "Indexer TVDB ID {0} differs from this show's ID {1}.", subject.Release.TvdbId, subject.Series.TvdbId);
            }

            var releaseId = NormalizeImdb(subject.Release.ImdbId);
            var seriesId = NormalizeImdb(subject.Series.ImdbId);
            if (releaseId != null && seriesId != null && releaseId != seriesId)
            {
                return DownloadSpecDecision.Reject(DownloadRejectionReason.WrongSeries, "Indexer IMDb ID {0} differs from this show's ID {1}.", subject.Release.ImdbId, subject.Series.ImdbId);
            }

            return DownloadSpecDecision.Accept();
        }

        private static string NormalizeImdb(string value)
        {
            var result = value?.Trim().ToLowerInvariant().Replace("tt", string.Empty).TrimStart('0');
            return string.IsNullOrEmpty(result) || !long.TryParse(result, out _) ? null : result;
        }
    }
}
