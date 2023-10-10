using Arcweave.Transpiler;
using UnityEngine;

namespace Arcweave
{
    //...
    [System.Serializable]
    public class Connection
    {
        [field: SerializeField]
        public string id { get; private set; }
        [field: SerializeField]
        public string rawLabel { get; private set; }
        [field: SerializeReference]
        public INode source { get; private set; }
        [field: SerializeReference]
        public INode target { get; private set; }

        public string displayLabel => Utils.CleanString(rawLabel);
        public bool isValid => !string.IsNullOrEmpty(id);
        public Project project => source.project;

        internal void Set(string id, string rawLabel, INode source, INode target) {
            this.id = id;
            this.rawLabel = rawLabel;
            this.source = source;
            this.target = target;
        }

        internal Path ResolvePath(Path p) {
            p.AppendConnection(this);
            p.label = GetRuntimeLabel();
            return target.ResolvePath(p);
        }

        ///----------------------------------------------------------------------------------------------

        ///<summary>Returns the runtime label taking into account and executing arcscript</summary>
        public string GetRuntimeLabel() {
            if ( string.IsNullOrEmpty(rawLabel) ) {
                return null;
            }

            var i = new Interpreter(project);
            var output = i.RunScript(rawLabel);
            if ( output.changes.Count > 0 ) {
                foreach ( var chage in output.changes ) {
                    project.SetVariable(chage.Key, chage.Value);
                }
            }
            return output.output;
        }
    }
}