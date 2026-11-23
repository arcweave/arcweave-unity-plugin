using Arcweave.Interpreter.INodes;
using System.Collections.Generic;
using System.Linq;
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
        public List<Component> Components => components;
        ///<summary>Project-level variables only. Use GetAllVariables to include board and component scopes.</summary>
        [field: FormerlySerializedAs("<Variables>k__BackingField")]
        [field: UnityEngine.SerializeReference]
        public List<Variable> GlobalVariables { get; private set; }

        ///<summary>Compatibility alias for GlobalVariables.</summary>
        [System.Obsolete("Use GlobalVariables for globals, or GetAllVariables() for every scope.")]
        public List<Variable> Variables => GlobalVariables;

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
            GlobalVariables = variables;
            LinkInputs();
        }

        ///<summary>Sets the Inputs of every element, branch and jumper from the outputs of the elements and branch conditions on the boards.
        /// Inputs are not serialized, so they are linked again when the project is made and when it is initialized.</summary>
        internal void LinkInputs() {
            var inputs = new Dictionary<INode, List<Connection>>();
            void Add(Connection connection) {
                // Unity deserializes a missing connection as an instance without an id
                if ( connection == null || !connection.isValid || connection.Target == null ) { return; }
                if ( !inputs.TryGetValue(connection.Target, out var list) ) {
                    inputs[connection.Target] = list = new List<Connection>();
                }
                if ( !list.Contains(connection) ) { list.Add(connection); }
            }

            if ( Boards == null ) { return; }
            foreach ( var board in Boards ) {
                if ( board?.Nodes == null ) { continue; }
                foreach ( var node in board.Nodes ) {
                    if ( node is Element element && element.Outputs != null ) {
                        foreach ( var output in element.Outputs ) { Add(output); }
                    } else if ( node is Condition condition ) {
                        Add(condition.Output);
                    }
                }
            }

            // Nodes without inputs get an empty list, so they are not linked again each time their Inputs are read
            foreach ( var board in Boards ) {
                if ( board?.Nodes == null ) { continue; }
                foreach ( var node in board.Nodes ) {
                    if ( node != null && !inputs.ContainsKey(node) ) { SetInputs(node, new List<Connection>()); }
                }
            }
            foreach ( var pair in inputs ) {
                SetInputs(pair.Key, pair.Value);
            }
        }

        static void SetInputs(INode node, List<Connection> inputs) {
            if ( node is Element element ) { element.SetInputs(inputs); }
            else if ( node is Branch branch ) { branch.SetInputs(inputs); }
            else if ( node is Jumper jumper ) { jumper.SetInputs(inputs); }
        }

        ///----------------------------------------------------------------------------------------------

        ///<summary>Should be called once before using the project.</summary>
        public void Initialize() {
            ResetVariablesToDefaultValues();
            ResetVisits();
            LinkInputs();

            foreach ( var board in Boards ) {
                board.InitializeInProject(this);
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



        ///----------------------------------------------------------------------------------------------

        ///<summary>Enumerates the image covers that use an Arcweave asset, of elements, their components and project components.</summary>
        internal IEnumerable<Cover> GetAssetCovers() {
            var elements = Boards.SelectMany(board => board.Nodes.OfType<Element>()).ToList();
            return elements.Select(element => element.cover)
                .Concat(elements.SelectMany(element => element.Components ?? new List<Component>()).Select(component => component.cover))
                .Concat((Components ?? new List<Component>()).Select(component => component.cover))
                .Where(cover => cover != null && cover.type == Cover.Type.Image && !string.IsNullOrEmpty(cover.assetId) && !string.IsNullOrEmpty(cover.filePath));
        }

        ///<summary>Enumerates the audio assets of elements that use an Arcweave asset.</summary>
        internal IEnumerable<AudioAsset> GetAssetAudio() {
            return Boards.SelectMany(board => board.Nodes.OfType<Element>())
                .SelectMany(element => element.AudioAssets ?? new AudioAsset[0])
                .Where(audioAsset => audioAsset != null && !string.IsNullOrEmpty(audioAsset.asset) && !string.IsNullOrEmpty(audioAsset.name));
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

        ///<summary>Returns a variable by name. Provide a board or component CustomId in <paramref name="scope"/> to search scoped variables; otherwise only global variables are searched.</summary>
        public Variable GetVariable(string name, string scope = null)
        {
            if (scope == null)
            {
                return GlobalVariables.FirstOrDefault(variable => variable.Name == name);
            }
            var container = Boards.Cast<IHasVariables>()
                .Concat(Components)
                .FirstOrDefault(candidate => candidate.CustomId == scope);
            return container?.Variables.FirstOrDefault(variable => variable.Name == name);
        }

        ///<summary>Enumerates every global, board-scoped, and component-scoped variable.</summary>
        public IEnumerable<Variable> GetAllVariables()
        {
            return (GlobalVariables ?? new List<Variable>())
                .Concat((Boards ?? new List<Board>()).SelectMany(board => board.Variables ?? new List<Variable>()))
                .Concat((Components ?? new List<Component>()).SelectMany(component => component.Variables ?? new List<Variable>()));
        }

        /// <summary>
        /// Sets the value of a global variable by its name.
        /// </summary>
        /// <param name="name">The name of the variable to set (e.g., "health").</param>
        /// <param name="value">The new value to assign to the variable.</param>
        /// <returns>True if the variable was found and updated successfully; otherwise, false.</returns>
        public bool SetVariable(string name, object value) 
        {
            var variable = GetVariable(name);
            if (variable == null)
            {
                Debug.LogError($"Global variable with name '{name}' not found.");
                return false;
            }
            variable.Value = value;
            return true;
        }

        /// <summary>
        /// Sets the value of a board- or component-scoped variable by its name and owner CustomId.
        /// </summary>
        /// <param name="name">The variable name inside the owner scope.</param>
        /// <param name="scope">The board or component CustomId used as the variable scope.</param>
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
            var variable = GetAllVariables().FirstOrDefault(x => x.Id == id);

            if (variable == null)
            {
                Debug.LogError($"Variable with ID '{id}' not found.");
                return false;
            }
            variable.Value = value;
            return true;
        }

        ///----------------------------------------------------------------------------------------------

        ///<summary>Reset all global, board-scoped, and component-scoped variables to their default values.</summary>
        public void ResetVariablesToDefaultValues()
        {
            foreach (var variable in GetAllVariables())
            {
                variable.ResetToDefaultValue();
            }
        }

        ///<summary>Returns a string containing the saved state of all global and scoped variables.</summary>
        public string SaveVariables()
        {
            var state = new State(GetAllVariables());
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
                if ( type == typeof(int) ) { value = int.Parse(variableState.value, System.Globalization.CultureInfo.InvariantCulture); }
                if ( type == typeof(double) ) { value = double.Parse(variableState.value, System.Globalization.CultureInfo.InvariantCulture); }
                if ( type == typeof(bool) ) { value = bool.Parse(variableState.value); }

                SetVariableById(variableState.id, value);
            }
        }


#region Visits
        ///<summary>Returns the number of visits of an element with id.</summary>
        public int Visits(string id) { return ElementWithId(id).Visits; }

        ///<summary>Reset the number of visits to 0 for all elements.</summary>
        public void ResetVisits()
        {
            if (Boards == null)
            {
                Debug.LogWarning("Boards list is null. Cannot reset visits.");
                return;
            }

            foreach (var board in Boards)
            {
                foreach (var element in board.Nodes.OfType<Element>())
                {
                    element.Visits = 0;
                }
            }
        }

        public string SaveVisits()
        {
            var visitDictionary = new Dictionary<string, int>();
            CollectVisits(visitDictionary);

            ArcweaveVisitsState visitsState = new ArcweaveVisitsState(visitDictionary);
            return visitsState.ToJson();
        }

        ///<summary>Adds the visit count of every visited element (count above 0) to the dictionary.</summary>
        private void CollectVisits(Dictionary<string, int> visitDictionary)
        {
            foreach (var board in Boards)
            {
                foreach (var element in board.Nodes.OfType<Element>())
                {
                    if (element.Visits > 0)
                    {
                        visitDictionary[element.Id] = element.Visits;
                    }
                }
            }
        }

        public void LoadVisits(string visitsSave)
        {
            // Clear all visits first, even when the snapshot is empty: elements with 0 visits are not
            // saved, so any count made after the checkpoint must not survive the load.
            ResetVisits();

            ArcweaveVisitsState visits = ArcweaveVisitsState.FromJson(visitsSave);
            ArcweaveVisitsState.VisitsState[] visitStates = visits?.GetVisits();

            if (visitStates == null || visitStates.Length == 0)
            {
                if (Debug.isDebugBuild)
                {
                    Debug.Log("[Project] No visit data to restore");
                }
                return;
            }

            if (Debug.isDebugBuild)
            {
                Debug.Log($"[Project] Loading {visitStates.Length} visit counts");
            }

            foreach (var visitState in visitStates)
            {
                var element = ElementWithId(visitState.elementId);
                if (element != null)
                {
                    element.Visits = visitState.nVisit;
                }
                else
                {
                    Debug.LogWarning($"[Project] Could not find element with ID '{visitState.elementId}' to restore visits");
                }
            }
        }
#endregion Visits

#region Snapshots

        ///<summary>
        /// Captures the current variable values and visit counts into the snapshot, without serializing them.
        /// The snapshot is cleared first, so the same instance can be reused. Turn it into save strings with
        /// SaveVariables(snapshot) and SaveVisits(snapshot) only when a save is actually made.
        ///</summary>
        public void CaptureState(StateSnapshot snapshot)
        {
            snapshot.Clear();
            foreach (var variable in GetAllVariables())
            {
                // Variable values are strings, ints, doubles or bools, which never change in place,
                // so keeping the reference is enough.
                snapshot.variables.Add(variable);
                snapshot.values.Add(variable.Value);
            }
            CollectVisits(snapshot.visits);
            snapshot.project = this;
        }

        ///<summary>Same as SaveVariables(), but with the values held by a snapshot captured from this project.</summary>
        public string SaveVariables(StateSnapshot snapshot)
        {
            EnsureSnapshotOfThisProject(snapshot);
            return new State(snapshot.variables, snapshot.values).ToJson();
        }

        ///<summary>Same as SaveVisits(), but with the visit counts held by a snapshot captured from this project.</summary>
        public string SaveVisits(StateSnapshot snapshot)
        {
            EnsureSnapshotOfThisProject(snapshot);
            return new ArcweaveVisitsState(snapshot.visits).ToJson();
        }

        private void EnsureSnapshotOfThisProject(StateSnapshot snapshot)
        {
            if (snapshot == null || snapshot.project != this)
            {
                throw new System.ArgumentException("The snapshot was not captured from this project.", nameof(snapshot));
            }
        }
#endregion Snapshots

    }// end of class

}// end of namespace
