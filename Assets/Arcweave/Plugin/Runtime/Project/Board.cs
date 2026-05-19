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
        public string Id { get; private set; }
        [field: SerializeField]
        public string CustomId { get; }
        [field: SerializeField]
        public string Name { get; private set; }
        [field: SerializeReference]
        public List<INode> Nodes { get; private set; }
        [field: SerializeField]
        public List<Note> Notes { get; private set; }
        [field: SerializeField]

        public List<Variable> Variables { get; private set; }

        public Board(string id, List<INode> nodes, string customId = null)
        {
            Id = id;
            Nodes = nodes;
            CustomId = customId;
            Variables = new List<Variable>();
        }

        /* Old constructor for backward compatibility */
        public Board(string id, string name, List<INode> nodes, List<Note> notes) {
            Id = id;
            Name = name;
            Nodes = nodes;
            Notes = notes;
        }

        public Board(string id, List<INode> nodes, List<Variable> variables, string customId = null)
        {
            Id = id;
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

        public void AddVariable(Variable variable)
        {
            variable.Parent = this;
            Variables.Add(variable);
        }
    }
}