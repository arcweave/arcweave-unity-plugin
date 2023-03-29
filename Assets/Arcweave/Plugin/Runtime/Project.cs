using System.Collections.Generic;
using System.Linq;

namespace Arcweave
{
    ///The actual C# arcweave project
    [System.Serializable]
    public class Project
    {
        [field: UnityEngine.SerializeField]
        public string name { get; private set; }
        [field: UnityEngine.SerializeReference]
        public List<Board> boards { get; private set; }
        [field: UnityEngine.SerializeReference]
        public List<Component> components { get; private set; }
        [field: UnityEngine.SerializeReference]
        public List<Variable> variables { get; private set; }

        [UnityEngine.SerializeField] private string _startingElementId;
        [System.NonSerialized] private Element _startingElement;
        public Element startingElement {
            get { return _startingElement != null ? _startingElement : _startingElement = GetNodeWithID<Element>(_startingElementId); }
            set { _startingElementId = value.id; _startingElement = value; }
        }


        public Project(string name, Element startingElement, List<Board> boards, List<Component> components, List<Variable> variables) {
            this.name = name;
            this.startingElement = startingElement;
            this.boards = boards;
            this.components = components;
            this.variables = variables;
        }

        ///----------------------------------------------------------------------------------------------

        //...
        public void Initialize() {
            ResetVariablesToDefaultValues();
            foreach ( var board in boards ) {
                foreach ( var node in board.nodes ) {
                    node.InitializeInProject(this);
                }
            }
        }

        ///----------------------------------------------------------------------------------------------

        public Board BoardWithID(string id) => boards.FirstOrDefault(x => x.id == id);
        public Board BoardWithName(string name) => boards.FirstOrDefault(x => x.name == name);
        public Element ElementWithID(string id) => GetNodeWithID<Element>(id);

        //...
        public T GetNodeWithID<T>(string id) where T : INode {
            T result = default(T);
            foreach ( var board in boards ) {
                result = board.NodeWithID<T>(id);
                if ( result != null ) { return result; }
            }
            return result;
        }

        ///----------------------------------------------------------------------------------------------

        //...
        public T GetVariable<T>(string name) => (T)GetVariable(name);
        public object GetVariable(string name) => variables.First(x => x.name == name).value;

        //...
        public bool SetVariable(string name, object value) {
            var variable = variables.First(x => x.name == name);
            if ( variable == null ) { return false; }
            variable.value = value;
            return true;
        }

        ///----------------------------------------------------------------------------------------------

        //...
        public void ResetVariablesToDefaultValues() {
            foreach ( var variable in variables ) {
                variable.ResetToDefaultValue();
            }
        }

        ///Returns a string of the saved variables that can be loaded later.
        public string SaveVariables() {
            var list = new List<string>();
            foreach ( var variable in variables ) {
                list.Add(string.Format("{0}-{1}-{2}", variable.name, variable.value.ToString(), variable.type.FullName));
            }
            var save = string.Join("|", list);
            return save;
        }

        ///Loads a previously saved string made with SaveVariables.
        public void LoadVariables(string save) {
            var list = save.Split('|');
            foreach ( var s in list ) {
                var split = s.Split('-');
                var sName = split[0];
                var sValue = split[1];
                var sType = split[2];
                var type = System.Type.GetType(sType);
                object value = null;
                if ( type == typeof(string) ) { value = sValue; }
                if ( type == typeof(int) ) { value = int.Parse(sValue); }
                if ( type == typeof(float) ) { value = float.Parse(sValue); }
                if ( type == typeof(bool) ) { value = bool.Parse(sValue); }
                SetVariable(sName, value);
            }
        }
    }
}