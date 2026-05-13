namespace EasySaveWpf
{
    // Shared synchronization context across all parallel backup jobs in a single run.
    // Enforces two constraints:
    //   1. No non-priority file may be copied while any priority file is still pending (across all jobs).
    //   2. At most one large file (> threshold) may be transferred at a time (across all jobs).
    public class BackupSyncContext : IDisposable
    {
        private int _priorityFilesRemaining = 0;
        private readonly ManualResetEventSlim _allPriorityDone = new(initialState: true);
        private readonly SemaphoreSlim _largeFileSemaphore = new(1, 1);

        public long LargeFileSizeThresholdBytes { get; }

        public BackupSyncContext(int largeFileSizeThresholdKb)
        {
            LargeFileSizeThresholdBytes = (long)largeFileSizeThresholdKb * 1024;
        }

        public void RegisterPriorityFiles(int count)
        {
            if (count <= 0) return;
            _allPriorityDone.Reset();
            Interlocked.Add(ref _priorityFilesRemaining, count);
        }

        public void NotifyPriorityFileDone()
        {
            if (Interlocked.Decrement(ref _priorityFilesRemaining) == 0)
                _allPriorityDone.Set();
        }

        public void WaitForAllPriorityFiles(CancellationToken ct = default)
        {
            if (ct == default)
                _allPriorityDone.Wait();
            else
                _allPriorityDone.Wait(ct);
        }

        public void AcquireLargeFileSlot() => _largeFileSemaphore.Wait();
        public void ReleaseLargeFileSlot() => _largeFileSemaphore.Release();

        public void Dispose()
        {
            _allPriorityDone.Dispose();
            _largeFileSemaphore.Dispose();
        }
    }
}
