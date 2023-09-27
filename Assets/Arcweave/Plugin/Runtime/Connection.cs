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

        public bool isValid => !string.IsNullOrEmpty(id);
        public Project project => source.project;
        // private System.Func<Project, string> runtimeLabelFunc { get; set; }

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
            // if ( runtimeLabelFunc == null ) {
            //     var methodName = "Connection_" + id.Replace("-", "_").ToString();
            //     var methodInfo = typeof(ArcscriptImplementations).GetMethod(methodName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            //     Debug.Assert(methodInfo != null);
            //     runtimeLabelFunc = (System.Func<Project, string>)System.Delegate.CreateDelegate(typeof(System.Func<Project, string>), null, methodInfo);
            // }
            // return Utils.CleanString(runtimeLabelFunc(project));

            var i = new Interpreter(project);
            var output = i.RunScript(rawLabel);
            foreach ( var chage in output.changes ) {
                // set variables
                // Debug.Log(chage.Key);
                // Debug.Log(chage.Value);
            }
            return output.output;
        }
    }
}