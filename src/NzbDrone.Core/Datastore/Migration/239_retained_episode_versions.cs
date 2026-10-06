using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(239)]
    public class retained_episode_versions : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Create.Table("RetainedVersions")
                .WithColumn("Id").AsInt32().PrimaryKey().Identity()
                .WithColumn("SeriesId").AsInt32().NotNullable()
                .WithColumn("RelativePath").AsString(int.MaxValue).NotNullable()
                .WithColumn("MetadataJson").AsString(int.MaxValue).NotNullable()
                .WithColumn("EpisodeIdsJson").AsString(int.MaxValue).NotNullable()
                .WithColumn("Created").AsDateTime().NotNullable();
            Create.Index().OnTable("RetainedVersions").OnColumn("SeriesId");
        }
    }
}
