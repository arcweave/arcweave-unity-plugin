using System.Collections.Generic;
using UnityEngine;
using Arcweave.Interpreter.INodes;

namespace Arcweave.Project
{
    //...
    [System.Serializable]
    public partial class Branch : INode
    {
        [field: SerializeField]
        public string Id { get; private set; }
        [field: SerializeField]
        public Vector2Int Pos { get; private set; }
        [field: SerializeReference]
        public List<Condition> conditions { get; private set; }
        [field: SerializeField]
        public string colorTheme { get; private set; }

        public Project Project { get; private set; }

        void INode.InitializeInProject(Project project) { Project = project; }
        Path INode.ResolvePath(Path p) {
            var condition = GetTrueCondition();
            return condition != null ? ( condition as INode ).ResolvePath(p) : Path.Invalid;
        }

        internal void Set(string id, Vector2Int pos, List<Condition> conditions, string colorTheme) {
            Id = id;
            Pos = pos;
            this.conditions = conditions;
            this.colorTheme = colorTheme;
        }

        ///<summary>Returns the true condition.</summary>
        Condition GetTrueCondition() {
            foreach ( var condition in conditions ) {
                if ( condition.Evaluate() ) { return condition; }
            }
            return null;
        }

        ///<summary>Returns the true condition output connection.</summary>
        // public Connection GetTrueConditionOutput() => GetTrueCondition()?.output;
    }
}