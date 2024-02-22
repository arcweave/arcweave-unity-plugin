using System.Collections.Generic;
using UnityEngine;

namespace Arcweave.Project
{
    //...
    [System.Serializable]
    public partial class Component
    {
        [field: SerializeField]
        public string Id { get; private set; }
        [field: SerializeField]
        public string Name { get; private set; }
        [field: SerializeField]
        public List<Attribute> Attributes { get; private set; }

        public void AddAttribute(Attribute attribute)
        {
            Attributes.Add(attribute);
        }

        [field: SerializeField]
        public Cover cover { get; private set; }

        public void Set(string id, string name, List<Attribute> attributes, Cover cover) {
            Id = id;
            Name = name;
            Attributes = attributes;
            this.cover = cover;
        }

        public Texture2D GetCoverImage() => cover?.ResolveImage();
    }
}