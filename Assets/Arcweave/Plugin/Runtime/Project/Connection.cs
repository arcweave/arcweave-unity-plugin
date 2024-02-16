using Arcweave.Interpreter;
using Arcweave.Interpreter.INodes;
using UnityEngine;

namespace Arcweave.Project
{
    //...
    [System.Serializable]
    public partial class Connection
    {
        [field: SerializeField]
        public string Id { get; private set; }
        [field: SerializeField]
        public string RawLabel { get; private set; }

        [field: SerializeField]
        public string RuntimeLabel { get; private set; }
        
        [field: SerializeReference]
        public INode Source { get; private set; }
        [field: SerializeReference]
        public INode Target { get; private set; }

        public bool isValid => !string.IsNullOrEmpty(Id);
        public Project Project => Source.Project;
        // private System.Func<Project, string> runtimeLabelFunc { get; set; }

        public Connection(string id)
        {
            Id = id;
        }
        
        public void Set(string rawLabel, INode source, INode target) {
            RawLabel = rawLabel;
            Source = source;
            Target = target;
        }

        public Path ResolvePath(Path p) {
            p.AppendConnection(this);
            RunLabelScript();
            if (RuntimeLabel != null)
            {
                p.label = RuntimeLabel;
            }
            return Target.ResolvePath(p);
        }

        ///----------------------------------------------------------------------------------------------
        
        /// <summary>
        /// Runs the label's script. This will also update the connection's
        /// RuntimeLabel.
        /// </summary>
        public void RunLabelScript()
        {
            if (string.IsNullOrEmpty(RawLabel))
            {
                RuntimeLabel = null;
                return;
            }

            AwInterpreter i = new AwInterpreter(Project);
            var output = i.RunScript(RawLabel);
            if ( output.Changes.Count > 0 ) {
                foreach ( var change in output.Changes ) {
                    Project.SetVariable(change.Key, change.Value);
                }
            }

            RuntimeLabel = output.Output;
        }
    }
}