using UnityEngine;

namespace Arcweave.Project
{
    //...
    [System.Serializable]
    public partial class Variable
    {
        [field: SerializeField]
        public string Name { get; set; }
        [field: SerializeReference]
        public object Value { get; set; }

        public object ObjectValue => Value;

        [SerializeField]
        private string _typeName;

        [SerializeReference]
        private object _defaultValue;

        public System.Type Type => System.Type.GetType(_typeName);

        public Variable(string name, object value) {
            this.Name = name;
            this.Value = value;
            this._defaultValue = value;
            this._typeName = value.GetType().FullName;
        }

        ///<summary>Reset the variable to its default value.</summary>
        public void ResetToDefaultValue() {
            if ( Type == typeof(string) ) { this.Value = (string)_defaultValue; }
            if ( Type == typeof(int) ) { this.Value = (int)_defaultValue; }
            if ( Type == typeof(float) ) { this.Value = (float)_defaultValue; }
            if ( Type == typeof(bool) ) { this.Value = (bool)_defaultValue; }
        }
    }
}