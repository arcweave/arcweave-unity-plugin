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
        public object DefaultValue
        {
            get => _defaultValue;
            set
            {
                _defaultValue = value;
            }
        }

        public System.Type Type => System.Type.GetType(_typeName);

        public Variable(string name, object value) {
            this.Name = name;
            this.Value = value;
            this.DefaultValue = value;
            this._typeName = value.GetType().FullName;
        }

        ///<summary>Reset the variable to its default value.</summary>
        public void ResetToDefaultValue() {
            if ( Type == typeof(string) ) { this.Value = (string)DefaultValue; }
            if ( Type == typeof(int) ) { this.Value = (int)DefaultValue; }
            if ( Type == typeof(double) ) { this.Value = (double)DefaultValue; }
            if ( Type == typeof(bool) ) { this.Value = (bool)DefaultValue; }
        }
    }
}