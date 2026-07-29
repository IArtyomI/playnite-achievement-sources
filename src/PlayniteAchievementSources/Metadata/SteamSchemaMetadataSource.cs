using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Script.Serialization;

namespace PlayniteAchievementSources.Metadata
{
    public sealed class SteamSchemaMetadataResult
    {
        public bool Success { get; set; }
        public string GeneratedDefinitionPath { get; set; } = string.Empty;
        public string CachedSchemaPath { get; set; } = string.Empty;
        public int AchievementCount { get; set; }
        public string Error { get; set; } = string.Empty;
    }

    public sealed class SteamSchemaMetadataSource
    {
        private const int MaximumResponseBytes = 4 * 1024 * 1024;
        private readonly HttpClient client;
        private readonly JavaScriptSerializer serializer = new JavaScriptSerializer
        {
            MaxJsonLength = MaximumResponseBytes,
            RecursionLimit = 64
        };

        public SteamSchemaMetadataSource(HttpClient client = null)
        {
            this.client = client ?? new HttpClient();
            this.client.Timeout = TimeSpan.FromSeconds(20);
        }

        public async Task<SteamSchemaMetadataResult> FetchAsync(
            uint appId,
            string apiKey,
            string language,
            string pluginDataRoot,
            CancellationToken cancellationToken)
        {
            var result = new SteamSchemaMetadataResult();
            if (appId == 0)
            {
                result.Error = "A non-zero Steam AppID is required.";
                return result;
            }

            var normalizedKey = apiKey?.Trim() ?? string.Empty;
            if (normalizedKey.Length != 32 || normalizedKey.Any(value => !Uri.IsHexDigit(value)))
            {
                result.Error = "No valid Steam Web API key is configured.";
                return result;
            }

            var cacheRoot = SafeCacheRoot(pluginDataRoot, appId);
            if (string.IsNullOrWhiteSpace(cacheRoot))
            {
                result.Error = "The metadata cache path is invalid.";
                return result;
            }

            var requestUri =
                "https://api.steampowered.com/ISteamUserStats/GetSchemaForGame/v2/?" +
                "key=" + HttpUtility.UrlEncode(normalizedKey) +
                "&appid=" + appId +
                "&l=" + HttpUtility.UrlEncode(string.IsNullOrWhiteSpace(language) ? "english" : language.Trim());
            byte[] responseBytes;
            try
            {
                using (var response = await client.GetAsync(requestUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                    .ConfigureAwait(false))
                {
                    if (response.StatusCode == HttpStatusCode.Forbidden ||
                        response.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        result.Error = "Steam rejected the configured Web API key for this schema request.";
                        return result;
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        result.Error = "Steam schema retrieval failed with HTTP status " + (int)response.StatusCode + ".";
                        return result;
                    }

                    var declaredLength = response.Content.Headers.ContentLength;
                    if (declaredLength.HasValue && declaredLength.Value > MaximumResponseBytes)
                    {
                        result.Error = "The Steam schema response exceeded the size limit.";
                        return result;
                    }

                    responseBytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                result.Error = "Steam schema retrieval was canceled or timed out.";
                return result;
            }
            catch
            {
                result.Error = "Steam schema retrieval failed before a valid response was received.";
                return result;
            }

            if (responseBytes.Length == 0 || responseBytes.Length > MaximumResponseBytes)
            {
                result.Error = "The Steam schema response was empty or exceeded the size limit.";
                return result;
            }

            IList<IDictionary<string, object>> definitions;
            try
            {
                var root = serializer.DeserializeObject(Encoding.UTF8.GetString(responseBytes)) as IDictionary<string, object>;
                definitions = NormalizeAchievements(root);
            }
            catch
            {
                result.Error = "The Steam schema response was malformed.";
                return result;
            }

            if (definitions == null || definitions.Count == 0)
            {
                result.Error = "Steam returned no usable achievement schema for this AppID.";
                return result;
            }

            var schemaPath = Path.Combine(cacheRoot, "schema.json");
            var definitionsPath = Path.Combine(cacheRoot, "achievements.json");
            try
            {
                Directory.CreateDirectory(cacheRoot);
                AtomicWrite(schemaPath, responseBytes);
                AtomicWrite(
                    definitionsPath,
                    Encoding.UTF8.GetBytes(serializer.Serialize(definitions)));
            }
            catch
            {
                result.Error = "The validated Steam schema could not be stored in the extension metadata cache.";
                return result;
            }

            result.Success = true;
            result.GeneratedDefinitionPath = definitionsPath;
            result.CachedSchemaPath = schemaPath;
            result.AchievementCount = definitions.Count;
            return result;
        }

        private static IList<IDictionary<string, object>> NormalizeAchievements(IDictionary<string, object> root)
        {
            var game = GetDictionary(root, "game");
            var available = GetDictionary(game, "availableGameStats");
            var source = GetArray(available, "achievements");
            var result = new List<IDictionary<string, object>>();
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in source.Take(10000))
            {
                var achievement = item as IDictionary<string, object>;
                var name = GetString(achievement, "name");
                if (string.IsNullOrWhiteSpace(name) || !ids.Add(name))
                {
                    throw new InvalidDataException("The Steam schema contains a missing or duplicate achievement API name.");
                }

                result.Add(new SortedDictionary<string, object>(StringComparer.Ordinal)
                {
                    ["description"] = GetString(achievement, "description"),
                    ["displayName"] = FirstNonEmpty(GetString(achievement, "displayName"), name),
                    ["hidden"] = GetBoolean(achievement, "hidden") ? 1 : 0,
                    ["icon"] = GetString(achievement, "icon"),
                    ["icongray"] = GetString(achievement, "icongray"),
                    ["name"] = name
                });
            }

            return result.OrderBy(item => GetString(item, "name"), StringComparer.Ordinal).ToList();
        }

        private static string SafeCacheRoot(string pluginDataRoot, uint appId)
        {
            try
            {
                var root = Path.GetFullPath(pluginDataRoot ?? string.Empty);
                if (string.IsNullOrWhiteSpace(root))
                {
                    return string.Empty;
                }

                var candidate = Path.GetFullPath(Path.Combine(root, "metadata-cache", "steam", appId.ToString()));
                return candidate.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase)
                    ? candidate
                    : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static void AtomicWrite(string path, byte[] bytes)
        {
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllBytes(temporary, bytes);
                if (File.Exists(path))
                {
                    var backup = path + "." + Guid.NewGuid().ToString("N") + ".replace";
                    try
                    {
                        File.Replace(temporary, path, backup);
                    }
                    finally
                    {
                        if (File.Exists(backup))
                        {
                            File.Delete(backup);
                        }
                    }
                }
                else
                {
                    File.Move(temporary, path);
                }
            }
            finally
            {
                if (File.Exists(temporary))
                {
                    File.Delete(temporary);
                }
            }
        }

        private static IDictionary<string, object> GetDictionary(IDictionary<string, object> source, string key)
        {
            if (source == null || !source.TryGetValue(key, out var value))
            {
                return null;
            }

            return value as IDictionary<string, object>;
        }

        private static IEnumerable<object> GetArray(IDictionary<string, object> source, string key)
        {
            if (source == null || !source.TryGetValue(key, out var value))
            {
                return Enumerable.Empty<object>();
            }

            return (value as IEnumerable)?.Cast<object>() ?? Enumerable.Empty<object>();
        }

        private static string GetString(IDictionary<string, object> source, string key)
        {
            return source != null && source.TryGetValue(key, out var value)
                ? Convert.ToString(value) ?? string.Empty
                : string.Empty;
        }

        private static bool GetBoolean(IDictionary<string, object> source, string key)
        {
            var value = GetString(source, key);
            return value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        private static string FirstNonEmpty(string first, string second)
        {
            return string.IsNullOrWhiteSpace(first) ? second : first;
        }
    }
}
