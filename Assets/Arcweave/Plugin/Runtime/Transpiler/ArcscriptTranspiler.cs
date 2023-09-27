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

        private IParseTree GetParseTree(string code) {
            ICharStream stream = CharStreams.fromString(code);
            ITokenSource lexer = new ArcscriptLexer(stream);
            ITokenStream tokens = new CommonTokenStream(lexer);
            ArcscriptParser parser = new ArcscriptParser(tokens);
            parser.SetProject(Project);
            IParseTree tree = parser.input();

            return tree;
        }

        public TranspilerOutput RunScript(string code) {
            IParseTree tree = this.GetParseTree(code);
            ArcscriptVisitor visitor = new ArcscriptVisitor(this.elementId, this.Project);
            Dictionary<string, object> result = (Dictionary<string, object>)tree.Accept(visitor);


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

            return new TranspilerOutput(outputResult, visitor.state.VariableChanges, result);
        }

        public class TranspilerOutput
        {
            public string output { get; private set; }
            public Dictionary<string, object> changes { get; private set; }
            public object result { get; private set; }

            public TranspilerOutput(string output, Dictionary<string, object> changes, object result) {
                this.result = result;
                this.output = output;
                this.changes = changes;
            }
        }
    }
}