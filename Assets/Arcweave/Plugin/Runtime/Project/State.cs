using System;
using System.Collections.Generic;
using UnityEngine;

namespace Arcweave.Project
{
    [Serializable]
    internal class State
    {
        [Serializable]
        internal struct VariableState
        {
            public string id;
            public string value;
            public string type;
        };

        [SerializeField] private VariableState[] variables;

        public State() {}

        public State(IEnumerable<Variable> variables)
        {
            SetState(variables);
        }
        
        /// <summary>Builds the state from the values that variables[i] had when values[i] was captured.</summary>
        internal State(IReadOnlyList<Variable> variables, IReadOnlyList<object> values)
        {
            SetState(variables, values);
        }

        public void SetState(IEnumerable<Variable> vars)
        {
            var variableList = new List<Variable>(vars);
            var values = new List<object>(variableList.Count);
            foreach (var variable in variableList)
            {
                values.Add(variable.Value);
            }
            SetState(variableList, values);
        }

        private void SetState(IReadOnlyList<Variable> vars, IReadOnlyList<object> values)
        {
            variables = new VariableState[vars.Count];
            for (int i = 0; i < vars.Count; i++)
            {
                var variable = vars[i];
                var value = values[i];
                variables[i].id = variable.Id;
                try
                {
                    variables[i].value = value is IFormattable formattable
                        ? formattable.ToString(null, System.Globalization.CultureInfo.InvariantCulture)
                        : value.ToString();
                }
                catch (Exception e)
                {
                    Debug.Log("Error serializing variable " + variable.Name + ": " + e.Message);
                }
                variables[i].type = variable.Type.FullName;
            }
        }

        public string ToJson()
        {
            return JsonUtility.ToJson(this);
        }

        public static State FromJson(string json)
        {
            var state = JsonUtility.FromJson<State>(json);
            return state;
        }

        public VariableState[] GetVariables()
        {
            return variables;
        }
    }
}
