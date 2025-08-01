using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Generators;

[Generator]
public class GenerateBodyGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static postInitializationContext =>
        {
            postInitializationContext.AddEmbeddedAttributeDefinition();
            postInitializationContext.AddSource("GenerateBodyAttribute.g.cs", SourceText.From("""
                using System;
                using Microsoft.CodeAnalysis;

                namespace Generators.Attributes
                {
                    [AttributeUsage(AttributeTargets.Method), Embedded]
                    internal sealed class GenerateBodyAttribute : Attribute
                    {
                    }
                }
                """, Encoding.UTF8));
        });

        var pipeline = context.SyntaxProvider.ForAttributeWithMetadataName(
            fullyQualifiedMetadataName: "Generators.Attributes.GenerateBodyAttribute",
            predicate: static (syntaxNode, cancellationToken) => syntaxNode is MethodDeclarationSyntax,
            transform: static (context, cancellationToken) =>
            {
                var methodSymbol = (IMethodSymbol)context.TargetSymbol;
                var containingClass = methodSymbol.ContainingType;
                return new GenerateBodyModel(
                    containingClass.ContainingNamespace?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted)) ?? "",
                    containingClass.Name,
                    methodSymbol.Name);
            }
        );

        context.RegisterSourceOutput(pipeline, static (context, model) =>
        {
            var sourceText = SourceText.From($$"""
                using System;
                {{(string.IsNullOrEmpty(model.Namespace) ? "" : $"namespace {model.Namespace};")}}

                partial class {{model.ClassName}}
                {
                    /// <summary>
                    /// Says "Hello, body!" just for fun.
                    /// </summary>
                    public partial void {{model.MethodName}}() => Console.WriteLine("Hello, body!");
                }
                """, Encoding.UTF8);

            context.AddSource($"{model.ClassName}.{model.MethodName}.g.cs", sourceText);
        });
    }

    private record GenerateBodyModel(string Namespace, string ClassName, string MethodName);
}
