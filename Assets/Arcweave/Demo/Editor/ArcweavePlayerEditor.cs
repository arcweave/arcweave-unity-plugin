#if UNITY_EDITOR

using UnityEngine;
using UnityEditor;

namespace Arcweave
{
    [CustomEditor(typeof(ArcweavePlayer))]
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

            ArcweavePlayer player = (ArcweavePlayer)target;

            // Add the Reset Save button
            GUI.enabled = Application.isPlaying || EditorApplication.isPlaying == false;

            if (GUILayout.Button("Reset Save File", GUILayout.Height(30)))
            {
                if (EditorUtility.DisplayDialog(
                    "Reset Save File",
                    "This will delete all saved game data including the current element and all variable states. This action cannot be undone.\n\nAre you sure you want to proceed?",
                    "Yes, Reset Save",
                    "Cancel"))
                {
                    ResetSaveFile(player);
                }
            }

            GUI.enabled = true;

            // Add help box with information
            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox(
                "Reset Save File: Clears all saved game data stored in PlayerPrefs. " +
                "This includes the current element ID, all variable states and visit counts. " +
                "Use this to start fresh or clear test data.",
                MessageType.Info);
        }

        private void ResetSaveFile(ArcweavePlayer player)
        {
            // Call the ResetVariables method which handles the save handler
            player.ResetVariables();

            Debug.Log("Save file reset successfully. All saved game data has been cleared.");

            EditorUtility.DisplayDialog(
                "Save File Reset",
                "Save file has been reset successfully.\n\nAll saved game data has been cleared from PlayerPrefs.",
                "OK");
        }
    }
}

#endif
