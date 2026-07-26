using System;
using System.Collections.Generic;
using System.IO;
using NLog;
using NzbDrone.Common.Http;
using NzbDrone.Common.Instrumentation.Extensions;

namespace NzbDrone.Core.MetadataSource.Imdb
{
    public interface IImdbDatasetService
    {
        void BuildIndex();
        bool ShouldRefreshOnSchedule();
    }

    /// <summary>
    /// Rebuilds the local IMDb index from the dumps IMDb publishes for non-commercial use.
    /// <para>
    /// Only title.episode is taken, of the eight files published. title.basics is by far the
    /// largest and holds titles the owning metadata provider supplies anyway, and
    /// title.ratings expands to more than a million rows for data those providers also
    /// supply, which doubled the index for no gain.
    /// </para>
    /// </summary>
    public class ImdbDatasetService : IImdbDatasetService
    {
        private const string EpisodesUrl = "https://datasets.imdbws.com/title.episode.tsv.gz";

        // The dumps regenerate daily, but episode numbering rarely changes and a rebuild
        // costs sixty megabytes, so it is not worth doing often.
        private static readonly TimeSpan MaxAge = TimeSpan.FromDays(30);

        private readonly IHttpClient _httpClient;
        private readonly IImdbIndex _index;
        private readonly Logger _logger;

        public ImdbDatasetService(IHttpClient httpClient, IImdbIndex index, Logger logger)
        {
            _httpClient = httpClient;
            _index = index;
            _logger = logger;
        }

        /// <summary>
        /// True only when an index already exists and has aged out. A missing index means
        /// the feature was never asked for, and building one unprompted would download fifty
        /// megabytes for someone who may never use IMDb ordering.
        /// </summary>
        public bool ShouldRefreshOnSchedule()
        {
            var builtAt = _index.LastBuilt();

            return builtAt != null && DateTime.UtcNow - builtAt.Value > MaxAge;
        }

        public void BuildIndex()
        {
            _logger.ProgressInfo("Downloading the IMDb episode dataset");

            var episodes = Download(EpisodesUrl);

            try
            {
                _logger.ProgressInfo("Building IMDb index");

                _index.Rebuild(writer =>
                {
                    ImportEpisodes(episodes, writer);

                    return DateTime.UtcNow;
                });
            }
            finally
            {
                Delete(episodes);
            }
        }

        /// <summary>
        /// The dump is ordered by episode id rather than by series, so rows for one series
        /// are scattered. They are gathered in memory keyed by series, which is the only
        /// part of the process that holds much at once: the ids alone, not the text.
        /// </summary>
        private void ImportEpisodes(string path, IImdbIndexWriter writer)
        {
            var grouped = new Dictionary<int, List<ImdbEpisodeEntry>>();

            using (var stream = File.OpenRead(path))
            {
                foreach (var (parent, episode) in ImdbTsvReader.ReadEpisodes(stream))
                {
                    if (!grouped.TryGetValue(parent, out var list))
                    {
                        list = new List<ImdbEpisodeEntry>();
                        grouped[parent] = list;
                    }

                    list.Add(episode);
                }
            }

            _logger.Debug("Read episodes for {0} series from the IMDb dataset", grouped.Count);

            foreach (var pair in grouped)
            {
                pair.Value.Sort((a, b) =>
                {
                    var season = a.SeasonNumber.CompareTo(b.SeasonNumber);

                    return season != 0 ? season : a.EpisodeNumber.CompareTo(b.EpisodeNumber);
                });

                writer.WriteSeries(pair.Key, pair.Value);
            }
        }

        private string Download(string url)
        {
            var path = Path.GetTempFileName();

            _logger.Debug("Downloading {0}", url);

            _httpClient.DownloadFile(url, path);

            return path;
        }

        private void Delete(string path)
        {
            try
            {
                if (path != null && File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception ex)
            {
                // A leftover temporary file is not worth failing the rebuild over.
                _logger.Debug(ex, "Unable to remove the temporary IMDb dataset {0}", path);
            }
        }
    }
}
