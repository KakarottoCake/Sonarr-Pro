using NzbDrone.Common.Cache;

namespace NzbDrone.Core.Messaging.Commands
{
    public class CleanupCommandMessagingService : IExecute<MessagingCleanupCommand>
    {
        private readonly IManageCommandQueue _commandQueueManager;
        private readonly ICacheManager _cacheManager;

        public CleanupCommandMessagingService(IManageCommandQueue commandQueueManager, ICacheManager cacheManager)
        {
            _commandQueueManager = commandQueueManager;
            _cacheManager = cacheManager;
        }

        public void Execute(MessagingCleanupCommand message)
        {
            _commandQueueManager.CleanCommands();
            _cacheManager.ClearExpired();
        }
    }
}
