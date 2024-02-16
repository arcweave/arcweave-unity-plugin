using System.Collections.Generic;
using System.Linq;
using Arcweave.Interpreter.INodes;
using UnityEngine;

namespace Arcweave.Project
{
    ///<summary>The actual C# arcweave project</summary>
    [System.Serializable]
    public partial class Project
    {
        [field: UnityEngine.SerializeField]
        public string name { get; private set; }
        [field: UnityEngine.SerializeReference]
        public List<Board> boards { get; private set; }
        [field: UnityEngine.SerializeReference]
        public List<Component> components { get; private set; }
        [field: UnityEngine.SerializeReference]
        public List<Variable> Variables { get; private set; }

        [UnityEngine.SerializeField] private string _startingElementId;
        [System.NonSerialized] private Element _startingElement;

        public Element StartingElement {
            get { return _startingElement != null ? _startingElement : _startingElement = GetNodeWithID<Element>(_startingElementId); }
            set { _startingElementId = value.Id; _startingElement = value; }
        }

        public Project(string name, Element startingElement, List<Board> boards, List<Component> components, List<Variable> variables) {
            this.name = name;
            this.StartingElement = startingElement;
            this.boards = boards;
            this.components = components;
            Variables = variables;
        }

        ///----------------------------------------------------------------------------------------------

        ///<summary>Should be called once before using the project.</summary>
        public void Initialize() {
            ResetVariablesToDefaultValues();
            ResetVisits();
            foreach ( var board in boards ) {
                foreach ( var node in board.Nodes ) {
                    node.InitializeInProject(this);
                }
            }
        }

        ///----------------------------------------------------------------------------------------------

        ///<summary>Returns the number of visits of an element with id.</summary>
        public int Visits(string id) { return ElementWithId(id).Visits; }

        ///<summary>Reset the number of visits to 0 for all elements.</summary>
        public void ResetVisits() {
            foreach ( var board in boards ) {
                foreach ( var element in board.Nodes.OfType<Element>() ) {
                    element.Visits = 0;
                }
            }
        }

        ///----------------------------------------------------------------------------------------------

        ///<summary>Returns the Board with id.</summary>
        public Board BoardWithID(string id) => boards.FirstOrDefault(x => x.Id == id);
        ///<summary>Returns the Board with name.</summary>
        public Board BoardWithName(string name) => boards.FirstOrDefault(x => x.Name == name);
        ///<summary>Returns the Element with id.</summary>
        public Element ElementWithId(string id) => GetNodeWithID<Element>(id);

        ///<summary>Returns the INode of type T with id.</summary>
        public T GetNodeWithID<T>(string id) where T : INode {
            T result = default(T);
            foreach ( var board in boards ) {
                result = board.NodeWithID<T>(id);
                if ( result != null ) { return result; }
            }
            return result;
        }

        ///----------------------------------------------------------------------------------------------

        ///<summary>Returns the variable with name.</summary>
        public Variable GetVariable(string name) => Variables.First(x => x.Name == name);

        ///<summary>Sets the variable with name to a new value. Returns if variable exists in the first place.</summary>
        public bool SetVariable(string name, object value) {
            var variable = Variables.First(x => x.Name == name);
            if ( variable == null ) { return false; }
            variable.Value = value;
            return true;
        }

        ///----------------------------------------------------------------------------------------------

        ///<summary>Reset all variables to their default value.</summary>
        public void ResetVariablesToDefaultValues()
        {
            if (Variables != null)
                foreach (var variable in Variables)
                {
                    variable.ResetToDefaultValue();
                }
        }

        ///<summary>Returns a string of the saved variables that can be loaded later.</summary>
        public string SaveVariables() {
            var list = new List<string>();
            foreach ( var variable in Variables ) {
                list.Add(string.Format("{0}-{1}-{2}", variable.Name, variable.Value.ToString(), variable.Type.FullName));
            }
            var save = string.Join("|", list);
            return save;
        }

        ///<summary>Loads a previously saved string made with SaveVariables.</summary>
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