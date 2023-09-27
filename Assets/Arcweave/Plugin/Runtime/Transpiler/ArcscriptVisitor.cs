using Antlr4.Runtime.Misc;
using System.Collections.Generic;
using System.Linq;
using System;
using Antlr4.Runtime.Tree;
using System.Collections;

namespace Arcweave.Transpiler
{
    public class ArcscriptVisitor : ArcscriptParserBaseVisitor<object>
    {
        public Project project = null;
        public ArcscriptState state = null;
        public string elementId = null;
        private Functions functions = null;
        public ArcscriptVisitor(string elementId, Project project) {
            this.elementId = elementId;
            this.project = project;
            this.state = new ArcscriptState(elementId, project);
            this.functions = new Functions(elementId, project, this.state);
        }

        public override object VisitInput([NotNull] ArcscriptParser.InputContext context) {
            if ( context.script() != null ) {
                return new Dictionary<string, object>() { { "script", this.VisitScript(context.script()) } };
            }

            return new Dictionary<string, object>() { { "condition", this.VisitCompound_condition_or(context.compound_condition_or()) } };
        }

        public override object VisitScript_section([NotNull] ArcscriptParser.Script_sectionContext context) {
            if ( context == null ) {
                return null;
            }
            if ( context.NORMALTEXT() != null && context.NORMALTEXT().Length > 0 ) {
                this.state.outputs.Add(context.GetText());
                return new Dictionary<string, object>() { { "value", context.GetText() } };
            }

            return this.VisitChildren(context);
        }

        public override object VisitAssignment_segment([NotNull] ArcscriptParser.Assignment_segmentContext context) {
            return this.VisitStatement_assignment(context.statement_assignment());
        }

        public override object VisitFunction_call_segment([NotNull] ArcscriptParser.Function_call_segmentContext context) {
            return this.VisitStatement_function_call(context.statement_function_call());
        }

        public override object VisitConditional_section([NotNull] ArcscriptParser.Conditional_sectionContext context) {
            object if_section = this.VisitIf_section(context.if_section());
            if ( if_section != null ) {
                return if_section;
            }
            var result = context.else_if_section().FirstOrDefault(else_if_section =>
            {
                object elif_section = this.VisitElse_if_section(else_if_section);
                if ( elif_section != null ) {
                    return true;
                }
                return false;
            });

            if ( result != null ) {
                return result;
            }

            if ( context.else_section() != null ) {
                return this.VisitElse_section(context.else_section());
            }
            return null;
        }

        public override object VisitIf_section([NotNull] ArcscriptParser.If_sectionContext context) {
            bool result = (bool)this.VisitIf_clause(context.if_clause());

            if ( result ) {
                return this.VisitScript(context.script());
            }
            return null;
        }

        public override object VisitElse_if_section([NotNull] ArcscriptParser.Else_if_sectionContext context) {
            bool result = (bool)this.VisitElse_if_clause(context.else_if_clause());

            if ( result ) {
                return this.VisitScript(context.script());
            }
            return null;
        }

        public override object VisitElse_section([NotNull] ArcscriptParser.Else_sectionContext context) {
            return this.VisitScript(context.script());
        }

        public override object VisitIf_clause([NotNull] ArcscriptParser.If_clauseContext context) {
            return this.VisitCompound_condition_or(context.compound_condition_or());
        }

        public override object VisitElse_if_clause([NotNull] ArcscriptParser.Else_if_clauseContext context) {
            return this.VisitCompound_condition_or(context.compound_condition_or());
        }

