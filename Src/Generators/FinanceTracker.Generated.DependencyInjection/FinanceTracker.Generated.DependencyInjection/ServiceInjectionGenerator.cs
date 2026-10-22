using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FinanceTracker.Generated.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace FinanceTracker.Generated.DependencyInjection;

[Generator]
public class ServiceInjectionGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValueProvider<Compilation> compilationProvider = context.CompilationProvider;

        context.RegisterSourceOutput(compilationProvider, static (spc, compilation) =>
        {
            List<string> serviceList = [];
            List<string> validatorList = [];
            List<string> externalServiceList = [];
            HashSet<string> namespaces = [];

            List<(INamedTypeSymbol Type, string ServiceLevel, string ServiceType, string Category)> typesToInject =
                [];


            foreach (MetadataReference? reference in compilation.References)
            {
                if (compilation.GetAssemblyOrModuleSymbol(reference) is not IAssemblySymbol symbol)
                {
                    continue;
                }

                if (symbol.Name.StartsWith("FinanceTracker", StringComparison.Ordinal))
                {
                    CollectTypesWithAttributes(symbol.GlobalNamespace, typesToInject);
                }
            }

            foreach ((INamedTypeSymbol typeSymbol, string serviceLevel, string serviceType, string category) in
                     typesToInject.OrderBy(x => x.ServiceType))
            {
                string className = typeSymbol.ToDisplayString();
                string? classNamespace = typeSymbol.ContainingNamespace?.ToDisplayString();
                string? serviceNamespace = ExtractNamespace(serviceType);

                if (string.Equals(category, "Service", StringComparison.OrdinalIgnoreCase))
                {
                    serviceList.Add($"services.TryAdd{serviceLevel}<{serviceType}, {className}>();");
                    if (!string.IsNullOrWhiteSpace(classNamespace))
                    {
                        namespaces.Add(classNamespace!);
                    }

                    if (!string.IsNullOrWhiteSpace(serviceNamespace))
                    {
                        namespaces.Add(serviceNamespace!);
                    }
                }
                else if (string.Equals(category, "Validator", StringComparison.OrdinalIgnoreCase))
                {
                    validatorList.Add($"services.TryAdd{serviceLevel}<{serviceType}, {className}>();");
                    if (!string.IsNullOrWhiteSpace(classNamespace))
                    {
                        namespaces.Add(classNamespace!);
                    }

                    if (!string.IsNullOrWhiteSpace(serviceNamespace))
                    {
                        namespaces.Add(serviceNamespace!);
                    }
                }
                else if (string.Equals(category, "External", StringComparison.OrdinalIgnoreCase))
                {
                    externalServiceList.Add($"services.TryAdd{serviceLevel}<{serviceType}, {className}>();");
                    if (!string.IsNullOrWhiteSpace(classNamespace))
                    {
                        namespaces.Add(classNamespace!);
                    }

                    if (!string.IsNullOrWhiteSpace(serviceNamespace))
                    {
                        namespaces.Add(serviceNamespace!);
                    }
                }
            }

            StringBuilder serviceBody = new();
            bool isFirstService = true;
            foreach (string? serviceModel in serviceList)
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

            StringBuilder validatorBody = new();
            bool isFirstValidator = true;
            foreach (string? validatorModel in validatorList)
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

            StringBuilder externalServiceBody = new();
            bool isFirstExternalService = true;
            foreach (string? externalServiceModel in externalServiceList)
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

            StringBuilder usingBuilder = new();

            usingBuilder.AppendLine("using Microsoft.Extensions.DependencyInjection;");
            usingBuilder.AppendLine("using Microsoft.Extensions.DependencyInjection.Extensions;");
            usingBuilder.AppendLine("using FluentValidation;");

            foreach (string? ns in namespaces.Distinct().OrderBy(s => s))
            {
                if (string.IsNullOrWhiteSpace(ns))
                {
                    continue;
                }

                if (ns is "Microsoft.Extensions.DependencyInjection" or "Microsoft.Extensions.DependencyInjection.Extensions" or "FluentValidation")
                {
                    continue;
                }

                usingBuilder.AppendLine($"using {ns};");
            }


            string codeToGenerate = $$"""
                                      // <auto-generated/>
                                      {{usingBuilder}}
                                      namespace FinanceTracker
                                      {
                                          public partial class Program
                                          {
                                              public static void AddFinanceTrackerServices(IServiceCollection services)
                                              {
                                                  {{serviceBody}}
                                              }

                                              public static void AddFinanceTrackerValidators(IServiceCollection services)
                                              {
                                                  {{validatorBody}}
                                              }

                                              public static void AddFinanceTrackerExternalServices(IServiceCollection services)
                                              {
                                                  {{externalServiceBody}}
                                              }

                                          }
                                      }
                                      """;

            spc.AddSource("Program.g.cs", SourceText.From(codeToGenerate, Encoding.UTF8));
        });
    }

    private static void CollectTypesWithAttributes(
        INamespaceSymbol? namespaceSymbol,
        List<(INamedTypeSymbol Type, string ServiceLevel, string ServiceType, string Category)> typesToInject)
    {
        if (namespaceSymbol == null)
        {
            return;
        }

        foreach (INamespaceOrTypeSymbol? member in namespaceSymbol.GetMembers())
        {
            switch (member)
            {
                case INamedTypeSymbol { TypeKind: TypeKind.Class, IsAbstract: false } typeSymbol:
                {
                    (bool hasCategory, string category) = GetCategoryAttribute(typeSymbol);

                    if (hasCategory)
                    {
                        // A class can be registered as several services, one attribute each
                        foreach ((string serviceLevel, string serviceType) in GetServiceLevelAttributes(typeSymbol))
                        {
                            typesToInject.Add((typeSymbol, serviceLevel, serviceType, category));
                        }
                    }
                    else if (GetValidatedType(typeSymbol) is { } validatedType)
                    {
                        // Validators are found by base type rather than attributes, so projects without the
                        // generators (Contracts, shared with the front end) can declare them
                        typesToInject.Add((typeSymbol, "Scoped", $"FluentValidation.IValidator<{validatedType}>",
                            "Validator"));
                    }

                    break;
                }
                case INamespaceSymbol nestedNamespace:
                    CollectTypesWithAttributes(nestedNamespace, typesToInject);
                    break;
            }
        }
    }

    private static IEnumerable<(string ServiceLevel, string ServiceType)> GetServiceLevelAttributes(
        INamedTypeSymbol typeSymbol)
    {
        foreach (AttributeData? attribute in typeSymbol.GetAttributes())
        {
            string attributeName = attribute.AttributeClass?.Name ?? string.Empty;

            string? serviceLevel =
                attributeName.StartsWith(SourceGeneratorConstants.ScopedAttributeName, StringComparison.Ordinal)
                    ? "Scoped"
                    : attributeName.StartsWith(SourceGeneratorConstants.TransientAttributeName, StringComparison.Ordinal)
                        ? "Transient"
                        : attributeName.StartsWith(SourceGeneratorConstants.SingletonAttributeName, StringComparison.Ordinal)
                            ? "Singleton"
                            : null;

            string serviceType = GetGenericArgumentFromAttribute(attribute);

            if (serviceLevel is not null && !string.IsNullOrWhiteSpace(serviceType))
            {
                yield return (serviceLevel, serviceType);
            }
        }
    }

    /// <summary>
    /// The <c>T</c> of a FluentValidation <c>AbstractValidator&lt;T&gt;</c> the class derives from, if it does.
    /// </summary>
    private static string? GetValidatedType(INamedTypeSymbol typeSymbol)
    {
        for (INamedTypeSymbol? baseType = typeSymbol.BaseType; baseType is not null; baseType = baseType.BaseType)
        {
            if (baseType is { IsGenericType: true, TypeArguments.Length: 1 } &&
                baseType.OriginalDefinition.ToDisplayString() == "FluentValidation.AbstractValidator<T>")
            {
                return baseType.TypeArguments[0].ToDisplayString();
            }
        }

        return null;
    }

    private static (bool HasAttribute, string Category) GetCategoryAttribute(INamedTypeSymbol typeSymbol)
    {
        // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
        // This is easier to understand
        foreach (AttributeData? attribute in typeSymbol.GetAttributes())
        {
            string attributeName = attribute.AttributeClass?.Name ?? string.Empty;

            if (!attributeName.StartsWith(SourceGeneratorConstants.InjectionCategoryAttributeName, StringComparison.Ordinal))
            {
                continue;
            }

            if (attribute.ConstructorArguments.Length <= 0)
            {
                continue;
            }

            TypedConstant categoryValue = attribute.ConstructorArguments[0];

            if (categoryValue is not
                { Value: int enumValue, Type: INamedTypeSymbol { TypeKind: TypeKind.Enum } enumType })
            {
                continue;
            }

            IFieldSymbol? enumMember = enumType.GetMembers()
                .OfType<IFieldSymbol>()
                .FirstOrDefault(f => f.ConstantValue != null && (int)f.ConstantValue == enumValue);

            if (enumMember != null)
            {
                return (true, enumMember.Name);
            }
        }

        return (false, string.Empty);
    }

    private static string GetGenericArgumentFromAttribute(AttributeData attribute)
    {
        if (attribute.AttributeClass is not { TypeArguments.Length: > 0 })
        {
            return string.Empty;
        }

        ITypeSymbol typeArg = attribute.AttributeClass.TypeArguments[0];
        return typeArg.ToDisplayString();

    }

    private static string? ExtractNamespace(string fullyQualifiedType)
    {
        if (!fullyQualifiedType.Contains('.'))
        {
            return null;
        }

        string nonGenericType = fullyQualifiedType;
        if (fullyQualifiedType.Contains('<'))
        {
            nonGenericType = fullyQualifiedType.Substring(0, fullyQualifiedType.IndexOf('<'));
        }

        string[] parts = nonGenericType.Split('.');


        return parts.Length > 1 ? string.Join(".", parts.Take(parts.Length - 1)) : null;
    }
}
