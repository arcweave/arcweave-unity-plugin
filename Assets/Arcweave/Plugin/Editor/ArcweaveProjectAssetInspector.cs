#if UNITY_EDITOR

using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Arcweave
{
    [CustomEditor(typeof(ArcweaveProjectAsset))]
    public class ArcweaveProjectAssetInspector : UnityEditor.Editor
    {
        private ArcweaveProjectAsset aw => target as ArcweaveProjectAsset;

        private bool isImporting;

        // Foldouts are closed by default, so the lists below are only drawn when the user asks for them
        private bool showGlobals;
        private bool showBoards;
        private bool showComponents;

        // Boards and components that have variables, found once per project instead of on every repaint.
        // Only the structure is cached: the variable values are still read when drawing, so they stay live.
        private Project.Project cachedProject;
        private readonly List<Project.Board> boardsWithVariables = new List<Project.Board>();
        private readonly List<Project.Component> componentsWithVariables = new List<Project.Component>();

        private void RefreshCache() {
            var project = aw.Project;
            if ( project == cachedProject ) { return; }

            cachedProject = project;
            boardsWithVariables.Clear();
            componentsWithVariables.Clear();
            if ( project == null ) { return; }

            foreach ( var board in project.Boards ) {
                if ( board.Variables != null && board.Variables.Count > 0 ) { boardsWithVariables.Add(board); }
            }
            foreach ( var component in project.Components ) {
                if ( component.Variables != null && component.Variables.Count > 0 ) { componentsWithVariables.Add(component); }
            }
        }

        // Variable values only change while playing, so only then is a continuous repaint needed.
        // Unity asks this only while this asset is shown in an Inspector.
        public override bool RequiresConstantRepaint() => Application.isPlaying && !isImporting;

        public override void OnInspectorGUI() {
            // base.OnInspectorGUI() and serializedObject.Update() are deliberately not used: they serialize the whole asset,
            // which calls Variable.OnBeforeSerialize() on every variable of the imported project each time the inspector is drawn.
            // The fields are drawn straight on the asset instead, so drawing the inspector never serializes it.
            using ( new EditorGUI.DisabledScope(true) ) {
                EditorGUILayout.ObjectField("Script", MonoScript.FromScriptableObject(aw), typeof(MonoScript), false);
            }
            EditorGUI.BeginChangeCheck();
            var importSource = (ArcweaveProjectAsset.ImportSource)EditorGUILayout.EnumPopup("Import Source", aw.importSource);
            if ( EditorGUI.EndChangeCheck() ) {
                aw.importSource = importSource;
                EditorUtility.SetDirty(aw);
            }

            var fromJson = aw.importSource == ArcweaveProjectAsset.ImportSource.FromJson && aw.projectJsonFile != null;
            var fromWeb = aw.importSource == ArcweaveProjectAsset.ImportSource.FromWeb && !string.IsNullOrEmpty(aw.userAPIKey) && !string.IsNullOrEmpty(aw.projectHash);
            var text = "Import Project " + aw.importSource;

            using (var group = new EditorGUILayout.FadeGroupScope(aw.importSource == ArcweaveProjectAsset.ImportSource.FromWeb ? 1f : 0f))
            {
                if (group.visible)
                {
                    EditorGUI.BeginChangeCheck();
                    aw.userAPIKey = EditorGUILayout.TextField(new GUIContent("User API Key", "Your Arcweave User API Key"), aw.userAPIKey);
                    aw.projectHash = EditorGUILayout.TextField(new GUIContent("Project Hash", "The Arcweave Project Hash"), aw.projectHash);
                    aw.locale = EditorGUILayout.TextField(new GUIContent("Language ISO", "The ISO of the language"), aw.locale);
                    
                    GUIContent content = new GUIContent("Fallback Content", "Enable to use fallback content when a string is not available in the specified language.");
                    aw.fallbackLocales = EditorGUILayout.Toggle(content, aw.fallbackLocales);
                    // SetDirty only marks the asset, the (expensive) serialization happens when it is saved
                    if ( EditorGUI.EndChangeCheck() ) { EditorUtility.SetDirty(aw); }

                    GUIContent mediaContent = new GUIContent("Download Media", "Enable to download the images and audio the project uses from Arcweave into a folder next to this asset and use them instead of a 'Resources' folder.");
                    var downloadMedia = EditorGUILayout.Toggle(mediaContent, aw.downloadMedia);
                    if ( downloadMedia != aw.downloadMedia ) {
                        // Saved even without importing, since it decides whether the project relies on Arcweave's media
                        Undo.RecordObject(aw, "Toggle Download Media");
                        aw.downloadMedia = downloadMedia;
                        EditorUtility.SetDirty(aw);
                    }
                }
            }

            using (var group = new EditorGUILayout.FadeGroupScope(aw.importSource == ArcweaveProjectAsset.ImportSource.FromJson ? 1f : 0f))
            {
                if (group.visible)
                {
                    EditorGUI.BeginChangeCheck();
                    aw.projectJsonFile = (TextAsset)EditorGUILayout.ObjectField(new GUIContent("Project JSON File", "The Arcweave project JSON file"), aw.projectJsonFile, typeof(TextAsset), false);
                    if ( EditorGUI.EndChangeCheck() ) { EditorUtility.SetDirty(aw); }
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
            if ( isImporting || aw.Project == null || string.IsNullOrEmpty(aw.Project.name) ) { return; }

            GUILayout.Space(5);

            GUILayout.BeginVertical("box");
            GUILayout.Label(string.Format("Arcweave Project: {0}", aw.Project.name), EditorStyles.boldLabel);

            RefreshCache();

            // Global Variables Section
            showGlobals = EditorGUILayout.Foldout(showGlobals, "Global Variables", true);
            if ( showGlobals ) {
                EditorGUI.indentLevel++;
                bool hasGlobalVariables = false;
                foreach ( var variable in aw.Project.GlobalVariables ) {
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

            // Board Variables Section - Grouped by Board
            showBoards = EditorGUILayout.Foldout(showBoards, $"Board Variables ({boardsWithVariables.Count})", true);
            if ( showBoards ) {
                foreach ( var board in boardsWithVariables ) {
                    EditorGUI.indentLevel++;
                    GUILayout.Label(string.IsNullOrEmpty(board.Name) ? $"Board: {board.Id}" : board.Name, EditorStyles.miniLabel);
                    EditorGUI.indentLevel++;
                    foreach ( var variable in board.Variables ) {
                        EditorGUILayout.LabelField(variable.Name, variable.Value?.ToString());
                    }
                    EditorGUI.indentLevel -= 2;
                    GUILayout.Space(5);
                }
                if ( boardsWithVariables.Count == 0 ) {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField("(None)");
                    EditorGUI.indentLevel--;
                }
            }

            // Component Variables Section - Grouped by Component
            showComponents = EditorGUILayout.Foldout(showComponents, $"Component Variables ({componentsWithVariables.Count})", true);
            if ( showComponents ) {
                foreach ( var component in componentsWithVariables ) {
                    EditorGUI.indentLevel++;
                    GUILayout.Label(string.IsNullOrEmpty(component.Name) ? $"Component: {component.Id}" : component.Name, EditorStyles.miniLabel);
                    EditorGUI.indentLevel++;
                    foreach ( var variable in component.Variables ) {
                        EditorGUILayout.LabelField(variable.Name, variable.Value?.ToString());
                    }
                    EditorGUI.indentLevel -= 2;
                    GUILayout.Space(5);
                }
                if ( componentsWithVariables.Count == 0 ) {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField("(None)");
                    EditorGUI.indentLevel--;
                }
            }

            GUILayout.Space(10);

            if ( aw.Project != null && GUILayout.Button("Open Project Viewer", GUILayout.Height(50)) ) {
                ProjectViewerWindow.Open(aw);
            }

            GUILayout.EndVertical();
        }
    }
}

#endif
