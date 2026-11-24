using System.Collections.Generic;

namespace Arcweave.Project
{
    /// <summary>
    /// Variable values and visit counts of a project, captured in memory at one moment without serializing them.
    /// Fill it with Project.CaptureState and turn it into save strings with Project.SaveVariables(snapshot) and
    /// Project.SaveVisits(snapshot) only when a save is actually made.
    /// </summary>
    public sealed class StateSnapshot
    {
        internal readonly List<Variable> variables = new List<Variable>();
        internal readonly List<object> values = new List<object>();
        internal readonly Dictionary<string, int> visits = new Dictionary<string, int>();
        internal Project project;

        /// <summary>True once Project.CaptureState filled it, until Clear is called.</summary>
        public bool IsCaptured => project != null;

        /// <summary>Empties the snapshot, keeping its memory so it can be captured again.</summary>
        public void Clear()
        {
            variables.Clear();
            values.Clear();
            visits.Clear();
            project = null;
        }
    }
}
