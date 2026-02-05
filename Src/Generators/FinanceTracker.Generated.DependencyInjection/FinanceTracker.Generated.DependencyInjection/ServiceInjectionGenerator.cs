using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FinanceTracker.Generated.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;


namespace FinanceTracker.Generated.DependencyInjection;


[Generator]
public class ServiceInjectionGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var compilationProvider = context.CompilationProvider;

        context.RegisterSourceOutput(compilationProvider, static (spc, compilation) =>
        {
            var serviceList = new List<string>();
            var validatorList = new List<string>();
            var externalServiceList = new List<string>();
            var namespaces = new HashSet<string>();

            // Collect types with attributes from referenced FinanceTracker assemblies
            var typesToInject = new List<(INamedTypeSymbol Type, string ServiceLevel, string ServiceType, string Category)>();

            // Get all referenced assemblies
            var debugInfo = new StringBuilder();
            var scannedAssemblies = new List<string>();
            
            foreach (var reference in compilation.References)
            {
                var symbol = compilation.GetAssemblyOrModuleSymbol(reference) as IAssemblySymbol;
                if (symbol == null)
                    continue;

                // Scan all FinanceTracker assemblies
                if (symbol.Name.StartsWith("FinanceTracker"))
                {
                    scannedAssemblies.Add(symbol.Name);
                    CollectTypesWithAttributes(symbol.GlobalNamespace, typesToInject);
                }
            }

            // Process collected types
            foreach (var (typeSymbol, serviceLevel, serviceType, category) in typesToInject.OrderBy(x => x.ServiceType))
            {
                var className = typeSymbol.ToDisplayString();
                var classNamespace = typeSymbol.ContainingNamespace?.ToDisplayString();
                var serviceNamespace = ExtractNamespace(serviceType);

                if (string.Equals(category, "Service", StringComparison.OrdinalIgnoreCase))
                {
                    serviceList.Add($"services.TryAdd{serviceLevel}<{serviceType}, {className}>();");
                    if (!string.IsNullOrWhiteSpace(classNamespace)) namespaces.Add(classNamespace!);
                    if (!string.IsNullOrWhiteSpace(serviceNamespace)) namespaces.Add(serviceNamespace!);
                }
                else if (string.Equals(category, "Validator", StringComparison.OrdinalIgnoreCase))
                {
                    validatorList.Add($"services.TryAdd{serviceLevel}<{serviceType}, {className}>();");
                    if (!string.IsNullOrWhiteSpace(classNamespace)) namespaces.Add(classNamespace!);
                    if (!string.IsNullOrWhiteSpace(serviceNamespace)) namespaces.Add(serviceNamespace!);
                }
                else if (string.Equals(category, "External", StringComparison.OrdinalIgnoreCase))
                {
                    externalServiceList.Add($"services.TryAdd{serviceLevel}<{serviceType}, {className}>();");
                    if (!string.IsNullOrWhiteSpace(classNamespace)) namespaces.Add(classNamespace!);
                    if (!string.IsNullOrWhiteSpace(serviceNamespace)) namespaces.Add(serviceNamespace!);
                }
            }

            var serviceBody = new StringBuilder();
            bool isFirstService = true;
            foreach (var serviceModel in serviceList)
            {
                if (isFirstService)
                {
                    serviceBody.AppendLine(serviceModel);
                    isFirstService = false;
                }
                else
                {
                    serviceBody.AppendLine("            " + serviceModel);
                }
            }

            var validatorBody = new StringBuilder();
            bool isFirstValidator = true;
            foreach (var validatorModel in validatorList)
            {
                if (isFirstValidator)
                {
                    validatorBody.AppendLine(validatorModel);
                    isFirstValidator = false;
                }
                else
                {
                    validatorBody.AppendLine("            " + validatorModel);
                }
            }

            var externalServiceBody = new StringBuilder();
            bool isFirstExternalService = true;
            foreach (var externalServiceModel in externalServiceList)
            {
                if (isFirstExternalService)
                {
                    externalServiceBody.AppendLine(externalServiceModel);
                    isFirstExternalService = false;
                }
                else
                {
                    externalServiceBody.AppendLine("            " + externalServiceModel);
                }
            }

            var usingBuilder = new StringBuilder();
            // Always include DI and extension usings
            usingBuilder.AppendLine("using Microsoft.Extensions.DependencyInjection;");
            usingBuilder.AppendLine("using Microsoft.Extensions.DependencyInjection.Extensions;");
            usingBuilder.AppendLine("using FluentValidation;");
            
            // Include discovered namespaces
            foreach (var ns in namespaces.Distinct().OrderBy(s => s))
            {
                if (string.IsNullOrWhiteSpace(ns)) continue;
                if (ns == "Microsoft.Extensions.DependencyInjection" || ns == "Microsoft.Extensions.DependencyInjection.Extensions" || ns == "FluentValidation") continue;
                usingBuilder.AppendLine($"using {ns};");
            }

            // Debug info comment
            var debugComment = $"// Scanned assemblies: {string.Join(", ", scannedAssemblies)}\n// Found types: {typesToInject.Count}";

            var codeToGenerate = $@"// <auto-generated/>
{debugComment}
{usingBuilder}
namespace FinanceTracker
{{
    public partial class Program 
    {{
        public static void AddFinanceTrackerServices(IServiceCollection services)
        {{
            {serviceBody}
        }}

        public static void AddFinanceTrackerValidators(IServiceCollection services)
        {{
            {validatorBody}
        }}

        public static void AddFinanceTrackerExternalServices(IServiceCollection services)
        {{
            {externalServiceBody}
        }}

    }}
}}";

            spc.AddSource("Program.g.cs", SourceText.From(codeToGenerate, Encoding.UTF8));
        });
    }

    private static void CollectTypesWithAttributes(
        INamespaceSymbol? namespaceSymbol,
        List<(INamedTypeSymbol Type, string ServiceLevel, string ServiceType, string Category)> typesToInject)
    {
        if (namespaceSymbol == null)
            return;

        // Process types in current namespace
        foreach (var member in namespaceSymbol.GetMembers())
        {
            if (member is INamedTypeSymbol typeSymbol && typeSymbol.TypeKind == TypeKind.Class && !typeSymbol.IsAbstract)
            {
                var (hasServiceLevel, serviceLevel, serviceType) = GetServiceLevelAttribute(typeSymbol);
                var (hasCategory, category) = GetCategoryAttribute(typeSymbol);

                if (hasServiceLevel && hasCategory && !string.IsNullOrWhiteSpace(serviceType))
                {
                    typesToInject.Add((typeSymbol, serviceLevel, serviceType, category));
                }
            }
            else if (member is INamespaceSymbol nestedNamespace)
            {
                // Recursively process nested namespaces
                CollectTypesWithAttributes(nestedNamespace, typesToInject);
            }
        }
    }

    private static (bool HasAttribute, string ServiceLevel, string ServiceType) GetServiceLevelAttribute(INamedTypeSymbol typeSymbol)
    {
        foreach (var attribute in typeSymbol.GetAttributes())
        {
            var attributeName = attribute.AttributeClass?.Name ?? string.Empty;

            if (attributeName.StartsWith(SourceGeneratorConstants.ScopedAttributeName))
            {
                var serviceType = GetGenericArgumentFromAttribute(attribute);
                return (true, "Scoped", serviceType);
            }
            else if (attributeName.StartsWith(SourceGeneratorConstants.TransientAttributeName))
            {
                var serviceType = GetGenericArgumentFromAttribute(attribute);
                return (true, "Transient", serviceType);
            }
            else if (attributeName.StartsWith(SourceGeneratorConstants.SingletonAttributeName))
            {
                var serviceType = GetGenericArgumentFromAttribute(attribute);
                return (true, "Singleton", serviceType);
            }
        }

        return (false, string.Empty, string.Empty);
    }

    private static (bool HasAttribute, string Category) GetCategoryAttribute(INamedTypeSymbol typeSymbol)
    {
        foreach (var attribute in typeSymbol.GetAttributes())
        {
            var attributeName = attribute.AttributeClass?.Name ?? string.Empty;

            if (attributeName.StartsWith(SourceGeneratorConstants.InjectionCategoryAttributeName))
            {
                if (attribute.ConstructorArguments.Length > 0)
                {
                    var categoryValue = attribute.ConstructorArguments[0];

                    // The constructor argument is an enum value, resolve the name
                    if (categoryValue.Value is int enumValue)
                    {
                        var enumType = categoryValue.Type as INamedTypeSymbol;
                        if (enumType != null && enumType.TypeKind == TypeKind.Enum)
                        {
                            // Get the enum member name for this value
                            var enumMember = enumType.GetMembers()
                                .OfType<IFieldSymbol>()
                                .FirstOrDefault(f => f.ConstantValue != null && (int)f.ConstantValue == enumValue);

                            if (enumMember != null)
                            {
                                return (true, enumMember.Name);
                            }
                        }
                    }
                }
            }
        }

        return (false, string.Empty);
    }

    private static string GetGenericArgumentFromAttribute(AttributeData attribute)
    {
        // The generic argument is in the attribute class's type arguments
        // For [Scoped<IValidator<LoginRequest>>], the IValidator<LoginRequest> will be in TypeArguments[0]
        if (attribute.AttributeClass != null && attribute.AttributeClass.TypeArguments.Length > 0)
        {
            var typeArg = attribute.AttributeClass.TypeArguments[0];
            return typeArg.ToDisplayString();
        }

        return string.Empty;
    }

    private static string? ExtractNamespace(string fullyQualifiedType)
    {
        // Extract namespace from fully qualified type name
        // Handle generic types like "FluentValidation.IValidator<FinanceTracker.Models.Request.Auth.LoginRequest>"
        if (fullyQualifiedType.Contains('.'))
        {
            // Remove generic type parameters first
            var nonGenericType = fullyQualifiedType;
            if (fullyQualifiedType.Contains('<'))
            {
                nonGenericType = fullyQualifiedType.Substring(0, fullyQualifiedType.IndexOf('<'));
            }

            var parts = nonGenericType.Split('.');
            if (parts.Length > 1)
            {
                return string.Join(".", parts.Take(parts.Length - 1));
            }
        }

        return null;
    }
}