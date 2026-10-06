using System;
using System.Threading;

namespace NzbDrone.Core.Organizer
{
    public static class NamingContext
    {
        private static readonly AsyncLocal<bool> Manual = new();

        public static bool ManualRename => Manual.Value;

        public static IDisposable ForManualRename()
        {
            return new Scope();
        }

        private sealed class Scope : IDisposable
        {
            private readonly bool _previous = Manual.Value;

            public Scope()
            {
                Manual.Value = true;
            }

            public void Dispose()
            {
                Manual.Value = _previous;
            }
        }
    }
}
