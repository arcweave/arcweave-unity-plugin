using UnityEngine;

namespace Arcweave
{
    //...
    [System.Serializable]
    public class Jumper : INode
    {
        [field: SerializeField]
        public string id { get; private set; }
        [field: SerializeReference]
        public Element target { get; private set; }

        void INode.InitializeInProject(Project project) { }
        Path INode.ResolvePath(Path p) => target != null ? ( target as INode ).ResolvePath(p) : Path.Invalid;

        internal void Set(string id, Element target) {
            this.id = id;
            this.target = target;
        }
    }
}