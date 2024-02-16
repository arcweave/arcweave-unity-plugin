#if UNITY_EDITOR

using UnityEngine;
using UnityEditor;

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

            GUI.enabled = !isImporting && ( fromJson || fromWeb );
            if ( GUILayout.Button(text) ) {
                isImporting = true;
                aw.ImportProject(() =>
                {
                    isImporting = false;
                    EditorUtility.SetDirty(aw);
                    AssetDatabase.SaveAssetIfDirty(aw);
                });
            }
            GUI.enabled = true;


            ///----------------------------------------------------------------------------------------------

            if ( isImporting ) { GUILayout.Label("Importing Project..."); }
            if ( isImporting || aw.Project == null || string.IsNullOrEmpty(aw.Project.name) ) { return; }

            GUILayout.Space(5);

            GUILayout.BeginVertical("box");
            GUILayout.Label(string.Format("Arcweave Project: {0}", aw.Project.name));
            GUILayout.Label("Global Variables:");
            EditorGUI.indentLevel++;
            foreach ( var variable in aw.Project.Variables ) {
                EditorGUILayout.LabelField(variable.Name, variable.Value?.ToString());
            }
            EditorGUI.indentLevel--;

            if ( aw.Project != null && GUILayout.Button("Open Project Viewer", GUILayout.Height(50)) ) {
                ProjectViewerWindow.Open(aw);
            }

            GUILayout.EndVertical();

            Repaint();
        }
    }
}

#endif