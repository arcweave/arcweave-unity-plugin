using System.Collections.Generic;
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

        [System.NonSerialized] private List<Connection> inputs;

        ///<summary>The connections that end at this jumper, in the order of the boards and of their source's outputs.
        /// Like Outputs, they are the connections themselves: the Source of an input is the element or branch condition it starts from,
        /// and the same connection is in its source's Outputs. Connections that end at a jumper are inputs of the jumper, not of the element it jumps to.</summary>
        public IReadOnlyList<Connection> Inputs {
            get {
                if ( inputs == null && Project != null ) { Project.LinkInputs(); }
                return inputs ?? (IReadOnlyList<Connection>)System.Array.Empty<Connection>();
            }
        }

        internal void SetInputs(List<Connection> inputs) => this.inputs = inputs;

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