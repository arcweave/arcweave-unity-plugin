using Arcweave.Transpiler;
using UnityEngine;

namespace Arcweave
{
    //Technically a node too
    [System.Serializable]
    public class Condition : INode
    {
        [field: SerializeField]
        public string id { get; private set; }
        [field: SerializeField]
        public Vector2Int pos { get; private set; }
        [field: SerializeField]
        public string script { get; private set; }
        [field: SerializeField]
        public Connection output { get; private set; }

        public Project project { get; private set; }

        void INode.InitializeInProject(Project project) { this.project = project; }
        Path INode.ResolvePath(Path p) {
            //Remark: connections are not serialized by reference thus unity makes an instance and can't be null.
            //Therefore we check if the connection is actually valid if it has an id.
            return output != null && !string.IsNullOrEmpty(output.id) ? output.ResolvePath(p) : Path.Invalid;
        }

        internal void Set(string id, Connection output, string script) {
            this.id = id;
            this.output = output;
            this.script = string.IsNullOrEmpty(script) ? string.Empty : string.Format("<pre><code>{0}</code></pre>", script);
        }

        ///----------------------------------------------------------------------------------------------

        ///<summary>Evaluates the condition (invalid scripts return True)</summary>
        public bool Evaluate() {
            if ( string.IsNullOrEmpty(script) ) {
                return true;
            }

            return (bool)new Interpreter(project).RunScript(script).result;
        }
    }
}