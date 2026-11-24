#if UNITY_EDITOR

using System.Collections.Generic;
using System.Linq;
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
            foreach ( var asset in AssetsWithSavedApiKey() ) {
                Debug.LogWarning(string.Format("The Arcweave project asset '{0}' in '{1}' contains an API key, which is included in the build if the game uses the asset. " +
                    "Select the asset and use the button in its inspector to remove the key from the asset.", asset.name, AssetDatabase.GetAssetPath(asset)), asset);
            }
        }

        ///<summary>Every project asset that still contains a key, including several project assets saved in one file.</summary>
        internal static IEnumerable<ArcweaveProjectAsset> AssetsWithSavedApiKey() {
            foreach ( var path in AssetDatabase.FindAssets("t:ArcweaveProjectAsset").Select(AssetDatabase.GUIDToAssetPath).Distinct() ) {
                foreach ( var asset in AssetDatabase.LoadAllAssetsAtPath(path).OfType<ArcweaveProjectAsset>() ) {
                    if ( ArcweaveUserSettings.HasApiKeySavedInAsset(asset) ) {
                        yield return asset;
                    }
                }
            }
        }
    }
}

#endif
