using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.IndexerSearch.Definitions;

namespace NzbDrone.Core.LibraryTools
{
    public class SearchActivity
    {
        public DateTime Date { get; set; }
        public int SeriesId { get; set; }
        public string Query { get; set; }
        public int Found { get; set; }
        public int Accepted { get; set; }
        public int Indexers { get; set; }
        public Dictionary<string, int> Rejections { get; set; }
        public List<string> ProviderFailures { get; set; }
    }

    public interface ISearchActivityService
    {
        List<SearchActivity> Read();
        void Record(SearchCriteriaBase criteria, List<DownloadDecision> decisions, int indexers, List<string> failures);
    }

    public class SearchActivityService : ISearchActivityService
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
        private readonly IConfigService _config;
        private readonly object _gate = new();

        public SearchActivityService(IConfigService config)
        {
            _config = config;
        }

        public List<SearchActivity> Read()
        {
            lock (_gate)
            {
                return JsonSerializer.Deserialize<List<SearchActivity>>(_config.ProSearchHistoryJson ?? "[]", JsonOptions) ?? new List<SearchActivity>();
            }
        }

        public void Record(SearchCriteriaBase criteria, List<DownloadDecision> decisions, int indexers, List<string> failures)
        {
            lock (_gate)
            {
                var history = Read();
                history.Insert(0, new SearchActivity
                {
                    Date = DateTime.UtcNow, SeriesId = criteria.Series.Id, Query = criteria.ToString(),
                    Found = decisions.Count, Accepted = decisions.Count(d => !d.Rejections.Any()), Indexers = indexers,
                    Rejections = decisions.SelectMany(d => d.Rejections).GroupBy(r => r.Message).OrderByDescending(g => g.Count()).Take(12).ToDictionary(g => g.Key, g => g.Count()),
                    ProviderFailures = failures.Distinct().ToList()
                });
                _config.ProSearchHistoryJson = JsonSerializer.Serialize(history.Take(100), JsonOptions);
            }
        }
    }
}
