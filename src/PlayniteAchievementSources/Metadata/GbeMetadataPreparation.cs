using PlayniteAchievementSources.Sources.Gbe;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PlayniteAchievementSources.Metadata
{
    public interface IGbeDefinitionMetadataSource
    {
        string Name { get; }

        bool TryValidate(uint appId, string sourcePath, out string error);
    }

    public sealed class ExplicitJsonDefinitionMetadataSource : IGbeDefinitionMetadataSource
    {
        private readonly GbeAchievementReader reader = new GbeAchievementReader();

        public string Name => "Explicit user-selected GBE-compatible JSON";

        public bool TryValidate(uint appId, string sourcePath, out string error)
        {
            var validation = reader.Read(new GbeAchievementReadContext
            {
                AppId = appId,
                ExplicitDefinitionPath = sourcePath
            });
            if (validation.HasDefinitions)
            {
                error = string.Empty;
                return true;
            }

            error = "The selected JSON is not a supported GBE-compatible definition array.";
            return false;
        }
    }

    public sealed class GbeMetadataPreparationPlan
    {
        public uint AppId { get; set; }
        public string SourcePath { get; set; } = string.Empty;
        public string DestinationPath { get; set; } = string.Empty;
        public string MetadataSource { get; set; } = "Explicit user-selected GBE-compatible JSON";
        public bool DestinationExists { get; set; }
        public bool IsValid { get; set; }
        public string Error { get; set; } = string.Empty;
        public IList<string> FilesToWrite { get; } = new List<string>();
    }

    public sealed class GbeMetadataPreparationResult
    {
        public bool Success { get; set; }
        public bool Changed { get; set; }
        public string DestinationPath { get; set; } = string.Empty;
        public string BackupPath { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
    }

    public sealed class GbeMetadataPreparer
    {
        private readonly GbeAchievementReader reader = new GbeAchievementReader();
        private readonly IGbeDefinitionMetadataSource importSource;
        private readonly Func<uint, string, bool> destinationValidator;

        public GbeMetadataPreparer(
            IGbeDefinitionMetadataSource importSource = null,
            Func<uint, string, bool> destinationValidator = null)
        {
            this.importSource = importSource ?? new ExplicitJsonDefinitionMetadataSource();
            this.destinationValidator = destinationValidator;
        }

        public GbeMetadataPreparationPlan CreateImportPlan(
            uint appId,
            string sourcePath,
            string steamSettingsDirectory)
        {
            var plan = new GbeMetadataPreparationPlan
            {
                AppId = appId,
                SourcePath = FullPath(sourcePath)
            };

            if (appId == 0)
            {
                plan.Error = "A non-zero Steam AppID is required.";
                return plan;
            }

            var settingsRoot = FullPath(steamSettingsDirectory);
            if (string.IsNullOrWhiteSpace(settingsRoot) ||
                !Directory.Exists(settingsRoot) ||
                !string.Equals(new DirectoryInfo(settingsRoot).Name, "steam_settings", StringComparison.OrdinalIgnoreCase))
            {
                plan.Error = "A recognized existing steam_settings directory is required. No directory will be created speculatively.";
                return plan;
            }

            if (string.IsNullOrWhiteSpace(plan.SourcePath) || !File.Exists(plan.SourcePath))
            {
                plan.Error = "The selected definition JSON does not exist.";
                return plan;
            }

            if (!importSource.TryValidate(appId, plan.SourcePath, out var sourceError))
            {
                plan.Error = sourceError;
                return plan;
            }

            plan.MetadataSource = importSource.Name;
            plan.DestinationPath = Path.Combine(settingsRoot, "achievements.json");
            plan.DestinationExists = File.Exists(plan.DestinationPath);
            plan.FilesToWrite.Add(plan.DestinationPath);
            plan.IsValid = true;
            return plan;
        }

        public GbeMetadataPreparationResult Execute(
            GbeMetadataPreparationPlan plan,
            bool globalPermission,
            bool explicitlyConfirmed)
        {
            var result = new GbeMetadataPreparationResult();
            if (plan == null || !plan.IsValid)
            {
                result.Error = plan?.Error ?? "The preparation plan is invalid.";
                return result;
            }

            if (!globalPermission)
            {
                result.Error = "Managed metadata writes are disabled in Achievement Sources settings.";
                return result;
            }

            if (!explicitlyConfirmed)
            {
                result.Error = "Explicit per-game confirmation is required.";
                return result;
            }

            var destinationDirectory = Path.GetDirectoryName(plan.DestinationPath);
            if (!string.Equals(new DirectoryInfo(destinationDirectory).Name, "steam_settings", StringComparison.OrdinalIgnoreCase))
            {
                result.Error = "The destination is no longer a recognized steam_settings directory.";
                return result;
            }

            var sourceBytes = File.ReadAllBytes(plan.SourcePath);
            if (File.Exists(plan.DestinationPath))
            {
                var existingBytes = File.ReadAllBytes(plan.DestinationPath);
                if (BytesEqual(sourceBytes, existingBytes))
                {
                    result.Success = true;
                    result.DestinationPath = plan.DestinationPath;
                    return result;
                }
            }

            var destinationExisted = File.Exists(plan.DestinationPath);
            byte[] originalBytes = null;
            var temporaryPath = plan.DestinationPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            var replacementBackupPath = plan.DestinationPath + "." + Guid.NewGuid().ToString("N") + ".replace-backup";
            try
            {
                File.WriteAllBytes(temporaryPath, sourceBytes);
                using (var stream = new FileStream(temporaryPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    stream.Flush();
                }

                if (destinationExisted)
                {
                    originalBytes = File.ReadAllBytes(plan.DestinationPath);
                    result.BackupPath = plan.DestinationPath + ".achievement-sources-backup-" +
                        DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" +
                        Guid.NewGuid().ToString("N") + ".json";
                    File.Copy(plan.DestinationPath, result.BackupPath, false);
                    File.Replace(temporaryPath, plan.DestinationPath, replacementBackupPath);
                }
                else
                {
                    File.Move(temporaryPath, plan.DestinationPath);
                }

                var valid = destinationValidator != null
                    ? destinationValidator(plan.AppId, plan.DestinationPath)
                    : reader.Read(new GbeAchievementReadContext
                    {
                        AppId = plan.AppId,
                        ExplicitDefinitionPath = plan.DestinationPath
                    }).HasDefinitions;
                if (!valid)
                {
                    throw new InvalidDataException("The written definition file failed read-back validation.");
                }

                result.Success = true;
                result.Changed = true;
                result.DestinationPath = plan.DestinationPath;
                return result;
            }
            catch (Exception exception)
            {
                try
                {
                    if (destinationExisted && originalBytes != null)
                    {
                        var restorePath = plan.DestinationPath + "." + Guid.NewGuid().ToString("N") + ".restore";
                        File.WriteAllBytes(restorePath, originalBytes);
                        if (File.Exists(plan.DestinationPath))
                        {
                            File.Replace(restorePath, plan.DestinationPath, replacementBackupPath);
                        }
                        else
                        {
                            File.Move(restorePath, plan.DestinationPath);
                        }
                    }
                    else if (!destinationExisted && File.Exists(plan.DestinationPath))
                    {
                        File.Delete(plan.DestinationPath);
                    }
                }
                catch (Exception restoreException)
                {
                    result.Error = exception.Message + " Automatic restoration also failed: " + restoreException.Message;
                    return result;
                }

                result.Error = exception.Message;
                return result;
            }
            finally
            {
                DeleteIfExists(temporaryPath);
                DeleteIfExists(replacementBackupPath);
            }
        }

        private static bool BytesEqual(byte[] first, byte[] second)
        {
            if (first == null || second == null || first.Length != second.Length)
            {
                return false;
            }

            for (var index = 0; index < first.Length; index++)
            {
                if (first[index] != second[index])
                {
                    return false;
                }
            }

            return true;
        }

        private static string FullPath(string path)
        {
            try
            {
                return string.IsNullOrWhiteSpace(path) ? string.Empty : Path.GetFullPath(path.Trim());
            }
            catch
            {
                return string.Empty;
            }
        }

        private static void DeleteIfExists(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
            }
        }
    }
}
