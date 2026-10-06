using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.LibraryTools
{
    public interface IRetainedVersionRepository : IBasicRepository<RetainedVersion>
    {
        List<RetainedVersion> ForSeries(int id);
    }

    public class RetainedVersionRepository : BasicRepository<RetainedVersion>, IRetainedVersionRepository
    {
        public RetainedVersionRepository(IMainDatabase database, IEventAggregator events)
            : base(database, events)
        {
        }

        public List<RetainedVersion> ForSeries(int id)
        {
            return Query(v => v.SeriesId == id).ToList();
        }
    }
}
