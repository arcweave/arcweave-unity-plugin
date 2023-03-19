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

        public Board(string id, string name, List<INode> nodes) {
            this.id = id;
            this.name = name;
            this.nodes = nodes;
        }

        public T NodeWithID<T>(string id) where T : INode => nodes.OfType<T>().FirstOrDefault(x => x.id == id);
        public Element ElementWithID(string id) => NodeWithID<Element>(id);
    }
}