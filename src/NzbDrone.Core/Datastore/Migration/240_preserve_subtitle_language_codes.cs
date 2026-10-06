using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(240)]
    public class preserve_subtitle_language_codes : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Alter.Table("SubtitleFiles").AddColumn("LanguageCode").AsString(64).Nullable();
        }
    }
}
