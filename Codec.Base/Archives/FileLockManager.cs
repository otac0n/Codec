namespace Codec.Archives
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;

    public sealed class FileLockManager<TEntry>
        where TEntry : notnull
    {
        private readonly Dictionary<TEntry, List<OpenHandle>> openHandles = [];

        public IDisposable Acquire(TEntry entry, FileAccess access, FileShare share)
        {
            List<OpenHandle>? handles;
            lock (this.openHandles)
            {
                if (!this.openHandles.TryGetValue(entry, out handles))
                {
                    this.openHandles[entry] = handles = [];
                }
            }

            lock (handles)
            {
                if (!handles.All(h => Compatible(h, access, share)))
                {
                    throw new IOException("The file cannot be opened because someone has it open and doesn't want to share.");
                }

                var handle = new OpenHandle(handles, access, share);
                handles.Add(handle);
                return handle;
            }
        }

        private static bool Compatible(
            OpenHandle existing,
            FileAccess requestedAccess,
            FileShare requestedShare)
        {
            return Allows(existing.Share, requestedAccess) && Allows(requestedShare, existing.Access);
        }

        private static bool Allows(FileShare share, FileAccess access) =>
            !(((access & FileAccess.Read) != 0 && (share & FileShare.Read) == 0) || ((access & FileAccess.Write) != 0 && (share & FileShare.Write) == 0));

        private sealed class OpenHandle(List<OpenHandle> group, FileAccess access, FileShare share) : IDisposable
        {
            public FileAccess Access { get; } = access;

            public FileShare Share { get; } = share;

#if DEBUG
            public StackTrace Origin { get; } = new StackTrace(5);
#endif

            public void Dispose()
            {
                lock (group)
                {
                    group.Remove(this);
                }
            }
        }
    }
}
