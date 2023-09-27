using System;
using System.Collections.Generic;
using System.Linq;

namespace Arcweave.Transpiler
{
    public class Functions
    {
        private static readonly Random _getrandom = new Random();
        private Project _project;
        private string elementId;
        private ArcscriptState state;
        public Dictionary<string, Func<IList<ArcscriptVisitor.Argument>, object>> functions { get; private set; } = new Dictionary<string, Func<IList<ArcscriptVisitor.Argument>, object>>();

        public Dictionary<string, Type> returnTypes = new Dictionary<string, Type>()
        {
            { "sqrt",  typeof (double) },
            { "sqr", typeof (double) },
            { "abs", typeof (double) },
            { "random", typeof (double) },
            { "roll", typeof (int) },
            { "show", typeof (string) },
            { "reset", typeof (void) },
            { "resetAll", typeof (void) },
            { "round", typeof (int) },
            { "min", typeof (double) },
            { "max", typeof (double) },
            { "visits", typeof (int) }
        };

        public Functions(string elementId, Project project, ArcscriptState state) {
            this._project = project;
            this.elementId = elementId;
            this.state = state;

            this.functions["sqrt"] = this.Sqrt;
            this.functions["sqr"] = this.Sqr;
            this.functions["abs"] = this.Abs;
            this.functions["random"] = this.Random;
            this.functions["roll"] = this.Roll;
            this.functions["show"] = this.Show;
            this.functions["reset"] = this.Reset;
            this.functions["resetAll"] = this.ResetAll;
            this.functions["round"] = this.Round;
            this.functions["min"] = this.Min;
            this.functions["max"] = this.Max;
            this.functions["visits"] = this.Visits;
        }

        public object Sqrt(IList<ArcscriptVisitor.Argument> args) {
            double n = (double)args[0].value;
            return Math.Sqrt(n);
        }

        public object Sqr(IList<ArcscriptVisitor.Argument> args) {
            double n = (double)args[0].value;
            return n * n;
        }

        public object Abs(IList<ArcscriptVisitor.Argument> args) {
            double n = (double)args[0].value;
            return Math.Abs(n);
        }

        public object Random(IList<ArcscriptVisitor.Argument> args) {
            lock ( _getrandom ) {
                return _getrandom.NextDouble();
            }
        }

        public object Roll(IList<ArcscriptVisitor.Argument> args) {
            int maxRoll = (int)args[0].value;
            int numRolls = 1;
            if ( args.Count == 2 ) {
                numRolls = (int)args[1].value;
            }
            int sum = 0;
            for ( int i = 0; i < numRolls; i++ ) {
                int oneRoll = _getrandom.Next(1, maxRoll + 1);
                sum += oneRoll;
            }
            return sum;
        }

        public object Show(IList<ArcscriptVisitor.Argument> args) {
            List<object> results = new List<object>();
            foreach ( ArcscriptVisitor.Argument arg in args ) {
                results.Add(arg.value.ToString());
            }
            string result = String.Join(' ', results.ToArray());
            UnityEngine.Debug.Log(result);
            this.state.outputs.Add(result);
            return null;
        }

        public object Reset(IList<ArcscriptVisitor.Argument> args) {
            foreach ( ArcscriptVisitor.Argument argument in args ) {
                Variable variable = argument.value as Variable;
                variable.ResetToDefaultValue();
            }
            return null;
        }

        public object ResetAll(IList<ArcscriptVisitor.Argument> args) {
            List<string> variableNames = new List<string>();
            foreach ( ArcscriptVisitor.Argument argument in args ) {
                variableNames.Add(( argument.value as Variable ).name);
            }
            foreach ( Variable variable in this._project.variables ) {
                if ( !variableNames.Contains(variable.name) ) {
                    variable.ResetToDefaultValue();
                }
            }
            return null;
        }

        public object Round(IList<ArcscriptVisitor.Argument> args) {
            double n = (double)args[0].value;
            return (int)Math.Round(n);
        }

        public object Min(IList<ArcscriptVisitor.Argument> args) {
            return args.Min();
        }

        public object Max(IList<ArcscriptVisitor.Argument> args) {
            return args.Max();
        }

        public object Visits(IList<ArcscriptVisitor.Argument> args) {
            string elementId = this.elementId;
            if ( args != null && args.Count == 1 ) {
                ArcscriptVisitor.Mention mention = (ArcscriptVisitor.Mention)args[0].value;
                elementId = mention.attrs["data-id"];
            }
            Element element = this._project.ElementWithID(elementId);
            UnityEngine.Debug.Log(element.visits);
            return element.visits;
        }
    }
}