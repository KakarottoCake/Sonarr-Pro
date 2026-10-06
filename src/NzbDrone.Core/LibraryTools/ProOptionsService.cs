using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NzbDrone.Core.Configuration;

namespace NzbDrone.Core.LibraryTools
{
    public interface IProOptionsService
    {
        ProOptions Read();
        void Save(ProOptions options);
        ProSeriesOptions ForSeries(int id);
        List<ProAccessKey> Keys();
        void SaveKeys(List<ProAccessKey> keys);
        ProAccessKey Authenticate(string token, string method, string path);
    }

    public class ProOptionsService : IProOptionsService
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
        private readonly IConfigService _config;
        private readonly object _gate = new();
        private string _lastJson;
        private ProOptions _cached = new();

        public ProOptionsService(IConfigService config)
        {
            _config = config;
        }

        public ProOptions Read()
        {
            lock (_gate)
            {
                RefreshCache();

                // Callers edit detached snapshots; failed saves cannot change live options.
                return new ProOptions
                {
                    AutomationPaused = _cached.AutomationPaused,
                    HardlinkOnly = _cached.HardlinkOnly,
                    MatchExternalIds = _cached.MatchExternalIds,
                    PreferAnimeSeasonPacks = _cached.PreferAnimeSeasonPacks,
                    Series = _cached.Series.ToDictionary(p => p.Key, p => new ProSeriesOptions
                    {
                        KeepVersions = p.Value.KeepVersions,
                        AutomaticRenaming = p.Value.AutomaticRenaming,
                        SeasonTitles = new Dictionary<int, string>(p.Value.SeasonTitles ?? new Dictionary<int, string>())
                    })
                };
            }
        }

        public void Save(ProOptions options)
        {
            lock (_gate)
            {
                _config.ProOptionsJson = JsonSerializer.Serialize(options, JsonOptions);
                _lastJson = null;
            }
        }

        public ProSeriesOptions ForSeries(int id)
        {
            lock (_gate)
            {
                RefreshCache();
                var options = _cached.Series.GetValueOrDefault(id) ?? new ProSeriesOptions();
                return new ProSeriesOptions
                {
                    KeepVersions = options.KeepVersions,
                    AutomaticRenaming = options.AutomaticRenaming,
                    SeasonTitles = new Dictionary<int, string>(options.SeasonTitles ?? new Dictionary<int, string>())
                };
            }
        }

        private void RefreshCache()
        {
            var json = _config.ProOptionsJson ?? "{}";
            if (json != _lastJson)
            {
                _cached = JsonSerializer.Deserialize<ProOptions>(json, JsonOptions) ?? new ProOptions();
                _cached.Series ??= new Dictionary<int, ProSeriesOptions>();
                _lastJson = json;
            }
        }

        public List<ProAccessKey> Keys()
        {
            try
            {
                return JsonSerializer.Deserialize<List<ProAccessKey>>(_config.ProAccessKeysJson ?? "[]", JsonOptions) ?? new List<ProAccessKey>();
            }
            catch (JsonException)
            {
                return new List<ProAccessKey>();
            }
        }

        public void SaveKeys(List<ProAccessKey> keys)
        {
            _config.ProAccessKeysJson = JsonSerializer.Serialize(keys, JsonOptions);
        }

        public ProAccessKey Authenticate(string token, string method, string path)
        {
            if (token?.StartsWith("spr_", StringComparison.Ordinal) != true || string.IsNullOrEmpty(path))
            {
                return null;
            }

            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            foreach (var key in Keys())
            {
                if (key == null || key.Permission is not ("read-library" or "add-series") || key.Hash?.Length != 64 || !key.Hash.All(Uri.IsHexDigit) ||
                    (key.Expires.HasValue && key.Expires.Value <= DateTime.UtcNow))
                {
                    continue;
                }

                if (!CryptographicOperations.FixedTimeEquals(hash, Convert.FromHexString(key.Hash)))
                {
                    continue;
                }

                // Explicit endpoint allowlists prevent read keys leaking configuration,
                // credentials, logs, task payloads or private compression state.
                var parts = path.Trim('/').Split('/');
                if (parts.Length < 3 || parts[0] != "api" || (parts[1] != "v3" && parts[1] != "v5"))
                {
                    return null;
                }

                var endpoint = parts[2];
                var read = method == "GET" && (endpoint is "series" or "episode" or "episodefile" or "calendar" or "tag" or "rootfolder" or "qualityprofile");
                read &= parts.Length == 3 || (parts.Length == 4 && int.TryParse(parts[3], out var resourceId) && resourceId > 0);
                var add = method == "POST" && parts.Length == 3 && endpoint == "series" && key.Permission == "add-series";
                if (read || add)
                {
                    return key;
                }
            }

            return null;
        }
    }
}
