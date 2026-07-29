using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PlayniteAchievementSources.Monitoring
{
    public sealed class DebouncedFileMonitor : IDisposable
    {
        private readonly TimeSpan pollInterval;
        private readonly object gate = new object();
        private readonly Dictionary<Guid, Registration> registrations = new Dictionary<Guid, Registration>();
        private bool disposed;

        public DebouncedFileMonitor(TimeSpan? pollInterval = null)
        {
            this.pollInterval = pollInterval ?? TimeSpan.FromSeconds(2);
        }

        public void Track(
            Guid gameId,
            IEnumerable<string> directories,
            Func<Guid, Task> onStableChange,
            IEnumerable<string> expectedFiles = null)
        {
            if (gameId == Guid.Empty || onStableChange == null)
            {
                return;
            }

            var normalized = (directories ?? Enumerable.Empty<string>())
                .Select(TryFullPath)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(16)
                .ToList();
            var normalizedExpectedFiles = (expectedFiles ?? Enumerable.Empty<string>())
                .Select(TryFullPath)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(32)
                .ToList();

            lock (gate)
            {
                RemoveLocked(gameId);
                if (disposed || (normalized.Count == 0 && normalizedExpectedFiles.Count == 0))
                {
                    return;
                }

                var registration = new Registration(
                    gameId,
                    onStableChange,
                    normalizedExpectedFiles,
                    pollInterval);
                foreach (var directory in normalized)
                {
                    registration.TryWatchDirectory(directory);
                }

                if (registration.Watchers.Count > 0 || registration.HasExpectedFiles)
                {
                    registrations[gameId] = registration;
                }
                else
                {
                    registration.Dispose();
                }
            }
        }

        public void Remove(Guid gameId)
        {
            lock (gate)
            {
                RemoveLocked(gameId);
            }
        }

        public void Clear()
        {
            lock (gate)
            {
                foreach (var registration in registrations.Values)
                {
                    registration.Dispose();
                }

                registrations.Clear();
            }
        }

        public void Dispose()
        {
            lock (gate)
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                foreach (var registration in registrations.Values)
                {
                    registration.Dispose();
                }

                registrations.Clear();
            }
        }

        private void RemoveLocked(Guid gameId)
        {
            if (registrations.TryGetValue(gameId, out var existing))
            {
                registrations.Remove(gameId);
                existing.Dispose();
            }
        }

        private static string TryFullPath(string path)
        {
            try
            {
                return string.IsNullOrWhiteSpace(path) ? string.Empty : Path.GetFullPath(path);
            }
            catch
            {
                return string.Empty;
            }
        }

        private sealed class Registration : IDisposable
        {
            private readonly Guid gameId;
            private readonly Func<Guid, Task> callback;
            private readonly object gate = new object();
            private CancellationTokenSource pending;
            private readonly IList<string> expectedFiles;
            private readonly Dictionary<string, string> expectedSignatures =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            private readonly HashSet<string> watchedDirectories =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            private readonly Timer pollTimer;
            private bool disposed;

            public Registration(
                Guid gameId,
                Func<Guid, Task> callback,
                IEnumerable<string> expectedFiles,
                TimeSpan pollInterval)
            {
                this.gameId = gameId;
                this.callback = callback;
                this.expectedFiles = (expectedFiles ?? Enumerable.Empty<string>()).ToList();
                foreach (var file in this.expectedFiles)
                {
                    expectedSignatures[file] = Signature(file);
                }
                if (this.expectedFiles.Count > 0)
                {
                    pollTimer = new Timer(_ => Poll(), null, pollInterval, pollInterval);
                }
            }

            public IList<FileSystemWatcher> Watchers { get; } = new List<FileSystemWatcher>();
            public bool HasExpectedFiles => expectedFiles.Count > 0;

            private void Poll()
            {
                var changed = false;
                lock (gate)
                {
                    if (disposed)
                    {
                        return;
                    }

                    foreach (var file in expectedFiles)
                    {
                        TryWatchDirectoryLocked(Path.GetDirectoryName(file));
                        var next = Signature(file);
                        if (!string.Equals(expectedSignatures[file], next, StringComparison.Ordinal))
                        {
                            expectedSignatures[file] = next;
                            changed = true;
                        }
                    }
                }

                if (changed)
                {
                    Signal();
                }
            }

            public bool TryWatchDirectory(string directory)
            {
                lock (gate)
                {
                    return TryWatchDirectoryLocked(directory);
                }
            }

            private bool TryWatchDirectoryLocked(string directory)
            {
                if (disposed ||
                    string.IsNullOrWhiteSpace(directory) ||
                    watchedDirectories.Contains(directory) ||
                    !Directory.Exists(directory))
                {
                    return false;
                }

                try
                {
                    var watcher = new FileSystemWatcher(directory, "achievements.json")
                    {
                        IncludeSubdirectories = false,
                        NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size
                    };
                    FileSystemEventHandler changed = (_, __) => Signal();
                    RenamedEventHandler renamed = (_, __) => Signal();
                    watcher.Changed += changed;
                    watcher.Created += changed;
                    watcher.Deleted += changed;
                    watcher.Renamed += renamed;
                    watcher.EnableRaisingEvents = true;
                    Watchers.Add(watcher);
                    watchedDirectories.Add(directory);
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            public void Signal()
            {
                CancellationTokenSource next;
                lock (gate)
                {
                    if (disposed)
                    {
                        return;
                    }

                    pending?.Cancel();
                    pending?.Dispose();
                    pending = next = new CancellationTokenSource();
                }

                _ = RunAsync(next.Token);
            }

            private async Task RunAsync(CancellationToken token)
            {
                try
                {
                    await Task.Delay(750, token).ConfigureAwait(false);
                    await Task.Delay(250, token).ConfigureAwait(false);
                    await callback(gameId).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
                catch
                {
                }
            }

            public void Dispose()
            {
                lock (gate)
                {
                    if (disposed)
                    {
                        return;
                    }

                    disposed = true;
                    pending?.Cancel();
                    pending?.Dispose();
                    pending = null;
                    pollTimer?.Dispose();
                }

                foreach (var watcher in Watchers)
                {
                    watcher.Dispose();
                }

                Watchers.Clear();
                watchedDirectories.Clear();
            }

            private static string Signature(string path)
            {
                try
                {
                    if (!File.Exists(path))
                    {
                        return "missing";
                    }

                    var info = new FileInfo(path);
                    return info.Length + "|" + info.LastWriteTimeUtc.Ticks;
                }
                catch
                {
                    return "unavailable";
                }
            }
        }
    }
}
