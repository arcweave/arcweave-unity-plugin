using System;
using UnityEngine;

namespace Arcweave.Project
{
    //...
    [System.Serializable]
    public partial class Variable : ISerializationCallbackReceiver
    {
        [field: SerializeField]
        public string Name { get; set; }
        private object _value;
        public object Value 
        { 
            get => _value;
            set 
            {
                _value = value;
            }
        }

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
                _defaultValue = null;

                // Create a copy of the value to preserve the original state
                if (value == null)
                {
                    _defaultValue = null;
                }
                else
                {
                    var type = value.GetType();

                    // value types and strings can be directly assigned as they are immutable, but reference types need to be cloned to avoid
                    // modifying the default value when the variable value changes
                    if (type == typeof(string) || type.IsValueType)
                    {
                        _defaultValue = value;
                    }
                    else 
                    {
                        // If the value is a reference type it has to implement ICloneable to create a deep copy, otherwise we will lose the default value
                        if (value is ICloneable cloneable)
                        {
                            _defaultValue = cloneable.Clone();
                        }
                        else
                        {
                            throw new InvalidOperationException($"Default value of type {type.FullName} must implement ICloneable to store a default value.");
                        }
                    }
                }
            }
        }
        
        [SerializeField, HideInInspector]
        private string defaultValueSerialized;

        public System.Type Type => System.Type.GetType(_typeName);

        public Variable(string name, object value) {
            this.Name = name;
            this.Value = value;
            this.DefaultValue = value;
            this._typeName = value.GetType().FullName;
        }

        ///<summary>Reset the variable to its default value.</summary>
        public void ResetToDefaultValue() {

            // Create a copy of the default value to ensure it's not modified
            if ( Type == typeof(string) ) { this.Value = DefaultValue == null ? null : (string)DefaultValue; }
            else if ( Type == typeof(int) ) { this.Value = (int)DefaultValue; }
            else if ( Type == typeof(double) ) { this.Value = (double)DefaultValue; }
            else if ( Type == typeof(bool) ) { this.Value = (bool)DefaultValue; }
            else if ( Type == typeof(float) ) { this.Value = (float)DefaultValue; }

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
            if (type == typeof(float))
            {
                return "f" + value;
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
                'd' => double.Parse(stringValue[1..]),
                'b' => bool.Parse(stringValue[1..]),
                'f' => float.Parse(stringValue[1..]),
                _ => default
            };
        }
        
        public void OnBeforeSerialize()
        {
            /* Clear serialization */
            valueSerialized = "";
            defaultValueSerialized = "";

            valueSerialized = GetSerializedValue(Value);
            defaultValueSerialized = GetSerializedValue(_defaultValue);
        }

        public void OnAfterDeserialize()
        {
            /* Clear the content*/
            _value = null;
            _defaultValue = null;

            _value = DeserializeValue(valueSerialized);
            _defaultValue = DeserializeValue(defaultValueSerialized);
        }
    }
}