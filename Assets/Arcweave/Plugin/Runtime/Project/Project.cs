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

        ///<summary>Returns a variable by name. Provide a board CustomId in <paramref name="scope"/> to search board-scoped variables; otherwise only global variables are searched.</summary>
        public Variable GetVariable(string name, string scope = null)
        {
            if (scope == null)
            {
                return Variables.FirstOrDefault(variable => variable.Name == name);
            }
            var board = Boards.FirstOrDefault(board => board.CustomId == scope);
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
            var variable = Variables.FirstOrDefault(x => x.Name == name);
            if (variable == null)
            {
                Debug.LogError($"Global variable with name '{name}' not found.");
                return false;
            }
            variable.Value = value;
            return true;
        }

        /// <summary>
        /// Sets the value of a board-scoped variable by its name and board CustomId.
        /// </summary>
        /// <param name="name">The variable name inside the board scope.</param>
        /// <param name="scope">The board CustomId used as the variable scope.</param>
        /// <param name="value">The new value to assign to the variable.</param>
        /// <returns>True if the scoped variable was found and updated successfully; otherwise, false.</returns>
        public bool SetVariable(string name, string scope, object value)
        {
            var variable = GetVariable(name, scope);
            if (variable == null)
            {
                Debug.LogError($"Variable with name '{name}' in scope '{scope}' not found.");
                return false;
            }

            variable.Value = value;
            return true;
        }

        ///----------------------------------------------------------------------------------------------

        /// <summary>
        /// Sets the value of a variable by Arcweave variable id.
        /// This is used internally when Arcscript applies state changes and when saved state is restored.
        /// </summary>
        public bool SetVariableById(string id, object value)
        {
            var variable = Variables.FirstOrDefault(x => x.Id == id);

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

            if (variable == null)
            {
                Debug.LogError($"Variable with ID '{id}' not found.");
                return false;
            }
            variable.Value = value;
            return true;
        }

        ///----------------------------------------------------------------------------------------------

        ///<summary>Reset all global and board-scoped variables to their default values.</summary>
        public void ResetVariablesToDefaultValues()
        {
            if (Variables != null)
                foreach (var variable in Variables)
                {
                    variable.ResetToDefaultValue();
                }
        }

        ///<summary>Returns a string containing the saved state of all global and board-scoped variables.</summary>
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
                if ( type == typeof(double) ) { value = double.Parse(variableState.value); }
                if ( type == typeof(bool) ) { value = bool.Parse(variableState.value); }

                SetVariableById(variableState.id, value);
            }
        }
    }
}