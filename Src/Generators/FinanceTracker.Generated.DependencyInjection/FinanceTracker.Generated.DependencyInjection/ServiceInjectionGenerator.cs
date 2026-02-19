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

            var typesToInject = new List<(INamedTypeSymbol Type, string ServiceLevel, string ServiceType, string Category)>();

            var scannedAssemblies = new List<string>();
            
            foreach (var reference in compilation.References)
            {
                var symbol = compilation.GetAssemblyOrModuleSymbol(reference) as IAssemblySymbol;
                if (symbol == null)
                    continue;

                if (symbol.Name.StartsWith("FinanceTracker"))
                {
                    scannedAssemblies.Add(symbol.Name);
                    CollectTypesWithAttributes(symbol.GlobalNamespace, typesToInject);
                }
            }

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

            usingBuilder.AppendLine("using Microsoft.Extensions.DependencyInjection;");
            usingBuilder.AppendLine("using Microsoft.Extensions.DependencyInjection.Extensions;");
            usingBuilder.AppendLine("using FluentValidation;");
            
            foreach (var ns in namespaces.Distinct().OrderBy(s => s))
            {
                if (string.IsNullOrWhiteSpace(ns)) continue;
                if (ns == "Microsoft.Extensions.DependencyInjection" || ns == "Microsoft.Extensions.DependencyInjection.Extensions" || ns == "FluentValidation") continue;
                usingBuilder.AppendLine($"using {ns};");
            }



            var codeToGenerate = $@"// <auto-generated/>
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

                    if (categoryValue.Value is int enumValue)
                    {
                        var enumType = categoryValue.Type as INamedTypeSymbol;
                        if (enumType != null && enumType.TypeKind == TypeKind.Enum)
                        {
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
        if (attribute.AttributeClass != null && attribute.AttributeClass.TypeArguments.Length > 0)
        {
            var typeArg = attribute.AttributeClass.TypeArguments[0];
            return typeArg.ToDisplayString();
        }

        return string.Empty;
    }

    private static string? ExtractNamespace(string fullyQualifiedType)
    {
        if (fullyQualifiedType.Contains('.'))
        {
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