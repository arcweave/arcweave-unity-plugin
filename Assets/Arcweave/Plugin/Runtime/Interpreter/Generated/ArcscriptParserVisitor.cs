//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     ANTLR Version: 4.13.1
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

// Generated from ArcscriptParser.g4 by ANTLR 4.13.1

// Unreachable code detected
#pragma warning disable 0162
// The variable '...' is assigned but its value is never used
#pragma warning disable 0219
// Missing XML comment for publicly visible type or member '...'
#pragma warning disable 1591
// Ambiguous reference in cref attribute
#pragma warning disable 419

namespace Arcweave.Interpreter {
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;
using IToken = Antlr4.Runtime.IToken;

/// <summary>
/// This interface defines a complete generic visitor for a parse tree produced
/// by <see cref="ArcscriptParser"/>.
/// </summary>
/// <typeparam name="Result">The return type of the visit operation.</typeparam>
[System.CodeDom.Compiler.GeneratedCode("ANTLR", "4.13.1")]
[System.CLSCompliant(false)]
public interface IArcscriptParserVisitor<Result> : IParseTreeVisitor<Result> {
	/// <summary>
	/// Visit a parse tree produced by <see cref="ArcscriptParser.input"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	/// <return>The visitor result.</return>
	Result VisitInput([NotNull] ArcscriptParser.InputContext context);
	/// <summary>
	/// Visit a parse tree produced by <see cref="ArcscriptParser.script"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	/// <return>The visitor result.</return>
	Result VisitScript([NotNull] ArcscriptParser.ScriptContext context);
	/// <summary>
	/// Visit a parse tree produced by <see cref="ArcscriptParser.script_section"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	/// <return>The visitor result.</return>
	Result VisitScript_section([NotNull] ArcscriptParser.Script_sectionContext context);
	/// <summary>
	/// Visit a parse tree produced by <see cref="ArcscriptParser.blockquote"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	/// <return>The visitor result.</return>
	Result VisitBlockquote([NotNull] ArcscriptParser.BlockquoteContext context);
	/// <summary>
	/// Visit a parse tree produced by <see cref="ArcscriptParser.paragraph"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	/// <return>The visitor result.</return>
	Result VisitParagraph([NotNull] ArcscriptParser.ParagraphContext context);
	/// <summary>
	/// Visit a parse tree produced by <see cref="ArcscriptParser.paragraph_start"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	/// <return>The visitor result.</return>
	Result VisitParagraph_start([NotNull] ArcscriptParser.Paragraph_startContext context);
	/// <summary>
	/// Visit a parse tree produced by <see cref="ArcscriptParser.codestart"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	/// <return>The visitor result.</return>
	Result VisitCodestart([NotNull] ArcscriptParser.CodestartContext context);
	/// <summary>
	/// Visit a parse tree produced by <see cref="ArcscriptParser.codeend"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	/// <return>The visitor result.</return>
	Result VisitCodeend([NotNull] ArcscriptParser.CodeendContext context);
	/// <summary>
	/// Visit a parse tree produced by <see cref="ArcscriptParser.assignment_segment"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	/// <return>The visitor result.</return>
	Result VisitAssignment_segment([NotNull] ArcscriptParser.Assignment_segmentContext context);
	/// <summary>
	/// Visit a parse tree produced by <see cref="ArcscriptParser.function_call_segment"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	/// <return>The visitor result.</return>
	Result VisitFunction_call_segment([NotNull] ArcscriptParser.Function_call_segmentContext context);
	/// <summary>
	/// Visit a parse tree produced by <see cref="ArcscriptParser.conditional_section"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	/// <return>The visitor result.</return>
	Result VisitConditional_section([NotNull] ArcscriptParser.Conditional_sectionContext context);
	/// <summary>
	/// Visit a parse tree produced by <see cref="ArcscriptParser.if_section"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	/// <return>The visitor result.</return>
	Result VisitIf_section([NotNull] ArcscriptParser.If_sectionContext context);
	/// <summary>
	/// Visit a parse tree produced by <see cref="ArcscriptParser.else_if_section"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	/// <return>The visitor result.</return>
	Result VisitElse_if_section([NotNull] ArcscriptParser.Else_if_sectionContext context);
	/// <summary>
	/// Visit a parse tree produced by <see cref="ArcscriptParser.else_section"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	/// <return>The visitor result.</return>
	Result VisitElse_section([NotNull] ArcscriptParser.Else_sectionContext context);
	/// <summary>
	/// Visit a parse tree produced by <see cref="ArcscriptParser.if_clause"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	/// <return>The visitor result.</return>
	Result VisitIf_clause([NotNull] ArcscriptParser.If_clauseContext context);
	/// <summary>
	/// Visit a parse tree produced by <see cref="ArcscriptParser.else_if_clause"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	/// <return>The visitor result.</return>
	Result VisitElse_if_clause([NotNull] ArcscriptParser.Else_if_clauseContext context);
	/// <summary>
	/// Visit a parse tree produced by <see cref="ArcscriptParser.endif_segment"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	/// <return>The visitor result.</return>
	Result VisitEndif_segment([NotNull] ArcscriptParser.Endif_segmentContext context);
	/// <summary>
	/// Visit a parse tree produced by <see cref="ArcscriptParser.statement_assignment"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	/// <return>The visitor result.</return>
	Result VisitStatement_assignment([NotNull] ArcscriptParser.Statement_assignmentContext context);
	/// <summary>
	/// Visit a parse tree produced by <see cref="ArcscriptParser.statement_function_call"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	/// <return>The visitor result.</return>
	Result VisitStatement_function_call([NotNull] ArcscriptParser.Statement_function_callContext context);
	/// <summary>
	/// Visit a parse tree produced by <see cref="ArcscriptParser.argument_list"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	/// <return>The visitor result.</return>
	Result VisitArgument_list([NotNull] ArcscriptParser.Argument_listContext context);
	/// <summary>
	/// Visit a parse tree produced by <see cref="ArcscriptParser.argument"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	/// <return>The visitor result.</return>
	Result VisitArgument([NotNull] ArcscriptParser.ArgumentContext context);
	/// <summary>
	/// Visit a parse tree produced by <see cref="ArcscriptParser.mention"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	/// <return>The visitor result.</return>
	Result VisitMention([NotNull] ArcscriptParser.MentionContext context);
	/// <summary>
	/// Visit a parse tree produced by <see cref="ArcscriptParser.mention_attributes"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	/// <return>The visitor result.</return>
	Result VisitMention_attributes([NotNull] ArcscriptParser.Mention_attributesContext context);
	/// <summary>
	/// Visit a parse tree produced by <see cref="ArcscriptParser.additive_numeric_expression"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	/// <return>The visitor result.</return>
	Result VisitAdditive_numeric_expression([NotNull] ArcscriptParser.Additive_numeric_expressionContext context);
	/// <summary>
	/// Visit a parse tree produced by <see cref="ArcscriptParser.multiplicative_numeric_expression"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	/// <return>The visitor result.</return>
	Result VisitMultiplicative_numeric_expression([NotNull] ArcscriptParser.Multiplicative_numeric_expressionContext context);
	/// <summary>
	/// Visit a parse tree produced by <see cref="ArcscriptParser.signed_unary_numeric_expression"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	/// <return>The visitor result.</return>
	Result VisitSigned_unary_numeric_expression([NotNull] ArcscriptParser.Signed_unary_numeric_expressionContext context);
	/// <summary>
	/// Visit a parse tree produced by <see cref="ArcscriptParser.unary_numeric_expression"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	/// <return>The visitor result.</return>
	Result VisitUnary_numeric_expression([NotNull] ArcscriptParser.Unary_numeric_expressionContext context);
	/// <summary>
	/// Visit a parse tree produced by <see cref="ArcscriptParser.function_call"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	/// <return>The visitor result.</return>
	Result VisitFunction_call([NotNull] ArcscriptParser.Function_callContext context);
	/// <summary>
	/// Visit a parse tree produced by <see cref="ArcscriptParser.void_function_call"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	/// <return>The visitor result.</return>
	Result VisitVoid_function_call([NotNull] ArcscriptParser.Void_function_callContext context);
	/// <summary>
	/// Visit a parse tree produced by <see cref="ArcscriptParser.sign"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	/// <return>The visitor result.</return>
	Result VisitSign([NotNull] ArcscriptParser.SignContext context);
	/// <summary>
	/// Visit a parse tree produced by <see cref="ArcscriptParser.variable_list"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	/// <return>The visitor result.</return>
	Result VisitVariable_list([NotNull] ArcscriptParser.Variable_listContext context);
	/// <summary>
	/// Visit a parse tree produced by <see cref="ArcscriptParser.compound_condition_or"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	/// <return>The visitor result.</return>
	Result VisitCompound_condition_or([NotNull] ArcscriptParser.Compound_condition_orContext context);
	/// <summary>
	/// Visit a parse tree produced by <see cref="ArcscriptParser.compound_condition_and"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	/// <return>The visitor result.</return>
	Result VisitCompound_condition_and([NotNull] ArcscriptParser.Compound_condition_andContext context);
	/// <summary>
	/// Visit a parse tree produced by <see cref="ArcscriptParser.negated_unary_condition"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	/// <return>The visitor result.</return>
	Result VisitNegated_unary_condition([NotNull] ArcscriptParser.Negated_unary_conditionContext context);
	/// <summary>
	/// Visit a parse tree produced by <see cref="ArcscriptParser.unary_condition"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	/// <return>The visitor result.</return>
	Result VisitUnary_condition([NotNull] ArcscriptParser.Unary_conditionContext context);
	/// <summary>
	/// Visit a parse tree produced by <see cref="ArcscriptParser.condition"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	/// <return>The visitor result.</return>
	Result VisitCondition([NotNull] ArcscriptParser.ConditionContext context);
	/// <summary>
	/// Visit a parse tree produced by <see cref="ArcscriptParser.conditional_operator"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	/// <return>The visitor result.</return>
	Result VisitConditional_operator([NotNull] ArcscriptParser.Conditional_operatorContext context);
	/// <summary>
	/// Visit a parse tree produced by <see cref="ArcscriptParser.expression"/>.
	/// </summary>
	/// <param name="context">The parse tree.</param>
	/// <return>The visitor result.</return>
	Result VisitExpression([NotNull] ArcscriptParser.ExpressionContext context);
}
} // namespace Arcweave.Interpreter
