using System;
using System.Globalization;
using Arcweave.Interpreter.INodes;
using UnityEngine;

namespace Arcweave.Project
{
    //...
    [System.Serializable]
    public partial class Variable : ISerializationCallbackReceiver
    {
        [field: SerializeField]
        public string Name { get; set; }
        [field: SerializeField]
        public string Id { get; set; }
        public object Value { get; set; }

        [field: SerializeReference]
        public IHasVariables Parent { get; set; }

        [SerializeField, HideInInspector]
        private string valueSerialized;

        [SerializeField, HideInInspector]
        private string parentIdSerialized;
        public object ObjectValue => Value;

        [SerializeField]
        private string _typeName;

        private object _defaultValue;
        public object DefaultValue
        {
            get => _defaultValue;
            set
            {
                _defaultValue = value;
            }
        }
        
        [SerializeField, HideInInspector]
        private string defaultValueSerialized;

        public System.Type Type => System.Type.GetType(_typeName);

        /// <summary>
        /// Initializes a variable with an explicit Arcweave variable id.
        /// </summary>
        public Variable(string id, string name, object value) {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value), "Variable value cannot be null.");
            }
            this.Id = id;
            this.Name = name;
            this.Value = value;
            this.DefaultValue = value;
            this._typeName = value.GetType().FullName;
        }

        /// <summary>
        /// Initializes a variable with an explicit Arcweave variable id and owning scope.
        /// </summary>
        public Variable(string id, string name, object value, IHasVariables parent)
            : this(id, name, value)
        {
            Parent = parent;
        }

        ///<summary>Reset the variable to its default value.</summary>
        public void ResetToDefaultValue() {
            if ( Type == typeof(string) ) { this.Value = (string)DefaultValue; }
            if ( Type == typeof(int) ) { this.Value = (int)DefaultValue; }
            if ( Type == typeof(double) ) { this.Value = (double)DefaultValue; }
            if ( Type == typeof(bool) ) { this.Value = (bool)DefaultValue; }
        }

        private string GetSerializedValue(object value)
        {
            if (value == null)
            {
                return "n";
            }
            var type = value.GetType();
            if (type == typeof(string))
            {
                return "s" + value;
            }
            if (type == typeof(int))
            {
                return "i" + ((int)value).ToString(CultureInfo.InvariantCulture);
            }
            if (type == typeof(double))
            {
                return "d" + ((double)value).ToString("R", CultureInfo.InvariantCulture);
            }
            if (type == typeof(bool))
            {
                return "b" + value;
            }

            return "";
        }

        private object DeserializeValue(string stringValue)
        {
            if (string.IsNullOrEmpty(stringValue))
            {
                return default;
            }
            
            var type = stringValue[0];
            return type switch
            {
                'n' => null,
                's' => stringValue[1..],
                'i' => int.Parse(stringValue[1..], CultureInfo.InvariantCulture),
                'd' => double.Parse(stringValue[1..], CultureInfo.InvariantCulture),
                'b' => bool.Parse(stringValue[1..]),
                _ => default
            };
        }
        
        public void OnBeforeSerialize()
        {
            valueSerialized = GetSerializedValue(Value);
            defaultValueSerialized = GetSerializedValue(_defaultValue);
        }

        public void OnAfterDeserialize()
        {
            Value = DeserializeValue(valueSerialized);
            _defaultValue = DeserializeValue(defaultValueSerialized);
        }
    }
}
