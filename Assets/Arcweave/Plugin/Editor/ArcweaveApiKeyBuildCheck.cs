#if UNITY_EDITOR

using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Arcweave
{
    ///<summary>Warns when a build starts while a project asset still contains an API key saved by an older version of the plugin,
    /// since a project asset used by the game is included in the build together with its key.</summary>
    internal class ArcweaveApiKeyBuildCheck : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report) {
            foreach ( var path in AssetsWithSavedApiKey() ) {
                Debug.LogWarning(string.Format("The Arcweave project asset '{0}' contains an API key, which is included in the build if the game uses the asset. " +
                    "Select the asset and click 'Move Key to User Settings' in its inspector.", path), AssetDatabase.LoadMainAssetAtPath(path));
            }
        }

        internal static IEnumerable<string> AssetsWithSavedApiKey() {
            foreach ( var guid in AssetDatabase.FindAssets("t:ArcweaveProjectAsset") ) {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<ArcweaveProjectAsset>(path);
#pragma warning disable 618 // Only checks whether the legacy key is still saved
                if ( asset != null && !string.IsNullOrEmpty(asset.userAPIKey) ) {
#pragma warning restore 618
                    yield return path;
                }
            }
        }
    }
}

#endif
