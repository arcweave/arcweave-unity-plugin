using System.Collections.Generic;
using UnityEngine;

namespace Arcweave
{
    //...
    [System.Serializable]
    public class Component
    {
        [field: SerializeField]
        public string id { get; private set; }
        [field: SerializeField]
        public string name { get; private set; }
        [field: SerializeField]
        public List<Attribute> attributes { get; private set; }
        [field: SerializeField]
        public Cover cover { get; private set; }

        public void Set(string id, string name, List<Attribute> attributes, Cover cover) {
            this.id = id;
            this.name = name;
            this.attributes = attributes;
            this.cover = cover;
        }

        public Texture2D GetCoverImage() => cover?.ResolveImage();
    }
}