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
        public string CustomId { get; private set; }
        [field: SerializeField]
        public string Name { get; private set; }
        [field: SerializeField]
        public List<Attribute> Attributes { get; private set; }
        [field: SerializeField]
        public List<Variable> Variables { get; private set; }

        public void AddAttribute(Attribute attribute)
        {
            Attributes.Add(attribute);
        }

        public void AddVariable(Variable variable)
        {
            variable.Parent = this;
            Variables.Add(variable);
        }

        public void InitializeInProject(Project project)
        {
            foreach (var attribute in Attributes)
            {
                attribute.InitializeInProject(project);
            }
        }

        [field: SerializeField]
        public Cover cover { get; private set; }

        public void Set(string id, string customId, string name, List<Attribute> attributes, Cover cover) {
            Id = id;
            CustomId = customId;
            Name = name;
            Attributes = attributes;
            Variables = new List<Variable>();
            this.cover = cover;
        }

        public Texture2D GetCoverImage() => cover?.ResolveImage();
    }
}
