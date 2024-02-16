using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Arcweave.Project
{
    //...
    [System.Serializable]
    public class Note
    {
        [field: SerializeField]
        public Vector2Int Pos { get; private set; }
        [field: SerializeField]
        public string RawContent { get; private set; }
        [field: SerializeField]
        public string ColorTheme { get; private set; }

        internal void Set(string id, Vector2Int pos, string rawContent, string colorTheme) {
            Pos = pos;
            RawContent = rawContent;
            ColorTheme = colorTheme;
        }
    }
}