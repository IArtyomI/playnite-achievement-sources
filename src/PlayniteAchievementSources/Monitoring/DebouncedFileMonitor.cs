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
        private readonly object gate = new object();
        private readonly Dictionary<Guid, Registration> registrations = new Dictionary<Guid, Registration>();
        private bool disposed;

        public void Track(Guid gameId, IEnumerable<string> directories, Func<Guid, Task> onStableChange)
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

            lock (gate)
            {
                RemoveLocked(gameId);
                if (disposed || normalized.Count == 0)
                {
                    return;
                }

                var registration = new Registration(gameId, onStableChange);
                foreach (var directory in normalized)
                {
                    try
                    {
                        if (!Directory.Exists(directory))
                        {
                            continue;
                        }
                        var watcher = new FileSystemWatcher(directory, "achievements.json")
                        {
                            IncludeSubdirectories = false,
                            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size
                        };
                        FileSystemEventHandler changed = (_, __) => registration.Signal();
                        RenamedEventHandler renamed = (_, __) => registration.Signal();
                        watcher.Changed += changed;
                        watcher.Created += changed;
                        watcher.Deleted += changed;
                        watcher.Renamed += renamed;
                        watcher.EnableRaisingEvents = true;
                        registration.Watchers.Add(watcher);
                    }
                    catch
                    {
                    }
                }

                if (registration.Watchers.Count > 0)
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
            private bool disposed;

            public Registration(Guid gameId, Func<Guid, Task> callback)
            {
                this.gameId = gameId;
                this.callback = callback;
            }

            public IList<FileSystemWatcher> Watchers { get; } = new List<FileSystemWatcher>();

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
                }

                foreach (var watcher in Watchers)
                {
                    watcher.Dispose();
                }

                Watchers.Clear();
            }
        }
    }
}
