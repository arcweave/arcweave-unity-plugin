using System;
using UnityEditor;

namespace Arcweave.Tests
{
    ///<summary>Runs a test's callback after Unity imports assets, like another editor extension's postprocessor could.</summary>
    public class ImportCallback : AssetPostprocessor
    {
        public static Action<string[], string[]> onPostprocessAllAssets;

        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            onPostprocessAllAssets?.Invoke(importedAssets, deletedAssets);
        }
    }
}
