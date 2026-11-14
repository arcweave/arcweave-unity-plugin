#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

namespace Arcweave
{
    /// <summary>
    /// Hides maxSecondsBeforeDiskWrite when every ArcweavePlayer that uses this handler is in Manual save mode:
    /// manual saves are written to disk right away, so the delay only applies to autosaves.
    /// </summary>
    [CustomEditor(typeof(ArcweaveSaveHandler))]
    [CanEditMultipleObjects]
    public class ArcweaveSaveHandlerEditor : Editor
    {
        private const string DiskWriteDelayField = "maxSecondsBeforeDiskWrite";

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var excluded = AllTargetsOnlyUsedByManualPlayers() ? new[] { "m_Script", DiskWriteDelayField } : new[] { "m_Script" };
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
            }
            DrawPropertiesExcluding(serializedObject, excluded);

            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// With several handlers selected, the field is hidden only if it is useless for every one of them.
        /// </summary>
        private bool AllTargetsOnlyUsedByManualPlayers()
        {
#if UNITY_2022_2_OR_NEWER
            var players = Object.FindObjectsByType<ArcweavePlayer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            var players = Object.FindObjectsOfType<ArcweavePlayer>(true);
#endif
            foreach (var handler in targets)
            {
                if (!OnlyUsedByManualPlayers(handler, players))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// True when at least one ArcweavePlayer in the open scenes uses the handler and all of them are in Manual mode.
        /// With no player using it the mode is unknown, so the field stays visible.
        /// </summary>
        private static bool OnlyUsedByManualPlayers(Object handlerObject, ArcweavePlayer[] players)
        {
            var usedByAny = false;
            foreach (var player in players)
            {
                // saveHandler is private, so it is read through the serialized property.
                var handler = new SerializedObject(player).FindProperty("saveHandler");
                if (handler == null || handler.objectReferenceValue != handlerObject)
                {
                    continue;
                }

                usedByAny = true;
                if (player.saveMode != SaveMode.Manual)
                {
                    return false;
                }
            }
            return usedByAny;
        }
    }
}

#endif
