using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(235)]
    public class add_alternate_titles_to_series : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            // Other names the owning provider knows for a series: romanisations, regional
            // titles and abbreviations. Release groups use these as readily as the canonical
            // title, so matching a release needs them too, not just the ranking of search
            // results. Populated on the next refresh of each series.
            Alter.Table("Series")
                 .AddColumn("AlternateTitles").AsString().Nullable();
        }
    }
}
