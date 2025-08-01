using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Generators;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class I18NAnalyzer : DiagnosticAnalyzer
{
    private static readonly Regex ParameterRegex = new(@"\$\{\s*(\w+)\s*\}", RegexOptions.Compiled);

    public static readonly DiagnosticDescriptor MissingTranslationRule = new(
        "I18N001",
        "Missing Translation",
        "Translation key '{0}' is missing {1} translation",
        "I18N",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A translation key is being used but it's missing one or more language translations.");

    public static readonly DiagnosticDescriptor ParameterMismatchRule = new(
        "I18N002",
        "Parameter Mismatch",
        "Translation key '{0}' has mismatched parameters between languages",
        "I18N",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The parameters in different language versions of the same key don't match.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [MissingTranslationRule, ParameterMismatchRule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        // Register to analyze member access expressions
        context.RegisterSyntaxNodeAction(AnalyzeMemberAccess, SyntaxKind.SimpleMemberAccessExpression);

        // Register to analyze additional files at compilation start
        context.RegisterCompilationStartAction(compilationContext =>
        {
            var translationData = LoadTranslationData(compilationContext.Options.AdditionalFiles);

            compilationContext.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeMemberAccessWithData(nodeContext, translationData),
                SyntaxKind.SimpleMemberAccessExpression);
        });
    }

    private static void AnalyzeMemberAccess(SyntaxNodeAnalysisContext context)
    {
        // This is a placeholder for simple analysis without translation data
        // The real analysis happens in AnalyzeMemberAccessWithData
    }

    private static void AnalyzeMemberAccessWithData(
        SyntaxNodeAnalysisContext context,
        Dictionary<string, TranslationInfo> translationData)
    {
        var memberAccess = (MemberAccessExpressionSyntax)context.Node;

        // Check if this is accessing an I18N namespace
        var identifier = memberAccess.Name.Identifier.Text;

        // Get the full expression text to check if it's an I18N class
        var expressionText = memberAccess.Expression.ToString();

        // Check if this is accessing a *I18N class
        if (!expressionText.EndsWith("I18N"))
            return;

        // Extract namespace from the class name (e.g., "BalanceI18N" -> "Balance")
        var namespaceName = expressionText.Replace("I18N", "");
        if (string.IsNullOrEmpty(namespaceName))
            return;

        // Build the full key name
        var fullKey = $"{namespaceName}:{identifier}";

        // Check if we have translation data for this key
        if (translationData.TryGetValue(fullKey, out var info))
        {
            var missingLanguages = new List<string>();

            if (string.IsNullOrEmpty(info.EnglishValue))
                missingLanguages.Add("EN-US");

            if (string.IsNullOrEmpty(info.RussianValue))
                missingLanguages.Add("RU-RU");

            if (missingLanguages.Any())
            {
                var diagnostic = Diagnostic.Create(
                    MissingTranslationRule,
                    memberAccess.Name.GetLocation(),
                    identifier,
                    string.Join(" and ", missingLanguages));

                context.ReportDiagnostic(diagnostic);
            }

            // Check for parameter mismatches
            if (!string.IsNullOrEmpty(info.EnglishValue) && !string.IsNullOrEmpty(info.RussianValue))
            {
                var enParams = ExtractParameters(info.EnglishValue!);
                var ruParams = ExtractParameters(info.RussianValue!);

                if (!enParams.SetEquals(ruParams))
                {
                    var diagnostic = Diagnostic.Create(
                        ParameterMismatchRule,
                        memberAccess.Name.GetLocation(),
                        identifier);

                    context.ReportDiagnostic(diagnostic);
                }
            }
        }
    }

    private static Dictionary<string, TranslationInfo> LoadTranslationData(
        ImmutableArray<AdditionalText> additionalFiles)
    {
        var result = new Dictionary<string, TranslationInfo>();

        foreach (var file in additionalFiles)
        {
            if (!file.Path.EndsWith(".i18n.json", StringComparison.OrdinalIgnoreCase))
                continue;

            var content = file.GetText()?.ToString();
            if (string.IsNullOrEmpty(content))
                continue;

            var fileName = Path.GetFileName(file.Path);
            var isEnglish = fileName.IndexOf("en-US", StringComparison.OrdinalIgnoreCase) >= 0;
            var isRussian = fileName.IndexOf("ru-RU", StringComparison.OrdinalIgnoreCase) >= 0;

            if (!isEnglish && !isRussian)
                continue;

            try
            {
                var translations = JsonSerializer.Deserialize<Dictionary<string, string>>(content!);
                if (translations == null)
                    continue;

                foreach (var kvp in translations)
                {
                    if (!result.ContainsKey(kvp.Key))
                    {
                        result[kvp.Key] = new TranslationInfo();
                    }

                    if (isEnglish)
                    {
                        result[kvp.Key].EnglishValue = kvp.Value;
                    }

                    if (isRussian)
                    {
                        result[kvp.Key].RussianValue = kvp.Value;
                    }
                }
            }
            catch
            {
                // Ignore JSON parsing errors in the analyzer
            }
        }

        return result;
    }

    private static HashSet<string> ExtractParameters(string template)
    {
        var matches = ParameterRegex.Matches(template);
        var parameters = new HashSet<string>();

        foreach (Match match in matches)
        {
            parameters.Add(match.Groups[1].Value);
        }

        return parameters;
    }

    private class TranslationInfo
    {
        public string? EnglishValue { get; set; }
        public string? RussianValue { get; set; }
    }
}
