using System;
using System.Linq;
using FluentAssertions;
using FluentMigrator;
using NUnit.Framework;
using NzbDrone.Core.Datastore.Migration;
using NzbDrone.Core.Datastore.Migration.Framework;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Datastore.Migration;

[TestFixture]
public class quality_profile_quality_ranksFixture : MigrationTest<quality_profile_quality_ranks>
{
    [Test]
    public void should_upgrade_the_fork_schema_with_upstream_quality_ranks()
    {
        var db = WithMigrationTestDb();

        db.Query<TableName>("SELECT name AS Name FROM sqlite_master WHERE type = 'table' AND name = 'QualityProfileQualityRanks'")
            .Should().ContainSingle();

        db.Query<ColumnName>("SELECT name AS Name FROM pragma_table_info('Series')").Select(c => c.Name)
            .Should().Contain(new[] { "MetadataSource", "ForeignId", "OrderingId", "AlternateTitles" });

        db.Query<ColumnName>("SELECT name AS Name FROM pragma_table_info('Episodes')").Select(c => c.Name)
            .Should().Contain("ForeignId");
    }

    [Test]
    public void should_not_reuse_a_fork_migration_number()
    {
        var versions = typeof(NzbDroneMigrationBase).Assembly.GetTypes()
            .Select(t => Attribute.GetCustomAttribute(t, typeof(MigrationAttribute)))
            .OfType<MigrationAttribute>()
            .Select(a => a.Version);

        versions.Should().OnlyHaveUniqueItems();
        MigrationVersion.Should().BeGreaterThan(235);
    }

    private class TableName
    {
        public string Name { get; set; }
    }

    private class ColumnName
    {
        public string Name { get; set; }
    }
}
