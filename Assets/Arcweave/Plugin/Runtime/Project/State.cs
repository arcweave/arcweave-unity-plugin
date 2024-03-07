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
            public string name;
            public string value;
            public string type;
        };

        [SerializeField] private VariableState[] variables;

        public State() {}

        public State(List<Variable> variables)
        {
            SetState(variables);
        }
        
        public void SetState(List<Variable> vars)
        {
            variables = new VariableState[vars.Count];
            int i = 0;
            foreach (var variable in vars)
            {
                variables[i].name = variable.Name;
                variables[i].value = variable.Value.ToString();
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