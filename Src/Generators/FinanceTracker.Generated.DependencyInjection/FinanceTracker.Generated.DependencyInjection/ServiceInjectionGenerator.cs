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

                if (symbol.Name.StartsWith("FinanceTracker"))
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
                    (bool hasServiceLevel, string serviceLevel, string serviceType) = GetServiceLevelAttribute(typeSymbol);
                    (bool hasCategory, string category) = GetCategoryAttribute(typeSymbol);

                    if (hasServiceLevel && hasCategory && !string.IsNullOrWhiteSpace(serviceType))
                    {
                        typesToInject.Add((typeSymbol, serviceLevel, serviceType, category));
                    }

                    break;
                }
                case INamespaceSymbol nestedNamespace:
                    CollectTypesWithAttributes(nestedNamespace, typesToInject);
                    break;
            }
        }
    }

    private static (bool HasAttribute, string ServiceLevel, string ServiceType) GetServiceLevelAttribute(
        INamedTypeSymbol typeSymbol)
    {
        foreach (AttributeData? attribute in typeSymbol.GetAttributes())
        {
            string attributeName = attribute.AttributeClass?.Name ?? string.Empty;

            if (attributeName.StartsWith(SourceGeneratorConstants.ScopedAttributeName))
            {
                string serviceType = GetGenericArgumentFromAttribute(attribute);
                return (true, "Scoped", serviceType);
            }

            if (attributeName.StartsWith(SourceGeneratorConstants.TransientAttributeName))
            {
                string serviceType = GetGenericArgumentFromAttribute(attribute);
                return (true, "Transient", serviceType);
            }

            if (!attributeName.StartsWith(SourceGeneratorConstants.SingletonAttributeName))
            {
                continue;
            }

            {
                string serviceType = GetGenericArgumentFromAttribute(attribute);
                return (true, "Singleton", serviceType);
            }
        }

        return (false, string.Empty, string.Empty);
    }

    private static (bool HasAttribute, string Category) GetCategoryAttribute(INamedTypeSymbol typeSymbol)
    {
        // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
        // This is easier to understand
        foreach (AttributeData? attribute in typeSymbol.GetAttributes())
        {
            string attributeName = attribute.AttributeClass?.Name ?? string.Empty;

            if (!attributeName.StartsWith(SourceGeneratorConstants.InjectionCategoryAttributeName))
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
