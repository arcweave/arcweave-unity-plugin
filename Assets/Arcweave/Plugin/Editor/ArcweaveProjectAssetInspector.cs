#if UNITY_EDITOR

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace Arcweave
{
    [CustomEditor(typeof(ArcweaveProjectAsset))]
    public class ArcweaveProjectAssetInspector : UnityEditor.Editor
    {
        private ArcweaveProjectAsset aw => target as ArcweaveProjectAsset;

        private bool isImporting;
        private bool showGlobals;
        private bool showBoards;
        private bool showComponents;

        public override void OnInspectorGUI() {
            // Only the fields we need are drawn, through serializedObject so Undo and dirty flags are handled by Unity.
            // The imported project is never drawn nor walked by the default inspector.
            serializedObject.Update();

            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(ArcweaveProjectAsset.importSource)));

            if ( aw.importSource == ArcweaveProjectAsset.ImportSource.FromWeb ) {
                Field(nameof(ArcweaveProjectAsset.userAPIKey), "User API Key", "Your Arcweave User API Key");
                Field(nameof(ArcweaveProjectAsset.projectHash), "Project Hash", "The Arcweave Project Hash");
                Field(nameof(ArcweaveProjectAsset.locale), "Language ISO", "The ISO of the language");
                Field(nameof(ArcweaveProjectAsset.fallbackLocales), "Fallback Content", "Enable to use fallback content when a string is not available in the specified language.");
                // Saved even without importing, since it decides whether the project relies on Arcweave's media
                Field(nameof(ArcweaveProjectAsset.downloadMedia), "Download Media", "Enable to download the images and audio the project uses from Arcweave into a folder next to this asset and use them instead of a 'Resources' folder.");
            } else {
                Field(nameof(ArcweaveProjectAsset.projectJsonFile), "Project JSON File", "The Arcweave project JSON file");
            }

            serializedObject.ApplyModifiedProperties();

            var fromJson = aw.importSource == ArcweaveProjectAsset.ImportSource.FromJson && aw.projectJsonFile != null;
            var fromWeb = aw.importSource == ArcweaveProjectAsset.ImportSource.FromWeb && !string.IsNullOrEmpty(aw.userAPIKey) && !string.IsNullOrEmpty(aw.projectHash);
            var text = "Import Project " + aw.importSource;

            GUI.enabled = !isImporting && ( fromJson || fromWeb );
            if ( GUILayout.Button(text) ) {
                isImporting = true;
                aw.ImportProject(() =>
                {
                    isImporting = false;
                    EditorUtility.SetDirty(aw);
                    AssetDatabase.SaveAssetIfDirty(aw);
                    Repaint();
                },
                (error) =>
                {
                    isImporting = false;
                    Repaint();
                });
            }
            GUI.enabled = true;


            ///----------------------------------------------------------------------------------------------

            if ( isImporting ) { GUILayout.Label("Importing Project..."); }
            // HasProject and ProjectName don't rebuild the project, which only happens when a foldout below is opened
            if ( isImporting || !aw.HasProject || string.IsNullOrEmpty(aw.ProjectName) ) { return; }

            GUILayout.Space(5);

            GUILayout.BeginVertical("box");
            GUILayout.Label(string.Format("Arcweave Project: {0}", aw.ProjectName), EditorStyles.boldLabel);

            // The variable lists are only walked while their foldout is open
            showGlobals = EditorGUILayout.Foldout(showGlobals, "Global Variables", true);
            if ( showGlobals ) {
                EditorGUI.indentLevel++;
                bool hasGlobalVariables = false;
                foreach ( var variable in Safe(aw.Project?.GlobalVariables) ) {
                    if ( variable.Parent == null ) {
                        hasGlobalVariables = true;
                        EditorGUILayout.LabelField(variable.Name, variable.Value?.ToString());
                    }
                }
                if ( !hasGlobalVariables ) {
                    EditorGUILayout.LabelField("(None)");
                }
                EditorGUI.indentLevel--;
            }

            showBoards = EditorGUILayout.Foldout(showBoards, "Board Variables", true);
            if ( showBoards ) {
                bool hasBoardVariables = false;
                foreach ( var board in Safe(aw.Project?.Boards) ) {
                    if ( board.Variables == null || board.Variables.Count == 0 ) { continue; }

                    hasBoardVariables = true;
                    EditorGUI.indentLevel++;
                    GUILayout.Label(string.IsNullOrEmpty(board.Name) ? $"Board: {board.Id}" : board.Name, EditorStyles.miniLabel);
                    EditorGUI.indentLevel++;
                    foreach ( var variable in board.Variables ) {
                        EditorGUILayout.LabelField(variable.Name, variable.Value?.ToString());
                    }
                    EditorGUI.indentLevel -= 2;
                    GUILayout.Space(5);
                }
                if ( !hasBoardVariables ) {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField("(None)");
                    EditorGUI.indentLevel--;
                }
            }

            showComponents = EditorGUILayout.Foldout(showComponents, "Component Variables", true);
            if ( showComponents ) {
                bool hasComponentVariables = false;
                foreach ( var component in Safe(aw.Project?.Components) ) {
                    if ( component.Variables == null || component.Variables.Count == 0 ) { continue; }

                    hasComponentVariables = true;
                    EditorGUI.indentLevel++;
                    GUILayout.Label(string.IsNullOrEmpty(component.Name) ? $"Component: {component.Id}" : component.Name, EditorStyles.miniLabel);
                    EditorGUI.indentLevel++;
                    foreach ( var variable in component.Variables ) {
                        EditorGUILayout.LabelField(variable.Name, variable.Value?.ToString());
                    }
                    EditorGUI.indentLevel -= 2;
                    GUILayout.Space(5);
                }
                if ( !hasComponentVariables ) {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField("(None)");
                    EditorGUI.indentLevel--;
                }
            }

            GUILayout.Space(10);

            if ( GUILayout.Button("Open Project Viewer", GUILayout.Height(50)) ) {
                ProjectViewerWindow.Open(aw);
            }

            GUILayout.EndVertical();
        }

        // The project can be null if the stored json can't be rebuilt
        static IEnumerable<T> Safe<T>(IEnumerable<T> items) => items ?? Enumerable.Empty<T>();

        void Field(string propertyName, string label, string tooltip) {
            EditorGUILayout.PropertyField(serializedObject.FindProperty(propertyName), new GUIContent(label, tooltip));
        }
    }
}

#endif
