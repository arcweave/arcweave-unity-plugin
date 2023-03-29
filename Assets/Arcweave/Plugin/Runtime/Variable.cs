using UnityEngine;

namespace Arcweave
{
    //...
    [System.Serializable]
    public class Variable
    {
        [field: SerializeField]
        public string name { get; private set; }
        [field: SerializeReference]
        public object value { get; set; }

        [SerializeField]
        private string _typeName;
        [SerializeReference]
        private object _defaultValue;

        public System.Type type => System.Type.GetType(_typeName);

        public Variable(string name, object value) {
            this.name = name;
            this.value = value;
            this._defaultValue = value;
            this._typeName = value.GetType().FullName;
        }

        ///<summary>Reset the variable to its default value.</summary>
        public void ResetToDefaultValue() {
            if ( type == typeof(string) ) { this.value = (string)_defaultValue; }
            if ( type == typeof(int) ) { this.value = (int)_defaultValue; }
            if ( type == typeof(float) ) { this.value = (float)_defaultValue; }
            if ( type == typeof(bool) ) { this.value = (bool)_defaultValue; }
        }
    }
}