        public override object VisitStatement_assignment([NotNull] ArcscriptParser.Statement_assignmentContext context) {
            string variableName = context.VARIABLE().GetText();
            Variable variable = this.state.GetVariable(variableName);
            object variableValue = this.state.GetVarValue(variableName);

            object compound_condition_or = this.VisitCompound_condition_or(context.compound_condition_or());
            double result = 0;
            if ( context.ASSIGN() != null ) {
                this.state.SetVarValue(variableName, Convert.ChangeType(variableValue, variable.type));
                return null;
            }

            double dblVariableValue = (double)Convert.ChangeType(variableValue, typeof(double));
            double dblCompoundCondition = (double)Convert.ChangeType(compound_condition_or, typeof(double));

            if ( context.ASSIGNADD() != null ) {
                result = dblVariableValue + dblCompoundCondition;
            } else if ( context.ASSIGNSUB() != null ) {
                result = dblVariableValue - dblCompoundCondition;
            } else if ( context.ASSIGNMUL() != null ) {
                result = dblVariableValue * dblCompoundCondition;
            } else if ( context.ASSIGNDIV() != null ) {
                result = dblVariableValue / dblCompoundCondition;
            }

            this.state.SetVarValue(variableName, Convert.ChangeType(result, variable.type));
            return null;
        }

        public override object VisitVoid_function_call([NotNull] ArcscriptParser.Void_function_callContext context) {
            string fname = "";
            IList argument_list_result = null;
            if ( context.VFNAME() != null ) {
                fname = context.VFNAME().GetText();
                if ( context.argument_list() != null ) {
                    argument_list_result = (IList)this.VisitArgument_list(context.argument_list());
                }
            }
            if ( context.VFNAMEVARS() != null ) {
                fname = context.VFNAMEVARS().GetText();
                if ( context.variable_list() != null ) {
                    argument_list_result = (IList)this.VisitVariable_list(context.variable_list());
                }
            }
            List<Argument> argument_list = argument_list_result?.Cast<Argument>().ToList();
            if ( argument_list == null ) {
                argument_list = new List<Argument>() { };
            }
            Type resultType = this.functions.returnTypes[fname];
            object returnValue = this.functions.functions[fname](argument_list);

            return new Dictionary<string, object>() { { "result", returnValue } };
        }

        public override object VisitVariable_list([NotNull] ArcscriptParser.Variable_listContext context) {
            List<Argument> variables = new List<Argument>();
            foreach ( ITerminalNode variable in context.VARIABLE() ) {
                Variable varObject = this.state.GetVariable(variable.GetText());
                Argument arg = new Argument(typeof(Variable), varObject);
                variables.Add(arg);
            }
            return new Dictionary<string, object>() { { "variable_list", variables } };
        }

        public override object VisitCompound_condition_or([NotNull] ArcscriptParser.Compound_condition_orContext context) {
            bool compound_condition_and = (bool)this.VisitCompound_condition_and(context.compound_condition_and());
            if ( context.compound_condition_or() != null ) {
                bool compound_condition_or = (bool)this.VisitCompound_condition_or(context.compound_condition_or());
                bool result = compound_condition_and || compound_condition_or;
                return result;
            }
            return compound_condition_and;
        }

        public override object VisitCompound_condition_and([NotNull] ArcscriptParser.Compound_condition_andContext context) {
            bool negated_unary_condition = (bool)this.VisitNegated_unary_condition(context.negated_unary_condition());
            if ( context.compound_condition_and() != null ) {
                bool compound_condition_and = (bool)this.VisitCompound_condition_and(context.compound_condition_and());
                bool result = (bool)negated_unary_condition && compound_condition_and;
                return result;
            }

            return negated_unary_condition;
        }

        public override object VisitNegated_unary_condition([NotNull] ArcscriptParser.Negated_unary_conditionContext context) {
            bool unary_condition = (bool)this.VisitUnary_condition(context.unary_condition());

            if ( context.NEG() != null || context.NOTKEYWORD() != null ) {
                return !unary_condition;
            }

            return unary_condition;
        }

        public override object VisitUnary_condition([NotNull] ArcscriptParser.Unary_conditionContext context) {
            return this.VisitCondition(context.condition());
        }

