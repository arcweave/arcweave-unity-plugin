using UnityEngine;
using UnityEditor;

namespace Arcweave
{
    [CustomEditor(typeof(ArcweaveProjectAsset))]
    public class ArcweaveProjectAssetInspector : Editor
    {
        private ArcweaveProjectAsset aw => target as ArcweaveProjectAsset;

        private bool isImporting;

        public override void OnInspectorGUI() {
            base.OnInspectorGUI();

            var fromJson = aw.importSource == ArcweaveProjectAsset.ImportSource.FromJson && aw.projectJsonFile != null;
            var fromWeb = aw.importSource == ArcweaveProjectAsset.ImportSource.FromWeb && !string.IsNullOrEmpty(aw.userAPIKey) && !string.IsNullOrEmpty(aw.projectHash);
            var text = "Generate Project " + aw.importSource;

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
            if ( isImporting || aw.project == null || string.IsNullOrEmpty(aw.project.name) ) { return; }

            GUILayout.BeginVertical("box");
            GUILayout.Label(string.Format("Arcweave Project: {0}", aw.project.name));
            GUILayout.Label("Global Variables:");
            EditorGUI.indentLevel++;
            foreach ( var variable in aw.project.variables ) {
                EditorGUILayout.LabelField(variable.name, variable.value?.ToString());
            }
            EditorGUI.indentLevel--;
            GUILayout.EndVertical();
            Repaint();
        }
    }
}