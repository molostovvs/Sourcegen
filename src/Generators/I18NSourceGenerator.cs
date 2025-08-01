using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Generators;

[Generator]
public class I18NSourceGenerator : IIncrementalGenerator
{
    private static readonly Regex ParameterRegex = new(@"\$\{\s*(\w+)\s*\}", RegexOptions.Compiled);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var i18nFiles = context.AdditionalTextsProvider
            .Where(static file => file.Path.EndsWith(".i18n.json", StringComparison.OrdinalIgnoreCase))
            .Select(static (file, ct) =>
            {
                var content = file.GetText(ct)?.ToString();
                if (content is null)
                    return null;

                var fileName = Path.GetFileName(file.Path);
                var isEnglish = fileName.IndexOf("en-US", StringComparison.OrdinalIgnoreCase) >= 0;
                var isRussian = fileName.IndexOf("ru-RU", StringComparison.OrdinalIgnoreCase) >= 0;

                return !isEnglish && !isRussian
                    ? null
                    : new I18NFile(
                    FileName: fileName,
                    Content: content,
                    IsEnglish: isEnglish,
                    IsRussian: isRussian);
            })
            .Where(static file => file is not null)
            .Select(static (file, ct) => file!);

        // Step 2: Parse each file into translations
        var parsedTranslations = i18nFiles
            .Select(static (file, ct) => ParseFileTranslations(file));

        // Step 3: Collect and combine translations by namespace
        var translationsByNamespace = parsedTranslations
            .Collect()
            .Select(static (translations, ct) => CombineTranslations(translations));

        // Step 4: Generate code for each namespace separately
        var namespaceSources = translationsByNamespace
            .SelectMany(static (namespaces, ct) => namespaces)
            .Select(static (ns, ct) => GenerateNamespaceSource(ns));

