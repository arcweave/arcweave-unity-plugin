using UnityEngine;
using Arcweave.Interpreter.INodes;

namespace Arcweave.Project
{
    //...
    [System.Serializable]
    public class Jumper : INode
    {
        [field: SerializeField]
        public string Id { get; private set; }
        [field: SerializeField]
        public Vector2Int Pos { get; private set; }
        [field: SerializeReference]
        public Element Target { get; private set; }

        public Project Project { get; private set; }

        void INode.InitializeInProject(Project project) { Project = project; }
        Path INode.ResolvePath(Path p) => Target != null ? ( Target as INode ).ResolvePath(p) : Path.Invalid;

        internal void Set(string id, Vector2Int pos, Element target) {
            Id = id;
            Pos = pos;
            Target = target;
        }
    }
}