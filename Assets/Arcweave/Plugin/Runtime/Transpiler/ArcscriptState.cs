using System.Collections.Generic;
using System.Linq;

namespace Arcweave.Transpiler
{
    public class ArcscriptState
    {
        public Dictionary<string, object> VariableChanges = new Dictionary<string, object>();
        public List<string> outputs = new List<string>();
        public string currentElement { get; set; }
        public Project project { get; set; }
        public ArcscriptState(string elementId, Project project) {
            this.currentElement = elementId;
            this.project = project;
        }

        public Variable GetVariable(string name) {
            return this.project.variables.First(variable => variable.name == name);
        }

        public object GetVarValue(string name) {
            if ( this.VariableChanges.ContainsKey(name) ) {
                return VariableChanges[name];
            }
            return this.project.GetVariable(name);
        }

        public void SetVarValue(string name, object value) { VariableChanges[name] = value; }

        public void SetVarValues(string[] names, string[] values) {
            for ( int i = 0; i < names.Length; i++ ) {
                this.VariableChanges[names[i]] = values[i];
            }
        }
    }
}