        // Step 5: Register output for each namespace
        context.RegisterSourceOutput(namespaceSources,
            static (ctx, source) =>
            {
                if (source.HasValue)
                {
                    var (fileName, sourceText) = source.Value;

                    if (sourceText is not null)
                        ctx.AddSource(fileName, sourceText);
                }
            });
    }

    private static ParsedFile? ParseFileTranslations(I18NFile file)
    {
        try
        {
            var translations = JsonSerializer.Deserialize<Dictionary<string, string>>(file.Content);
            if (translations is null)
                return null;

            var parsedTranslations = new List<ParsedTranslation>();

            foreach (var kvp in translations)
            {
                var parts = kvp.Key.Split(':');
                if (parts.Length != 2)
                    continue;

                parsedTranslations.Add(new ParsedTranslation(
                    Namespace: parts[0],
                    Key: parts[1],
                    Value: kvp.Value,
                    IsEnglish: file.IsEnglish,
                    IsRussian: file.IsRussian));
            }

            return new ParsedFile(
                FileName: file.FileName,
                Translations: new EquatableArray<ParsedTranslation>(parsedTranslations));
        }
        catch
        {
            return new ParsedFile(
                FileName: file.FileName,
                Translations: EquatableArray<ParsedTranslation>.Empty);
        }
    }

    private static ImmutableArray<NamespaceTranslations> CombineTranslations(
        ImmutableArray<ParsedFile?> files)
    {
        var translationsByKey = new Dictionary<string, Dictionary<string, TranslationData>>();

        foreach (var file in files)
        {
            if (file?.Translations is null)
                continue;

            foreach (var translation in file.Translations)
            {
                if (!translationsByKey.ContainsKey(translation.Namespace))
                {
                    translationsByKey[translation.Namespace] = new Dictionary<string, TranslationData>();
                }

                var nsTranslations = translationsByKey[translation.Namespace];

                if (!nsTranslations.ContainsKey(translation.Key))
                {
                    nsTranslations[translation.Key] = new TranslationData(
                        Key: translation.Key,
                        EnglishValue: "",
                        RussianValue: "",
                        Parameters: EquatableArray<string>.Empty);
                }

                var data = nsTranslations[translation.Key];

                if (translation.IsEnglish)
                {
                    var parameters = ExtractParameters(translation.Value);
                    nsTranslations[translation.Key] = data with
                    {
                        EnglishValue = translation.Value,
                        Parameters = parameters
                    };
                }

                if (translation.IsRussian)
                {
                    nsTranslations[translation.Key] = data with
                    {
                        RussianValue = translation.Value
                    };
                }
            }
        }

        var result = new List<NamespaceTranslations>();

        foreach (var ns in translationsByKey)
            result.Add(new NamespaceTranslations(
                Namespace: ns.Key,
                Translations: new EquatableArray<TranslationData>(ns.Value.Values)));

        return [.. result];
    }

    private static EquatableArray<string> ExtractParameters(string template)
    {
        var matches = ParameterRegex.Matches(template);
        var parameters = new List<string>();
        var seen = new HashSet<string>();

        foreach (Match match in matches)
        {
            var paramName = match.Groups[1].Value;
            if (seen.Add(paramName))
                parameters.Add(paramName);
        }

        return new EquatableArray<string>(parameters);
    }

    private static (string FileName, SourceText? SourceText)? GenerateNamespaceSource(NamespaceTranslations ns)
    {
        if (ns.Translations.Count == 0)
            return null;

        var code = GenerateNamespaceCode(ns);
        var sourceText = SourceText.From(code, Encoding.UTF8);

        return ($"{ns.Namespace}I18N.g.cs", sourceText);
    }

    private static string GenerateNamespaceCode(NamespaceTranslations ns)
    {
        var sb = new StringBuilder();

        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine();
        sb.AppendLine("namespace I18N");
        sb.AppendLine("{");

        // Generate main static class
        sb.AppendLine($"    public static class {ns.Namespace}I18N");
        sb.AppendLine("    {");

        foreach (var translation in ns.Translations)
        {
            if (translation.Parameters.Count == 0)
            {
                // Simple property for keys without parameters
                sb.AppendLine($"        /// <summary>");
                sb.AppendLine($"        /// EN: {EscapeXml(translation.EnglishValue)} <br/>");
                sb.AppendLine($"        /// RU: {EscapeXml(translation.RussianValue)} ");
                sb.AppendLine($"        /// </summary>");
                sb.AppendLine($"        public static I{translation.Key}SimpleKey {translation.Key} => new {translation.Key}SimpleKey();");
            }
            else
            {
                // Property that returns the initializer for keys with parameters
                sb.AppendLine($"        /// <summary>");
                sb.AppendLine($"        /// EN: {EscapeXml(translation.EnglishValue)} <br/> ");
                sb.AppendLine($"        /// RU: {EscapeXml(translation.RussianValue)} ");
                sb.AppendLine($"        /// </summary>");
                sb.AppendLine($"        public static I{translation.Key}Initializer {translation.Key} => new {translation.Key}Builder();");
            }
        }

        sb.AppendLine("    }");
        sb.AppendLine();

        // Generate interfaces and builders for each key
        foreach (var translation in ns.Translations)
        {
            if (translation.Parameters.Count == 0)
            {
                GenerateSimpleKey(sb, translation);
            }
            else
            {
                GenerateFluentBuilder(sb, translation);
            }
        }

        sb.AppendLine("}");

        return sb.ToString();
    }

    private static void GenerateSimpleKey(StringBuilder sb, TranslationData translation)
    {
        // Interface
        sb.AppendLine($"    public interface I{translation.Key}SimpleKey");
        sb.AppendLine("    {");
        sb.AppendLine("        string Render(string locale);");
        sb.AppendLine("    }");
        sb.AppendLine();

        // Implementation
        sb.AppendLine($"    internal class {translation.Key}SimpleKey : I{translation.Key}SimpleKey");
        sb.AppendLine("    {");
        sb.AppendLine("        public string Render(string locale)");
        sb.AppendLine("        {");
        sb.AppendLine("            return locale?.ToUpperInvariant() switch");
        sb.AppendLine("            {");
        if (!string.IsNullOrEmpty(translation.EnglishValue))
            sb.AppendLine($"                \"EN-US\" => @\"{EscapeString(translation.EnglishValue)}\",");
        if (!string.IsNullOrEmpty(translation.RussianValue))
            sb.AppendLine($"                \"RU-RU\" => @\"{EscapeString(translation.RussianValue)}\",");
        sb.AppendLine($"                _ => throw new System.NotSupportedException($\"Translation not found for locale '{{locale}}' and key '{translation.Key}'\")");
        sb.AppendLine("            };");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();
    }

    private static void GenerateFluentBuilder(StringBuilder sb, TranslationData translation)
    {
        var parameters = translation.Parameters.ToArray();

        if (parameters.Length == 1)
        {
            // For single parameter, simplify the interface structure
            sb.AppendLine($"    public interface I{translation.Key}Initializer");
            sb.AppendLine("    {");
            sb.AppendLine($"        I{translation.Key}Ready With{parameters[0]}(string {ToCamelCase(parameters[0])});");
            sb.AppendLine("    }");
            sb.AppendLine();
        }
        else
        {
            // Generate interfaces for fluent pattern - for multiple parameters
            for (int i = 0; i < parameters.Length; i++)
            {
                var currentParam = parameters[i];
                var interfaceName = i == 0 ? $"I{translation.Key}Initializer" : $"I{translation.Key}With{parameters[i - 1]}";

                sb.AppendLine($"    public interface {interfaceName}");
                sb.AppendLine("    {");

                if (i < parameters.Length - 1)
                {
                    var nextInterface = $"I{translation.Key}With{currentParam}";
                    sb.AppendLine($"        {nextInterface} With{currentParam}(string {ToCamelCase(currentParam)});");
                }
                else
                {
                    // Last parameter interface also has Render method
                    sb.AppendLine($"        I{translation.Key}Ready With{currentParam}(string {ToCamelCase(currentParam)});");
                }

                sb.AppendLine("    }");
                sb.AppendLine();
            }
        }

        // Ready interface with Render method
        sb.AppendLine($"    public interface I{translation.Key}Ready");
        sb.AppendLine("    {");
        sb.AppendLine("        string Render(string locale);");
        sb.AppendLine("    }");
        sb.AppendLine();

        // Generate builder class that implements all interfaces
        sb.Append($"    internal class {translation.Key}Builder : I{translation.Key}Initializer");
        for (int i = 0; i < parameters.Length - 1; i++)
        {
            sb.Append($", I{translation.Key}With{parameters[i]}");
        }
        sb.AppendLine($", I{translation.Key}Ready");
        sb.AppendLine("    {");

        // Private fields for parameters
        foreach (var param in parameters)
        {
            sb.AppendLine($"        private string _{ToCamelCase(param)};");
        }
        sb.AppendLine();

        // With methods
        for (int i = 0; i < parameters.Length; i++)
        {
            var param = parameters[i];
            var returnType = i < parameters.Length - 1
                ? $"I{translation.Key}With{param}"
                : $"I{translation.Key}Ready";

            sb.AppendLine($"        public {returnType} With{param}(string {ToCamelCase(param)})");
            sb.AppendLine("        {");
            sb.AppendLine($"            _{ToCamelCase(param)} = {ToCamelCase(param)};");
            sb.AppendLine("            return this;");
            sb.AppendLine("        }");
            sb.AppendLine();
        }

        // Render method with string.Format
        sb.AppendLine("        public string Render(string locale)");
        sb.AppendLine("        {");
        sb.AppendLine("            var template = locale?.ToUpperInvariant() switch");
        sb.AppendLine("            {");

        // Generate templates with indexed placeholders
        if (!string.IsNullOrEmpty(translation.EnglishValue))
        {
            var englishTemplate = ConvertToFormatString(translation.EnglishValue, parameters);
            sb.AppendLine($"                \"EN-US\" => @\"{EscapeString(englishTemplate)}\",");
        }
        if (!string.IsNullOrEmpty(translation.RussianValue))
        {
            var russianTemplate = ConvertToFormatString(translation.RussianValue, parameters);
            sb.AppendLine($"                \"RU-RU\" => @\"{EscapeString(russianTemplate)}\",");
        }

        sb.AppendLine($"                _ => throw new System.NotSupportedException($\"Translation not found for locale '{{locale}}' and key '{translation.Key}'\")");
        sb.AppendLine("            };");
        sb.AppendLine();

        // Build the parameters array for string.Format
        sb.AppendLine("            var args = new object[]");
        sb.AppendLine("            {");
        for (int i = 0; i < parameters.Length; i++)
        {
            var comma = i < parameters.Length - 1 ? "," : "";
            sb.AppendLine($"                _{ToCamelCase(parameters[i])} ?? string.Empty{comma}");
        }
        sb.AppendLine("            };");
        sb.AppendLine();
        sb.AppendLine("            return string.Format(template, args);");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();
    }

    private static string ToCamelCase(string str)
    {
        if (string.IsNullOrEmpty(str))
            return str;
        return char.ToLowerInvariant(str[0]) + str.Substring(1);
    }

    private static string EscapeString(string str)
    {
        return str.Replace("\"", "\"\"");
    }

    private static string EscapeXml(string str)
    {
        return str
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;");
    }

    private static string ConvertToFormatString(string template, string[] parameters)
    {
        if (string.IsNullOrEmpty(template) || parameters.Length == 0)
            return template;

        var result = template;

        // Replace each parameter with its index
        for (int i = 0; i < parameters.Length; i++)
        {
            var placeholder = $"${{ {parameters[i]} }}";
            result = result.Replace(placeholder, $"{{{i}}}");
        }

        return result;
    }

    // Equatable data models for proper caching
    private sealed record I18NFile(
        string FileName,
        string Content,
        bool IsEnglish,
        bool IsRussian);

    private sealed record ParsedTranslation(
        string Namespace,
        string Key,
        string Value,
        bool IsEnglish,
        bool IsRussian) : IEquatable<ParsedTranslation>;

    private sealed record ParsedFile(
        string FileName,
        EquatableArray<ParsedTranslation> Translations);

    private sealed record TranslationData(
        string Key,
        string EnglishValue,
        string RussianValue,
        EquatableArray<string> Parameters) : IEquatable<TranslationData>;

    private sealed record NamespaceTranslations(
        string Namespace,
        EquatableArray<TranslationData> Translations);
}
