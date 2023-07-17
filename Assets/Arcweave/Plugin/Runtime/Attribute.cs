using System.Collections.Generic;
using UnityEngine;

namespace Arcweave
{
    //...
    [System.Serializable]
    public class Attribute
    {

        public enum DataType
        {
            Undefined,
            StringPlainText,
            StringRichText,
            ComponentList,
        }

        public enum ContainerType
        {
            Undefined,
            Component,
            Element,
        }

        [field: SerializeField]
        public string name { get; private set; }
        [field: SerializeField]
        public DataType type { get; private set; }

        [field: SerializeField]
        public ContainerType containerType { get; private set; }
        [field: SerializeField]
        public string containerId { get; private set; }

        [SerializeField]
        private string data_string;
        [SerializeReference]
        private List<Component> data_componentList;

        public object data {
            get
            {
                if ( type == DataType.StringPlainText || type == DataType.StringRichText ) { return data_string; }
                if ( type == DataType.ComponentList ) { return data_componentList; }
                return null;
            }
        }

        internal void Set(string name, DataType type, object data, ContainerType containerType, string containerId) {
            this.name = name;
            this.type = type;
            if ( type == DataType.StringPlainText || type == DataType.StringRichText ) { data_string = (string)data; }
            if ( type == DataType.ComponentList ) { data_componentList = (List<Component>)data; }
            this.containerType = containerType;
            this.containerId = containerId;
        }
    }
}