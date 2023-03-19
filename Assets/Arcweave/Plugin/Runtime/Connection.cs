using UnityEngine;

namespace Arcweave
{
    //...
    [System.Serializable]
    public class Connection
    {
        [field: SerializeField]
        public string id { get; private set; }
        [field: SerializeField]
        public string label { get; private set; }

        [field: SerializeReference]
        public INode source { get; private set; }
        [field: SerializeReference]
        public INode target { get; private set; }

        internal void Set(string id, string label, INode source, INode target) {
            this.id = id;
            this.label = label;
            this.source = source;
            this.target = target;
        }

        internal Path ResolvePath(Path p) {
            p.label = label;
            return target.ResolvePath(p);
        }
    }
}