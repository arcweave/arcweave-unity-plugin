using UnityEngine;

namespace Arcweave.Project
{
    //...
    [System.Serializable]
    public partial class Variable : ISerializationCallbackReceiver
    {
        [field: SerializeField]
        public string Name { get; set; }
        public object Value { get; set; }

        /*cType: tells wether the variable is global or board-local.*/
        [SerializeField]
        public VariableScopeType componentType;

        [SerializeField, HideInInspector]
        private string valueSerialized;

        public object ObjectValue => Value;

        /* object type */
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

        public Variable(string name, object value, VariableScopeType scopeType) {
            this.Name = name;
            this.Value = value;
            this.DefaultValue = value;
            this._typeName = value.GetType().FullName;
            this.componentType = scopeType;
        }

        ///<summary>Reset the variable to its default value.</summary>
        public void ResetToDefaultValue() {
            if ( Type == typeof(string) ) { this.Value = (string)DefaultValue; }
            if ( Type == typeof(int) ) { this.Value = (int)DefaultValue; }
            if ( Type == typeof(float) ) { this.Value = (float)DefaultValue; }
            if ( Type == typeof(double) )
            {
                this.Value = (double)DefaultValue;
            }


            if ( Type == typeof(bool) ) { this.Value = (bool)DefaultValue; }
        }

        public static bool IsGlobal(Variable variable)
        {
            if(variable.componentType == VariableScopeType.Global)
            {
                return true;
            }

            return false;
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
            if (type == typeof(float))
            {
                return "f" + value;
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
                's' => stringValue[1..],
                'i' => int.Parse(stringValue[1..]),
                'f' => float.Parse(stringValue[1..]),
                'd' => double.Parse(stringValue[1..]),
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