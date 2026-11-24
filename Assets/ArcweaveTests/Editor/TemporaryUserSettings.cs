using System;
using System.IO;

namespace Arcweave.Tests
{
    ///<summary>Points the Arcweave user settings at a temporary file and clears the ARCWEAVE_API_KEY environment variable,
    /// so tests never read or change the developer's own API key. Disposing restores both.</summary>
    public sealed class TemporaryUserSettings : IDisposable
    {
        private readonly string previousFilePath;
        private readonly string previousEnvironmentKey;
        private readonly string folder;

        public string FilePath { get; }

        public TemporaryUserSettings() {
            previousFilePath = ArcweaveUserSettings.filePath;
            previousEnvironmentKey = Environment.GetEnvironmentVariable(ArcweaveUserSettings.API_KEY_ENVIRONMENT_VARIABLE);
            folder = Path.Combine(Path.GetTempPath(), "ArcweaveUserSettingsTest_" + Guid.NewGuid().ToString("N"));
            FilePath = Path.Combine(folder, "ArcweaveSettings.json");
            ArcweaveUserSettings.filePath = FilePath;
            Environment.SetEnvironmentVariable(ArcweaveUserSettings.API_KEY_ENVIRONMENT_VARIABLE, null);
        }

        public void SetEnvironmentApiKey(string key) {
            Environment.SetEnvironmentVariable(ArcweaveUserSettings.API_KEY_ENVIRONMENT_VARIABLE, key);
        }

        public void Dispose() {
            ArcweaveUserSettings.filePath = previousFilePath;
            Environment.SetEnvironmentVariable(ArcweaveUserSettings.API_KEY_ENVIRONMENT_VARIABLE, previousEnvironmentKey);
            if ( Directory.Exists(folder) ) {
                Directory.Delete(folder, true);
            }
        }
    }
}
