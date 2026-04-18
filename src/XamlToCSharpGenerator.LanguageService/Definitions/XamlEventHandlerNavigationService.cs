using System;
using System.Linq;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using XamlToCSharpGenerator.Core.Parsing;
using XamlToCSharpGenerator.LanguageService.Completion;
using XamlToCSharpGenerator.LanguageService.Models;
using XamlToCSharpGenerator.LanguageService.Text;

namespace XamlToCSharpGenerator.LanguageService.Definitions;

/// <summary>
/// Resolves go-to-definition for XAML event handler attribute values (e.g. Click="OnClick")
/// by navigating to the handler method in the code-behind.
/// </summary>
internal static class XamlEventHandlerNavigationService
{
    private const string Xaml2006Namespace = "http://schemas.microsoft.com/winfx/2006/xaml";

    /// <summary>
    /// Attempts to resolve the definition location of an event handler method referenced
    /// by an attribute value at the given cursor position.
    /// </summary>
    public static bool TryResolveEventHandlerDefinition(
        XamlAnalysisResult analysis,
        SourcePosition position,
        out XamlDefinitionLocation location)
    {
        location = default!;

        if (analysis.XmlDocument?.Root is not XElement root
            || analysis.Compilation is null
            || !XamlXmlSourceRangeService.TryFindAttributeAtPosition(
                analysis.Document.Text,
                analysis.XmlDocument,
                position,
                out var element,
                out var attribute,
                out _,
                out var attributeValueRange)
            || !IsPositionInRange(position, attributeValueRange)
            || attribute.IsNamespaceDeclaration
            || string.Equals(attribute.Name.NamespaceName, Xaml2006Namespace, StringComparison.Ordinal)
            || !XamlEventHandlerNameSemantics.TryParseHandlerName(attribute.Value, out var handlerName)
            || string.IsNullOrWhiteSpace(handlerName)
            || !XamlSemanticSourceTypeResolver.TryResolveElementTypeSymbol(analysis, element, out var elementType)
            || !HasEvent(elementType, attribute.Name.LocalName)
            || !TryResolveRootType(root, analysis.Compilation, out var rootType)
            || FindHandlerMethod(rootType, handlerName) is not IMethodSymbol method)
        {
            return false;
        }

        var symbolLocation = XamlClrNavigationLocationResolver.ResolveSymbolLocation(analysis, method);
        location = new XamlDefinitionLocation(symbolLocation.Uri, symbolLocation.Range);
        return true;
    }

    /// <summary>
    /// Attempts to resolve the method symbol of an event handler referenced
    /// by an attribute value at the given cursor position.
    /// </summary>
    public static bool TryResolveEventHandlerSymbol(
        XamlAnalysisResult analysis,
        SourcePosition position,
        out IMethodSymbol methodSymbol,
        out SourceRange valueRange)
    {
        methodSymbol = null!;
        valueRange = default;

        if (analysis.XmlDocument?.Root is not XElement root
            || analysis.Compilation is null
            || !XamlXmlSourceRangeService.TryFindAttributeAtPosition(
                analysis.Document.Text,
                analysis.XmlDocument,
                position,
                out var element,
                out var attribute,
                out _,
                out var attributeValueRange)
            || !IsPositionInRange(position, attributeValueRange)
            || attribute.IsNamespaceDeclaration
            || string.Equals(attribute.Name.NamespaceName, Xaml2006Namespace, StringComparison.Ordinal)
            || !XamlEventHandlerNameSemantics.TryParseHandlerName(attribute.Value, out var handlerName)
            || string.IsNullOrWhiteSpace(handlerName)
            || !XamlSemanticSourceTypeResolver.TryResolveElementTypeSymbol(analysis, element, out var elementType)
            || !HasEvent(elementType, attribute.Name.LocalName)
            || !TryResolveRootType(root, analysis.Compilation, out var rootType)
            || FindHandlerMethod(rootType, handlerName) is not IMethodSymbol method)
        {
            return false;
        }

        methodSymbol = method;
        valueRange = attributeValueRange;
        return true;
    }

    private static bool IsPositionInRange(SourcePosition position, SourceRange range)
        => !(position.Line < range.Start.Line
            || position.Line > range.End.Line
            || position.Line == range.Start.Line && position.Character < range.Start.Character
            || position.Line == range.End.Line && position.Character > range.End.Character);

    private static bool HasEvent(INamedTypeSymbol typeSymbol, string eventName)
    {
        for (var current = typeSymbol; current is not null; current = current.BaseType)
        {
            if (current.GetMembers(eventName).OfType<IEventSymbol>().Any())
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryResolveRootType(
        XElement root,
        Compilation compilation,
        out INamedTypeSymbol typeSymbol)
    {
        typeSymbol = null!;

        XAttribute? classAttribute = root.Attributes().FirstOrDefault(static attribute =>
            string.Equals(attribute.Name.LocalName, "Class", StringComparison.Ordinal) &&
            string.Equals(attribute.Name.NamespaceName, Xaml2006Namespace, StringComparison.Ordinal));
        if (classAttribute is null)
        {
            return false;
        }

        var resolved = compilation.GetTypeByMetadataName(classAttribute.Value.Trim()) ??
                       compilation.GetTypeByMetadataName(classAttribute.Value.Trim().Replace('.', '+'));
        if (resolved is not INamedTypeSymbol namedType)
        {
            return false;
        }

        typeSymbol = namedType;
        return true;
    }

    private static IMethodSymbol? FindHandlerMethod(INamedTypeSymbol type, string handlerName)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            var method = current.GetMembers(handlerName)
                .OfType<IMethodSymbol>()
                .FirstOrDefault(static m => !m.IsStatic && m.MethodKind == MethodKind.Ordinary);
            if (method is not null)
            {
                return method;
            }
        }

        return null;
    }
}
