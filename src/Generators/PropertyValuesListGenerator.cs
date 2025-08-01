using System.Collections.Immutable;
using System.Linq;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Generators;

[Generator]
public class PropertyValuesListGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static postInitializationContext =>
        {
            postInitializationContext.AddEmbeddedAttributeDefinition();
            postInitializationContext.AddSource("GeneratePropertyValuesListAttribute.g.cs", SourceText.From("""
                using System;
                using Microsoft.CodeAnalysis;

                namespace Generators.Attributes
                {
                    [AttributeUsage(AttributeTargets.Class), Embedded]
                    internal sealed class GeneratePropertyValuesListAttribute : Attribute
                    {
                        public Type PropertyType { get; }

                        public GeneratePropertyValuesListAttribute(Type propertyType)
                        {
                            PropertyType = propertyType;
                        }
                    }
                }
                """, Encoding.UTF8));
        });

        var pipeline = context.SyntaxProvider.ForAttributeWithMetadataName(
            fullyQualifiedMetadataName: "Generators.Attributes.GeneratePropertyValuesListAttribute",
            predicate: static (syntaxNode, cancellationToken) => syntaxNode is ClassDeclarationSyntax,
            transform: static (context, cancellationToken) =>
            {
                var classSymbol = (INamedTypeSymbol)context.TargetSymbol;
                var attributeData = context.Attributes[0];

                if (attributeData.ConstructorArguments.Length == 0 ||
                    attributeData.ConstructorArguments[0].Value is not INamedTypeSymbol targetType)
                    return null;

                // Get all properties of the target type
                var matchingProperties = classSymbol.GetMembers()
                    .OfType<IPropertySymbol>()
                    .Where(p => p.Type.Equals(targetType, SymbolEqualityComparer.Default) &&
                               p.GetMethod != null && p.SetMethod != null)
                    .Select(p => p.Name)
                    .ToImmutableArray();

                return new PropertyValuesListModel(
                    classSymbol.ContainingNamespace?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted)) ?? "",
                    classSymbol.Name,
                    targetType.ToDisplayString(),
                    GetMethodName(targetType.Name),
                    matchingProperties);
            }
        ).Where(static model => model is not null);

        context.RegisterSourceOutput(pipeline, static (context, model) =>
        {
            if (model!.Properties.IsEmpty)
                return;

            var propertiesCode = string.Join(",\n            ", model.Properties);

            var sourceText = SourceText.From($$"""
                {{(string.IsNullOrEmpty(model.Namespace) ? "" : $"namespace {model.Namespace};")}}

                partial class {{model.ClassName}}
                {
                    public System.Collections.Generic.List<{{model.TypeName}}> {{model.MethodName}}()
                    {
                        return 
                        [
                            {{propertiesCode}}
                        ];
                    }
                }
                """, Encoding.UTF8);

            context.AddSource($"{model.ClassName}.{model.MethodName}.g.cs", sourceText);
        });
    }

    private static string GetMethodName(string typeName) => typeName switch
    {
        "String" => "GetAllStringValues",
        "Int32" => "GetAllIntValues",
        "Boolean" => "GetAllBooleanValues",
        "Double" => "GetAllDoubleValues",
        "Decimal" => "GetAllDecimalValues",
        _ => $"GetAll{typeName}Values"
    };

    private record PropertyValuesListModel(
        string Namespace,
        string ClassName,
        string TypeName,
        string MethodName,
        ImmutableArray<string> Properties);
}
