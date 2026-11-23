#if UNITY_EDITOR

using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Arcweave
{
    ///<summary>Arcweave settings saved for the current user of this Unity project, in <c>UserSettings/ArcweaveSettings.json</c>.
    /// Unity's UserSettings folder is outside Assets, so it is never included in builds, and Unity's .gitignore template excludes it from version control.
    /// Each developer keeps their own API key there instead of sharing one saved in the project asset.</summary>
    public static class ArcweaveUserSettings
    {
        ///<summary>The environment variable a web import reads the API key from when none is saved in the user settings, eg. on a build server.</summary>
        public const string API_KEY_ENVIRONMENT_VARIABLE = ArcweaveProjectAsset.API_KEY_ENVIRONMENT_VARIABLE;

        ///<summary>The settings file, relative to the Unity project folder. Tests point it at a temporary file.</summary>
        internal static string filePath = "UserSettings/ArcweaveSettings.json";

        [Serializable]
        class Data
        {
            public string apiKey = "";
        }

        // The file is read again only when it changes, since the inspector reads the key on every repaint.
        static Data cache;
        static string cachePath;
        static DateTime cacheWriteTime;

        ///<summary>The API key saved for the current user. Empty if none is saved. Setting it saves the file.</summary>
        public static string ApiKey {
            get => Load().apiKey ?? "";
            set {
                var data = Load();
                data.apiKey = value == null ? "" : value.Trim();
                Save(data);
            }
        }

        ///<summary>The API key a web import uses when none is passed to it: the key saved in the user settings,
        /// otherwise the <c>ARCWEAVE_API_KEY</c> environment variable. Empty if neither has one.</summary>
        public static string ResolveApiKey() {
            var key = ApiKey;
            if ( !string.IsNullOrEmpty(key) ) {
                return key;
            }
            return EnvironmentApiKey;
        }

        ///<summary>The API key in the <c>ARCWEAVE_API_KEY</c> environment variable. Empty if it is not set.</summary>
        public static string EnvironmentApiKey => ( Environment.GetEnvironmentVariable(API_KEY_ENVIRONMENT_VARIABLE) ?? "" ).Trim();

        ///<summary>Removes the API key saved in a project asset by older versions of the plugin and saves the asset.
        /// The key is moved to the user settings unless they already have one, which is kept.
        /// Returns true if the key was moved, false if the user settings kept their own key or the asset had none.</summary>
        public static bool MoveApiKeyOutOfAsset(ArcweaveProjectAsset asset) {
#pragma warning disable 618 // The legacy key is read to move it out of the asset
            var legacyKey = asset.userAPIKey;
            if ( string.IsNullOrEmpty(legacyKey) ) {
                return false;
            }
            var moved = string.IsNullOrEmpty(ApiKey);
            if ( moved ) {
                ApiKey = legacyKey;
            }
            asset.userAPIKey = "";
#pragma warning restore 618
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
            return moved;
        }

        static Data Load() {
            var writeTime = File.Exists(filePath) ? File.GetLastWriteTimeUtc(filePath) : DateTime.MinValue;
            if ( cache != null && cachePath == filePath && cacheWriteTime == writeTime ) {
                return cache;
            }

            var data = new Data();
            if ( writeTime != DateTime.MinValue ) {
                try {
                    JsonUtility.FromJsonOverwrite(File.ReadAllText(filePath), data);
                }
                catch ( Exception e ) {
                    Debug.LogWarning($"Could not read the Arcweave user settings in '{filePath}': {e.Message}");
                }
            }
            cache = data;
            cachePath = filePath;
            cacheWriteTime = writeTime;
            return data;
        }

        static void Save(Data data) {
            var directory = Path.GetDirectoryName(filePath);
            if ( !string.IsNullOrEmpty(directory) ) {
                Directory.CreateDirectory(directory);
            }
            File.WriteAllText(filePath, JsonUtility.ToJson(data, true));
            cache = data;
            cachePath = filePath;
            cacheWriteTime = File.GetLastWriteTimeUtc(filePath);
        }
    }
}

#endif
