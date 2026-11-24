#if UNITY_EDITOR

using UnityEngine;
using UnityEditor;

namespace Arcweave
{
    [CustomEditor(typeof(ArcweavePlayer))]
    [CanEditMultipleObjects]
    public class ArcweavePlayerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            // Draw the default inspector
            DrawDefaultInspector();

            // Add some space before the button
            EditorGUILayout.Space(10);

            // Add a separator line
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            // Add a header for the editor tools section
            EditorGUILayout.LabelField("Editor Tools", EditorStyles.boldLabel);

            // Every ArcweaveSaveHandler uses the same PlayerPrefs keys, so with several players selected one reset
            // clears the save for all of them. Prefer a player that has a handler assigned.
            ArcweavePlayer player = (ArcweavePlayer)target;
            foreach (var selected in targets)
            {
                if (((ArcweavePlayer)selected).HasSaveHandler)
                {
                    player = (ArcweavePlayer)selected;
                    break;
                }
            }

            // In Play mode deleting the save alone would leave the running game in its old state, and with
            // AutoSaveAlways the next element would save that state again. So the running game restarts too.
            if (Application.isPlaying)
            {
                if (GUILayout.Button("Reset Save and Restart", GUILayout.Height(30)))
                {
                    if (EditorUtility.DisplayDialog(
                        "Reset Save and Restart",
                        "This will delete all saved game data, reset the variables and visits, and restart the story from its starting element. This action cannot be undone.\n\nAre you sure you want to proceed?",
                        "Yes, Reset and Restart",
                        "Cancel"))
                    {
                        ResetSaveAndRestart();
                    }
                }
            }
            else if (GUILayout.Button("Reset Save File", GUILayout.Height(30)))
            {
                if (EditorUtility.DisplayDialog(
                    "Reset Save File",
                    "This will delete the saved game: the element to resume from, the variables and the visit counts.\n\nAre you sure you want to proceed?",
                    "Yes, Reset Save",
                    "Cancel"))
                {
                    ResetSaveFile(player);
                }
            }

            // Add help box with information
            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox(
                "Reset Save File: Clears all saved game data stored in PlayerPrefs. " +
                "This includes the current element ID, all variable states and visit counts. " +
                "Use this to start fresh or clear test data. In Play mode it also restarts the story from a clean state.",
                MessageType.Info);
        }

        /// <summary>
        /// Play mode: deletes the save and restarts every selected player from a clean state. Every save handler
        /// uses the same PlayerPrefs keys, so a player left running could save its old state again.
        /// </summary>
        private void ResetSaveAndRestart()
        {
            bool restarted = false;
            foreach (var selected in targets)
            {
                var selectedPlayer = (ArcweavePlayer)selected;
                if (!selectedPlayer.HasSaveHandler)
                {
                    continue;
                }

                // Deletes the save, resets variables and visits, then starts from the starting element.
                selectedPlayer.RestartProject();
                restarted = true;
            }

            if (!restarted)
            {
                EditorUtility.DisplayDialog(
                    "Missing Save Handler",
                    "Cannot reset saved data because no ArcweaveSaveHandler is assigned to the selected player.",
                    "OK");
            }
        }

        private void ResetSaveFile(ArcweavePlayer player)
        {
            if (!player.HasSaveHandler)
            {
                EditorUtility.DisplayDialog(
                    "Missing Save Handler",
                    "Cannot reset saved data because no ArcweaveSaveHandler is assigned to this player.",
                    "OK");
                return;
            }

            if (player.ResetSave())
            {
                Debug.Log("Save file reset successfully. All saved game data has been cleared.");
                EditorUtility.DisplayDialog(
                    "Save File Reset",
                    "Save file has been reset successfully.\n\nAll saved game data has been cleared from PlayerPrefs.",
                    "OK");
            }
            else
            {
                EditorUtility.DisplayDialog(
                    "No Saved Data",
                    "No saved game data was found to reset.",
                    "OK");
            }
        }
    }
}

#endif
