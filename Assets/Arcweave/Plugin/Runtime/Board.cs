using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace Arcweave
{
    //...
    [System.Serializable]
    public class Board
    {
        [field: SerializeField]
        public string id { get; private set; }
        [field: SerializeField]
        public string name { get; private set; }
        [field: SerializeReference]
        public List<INode> nodes { get; private set; }
        [field: SerializeField]
        public List<Note> notes { get; private set; }

        public Board(string id, string name, List<INode> nodes, List<Note> notes) {
            this.id = id;
            this.name = name;
            this.nodes = nodes;
            this.notes = notes;
        }

        ///<summary>Returns INode of type T with id.</summary>
        public T NodeWithID<T>(string id) where T : INode => nodes.OfType<T>().FirstOrDefault(x => x.id == id);
        ///<summary>Returns Element with id.</summary>
        public Element ElementWithID(string id) => NodeWithID<Element>(id);
    }
}