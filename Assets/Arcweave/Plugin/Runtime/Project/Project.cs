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
        public Variable GetVariable(string name, string customId = null)
        {
            if (string.IsNullOrEmpty(customId))
            {
                return Variables.FirstOrDefault(variable => variable.Name == name);
            }

            var board = Boards.FirstOrDefault(board => board.CustomId == customId);
            if (board != null)
            {
                return board.Variables.FirstOrDefault(variable => variable.Name == name);
            }
            return null;
        }

        /// <summary>
        /// Sets the value of a global variable by its name.
        /// </summary>
        /// <param name="name">The name of the variable to set (e.g., "health").</param>
        /// <param name="value">The new value to assign to the variable.</param>
        /// <returns>True if the variable was found and updated successfully; otherwise, false.</returns>
        public bool SetVariable(string name, object value) 
        {
            // set variable also checking in the boards 
            var variable = Variables.FirstOrDefault(x => x.Name == name);
            if (variable == null)
            {
                Debug.LogError($"Variable with ID '{name}' not found.");
                return false; // or handle appropriately based on method return type
            }
            variable.Value = value;
            return true;
        }

        ///----------------------------------------------------------------------------------------------

        public bool SetVariableById(string id, object value)
        {
            // set variable also checking in the boards 
            var variable = Variables.FirstOrDefault(x => x.Id == id);

            // If not found in Variables, look in the boards
            if (variable == null)
            {
                foreach (var board in Boards)
                {
                    variable = board.Variables?.FirstOrDefault(x => x.Id == id);
                    if (variable != null)
                    {
                        break;
                    }
                }
            }

            // If still not found, log error and return false
            if (variable == null)
            {
                Debug.LogError($"Variable with ID '{id}' not found.");
                return false;
            }

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