using System.Collections.Generic;
using UnityEngine;

namespace Arcweave
{
    //...
    [System.Serializable]
    public class Branch : INode
    {
        [field: SerializeField]
        public string id { get; private set; }
        [field: SerializeField]
        public Vector2Int pos { get; private set; }
        [field: SerializeReference]
        public List<Condition> conditions { get; private set; }
        [field: SerializeField]
        public string colorTheme { get; private set; }

        public Project project { get; private set; }

        void INode.InitializeInProject(Project project) { this.project = project; }
        Path INode.ResolvePath(Path p) {
            var condition = GetTrueCondition();
            return condition != null ? ( condition as INode ).ResolvePath(p) : Path.Invalid;
        }

        internal void Set(string id, Vector2Int pos, List<Condition> conditions, string colorTheme) {
            this.id = id;
            this.pos = pos;
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
        public Connection GetTrueConditionOutput() => GetTrueCondition()?.output;
    }
}