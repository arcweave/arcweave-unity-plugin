using System.Collections.Generic;
using UnityEngine;

namespace Arcweave
{

    //...
    [System.Serializable]
    public class Component
    {

        //...
        [System.Serializable]
        public class Attribute
        {

            public enum DataType
            {
                Undefined,
                String,
                ComponentList,
            }

            [field: SerializeField]
            public string name { get; private set; }
            [field: SerializeField]
            public DataType type { get; private set; }

            [SerializeField]
            private string data_string;
            [SerializeReference]
            private List<Component> data_componentList;

            public object data {
                get
                {
                    if ( type == DataType.String ) { return data_string; }
                    if ( type == DataType.String ) { return data_componentList; }
                    return null;
                }
            }

            public void Set(string name, DataType type, object data) {
                this.name = name;
                this.type = type;
                if ( type == DataType.String ) { data_string = (string)data; }
                if ( type == DataType.ComponentList ) { data_componentList = (List<Component>)data; }
            }
        }

        [field: SerializeField]
        public string id { get; private set; }
        [field: SerializeField]
        public string name { get; private set; }
        [field: SerializeField]
        public List<Attribute> attributes { get; private set; }
        [field: SerializeField]
        public Cover cover { get; private set; }

        public void Set(string id, string name, List<Attribute> attributes, Cover cover) {
            this.id = id;
            this.name = name;
            this.attributes = attributes;
            this.cover = cover;
        }

        public Texture2D GetCoverImage() => cover?.ResolveImage();
    }
}