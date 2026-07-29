using PlayniteAchievementSources.Monitoring;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace PlayniteAchievementSources.Tests
{
    public sealed class DebouncedFileMonitorTests
    {
        [Fact]
        public async Task DetectsStateFileCreatedAfterRegistrationOnce()
        {
            var root = Path.Combine(Path.GetTempPath(), "AchievementSourcesMonitor", Guid.NewGuid().ToString("N"));
            var expected = Path.Combine(root, "GSE Saves", "632470", "achievements.json");
            var callbacks = 0;
            try
            {
                using (var monitor = new DebouncedFileMonitor(TimeSpan.FromMilliseconds(50)))
                {
                    monitor.Track(
                        Guid.NewGuid(),
                        Array.Empty<string>(),
                        _ =>
                        {
                            Interlocked.Increment(ref callbacks);
                            return Task.CompletedTask;
                        },
                        new[] { expected });

                    Directory.CreateDirectory(Path.GetDirectoryName(expected));
                    File.WriteAllText(expected, "{\"ACH_ONE\":{\"earned\":false}}");

                    var timeout = DateTime.UtcNow.AddSeconds(4);
                    while (Volatile.Read(ref callbacks) == 0 && DateTime.UtcNow < timeout)
                    {
                        await Task.Delay(50);
                    }
                    await Task.Delay(1200);
                }

                Assert.Equal(1, callbacks);
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        [Fact]
        public async Task DisposalPreventsLaterExpectedFileCallback()
        {
            var root = Path.Combine(Path.GetTempPath(), "AchievementSourcesMonitor", Guid.NewGuid().ToString("N"));
            var expected = Path.Combine(root, "GSE Saves", "632470", "achievements.json");
            var callbacks = 0;
            var monitor = new DebouncedFileMonitor(TimeSpan.FromMilliseconds(30));
            monitor.Track(
                Guid.NewGuid(),
                Array.Empty<string>(),
                _ =>
                {
                    Interlocked.Increment(ref callbacks);
                    return Task.CompletedTask;
                },
                new[] { expected });
            monitor.Dispose();
            Directory.CreateDirectory(Path.GetDirectoryName(expected));
            File.WriteAllText(expected, "{}");
            await Task.Delay(300);
            Assert.Equal(0, callbacks);
            Directory.Delete(root, true);
        }
    }
}
