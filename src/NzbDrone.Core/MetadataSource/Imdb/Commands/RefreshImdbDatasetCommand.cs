using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.MetadataSource.Imdb.Commands
{
    public class RefreshImdbDatasetCommand : Command
    {
        public override bool SendUpdatesToClient => true;

        public override string CompletionMessage => "IMDb index updated";
    }
}
