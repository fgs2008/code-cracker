using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Linq;

namespace CodeCracker.Usage
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class UnusedParametersAnalyzer : DiagnosticAnalyzer
    {
        internal const string Title = "Unused parameters";
        internal const string Message = "Parameter '{0}' is not used.";
        internal const string Category = SupportedCategories.Usage;
        const string Description = "When a method declares a parameter and does not use it might bring incorrect conclusions for anyone reading the code and also demands the parameter when the method is called, unecessarily.\r\n"
            + "You should delete the parameter is such cases.";

        internal static DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId.UnusedParameters.ToDiagnosticId(),
            Title,
            Message,
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: Description,
            customTags: WellKnownDiagnosticTags.Unnecessary,
            helpLink: HelpLink.ForDiagnostic(DiagnosticId.UnusedParameters));

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context) =>
            context.RegisterSyntaxNodeAction(Analyzer, SyntaxKind.MethodDeclaration, SyntaxKind.ConstructorDeclaration);

        private void Analyzer(SyntaxNodeAnalysisContext context)
        {
            var methodOrConstructor = context.Node as BaseMethodDeclarationSyntax;
            if (methodOrConstructor == null) return;
            var semanticModel = context.SemanticModel;
            if (!IsCandidateForRemoval(methodOrConstructor, semanticModel)) return;
            if (methodOrConstructor.Body.Statements.Any())
            {
                var dataFlowAnalysis = semanticModel.AnalyzeDataFlow(methodOrConstructor.Body.Statements.First(), methodOrConstructor.Body.Statements.Last());
                if (!dataFlowAnalysis.Succeeded) return;
                foreach (var parameter in methodOrConstructor.ParameterList.Parameters)
                {
                    var parameterSymbol = semanticModel.GetDeclaredSymbol(parameter);
                    if (parameterSymbol == null) continue;
                    if (!dataFlowAnalysis.ReadInside.Contains(parameterSymbol) && !dataFlowAnalysis.WrittenInside.Contains(parameterSymbol))
                        context = ReportDiagnostic(context, parameter);
                }
            }
            else
            {
                foreach (var parameter in methodOrConstructor.ParameterList.Parameters)
                    context = ReportDiagnostic(context, parameter);
            }
        }

        private static bool IsCandidateForRemoval(BaseMethodDeclarationSyntax methodOrConstructor, SemanticModel semanticModel)
        {
            if (methodOrConstructor.Modifiers.Any(m => m.ValueText == "partial" || m.ValueText == "override")
                || !methodOrConstructor.ParameterList.Parameters.Any()
                || methodOrConstructor.Body == null)
                return false;
            var method = methodOrConstructor as MethodDeclarationSyntax;
            if (method != null)
            {
                if (method.ExplicitInterfaceSpecifier != null) return false;
                var methodSymbol = semanticModel.GetDeclaredSymbol(method);
                if (methodSymbol == null) return false;
                var typeSymbol = methodSymbol.ContainingType;
                if (typeSymbol.AllInterfaces.SelectMany(i => i.GetMembers())
                    .Any(member => methodSymbol.Equals(typeSymbol.FindImplementationForInterfaceMember(member))))
                    return false;
                if (IsEventHandlerLike(method, semanticModel)) return false;
            }
            else
            {
                var constructor = methodOrConstructor as ConstructorDeclarationSyntax;
                if (constructor != null)
                {
                    if (IsSerializationConstructor(constructor, semanticModel)) return false;
                }
                else
                {
                    return false;
                }
            }
            return true;
        }

        private static bool IsSerializationConstructor(ConstructorDeclarationSyntax constructor, SemanticModel semanticModel)
        {
            if (constructor.ParameterList.Parameters.Count != 2) return false;
            var constructorSymbol = semanticModel.GetDeclaredSymbol(constructor);
            var typeSymbol = constructorSymbol?.ContainingType;
            if (!typeSymbol?.AllInterfaces.Any(i => i.ToString() == "System.Runtime.Serialization.ISerializable") ?? true) return false;
            if (!typeSymbol.GetAttributes().Any(a => a.AttributeClass.ToString() == "System.SerializableAttribute")) return false;
            var serializationInfoType = semanticModel.GetTypeInfo(constructor.ParameterList.Parameters[0].Type).Type as INamedTypeSymbol;
            if (serializationInfoType == null) return false;
            if (!serializationInfoType.AllBaseTypesAndSelf().Any(type => type.ToString() == "System.Runtime.Serialization.SerializationInfo"))
                return false;
            var streamingContextType = semanticModel.GetTypeInfo(constructor.ParameterList.Parameters[1].Type).Type as INamedTypeSymbol;
            if (streamingContextType == null) return false;
            return streamingContextType.AllBaseTypesAndSelf().Any(type => type.ToString() == "System.Runtime.Serialization.StreamingContext");
        }

        private static bool IsEventHandlerLike(MethodDeclarationSyntax method, SemanticModel semanticModel)
        {
            if (method.ParameterList.Parameters.Count != 2
                || method.ReturnType.ToString() != "void")
                return false;
            var senderType = semanticModel.GetTypeInfo(method.ParameterList.Parameters[0].Type).Type;
            if (senderType.SpecialType != SpecialType.System_Object) return false;
            var eventArgsType = semanticModel.GetTypeInfo(method.ParameterList.Parameters[1].Type).Type as INamedTypeSymbol;
            if (eventArgsType == null) return false;
            return eventArgsType.AllBaseTypesAndSelf().Any(type => type.ToString() == "System.EventArgs");
        }

        private static SyntaxNodeAnalysisContext ReportDiagnostic(SyntaxNodeAnalysisContext context, ParameterSyntax parameter)
        {
            var diagnostic = Diagnostic.Create(Rule, parameter.GetLocation(), parameter.Identifier.ValueText);
            context.ReportDiagnostic(diagnostic);
            return context;
        }
    }
}