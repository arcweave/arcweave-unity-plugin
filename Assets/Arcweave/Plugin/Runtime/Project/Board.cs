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
        /* Custom id used to distinguish between different board variables */
        public string CustomId { get; private set; }
        [field: SerializeField]
        public string Name { get; private set; }
        [field: SerializeReference]
        public List<INode> Nodes { get; private set; }
        [field: SerializeField]
        public List<Note> Notes { get; private set; }

        [field: SerializeField]
        public List<Variable> Variables { get; private set; }

        public Board(string id, string customId, string name, List<INode> nodes, List<Note> notes, List<Variable> variables) {
            Id = id;
            CustomId = customId;
            Name = name;
            Nodes = nodes;
            Notes = notes;
            Variables = variables;
        }

        ///<summary>Returns INode of type T with id.</summary>
        public T NodeWithID<T>(string id) where T : INode => Nodes.OfType<T>().FirstOrDefault(x => x.Id == id);
        ///<summary>Returns Element with id.</summary>
        public Element ElementWithID(string id) => NodeWithID<Element>(id);

        public void PrintBoard()
        {
            Debug.Log($"Board: {Name} (id: {Id})");
            foreach (var node in Nodes)
            {
                Debug.Log($"  Node: {node.GetType().Name} (id: {node.Id})");
            }

            foreach (var variable in Variables)
            {
                Debug.Log($"  Variable: {variable.Name} (value: {variable.Value})");
            }
        }

    }
}