using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Arcweave.Interpreter.INodes;

namespace Arcweave.Project
{
    //...
    [System.Serializable]
    public partial class Board
    {
        [field: SerializeField]

        /* Hash key related to this board (e.g "1c11fa2a-f76c-4be7-aac4-6bf92a63240f") */
        public string Id { get; private set; }
        [field: SerializeField]
        
        /* Name string id (e.g healer dialogue) */
        public string CustomId { get; private set; }
        [field: SerializeField]
        public string Name { get; private set; }
        [field: SerializeReference]
        public List<INode> Nodes { get; private set; }
        [field: SerializeField]
        public List<Note> Notes { get; private set; }
        [field: SerializeField]
        public List<Variable> Variables { get; private set; }

        /// <summary>
        /// Initializes a board with an optional Arcweave custom id used for board-scoped variables.
        /// </summary>
        public Board(string id, string name, string customId, List<INode> nodes)
        {
            Id = id;
            Name = name;
            Nodes = nodes;
            CustomId = customId;
            Variables = new List<Variable>();
        }
        
        /// <summary>
        /// Initializes a board with notes and an optional Arcweave custom id used for board-scoped variables.
        /// </summary>
        public Board(string id, string name, string customId, List<INode> nodes, List<Note> notes) {
            Id = id;
            CustomId = customId;
            Name = name;
            Nodes = nodes;
            Notes = notes;
            Variables = new List<Variable>();
        }
        
        /// <summary>
        /// Initializes a board with pre-created scoped variables and an optional Arcweave custom id.
        /// </summary>
        public Board(string id, string name, string customId, List<INode> nodes, List<Variable> variables)
        {
            Id = id;
            Name = name;
            Nodes = nodes;
            foreach (var variable in variables)
            {
                variable.Parent = this;
            }
            Variables = variables;
            CustomId = customId;
        }

        ///<summary>Returns INode of type T with id.</summary>
        public T NodeWithID<T>(string id) where T : INode => Nodes.OfType<T>().FirstOrDefault(x => x.Id == id);
        ///<summary>Returns Element with id.</summary>
        public Element ElementWithID(string id) => NodeWithID<Element>(id);

        /// <summary>
        /// Adds a board-scoped variable and assigns this board as its parent scope.
        /// </summary>
        public void AddVariable(Variable variable)
        {
            variable.Parent = this;
            Variables.Add(variable);
        }
    }
}
