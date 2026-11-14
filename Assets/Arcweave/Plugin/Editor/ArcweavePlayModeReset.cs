#if UNITY_EDITOR

using UnityEditor;

namespace Arcweave
{
    /// <summary>
    /// Playing in the editor changes the variables, visits and starting element stored in the
    /// ArcweaveProjectAsset in memory, and those changes stay after leaving play mode. This puts every
    /// project asset back to its default state when play mode ends, so it never keeps the state of the last run.
    /// The asset is not marked dirty on purpose: marking it would save the last run's state to disk.
    /// </summary>
    [InitializeOnLoad]
    internal static class ArcweavePlayModeReset
    {
        static ArcweavePlayModeReset()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingPlayMode)
            {
                return;
            }

            foreach (var guid in AssetDatabase.FindAssets("t:ArcweaveProjectAsset"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<ArcweaveProjectAsset>(path);
                var project = asset != null ? asset.Project : null;
                if (project == null || project.Boards == null)
                {
                    continue;
                }

                project.ResetVariablesToDefaultValues();
                project.ResetVisits();
                project.ResetStartingElement();
            }
        }
    }
}

#endif
