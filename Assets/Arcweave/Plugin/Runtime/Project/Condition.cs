using Arcweave.Interpreter;
using Arcweave.Interpreter.INodes;
using UnityEngine;

namespace Arcweave.Project
{
    //Technically a node too
    [System.Serializable]
    public class Condition : INode
    {
        [field: SerializeField]
        public string Id { get; private set; }
        [field: SerializeField]
        public Vector2Int Pos { get; private set; }
        [field: SerializeField]
        public string Script { get; private set; }
        [field: SerializeField]
        public Connection Output { get; private set; }

        // private System.Func<Project, bool> runtimeScript { get; set; }
        public Project Project { get; private set; }

        void INode.InitializeInProject(Project project) { Project = project; }
        Path INode.ResolvePath(Path p) {
            //Remark: connections are not serialized by reference thus unity makes an instance and can't be null.
            //Therefore we check if the connection is actually valid if it has an id.
            return Output != null && !string.IsNullOrEmpty(Output.Id) ? Output.ResolvePath(p) : Path.Invalid;
        }

        internal void Set(string id, Connection output, string script) {
            Id = id;
            Output = output;
            Script = string.IsNullOrEmpty(script) ? string.Empty : string.Format("<pre><code>{0}</code></pre>", script);
        }

        ///----------------------------------------------------------------------------------------------

        ///<summary>Evaluates the condition (invalid scripts return True)</summary>
        public bool Evaluate() {
            if ( string.IsNullOrEmpty(Script) ) {
                return true;
            }
            
            return (bool)( new AwInterpreter(Project).RunScript(Script).Result );
        }
    }
}