        public override object VisitCondition([NotNull] ArcscriptParser.ConditionContext context) {
            if ( context.expression().Length == 1 ) {
                object expr = this.VisitExpression(context.expression()[0]);
                if ( expr.GetType() == typeof(double) ) {
                    return (double)expr > 0;
                } else if ( expr.GetType() == typeof(int) ) {
                    return (int)expr > 0;
                } else if ( expr.GetType() == typeof(string) ) {
                    return (string)expr != "";
                }
                return (bool)expr;
            }
            ArcscriptParser.Conditional_operatorContext conditional_operator_context = context.conditional_operator();
            object exp0 = this.VisitExpression(context.expression()[0]);
            object exp1 = this.VisitExpression(context.expression()[1]);

            Dictionary<string, object> result = new Dictionary<string, object>();
            if ( conditional_operator_context.GT() != null ) {
                return (double)Convert.ChangeType(exp0, typeof(double)) > (double)Convert.ChangeType(exp1, typeof(double));

            }
            if ( conditional_operator_context.GE() != null ) {
                return (double)Convert.ChangeType(exp0, typeof(double)) >= (double)Convert.ChangeType(exp1, typeof(double));
            }
            if ( conditional_operator_context.LT() != null ) {
                return (double)Convert.ChangeType(exp0, typeof(double)) < (double)Convert.ChangeType(exp1, typeof(double));
            }
            if ( conditional_operator_context.LE() != null ) {
                return (double)Convert.ChangeType(exp0, typeof(double)) <= (double)Convert.ChangeType(exp1, typeof(double));
            }
            if ( conditional_operator_context.EQ() != null ) {
                return (double)Convert.ChangeType(exp0, typeof(double)) == (double)Convert.ChangeType(exp1, typeof(double));
            }
            if ( conditional_operator_context.NE() != null ) {
                return (double)Convert.ChangeType(exp0, typeof(double)) != (double)Convert.ChangeType(exp1, typeof(double));
            }
            if ( conditional_operator_context.ISKEYWORD() != null ) {
                if ( conditional_operator_context.NOTKEYWORD() != null ) {
                    return (double)Convert.ChangeType(exp0, typeof(double)) != (double)Convert.ChangeType(exp1, typeof(double));
                }

                return (double)Convert.ChangeType(exp0, typeof(double)) == (double)Convert.ChangeType(exp1, typeof(double));
            }
            return this.VisitChildren(context);
        }

        public override object VisitExpression([NotNull] ArcscriptParser.ExpressionContext context) {
            if ( context.STRING() != null ) {
                string result = context.STRING().GetText();
                result = result.Substring(1, result.Length - 2);
                return result;
            }
            if ( context.BOOLEAN() != null ) {
                return context.BOOLEAN().GetText() == "true";
            }
            return this.VisitAdditive_numeric_expression(context.additive_numeric_expression());
        }

        public override object VisitAdditive_numeric_expression([NotNull] ArcscriptParser.Additive_numeric_expressionContext context) {
            object mult_num_expression = this.VisitMultiplicative_numeric_expression(context.multiplicative_numeric_expression());

            if ( context.additive_numeric_expression() != null ) {
                object result = this.VisitAdditive_numeric_expression(context.additive_numeric_expression());
                if ( context.ADD() != null ) {
                    return (double)mult_num_expression + (double)result;
                }
                // Else MINUS
                return (double)mult_num_expression - (double)result;
            }

            return mult_num_expression;
        }

        public override object VisitMultiplicative_numeric_expression([NotNull] ArcscriptParser.Multiplicative_numeric_expressionContext context) {
            object signed_unary_num_expr = this.VisitSigned_unary_numeric_expression(context.signed_unary_numeric_expression());

            if ( context.multiplicative_numeric_expression() != null ) {
                object result = this.VisitMultiplicative_numeric_expression(context.multiplicative_numeric_expression());
                if ( context.MUL() != null ) {
                    return (double)signed_unary_num_expr * (double)result;
                }
                // Else DIV
                return (double)signed_unary_num_expr / (double)result;
            }

            return signed_unary_num_expr;
        }

        public override object VisitSigned_unary_numeric_expression([NotNull] ArcscriptParser.Signed_unary_numeric_expressionContext context) {
            object unary_num_expr = this.VisitUnary_numeric_expression(context.unary_numeric_expression());
            ArcscriptParser.SignContext sign = context.sign();

            if ( sign != null ) {
                if ( sign.ADD() != null ) {
                    return +(double)unary_num_expr;
                }
                return -(double)unary_num_expr;
            }
            return unary_num_expr;
        }

