using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(234)]
    public class add_episode_ordering : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            // The ordering a series was added with, e.g. a TMDB episode group id.
            // Null means the provider's default ordering, which is what every existing
            // series uses.
            Alter.Table("Series")
                 .AddColumn("OrderingId").AsString().Nullable();

            // The owning provider's episode id. Stable across renumbering, so episode
            // files stay attached to the right episode.
            Alter.Table("Episodes")
                 .AddColumn("ForeignId").AsString().Nullable();

            Execute.Sql("UPDATE \"Episodes\" SET \"ForeignId\" = CAST(\"TvdbId\" AS VARCHAR) WHERE \"TvdbId\" > 0");
        }
    }
}
