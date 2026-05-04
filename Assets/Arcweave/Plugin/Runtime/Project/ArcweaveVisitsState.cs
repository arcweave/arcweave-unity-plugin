using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using static Arcweave.Project.State;


namespace Arcweave.Project
{
    /* 
     * Support class to keep a save of the element visits
     */
    internal class ArcweaveVisitsState
    {
        [Serializable]
        internal struct VisitsState
        {
            public string elementId;
            public int nVisit;
        };

        [SerializeField]
        private VisitsState[] visits;

        public ArcweaveVisitsState(Dictionary<string, int> visitDictionary)
        {
           SetVisitsState(visitDictionary);
        }

        public void SetVisitsState(Dictionary<string, int> elementVisits)
        {
            if (elementVisits != null && elementVisits.Count > 0)
            {
                visits = new VisitsState[elementVisits.Count];
                int i = 0;
                foreach (var kvp in elementVisits)
                {
                    visits[i].elementId = kvp.Key;
                    visits[i].nVisit = kvp.Value;
                    i++;
                }
            }
        }

        public string ToJson()
        {
            return JsonUtility.ToJson(this);
        }

        public static ArcweaveVisitsState FromJson(string json)
        {
            var state = JsonUtility.FromJson<ArcweaveVisitsState>(json);
            return state;
        }

        public VisitsState[] GetVisits()
        {
            return visits;
        }
    }

}