        public override object VisitUnary_numeric_expression([NotNull] ArcscriptParser.Unary_numeric_expressionContext context) {
            if ( context.FLOAT() != null ) {
                return double.Parse(context.FLOAT().GetText());
            }
            if ( context.INTEGER() != null ) {
                return int.Parse(context.INTEGER().GetText());
            }
            if ( context.VARIABLE() != null ) {
                string variableName = context.VARIABLE().GetText();
                return this.state.GetVarValue(variableName);
            }

            if ( context.function_call() != null ) {
                return this.VisitFunction_call(context.function_call());
            }
            return this.VisitCompound_condition_or(context.compound_condition_or());
        }

        public override object VisitFunction_call([NotNull] ArcscriptParser.Function_callContext context) {
            IList argument_list_result = null;
            if ( context.argument_list() != null ) {
                argument_list_result = (IList)this.VisitArgument_list(context.argument_list());
            }

            List<Argument> argument_list = argument_list_result?.Cast<Argument>().ToList();

            if ( argument_list == null ) {
                argument_list = new List<Argument>() { };
            }
            string fname = context.FNAME().GetText();

            Type resultType = this.functions.returnTypes[fname];
            object returnValue = this.functions.functions[fname](argument_list);

            return returnValue;
        }

        public override object VisitArgument_list([NotNull] ArcscriptParser.Argument_listContext context) {
            List<object> argumentList = new List<object>();
            foreach ( ArcscriptParser.ArgumentContext argument in context.argument() ) {
                argumentList.Add(this.VisitArgument(argument));
            }
            return argumentList;
        }

        public override object VisitArgument([NotNull] ArcscriptParser.ArgumentContext context) {
            if ( context.STRING() != null ) {
                string result = context.STRING().GetText();
                result = result.Substring(1, result.Length - 2);
                return new Argument(typeof(string), result);
            }
            if ( context.mention() != null ) {
                Mention mention_result = (Mention)this.VisitMention(context.mention());
                Argument argument = new Argument(typeof(Mention), mention_result);
                return argument;
            }
            object num_expr_result = this.VisitAdditive_numeric_expression(context.additive_numeric_expression());
            return new Argument(typeof(double), num_expr_result);
        }

        public override object VisitMention([NotNull] ArcscriptParser.MentionContext context) {
            Dictionary<string, string> attrs = new Dictionary<string, string>();

            foreach ( ArcscriptParser.Mention_attributesContext attr in context.mention_attributes() ) {
                Dictionary<string, object> res = (Dictionary<string, object>)this.VisitMention_attributes(attr);
                attrs.Add((string)res["name"], (string)res["value"]);
            }
            string label = "";
            if ( context.MENTION_LABEL() != null ) {
                label = context.MENTION_LABEL().GetText();
            }
            return new Mention(label, attrs);
        }

        public override object VisitMention_attributes([NotNull] ArcscriptParser.Mention_attributesContext context) {
            string name = context.ATTR_NAME().GetText();
            ITerminalNode ctxvalue = context.ATTR_VALUE();
            object value = true;
            if ( ctxvalue != null ) {
                string strvalue = ctxvalue.GetText();
                if ( ( strvalue.StartsWith('"') && strvalue.EndsWith('"') ) ||
                    ( strvalue.StartsWith("'") && strvalue.EndsWith("'") ) ) {
                    strvalue = strvalue.Substring(1, strvalue.Length - 2);
                }
                value = strvalue;
            }
            return new Dictionary<string, object> { { "name", name }, { "value", value } };
        }

        public class Argument
        {
            public Type type { get; private set; }
            public object value { get; private set; }
            public Argument(Type type, object value) {
                this.type = type;
                this.value = value;
            }
        }

        public class Mention
        {
            public string label { get; private set; }
            public Dictionary<string, string> attrs { get; private set; }

            public Mention(string label, Dictionary<string, string> attrs) {
                this.label = label;
                this.attrs = attrs;
            }
        }
    }
}