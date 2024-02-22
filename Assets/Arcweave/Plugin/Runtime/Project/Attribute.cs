using System.Collections.Generic;
using UnityEngine;
using Arcweave.Interpreter.INodes;

namespace Arcweave.Project
{
    //...
    [System.Serializable]
    public partial class Attribute
    {

        [field: SerializeField]
        public string Name { get; private set; }
        [field: SerializeField]
        public IAttribute.DataType Type { get; private set; }

        [field: SerializeField]
        public IAttribute.ContainerType containerType { get; private set; }
        [field: SerializeField]
        public string containerId { get; private set; }

        [SerializeField]
        private string data_string;
        [SerializeReference]
        private List<Component> data_componentList;

        public object data {
            get
            {
                if ( Type == IAttribute.DataType.StringPlainText || Type == IAttribute.DataType.StringRichText ) { return data_string; }
                if ( Type == IAttribute.DataType.ComponentList ) { return data_componentList; }
                return null;
            }
        }

        internal void Set(string name, IAttribute.DataType type, object data, IAttribute.ContainerType containerType, string containerId) {
            this.Name = name;
            this.Type = type;
            if ( type == IAttribute.DataType.StringPlainText || type == IAttribute.DataType.StringRichText ) { data_string = (string)data; }
            if ( type == IAttribute.DataType.ComponentList ) { data_componentList = (List<Component>)data; }
            this.containerType = containerType;
            this.containerId = containerId;
        }
    }
}