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
            postInitializationContext.AddSource("myGeneratedFile.cs", SourceText.From("""
                using System;
                using Microsoft.CodeAnalysis;

                namespace GeneratedNamespace
                {
                    [AttributeUsage(AttributeTargets.Method), Embedded]
                    internal sealed class GeneratedAttribute : Attribute
                    {
                    }
                }
                """, Encoding.UTF8));
        });

        var pipeline = context.SyntaxProvider.ForAttributeWithMetadataName(
            fullyQualifiedMetadataName: "GeneratedNamespace.GeneratedAttribute",
            predicate: static (syntaxNode, cancellationToken) => syntaxNode is BaseMethodDeclarationSyntax,
            transform: static (context, cancellationToken) =>
            {
                var containingClass = context.TargetSymbol.ContainingType;
                return new Model(
                    containingClass.ContainingNamespace?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted)) ?? "",
                    containingClass.Name,
                    context.TargetSymbol.Name);
            }
        );

        context.RegisterSourceOutput(pipeline, static (context, model) =>
            {
                var sourceText = SourceText.From($$"""
                {{(string.IsNullOrEmpty(model.Namespace) ? "" : $"namespace {model.Namespace};")}}
                partial class {{model.ClassName}}
                {
                    public partial void {{model.MethodName}}()
                    {
                        System.Console.WriteLine("Generated implementation for {{model.MethodName}}!");
                    }
                }
                """, Encoding.UTF8);

                context.AddSource($"{model.ClassName}_{model.MethodName}.g.cs", sourceText);
            });
    }

    private class Model(string @namespace, string className, string methodName)
    {
        public string Namespace => @namespace;
        public string ClassName => className;
        public string MethodName => methodName;
    };
}