using System.Collections.Generic;
using Arcweave.Interpreter;
using UnityEngine;
using Arcweave.Interpreter.INodes;

namespace Arcweave.Project
{
    //...
    [System.Serializable]
    public partial class Attribute
    {

        [field: SerializeField]
        public string Id { get; private set; }
        [field: SerializeField]
        public string CustomId { get; private set; }
        [field: SerializeField]
        public string Name { get; private set; }
        [field: SerializeField]
        public IAttribute.DataType Type { get; private set; }

        [field: SerializeField]
        public IAttribute.ContainerType containerType { get; private set; }
        [field: SerializeField]
        public string containerId { get; private set; }

        private Project project;
        
        [SerializeField]
        private string data_string;

        [SerializeField] private string data_stringRichTextRaw;
        [SerializeField] private string data_stringRichText;

        [SerializeReference]
        private List<Component> data_componentList;

        [SerializeField]
        private bool data_boolean;

        [SerializeField]
        private int data_integer;

        [SerializeField]
        private double data_float;

        public object data {
            get
            {
                if ( Type == IAttribute.DataType.StringPlainText ) { return data_string; }

                if (Type == IAttribute.DataType.StringRichText)
                {
                    if (string.IsNullOrEmpty(data_stringRichText))
                    {
                        var i = new AwInterpreter(project);
                        var output = i.RunScript(data_stringRichTextRaw);
                        data_stringRichText = Utils.CleanString(output.Output);
                    }

                    return data_stringRichText;
                }
                if ( Type == IAttribute.DataType.ComponentList ) { return data_componentList; }
                if ( Type == IAttribute.DataType.Boolean ) { return data_boolean; }
                if ( Type == IAttribute.DataType.Integer ) { return data_integer; }
                if ( Type == IAttribute.DataType.Float ) { return data_float; }
                return null;
            }
        }

        public void InitializeInProject(Project project) { this.project = project; }

        internal void Set(string id, string customId, string name, IAttribute.DataType type, object _data, IAttribute.ContainerType containerType, string containerId) {
            this.Id = id;
            this.CustomId = customId;
            this.Name = name;
            this.Type = type;
            if ( type == IAttribute.DataType.StringPlainText ) { data_string = (string)_data; }

            if (type == IAttribute.DataType.StringRichText)
            {
                data_stringRichTextRaw = (string)_data;
                data_stringRichText = null;
            }
            if ( type == IAttribute.DataType.ComponentList ) { data_componentList = (List<Component>)_data; }
            if ( type == IAttribute.DataType.Boolean ) { data_boolean = (bool)_data; }
            if ( type == IAttribute.DataType.Integer ) { data_integer = (int)_data; }
            if ( type == IAttribute.DataType.Float ) { data_float = (double)_data; }
            this.containerType = containerType;
            this.containerId = containerId;
        }
    }
}
