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
        
        public void SetState(IEnumerable<Variable> vars)
        {
            var variableList = new List<Variable>(vars);
            variables = new VariableState[variableList.Count];
            int i = 0;
            foreach (var variable in variableList)
            {
                variables[i].id = variable.Id;
                try
                {
                    variables[i].value = variable.Value is IFormattable formattable
                        ? formattable.ToString(null, System.Globalization.CultureInfo.InvariantCulture)
                        : variable.Value.ToString();
                }
                catch (Exception e)
                {
                    Debug.Log("Error serializing variable " + variable.Name + ": " + e.Message);
                }
                variables[i].type = variable.Type.FullName;
                i++;
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
