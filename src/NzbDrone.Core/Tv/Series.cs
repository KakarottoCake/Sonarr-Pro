using System;
using System.Collections.Generic;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Languages;
using NzbDrone.Core.MetadataSource;
using NzbDrone.Core.Profiles.Qualities;

namespace NzbDrone.Core.Tv
{
    public class Series : ModelBase
    {
        public Series()
        {
            Images = new List<MediaCover.MediaCover>();
            Genres = new List<string>();
            Actors = new List<Actor>();
            Seasons = new List<Season>();
            Tags = new HashSet<int>();
            OriginalLanguage = Language.English;
            MalIds = new HashSet<int>();
            AniListIds = new HashSet<int>();
            AlternateTitles = new List<string>();
        }

        public int TvdbId { get; set; }
        public int TvRageId { get; set; }
        public int TvMazeId { get; set; }
        public string ImdbId { get; set; }
        public int TmdbId { get; set; }

        /// <summary>
        /// Which provider owns this series' metadata. Exactly one provider owns a series;
        /// see <see cref="MetadataSource.MetadataSourceType"/> for why they are not merged.
        /// </summary>
        public MetadataSourceType MetadataSource { get; set; }

        /// <summary>
        /// The identifier the owning provider uses for this series.
        /// </summary>
        public string ForeignId { get; set; }

        /// <summary>
        /// Other names the owning provider knows for this series: romanisations, regional
        /// titles, and fan abbreviations. Used to rank search results, so a search for "AoT"
        /// scores "Shingeki no Kyojin" on the name that was actually typed, and to match a
        /// release named with one of them rather than the canonical title.
        /// </summary>
        public List<string> AlternateTitles { get; set; }

        /// <summary>
        /// The episode ordering this series was added with, such as a TMDB episode group id.
        /// Null uses the provider's default ordering. Chosen when the series is added and
        /// fixed thereafter, because season and episode numbers appear in file and folder
        /// names on disk.
        /// </summary>
        public string OrderingId { get; set; }
        public HashSet<int> MalIds { get; set; }
        public HashSet<int> AniListIds { get; set; }
        public string Title { get; set; }
        public string CleanTitle { get; set; }
        public string SortTitle { get; set; }
        public SeriesStatusType Status { get; set; }
        public string Overview { get; set; }
        public string AirTime { get; set; }
        public bool Monitored { get; set; }
        public NewItemMonitorTypes MonitorNewItems { get; set; }
        public int QualityProfileId { get; set; }
        public bool SeasonFolder { get; set; }
        public DateTime? LastInfoSync { get; set; }
        public int Runtime { get; set; }
        public List<MediaCover.MediaCover> Images { get; set; }
        public SeriesTypes SeriesType { get; set; }
        public string Network { get; set; }
        public bool UseSceneNumbering { get; set; }
        public string TitleSlug { get; set; }
        public string Path { get; set; }
        public string PendingPath { get; set; }
        public int Year { get; set; }
        public Ratings Ratings { get; set; }
        public List<string> Genres { get; set; }
        public List<Actor> Actors { get; set; }
        public string Certification { get; set; }
        public string RootFolderPath { get; set; }
        public DateTime Added { get; set; }
        public DateTime? FirstAired { get; set; }
        public DateTime? LastAired { get; set; }
        public LazyLoaded<QualityProfile> QualityProfile { get; set; }
        public Language OriginalLanguage { get; set; }
        public string OriginalCountry { get; set; }
        public List<Season> Seasons { get; set; }
        public HashSet<int> Tags { get; set; }
        public AddSeriesOptions AddOptions { get; set; }

        public override string ToString()
        {
            return string.Format("[{0}][{1}]", TvdbId, Title.NullSafe());
        }

        public void ApplyChanges(Series otherSeries)
        {
            // Only overwrite when the incoming series actually carries an id. A series added
            // from a provider that keys on something else arrives without one, and zeroing it
            // would discard the id the provider resolved.
            if (otherSeries.TvdbId != 0)
            {
                TvdbId = otherSeries.TvdbId;
            }

            // MetadataSource, ForeignId and OrderingId are deliberately not carried here.
            // This method is also used to apply an edit onto an existing series, so taking
            // them from the request would let a client that omits them silently re-point a
            // series at a different provider. AddSeriesService sets them explicitly instead.

            Seasons = otherSeries.Seasons;
            Path = otherSeries.Path;
            QualityProfileId = otherSeries.QualityProfileId;

            SeasonFolder = otherSeries.SeasonFolder;
            Monitored = otherSeries.Monitored;
            MonitorNewItems = otherSeries.MonitorNewItems;

            SeriesType = otherSeries.SeriesType;
            RootFolderPath = otherSeries.RootFolderPath;
            Tags = otherSeries.Tags;
            AddOptions = otherSeries.AddOptions;
        }
    }
}
