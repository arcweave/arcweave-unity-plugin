using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using System.Collections.Generic;

namespace Arcweave.Transpiler
{
    public class Interpreter
    {
        public Project Project { get; set; }
        public string elementId { get; set; }
        public Interpreter(Project project, string elementId = "") {
            this.Project = project;
            this.elementId = elementId;
        }

        private ArcscriptParser.InputContext GetParseTree(string code) {
            ICharStream stream = CharStreams.fromString(code);
            ITokenSource lexer = new ArcscriptLexer(stream);
            ITokenStream tokens = new CommonTokenStream(lexer);
            ArcscriptParser parser = new ArcscriptParser(tokens);
            parser.SetProject(Project);

            ArcscriptParser.InputContext tree = parser.input();
            return tree;
        }

        public TranspilerOutput RunScript(string code) {
            ArcscriptParser.InputContext tree = this.GetParseTree(code);
            ArcscriptVisitor visitor = new ArcscriptVisitor(this.elementId, this.Project);
            object result = tree.Accept(visitor);

            List<string> outputs = visitor.state.outputs;
            string outputResult = "";
            if ( outputs.Count > 0 ) {
                for ( int i = 0; i < outputs.Count; i++ ) {
                    string output = outputs[i];
                    output = output.Trim();
                    outputResult += output;
                }
                outputResult = Utils.CleanString(outputResult);
            }

            bool isCondition = false;
            if (tree.script() != null)
            {
                isCondition = true;
            }

            return new TranspilerOutput(outputResult, visitor.state.VariableChanges, result, isCondition);
        }

        public class TranspilerOutput
        {
            public string output { get; private set; }
            public Dictionary<string, object> changes { get; private set; }
            public object result { get; private set; }
            public bool IsCondition { get; private set; }
            public TranspilerOutput(string output, Dictionary<string, object> changes, object result, bool isCondition = false)
            {
                this.result = result;
                this.output = output;
                this.changes = changes;
                IsCondition = isCondition;
            }
        }
    }
}