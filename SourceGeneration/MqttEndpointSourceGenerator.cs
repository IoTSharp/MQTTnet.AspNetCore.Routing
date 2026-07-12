using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;

namespace MQTTnet.AspNetCore.Routing.SourceGeneration;

/// <summary>
/// 为显式 opt-in 的 MQTT controller 生成无反射 route 注册和 action 调用委托。
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class MqttEndpointSourceGenerator : IIncrementalGenerator
{
    private const string GeneratedControllerAttributeName = "MQTTnet.AspNetCore.Routing.Attributes.MqttGeneratedControllerAttribute";
    private const string RouteAttributeName = "MQTTnet.AspNetCore.Routing.MqttRouteAttribute";
    private const string FromRouteAttributeName = "MQTTnet.AspNetCore.Routing.Attributes.FromMqttRouteAttribute";
    private const string MqttResultTypeName = "MQTTnet.AspNetCore.Routing.MqttResult";

    private static readonly DiagnosticDescriptor UnsupportedController = new(
        "MQTTGEN001",
        "MQTT controller 无法生成",
        "Controller '{0}' 必须是非抽象、非泛型且具有一个可访问实例构造函数",
        "MQTT.SourceGeneration",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor UnsupportedAction = new(
        "MQTTGEN002",
        "MQTT action 签名无法生成",
        "Action '{0}' 当前仅支持同步 MqttResult 返回值和 [FromMqttRoute] string 参数",
        "MQTT.SourceGeneration",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly SymbolDisplayFormat TypeDisplayFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions:
            SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
            SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var controllers = context.SyntaxProvider.CreateSyntaxProvider(
            static (node, _) => node is ClassDeclarationSyntax type && type.AttributeLists.Count > 0,
            static (syntaxContext, cancellationToken) => CreateControllerModel(syntaxContext, cancellationToken))
            .Where(static model => model is not null);

        context.RegisterSourceOutput(
            controllers.Collect(),
            static (productionContext, models) => Emit(productionContext, models));
    }

    private static ControllerModel? CreateControllerModel(
        GeneratorSyntaxContext context,
        CancellationToken cancellationToken)
    {
        var syntax = (ClassDeclarationSyntax)context.Node;
        if (context.SemanticModel.GetDeclaredSymbol(syntax, cancellationToken) is not INamedTypeSymbol type
            || !HasAttribute(type, GeneratedControllerAttributeName))
        {
            return null;
        }

        if (type.IsAbstract || type.IsGenericType)
        {
            return ControllerModel.Invalid(type, syntax.GetLocation(), UnsupportedController);
        }

        var constructor = type.InstanceConstructors
            .Where(static ctor => !ctor.IsStatic && IsAccessible(ctor.DeclaredAccessibility))
            .OrderByDescending(static ctor => ctor.Parameters.Length)
            .FirstOrDefault();
        if (constructor is null)
        {
            return ControllerModel.Invalid(type, syntax.GetLocation(), UnsupportedController);
        }

        string[] prefixes = GetRouteTemplates(type.GetAttributes());
        if (prefixes.Length == 0)
            prefixes = [string.Empty];

        var actions = new List<ActionModel>();
        foreach (var method in type.GetMembers().OfType<IMethodSymbol>())
        {
            string[] actionRoutes = GetRouteTemplates(method.GetAttributes());
            if (actionRoutes.Length == 0)
                continue;

            if (method.IsStatic
                || method.IsGenericMethod
                || !IsAccessible(method.DeclaredAccessibility)
                || !InheritsFrom(method.ReturnType, MqttResultTypeName)
                || method.Parameters.Any(static parameter =>
                    parameter.Type.SpecialType != SpecialType.System_String
                    || !HasAttribute(parameter, FromRouteAttributeName)))
            {
                actions.Add(ActionModel.Invalid(method, method.Locations.FirstOrDefault(), UnsupportedAction));
                continue;
            }

            var parameters = method.Parameters
                .Select(parameter => new ParameterModel(
                    EscapeIdentifier(parameter.Name),
                    GetBindingName(parameter)))
                .ToArray();

            foreach (string prefix in prefixes)
            {
                foreach (string actionRoute in actionRoutes)
                {
                    actions.Add(new ActionModel(
                        method.Name,
                        CombineTemplates(prefix, actionRoute),
                        parameters,
                        method.Locations.FirstOrDefault(),
                        null));
                }
            }
        }

        return new ControllerModel(
            type.ToDisplayString(TypeDisplayFormat),
            constructor.Parameters.Select(parameter => parameter.Type.ToDisplayString(TypeDisplayFormat)).ToArray(),
            actions.ToArray(),
            syntax.GetLocation(),
            null,
            type.ToDisplayString());
    }

    private static void Emit(SourceProductionContext context, ImmutableArray<ControllerModel?> models)
    {
        var controllers = models.Where(static model => model is not null).Cast<ControllerModel>().ToArray();
        foreach (var controller in controllers)
        {
            if (controller.Diagnostic is not null)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    controller.Diagnostic,
                    controller.Location,
                    controller.DisplayName));
            }

            foreach (var action in controller.Actions)
            {
                if (action.Diagnostic is not null)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        action.Diagnostic,
                        action.Location,
                        controller.DisplayName + "." + action.MethodName));
                }
            }
        }

        var endpoints = controllers
            .Where(static controller => controller.Diagnostic is null)
            .SelectMany(static controller => controller.Actions
                .Where(static action => action.Diagnostic is null)
                .Select(action => new EndpointModel(controller, action)))
            .ToArray();
        if (endpoints.Length == 0)
            return;

        var source = new StringBuilder();
        source.AppendLine("// <auto-generated />");
        source.AppendLine("#nullable enable");
        source.AppendLine();
        source.AppendLine("internal static class MyGeneratedMqttEndpoints");
        source.AppendLine("{");
        source.AppendLine("    public static void Map(global::MQTTnet.AspNetCore.Routing.MqttApplicationMessageRouteBuilder routes)");
        source.AppendLine("    {");
        source.AppendLine("        if (routes is null) throw new global::System.ArgumentNullException(nameof(routes));");
        for (int i = 0; i < endpoints.Length; i++)
        {
            source.Append("        routes.Map(")
                .Append(Literal(endpoints[i].Action.RouteTemplate))
                .Append(", Invoke_")
                .Append(i.ToString(CultureInfo.InvariantCulture))
                .AppendLine(");");
        }
        source.AppendLine("    }");

        for (int i = 0; i < endpoints.Length; i++)
        {
            EmitEndpoint(source, endpoints[i], i);
        }

        source.AppendLine("}");
        context.AddSource("MyGeneratedMqttEndpoints.g.cs", SourceText.From(source.ToString(), Encoding.UTF8));
    }

    private static void EmitEndpoint(StringBuilder source, EndpointModel endpoint, int index)
    {
        source.AppendLine();
        source.Append("    private static async global::System.Threading.Tasks.ValueTask Invoke_")
            .Append(index.ToString(CultureInfo.InvariantCulture))
            .AppendLine("(global::MQTTnet.AspNetCore.Routing.MqttApplicationMessageRouteContext context)");
        source.AppendLine("    {");
        source.Append("        var controller = new ").Append(endpoint.Controller.TypeName).AppendLine("(");
        for (int i = 0; i < endpoint.Controller.ConstructorParameterTypes.Length; i++)
        {
            source.Append("            global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<")
                .Append(endpoint.Controller.ConstructorParameterTypes[i])
                .Append(">(context.Services)")
                .AppendLine(i + 1 == endpoint.Controller.ConstructorParameterTypes.Length ? string.Empty : ",");
        }
        source.AppendLine("        );");
        source.AppendLine("        global::MQTTnet.AspNetCore.Routing.MqttGeneratedEndpointHelpers.InitializeController(controller, context);");
        source.Append("        var result = controller.").Append(endpoint.Action.MethodName).Append('(');
        for (int i = 0; i < endpoint.Action.Parameters.Length; i++)
        {
            if (i > 0) source.Append(", ");
            source.Append("context.GetRouteValue(").Append(Literal(endpoint.Action.Parameters[i].BindingName)).Append(')');
        }
        source.AppendLine(");");
        source.AppendLine("        await global::MQTTnet.AspNetCore.Routing.MqttGeneratedEndpointHelpers.ExecuteResultAsync(result, context).ConfigureAwait(false);");
        source.AppendLine("    }");
    }

    private static string[] GetRouteTemplates(ImmutableArray<AttributeData> attributes)
        => attributes
            .Where(attribute => IsAttribute(attribute, RouteAttributeName))
            .Select(attribute => attribute.ConstructorArguments.Length > 0
                ? attribute.ConstructorArguments[0].Value as string ?? string.Empty
                : string.Empty)
            .ToArray();

    private static string GetBindingName(IParameterSymbol parameter)
    {
        var attribute = parameter.GetAttributes().First(item => IsAttribute(item, FromRouteAttributeName));
        return attribute.ConstructorArguments.Length > 0
            ? attribute.ConstructorArguments[0].Value as string ?? parameter.Name
            : parameter.Name;
    }

    private static bool HasAttribute(ISymbol symbol, string metadataName)
        => symbol.GetAttributes().Any(attribute => IsAttribute(attribute, metadataName));

    private static bool IsAttribute(AttributeData attribute, string metadataName)
        => string.Equals(attribute.AttributeClass?.ToDisplayString(), metadataName, StringComparison.Ordinal);

    private static bool InheritsFrom(ITypeSymbol type, string metadataName)
    {
        for (ITypeSymbol? current = type; current is not null; current = current.BaseType)
        {
            if (string.Equals(current.ToDisplayString(), metadataName, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private static bool IsAccessible(Accessibility accessibility)
        => accessibility is Accessibility.Public or Accessibility.Internal or Accessibility.ProtectedOrInternal;

    private static string CombineTemplates(string prefix, string route)
    {
        string left = prefix.Trim('/');
        string right = route.Trim('/');
        if (left.Length == 0) return right;
        if (right.Length == 0) return left;
        return left + "/" + right;
    }

    private static string Literal(string value)
        => SymbolDisplay.FormatLiteral(value, quote: true);

    private static string EscapeIdentifier(string value)
        => SyntaxFacts.GetKeywordKind(value) == SyntaxKind.None ? value : "@" + value;

    private sealed record ControllerModel(
        string TypeName,
        string[] ConstructorParameterTypes,
        ActionModel[] Actions,
        Location? Location,
        DiagnosticDescriptor? Diagnostic,
        string DisplayName)
    {
        public static ControllerModel Invalid(INamedTypeSymbol type, Location? location, DiagnosticDescriptor diagnostic)
            => new(type.ToDisplayString(TypeDisplayFormat), [], [], location, diagnostic, type.ToDisplayString());
    }

    private sealed record ActionModel(
        string MethodName,
        string RouteTemplate,
        ParameterModel[] Parameters,
        Location? Location,
        DiagnosticDescriptor? Diagnostic)
    {
        public static ActionModel Invalid(IMethodSymbol method, Location? location, DiagnosticDescriptor diagnostic)
            => new(method.Name, string.Empty, [], location, diagnostic);
    }

    private sealed record ParameterModel(string ParameterName, string BindingName);

    private sealed record EndpointModel(ControllerModel Controller, ActionModel Action);
}
