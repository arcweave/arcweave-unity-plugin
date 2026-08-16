#if UNITY_EDITOR

using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace Arcweave
{
    [CustomEditor(typeof(ArcweaveProjectAsset))]
    public class ArcweaveProjectAssetInspector : UnityEditor.Editor
    {
        private ArcweaveProjectAsset aw => target as ArcweaveProjectAsset;

        private bool isImporting;

        public override void OnInspectorGUI() {
            base.OnInspectorGUI();

            var fromJson = aw.importSource == ArcweaveProjectAsset.ImportSource.FromJson && aw.projectJsonFile != null;
            var fromWeb = aw.importSource == ArcweaveProjectAsset.ImportSource.FromWeb && !string.IsNullOrEmpty(aw.userAPIKey) && !string.IsNullOrEmpty(aw.projectHash);
            var text = "Import Project " + aw.importSource;

            using (var group = new EditorGUILayout.FadeGroupScope(aw.importSource == ArcweaveProjectAsset.ImportSource.FromWeb ? 1f : 0f))
            {
                if (group.visible)
                {
                    aw.userAPIKey = EditorGUILayout.TextField(new GUIContent("User API Key", "Your Arcweave User API Key"), aw.userAPIKey);
                    aw.projectHash = EditorGUILayout.TextField(new GUIContent("Project Hash", "The Arcweave Project Hash"), aw.projectHash);
                    aw.locale = EditorGUILayout.TextField(new GUIContent("Language ISO", "The ISO of the language"), aw.locale);
                    
                    GUIContent content = new GUIContent("Fallback Content", "Enable to use fallback content when a string is not available in the specified language.");
                    aw.fallbackLocales = EditorGUILayout.Toggle(content, aw.fallbackLocales);
                }
            }

            using (var group = new EditorGUILayout.FadeGroupScope(aw.importSource == ArcweaveProjectAsset.ImportSource.FromJson ? 1f : 0f))
            {
                if (group.visible)
                {
                    aw.projectJsonFile = (TextAsset)EditorGUILayout.ObjectField(new GUIContent("Project JSON File", "The Arcweave project JSON file"), aw.projectJsonFile, typeof(TextAsset), false);
                }
            }

            GUI.enabled = !isImporting && ( fromJson || fromWeb );
            if ( GUILayout.Button(text) ) {
                isImporting = true;
                aw.ImportProject(() =>
                {
                    isImporting = false;
                    EditorUtility.SetDirty(aw);
                    AssetDatabase.SaveAssetIfDirty(aw);
                },
                (error) =>
                {
                    isImporting = false;
                });
            }
            GUI.enabled = true;


            ///----------------------------------------------------------------------------------------------

            if ( isImporting ) { GUILayout.Label("Importing Project..."); }
            if ( isImporting || aw.Project == null || string.IsNullOrEmpty(aw.Project.name) ) { return; }

            GUILayout.Space(5);

            GUILayout.BeginVertical("box");
            GUILayout.Label(string.Format("Arcweave Project: {0}", aw.Project.name), EditorStyles.boldLabel);

            // Global Variables Section
            GUILayout.Label("Global Variables:", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            bool hasGlobalVariables = false;
            foreach ( var variable in aw.Project.Variables)
            {
                if(variable.Parent == null)
                {
                    hasGlobalVariables = true;
                    EditorGUILayout.LabelField(variable.Name, variable.Value?.ToString());
                }
            }
            if (!hasGlobalVariables)
            {
                EditorGUILayout.LabelField("(None)");
            }
            EditorGUI.indentLevel--;

            GUILayout.Space(10);

            // Board Variables Section - Grouped by Board
            GUILayout.Label("Board Variables:", EditorStyles.boldLabel);
            bool hasBoardVariables = false;
            foreach (var board in aw.Project.Boards)
            {
                if (board.Variables != null && board.Variables.Count > 0)
                {
                    hasBoardVariables = true;
                    EditorGUI.indentLevel++;
                    GUILayout.Label(string.IsNullOrEmpty(board.Name) ? $"Board: {board.Id}" : board.Name, EditorStyles.miniLabel);
                    EditorGUI.indentLevel++;
                    foreach (var variable in board.Variables)
                    {
                        string variableName = variable.Name;
                        EditorGUILayout.LabelField(variableName, variable.Value?.ToString());
                    }
                    EditorGUI.indentLevel -= 2;
                    GUILayout.Space(5);
                }
            }
            if (!hasBoardVariables)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField("(None)");
                EditorGUI.indentLevel--;
            }

            GUILayout.Space(10);

            if ( aw.Project != null && GUILayout.Button("Open Project Viewer", GUILayout.Height(50)) ) {
                ProjectViewerWindow.Open(aw);
            }

            GUILayout.EndVertical();

            Repaint();
        }
    }
}

#endif