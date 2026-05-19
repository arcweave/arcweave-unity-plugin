using UnityEngine;
using Arcweave.Interpreter.INodes;

namespace Arcweave.Project
{
    //...
    [System.Serializable]
    public partial class Variable : ISerializationCallbackReceiver
    {
        [field: SerializeField]
        public string Name { get; set; }
        public string Id => Name;
        public object Value { get; set; }
        public IHasVariables Parent { get; set; }

        public System.Type Type => System.Type.GetType(_typeName);

        [SerializeField, HideInInspector]
        private string valueSerialized;

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

        public Variable(string id, string name, object value)
        {
            Name = name;
            Value = value;
            _defaultValue = value;
            this._typeName = value.GetType().FullName;
        }
        public Variable(string id, string name, object value, IHasVariables parent)
        {

            Name = name;
            Value = value;
            _defaultValue = value;
            this._typeName = value.GetType().FullName;
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
                return "i" + value;
            }
            if (type == typeof(double))
            {
                return "d" + value;
            }
            if (type == typeof(bool))
            {
                return "b" + value;
            }

            return "";
        }

        private object DeserializeValue(string stringValue)
        {
            if (stringValue.Length == 0)
            {
                return default;
            }
            
            var type = stringValue[0];
            return type switch
            {
                'n' => null,
                's' => valueSerialized[1..],
                'i' => int.Parse(valueSerialized[1..]),
                'd' => double.Parse(valueSerialized[1..]),
                'b' => bool.Parse(valueSerialized[1..]),
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