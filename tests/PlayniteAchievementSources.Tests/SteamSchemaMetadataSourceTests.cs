using PlayniteAchievementSources.Metadata;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace PlayniteAchievementSources.Tests
{
    public sealed class SteamSchemaMetadataSourceTests
    {
        [Fact]
        public async Task GeneratesDeterministicGbeDefinitionsAndCachesSource()
        {
            var root = Path.Combine(Path.GetTempPath(), "AchievementSourcesSteam", Guid.NewGuid().ToString("N"));
            try
            {
                var response = "{\"game\":{\"availableGameStats\":{\"achievements\":[" +
                    "{\"name\":\"ACH_B\",\"displayName\":\"Bee\",\"description\":\"Second\",\"hidden\":1,\"icon\":\"https://cdn/b.jpg\",\"icongray\":\"https://cdn/bg.jpg\"}," +
                    "{\"name\":\"ACH_A\",\"displayName\":\"Aye\",\"description\":\"First\",\"hidden\":0,\"icon\":\"https://cdn/a.jpg\",\"icongray\":\"https://cdn/ag.jpg\"}]}}}";
                var source = new SteamSchemaMetadataSource(new HttpClient(new StaticHandler(response)));
                var result = await source.FetchAsync(
                    632470,
                    "0123456789abcdef0123456789abcdef",
                    "english",
                    root,
                    CancellationToken.None);

                Assert.True(result.Success, result.Error);
                Assert.Equal(2, result.AchievementCount);
                Assert.True(File.Exists(result.CachedSchemaPath));
                Assert.True(File.Exists(result.GeneratedDefinitionPath));
                var generated = File.ReadAllText(result.GeneratedDefinitionPath);
                Assert.True(generated.IndexOf("ACH_A", StringComparison.Ordinal) <
                    generated.IndexOf("ACH_B", StringComparison.Ordinal));
                Assert.Contains("\"hidden\":1", generated);
                Assert.Contains("https://cdn/a.jpg", generated);
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
        public async Task RejectsMissingKeyAndEmptySchema()
        {
            var root = Path.Combine(Path.GetTempPath(), "AchievementSourcesSteam", Guid.NewGuid().ToString("N"));
            var source = new SteamSchemaMetadataSource(new HttpClient(new StaticHandler("{}")));
            var missing = await source.FetchAsync(632470, "", "english", root, CancellationToken.None);
            var empty = await source.FetchAsync(
                632470,
                "0123456789abcdef0123456789abcdef",
                "english",
                root,
                CancellationToken.None);
            Assert.False(missing.Success);
            Assert.Contains("key", missing.Error, StringComparison.OrdinalIgnoreCase);
            Assert.False(empty.Success);
        }

        [Fact]
        public async Task RejectsMalformedOrUnavailableSchemaWithoutLeakingKey()
        {
            const string key = "0123456789abcdef0123456789abcdef";
            var root = Path.Combine(Path.GetTempPath(), "AchievementSourcesSteam", Guid.NewGuid().ToString("N"));
            try
            {
                var malformed = await new SteamSchemaMetadataSource(
                    new HttpClient(new StaticHandler("{")))
                    .FetchAsync(632470, key, "english", root, CancellationToken.None);
                var unavailable = await new SteamSchemaMetadataSource(
                    new HttpClient(new StaticHandler("{}", HttpStatusCode.Forbidden)))
                    .FetchAsync(632470, key, "english", root, CancellationToken.None);

                Assert.False(malformed.Success);
                Assert.Contains("malformed", malformed.Error, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain(key, malformed.Error);
                Assert.False(unavailable.Success);
                Assert.Contains("rejected", unavailable.Error, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain(key, unavailable.Error);
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        private sealed class StaticHandler : HttpMessageHandler
        {
            private readonly string response;
            private readonly HttpStatusCode status;
            public StaticHandler(string response, HttpStatusCode status = HttpStatusCode.OK)
            {
                this.response = response;
                this.status = status;
            }
            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(new HttpResponseMessage(status)
                {
                    Content = new StringContent(response)
                });
            }
        }
    }
}
