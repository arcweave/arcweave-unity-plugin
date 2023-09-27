using UnityEngine;

namespace Arcweave
{
    //...
    [System.Serializable]
    public class Jumper : INode
    {
        [field: SerializeField]
        public string id { get; private set; }
        [field: SerializeField]
        public Vector2Int pos { get; private set; }
        [field: SerializeReference]
        public Element target { get; private set; }

        public Project project { get; private set; }

        void INode.InitializeInProject(Project project) { this.project = project; }
        Path INode.ResolvePath(Path p) => target != null ? ( target as INode ).ResolvePath(p) : Path.Invalid;

        internal void Set(string id, Vector2Int pos, Element target) {
            this.id = id;
            this.pos = pos;
            this.target = target;
        }
    }
}