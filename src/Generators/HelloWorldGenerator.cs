using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Generators;

[Generator]
public class HelloWorldGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static postInitializationContext =>
        {
            postInitializationContext.AddEmbeddedAttributeDefinition();
            postInitializationContext.AddSource("HelloWorldAttribute.g.cs", SourceText.From("""
                using System;
                using Microsoft.CodeAnalysis;

                namespace Generators.Attributes
                {
                    [AttributeUsage(AttributeTargets.Class), Embedded]
                    internal sealed class GenerateMethodAttribute : Attribute
                    {
                    }
                }
                """, Encoding.UTF8));
        });

        var pipeline = context.SyntaxProvider.ForAttributeWithMetadataName(
            fullyQualifiedMetadataName: "Generators.Attributes.GenerateMethodAttribute",
            predicate: static (syntaxNode, _) => syntaxNode is ClassDeclarationSyntax,
            transform: static (context, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                var classSymbol = (INamedTypeSymbol)context.TargetSymbol;
                return new HelloWorldModel(
                    classSymbol.ContainingNamespace?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted)) ?? "",
                    classSymbol.Name);
            }
        );

        context.RegisterSourceOutput(pipeline, static (context, model) =>
        {
            var sourceText = SourceText.From($$"""
                using System;
                {{(string.IsNullOrEmpty(model.Namespace) ? "" : $"namespace {model.Namespace};")}}

                partial class {{model.ClassName}}
                {
                    public void HelloWorld() => Console.WriteLine("Hello, class!");
                }
                """, Encoding.UTF8);

            context.AddSource($"{model.ClassName}.HelloWorld.g.cs", sourceText);
        });
    }

    private record HelloWorldModel(string Namespace, string ClassName);
}
