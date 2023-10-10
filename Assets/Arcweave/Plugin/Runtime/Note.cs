using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Arcweave
{
    //...
    [System.Serializable]
    public class Note
    {
        [field: SerializeField]
        public Vector2Int pos { get; private set; }
        [field: SerializeField]
        public string rawContent { get; private set; }
        [field: SerializeField]
        public string colorTheme { get; private set; }

        internal void Set(string id, Vector2Int pos, string rawContent, string colorTheme) {
            this.pos = pos;
            this.rawContent = rawContent;
            this.colorTheme = colorTheme;
        }
    }
}