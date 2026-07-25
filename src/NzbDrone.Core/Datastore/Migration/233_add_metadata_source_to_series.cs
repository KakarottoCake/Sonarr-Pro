using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(233)]
    public class add_metadata_source_to_series : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            // MetadataSource identifies which provider owns this series' metadata.
            // 0 == Tvdb, which is what every existing series uses, so the default
            // preserves current behaviour for anything already in the library.
            Alter.Table("Series")
                 .AddColumn("MetadataSource").AsInt32().NotNullable().WithDefaultValue(0);

            // ForeignId is the identifier the owning provider uses for this series.
            // Kept as a string because providers do not agree on the type: TVDB and
            // TMDB use integers, but others may not.
            Alter.Table("Series")
                 .AddColumn("ForeignId").AsString().Nullable();

            Execute.Sql("UPDATE \"Series\" SET \"ForeignId\" = CAST(\"TvdbId\" AS VARCHAR) WHERE \"TvdbId\" > 0");
        }
    }
}
