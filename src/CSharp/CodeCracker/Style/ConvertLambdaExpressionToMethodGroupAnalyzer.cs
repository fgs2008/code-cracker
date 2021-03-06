﻿using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace CodeCracker.Style
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class ConvertLambdaExpressionToMethodGroupAnalyzer : DiagnosticAnalyzer
    {
        internal const string Title = "You should remove the lambda expression when it only invokes a method with the same signature";
        internal const string MessageFormat = "You should remove the lambda expression and pass just '{0}' instead.";
        internal const string Category = SupportedCategories.Style;
        const string Description = "The extra unnecessary layer of indirection induced by the lambda expression may be avoided by passing the method group instead.";

        internal static DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId.ConvertLambdaExpressionToMethodGroup.ToDiagnosticId(),
            Title,
            MessageFormat,
            Category,
            DiagnosticSeverity.Hidden,
            isEnabledByDefault: true,
            description: Description,
            helpLink: HelpLink.ForDiagnostic(DiagnosticId.ConvertLambdaExpressionToMethodGroup));

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context) =>
            context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.SimpleLambdaExpression, SyntaxKind.ParenthesizedLambdaExpression);

        private void AnalyzeNode(SyntaxNodeAnalysisContext context)
        {
            var lambda = context.Node as ExpressionSyntax;

            var invocation = GetInvocationIfAny(lambda);
            if (invocation == null || invocation.ArgumentList.Arguments.Count == 0) return;

            var lambdaParameters = BuildParameters(lambda);
            if (!MatchArguments(lambdaParameters, invocation.ArgumentList)) return;

            var root = lambda.SyntaxTree.GetRoot();
            var newRoot = root.ReplaceNode(lambda, invocation.Expression as ExpressionSyntax);

            var semanticNode = GetNodeRootForAnalysis(lambda);
            var newSemanticNode = newRoot.DescendantNodesAndSelf()
                .Where(x => x.SpanStart == semanticNode.SpanStart && x.Span.OverlapsWith(context.Node.Span))
                .LastOrDefault(x => x.CSharpKind() == semanticNode.CSharpKind());

            if (newSemanticNode == null || ReplacementChangesSemantics(semanticNode, newSemanticNode, context.SemanticModel)) return;

            var diagnostic = Diagnostic.Create(
                Rule,
                context.Node.GetLocation(),
                invocation.Expression.ToString());
            context.ReportDiagnostic(diagnostic);
        }

        internal static InvocationExpressionSyntax GetInvocationIfAny(SyntaxNode node)
        {
            var body = node is SimpleLambdaExpressionSyntax
                ? (node as SimpleLambdaExpressionSyntax)?.Body
                : (node as ParenthesizedLambdaExpressionSyntax)?.Body;

            var invocation = body as InvocationExpressionSyntax;
            if (invocation != null) return invocation;

            var possibleBlock = body as BlockSyntax;
            if (possibleBlock == null || possibleBlock.Statements.Count != 1) return null;

            return body.DescendantNodesAndSelf()
                .OfType<InvocationExpressionSyntax>()
                .FirstOrDefault();
        }

        private ParameterListSyntax BuildParameters(SyntaxNode node)
        {
            if (node is SimpleLambdaExpressionSyntax)
                return SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList(new ParameterSyntax[] { ((SimpleLambdaExpressionSyntax)node).Parameter }));
            if (node is ParenthesizedLambdaExpressionSyntax)
                return ((ParenthesizedLambdaExpressionSyntax)node).ParameterList;
            return null;
        }

        private bool MatchArguments(ParameterListSyntax parameters, ArgumentListSyntax arguments)
        {
            if (arguments.Arguments.Count != parameters.Parameters.Count) return false;

            var paramNameList = parameters.Parameters.Select(p => p.Identifier.Text);
            var argNameList = arguments.Arguments
                .Where(a => a.Expression is IdentifierNameSyntax)
                .Select(a => (a.Expression as IdentifierNameSyntax).Identifier.Text);

            return paramNameList.SequenceEqual(argNameList);
        }

        private SyntaxNode GetSemanticRootForSpeculation(SyntaxNode expression)
        {
            var parentNodeToSpeculate = expression
                .AncestorsAndSelf(ascendOutOfTrivia: false)
                .LastOrDefault(node => CanSpeculateOnNode(node));
            return parentNodeToSpeculate ?? expression;
        }

        private SyntaxNode GetNodeRootForAnalysis(ExpressionSyntax expression)
        {
            var parentNodeToSpeculate = expression
                .Ancestors(ascendOutOfTrivia: false)
                .FirstOrDefault(node =>
                node.CSharpKind() != SyntaxKind.Argument &&
                node.CSharpKind() != SyntaxKind.ArgumentList);
            return parentNodeToSpeculate ?? expression;
        }

        public static bool CanSpeculateOnNode(SyntaxNode node)
        {
            return (node is StatementSyntax && node.CSharpKind() != SyntaxKind.Block) ||
                node is CrefSyntax ||
                node.CSharpKind() == SyntaxKind.Attribute ||
                node.CSharpKind() == SyntaxKind.ThisConstructorInitializer ||
                node.CSharpKind() == SyntaxKind.BaseConstructorInitializer ||
                node.CSharpKind() == SyntaxKind.EqualsValueClause ||
                node.CSharpKind() == SyntaxKind.ArrowExpressionClause;
        }

        private bool ReplacementChangesSemantics(SyntaxNode originalExpression, SyntaxNode replacedExpression, SemanticModel semanticModel)
        {
            SemanticModel speculativeModel;
#pragma warning disable CC0026
            if (!Microsoft.CodeAnalysis.CSharp.CSharpExtensions.TryGetSpeculativeSemanticModel(semanticModel, originalExpression.SpanStart, (dynamic)GetSemanticRootForSpeculation(replacedExpression), out speculativeModel))
#pragma warning restore CC0026
                return true;
            var originalInfo = semanticModel.GetSymbolInfo(originalExpression);
            var replacementInfo = speculativeModel.GetSymbolInfo(replacedExpression);
            return !originalInfo.Equals(replacementInfo);
        }
    }
}