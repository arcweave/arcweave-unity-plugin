using System.Collections.Generic;
using System.Linq;
using Arcweave.Interpreter.INodes;
using UnityEngine;
using UnityEngine.Serialization;

namespace Arcweave.Project
{
    ///<summary>The actual C# arcweave project</summary>
    [System.Serializable]
    public partial class Project
    {
        [field: UnityEngine.SerializeField]
        public string name { get; private set; }
        [field: FormerlySerializedAs("<boards>k__BackingField")]
        [field: UnityEngine.SerializeReference]
        public List<Board> Boards { get; private set; }
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
            this.Boards = boards;
            this.components = components;
            Variables = variables;
        }

        ///----------------------------------------------------------------------------------------------

        ///<summary>Should be called once before using the project.</summary>
        public void Initialize() {
            ResetVariablesToDefaultValues();
            ResetVisits();
            foreach ( var board in Boards ) {
                foreach ( var node in board.Nodes ) {
                    node.InitializeInProject(this);
                }
            }

            foreach (var component in components)
            {
                component.InitializeInProject(this);
            }
        }

        ///----------------------------------------------------------------------------------------------

        ///<summary>Returns the number of visits of an element with id.</summary>
        public int Visits(string id) { return ElementWithId(id).Visits; }

        ///<summary>Reset the number of visits to 0 for all elements.</summary>
        public void ResetVisits() {
            foreach ( var board in Boards ) {
                foreach ( var element in board.Nodes.OfType<Element>() ) {
                    element.Visits = 0;
                }
            }
        }

        ///----------------------------------------------------------------------------------------------

        ///<summary>Returns the Board with id.</summary>
        public Board BoardWithID(string id) => Boards.FirstOrDefault(x => x.Id == id);
        ///<summary>Returns the Board with name.</summary>
        public Board BoardWithName(string name) => Boards.FirstOrDefault(x => x.Name == name);
        ///<summary>Returns the Element with id.</summary>
        public Element ElementWithId(string id) => GetNodeWithID<Element>(id);

        ///<summary>Returns the INode of type T with id.</summary>
        public T GetNodeWithID<T>(string id) where T : INode {
            T result = default(T);
            foreach ( var board in Boards ) {
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
        public string SaveVariables()
        {
            var state = new State(Variables);
            return state.ToJson();
        }

        ///<summary>Loads a previously saved string made with SaveVariables.</summary>
        public void LoadVariables(string save)
        {
            State.VariableState[] variableStates = State.FromJson(save).GetVariables();
            foreach (var variableState in variableStates)
            {
                var type = System.Type.GetType(variableState.type);
                object value = null;
                if (type == typeof(string)) { value = variableState.value; }
                if ( type == typeof(int) ) { value = int.Parse(variableState.value); }
                if ( type == typeof(float) ) { value = float.Parse(variableState.value); }
                if ( type == typeof(bool) ) { value = bool.Parse(variableState.value); }
                SetVariable(variableState.name, value);
            }
        }
    }
}