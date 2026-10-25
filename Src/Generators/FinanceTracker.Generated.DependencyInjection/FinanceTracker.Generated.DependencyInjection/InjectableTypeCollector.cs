using System;
using System.Collections.Generic;
using System.Linq;
using FinanceTracker.Generated.Shared;
using Microsoft.CodeAnalysis;

namespace FinanceTracker.Generated.DependencyInjection;

internal static class InjectableTypeCollector
{
    /// <summary>
    /// Every class marked <c>[InjectionCategory]</c> in the project and the referenced assemblies.
    /// </summary>
    public static List<InjectableType> Collect(Compilation compilation)
    {
        List<InjectableType> types = [];
        Collect(compilation.Assembly.GlobalNamespace, types);

        foreach (MetadataReference reference in compilation.References)
        {
            // Skip any classes out of our standard namespaces
            if (compilation.GetAssemblyOrModuleSymbol(reference) is IAssemblySymbol assembly &&
                assembly.Name.StartsWith("FinanceTracker", StringComparison.Ordinal))
            {
                Collect(assembly.GlobalNamespace, types);
            }
        }

        return types;
    }

    private static void Collect(INamespaceSymbol namespaceSymbol, List<InjectableType> types)
    {
        foreach (INamespaceOrTypeSymbol member in namespaceSymbol.GetMembers())
        {
            switch (member)
            {
                case INamedTypeSymbol { TypeKind: TypeKind.Class, IsAbstract: false } typeSymbol:
                {
                    // Skip the symbol if the required attributes are not present.
                    if (GetInjectionCategory(typeSymbol) is not { } injection)
                    {
                        break;
                    }

                    string implementationType = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                    // A class can be registered as several services, one attribute each
                    foreach ((string lifetime, string serviceType) in GetLifetimeAttributes(typeSymbol))
                    {
                        types.Add(new InjectableType(implementationType, serviceType, lifetime, injection.Category,
                            injection.Destinations));
                    }

                    break;
                }
                case INamespaceSymbol nestedNamespace:
                    Collect(nestedNamespace, types);
                    break;
            }
        }
    }

    private static IEnumerable<(string Lifetime, string ServiceType)> GetLifetimeAttributes(INamedTypeSymbol typeSymbol)
    {
        foreach (AttributeData attribute in typeSymbol.GetAttributes())
        {
            string attributeName = attribute.AttributeClass?.Name ?? string.Empty;

            string? lifetime =
                attributeName.StartsWith(SourceGeneratorConstants.ScopedAttributeName, StringComparison.Ordinal)
                    ? "Scoped"
                    : attributeName.StartsWith(SourceGeneratorConstants.TransientAttributeName, StringComparison.Ordinal)
                        ? "Transient"
                        : attributeName.StartsWith(SourceGeneratorConstants.SingletonAttributeName, StringComparison.Ordinal)
                            ? "Singleton"
                            : null;

            if (lifetime is not null && attribute.AttributeClass is { TypeArguments.Length: > 0 } attributeClass)
            {
                yield return (lifetime,
                    attributeClass.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
            }
        }
    }

    /// <summary>
    /// The category and destinations of the class's <c>[InjectionCategory]</c>, if it has one.
    /// </summary>
    private static (string Category, IReadOnlyCollection<string> Destinations)? GetInjectionCategory(
        INamedTypeSymbol typeSymbol)
    {
        AttributeData? attribute = typeSymbol.GetAttributes().FirstOrDefault(static x =>
            (x.AttributeClass?.Name ?? string.Empty).StartsWith(SourceGeneratorConstants.InjectionCategoryAttributeName,
                StringComparison.Ordinal));

        // Skip invalid configurations.
        if (attribute is not { ConstructorArguments.Length: 2 } ||
            GetEnumMemberName(attribute.ConstructorArguments[0]) is not { } category ||
            GetEnumFlagNames(attribute.ConstructorArguments[1]) is not { Count: > 0 } destinations)
        {
            return null;
        }

        return (category, destinations);
    }

    private static string? GetEnumMemberName(TypedConstant value) =>
        value is { Value: int enumValue, Type: INamedTypeSymbol { TypeKind: TypeKind.Enum } enumType }
            ? enumType.GetMembers()
                .OfType<IFieldSymbol>()
                .FirstOrDefault(f => f.ConstantValue is int constant && constant == enumValue)?.Name
            : null;

    /// <summary>
    /// The names of the flags set in a <c>[Flags]</c> enum value.
    /// </summary>
    private static List<string>? GetEnumFlagNames(TypedConstant value) =>
        value is { Value: int enumValue, Type: INamedTypeSymbol { TypeKind: TypeKind.Enum } enumType }
            ? enumType.GetMembers()
                .OfType<IFieldSymbol>()
                .Where(f => f.ConstantValue is int flag && flag != 0 && (enumValue & flag) == flag)
                .Select(static f => f.Name)
                .ToList()
            : null;
}
