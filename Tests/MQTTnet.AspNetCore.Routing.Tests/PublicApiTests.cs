using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace MQTTnet.AspNetCore.Routing.Tests
{
    [TestClass]
    public sealed class PublicApiTests
    {
        [TestMethod]
        public void PublicApi_MatchesApprovedSnapshot()
        {
            var approvedPath = Path.Combine(AppContext.BaseDirectory, "PublicApi.approved.txt");
            var approved = Normalize(File.ReadAllText(approvedPath));
            var actual = Normalize(PublicApiSnapshot.Create(typeof(MqttRouteCatalog).Assembly));

            if (!string.Equals(approved, actual, StringComparison.Ordinal))
            {
                Assert.Fail("Public API snapshot mismatch. Actual snapshot:" + Environment.NewLine + actual);
            }
        }

        private static string Normalize(string value)
        {
            return value.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd() + "\n";
        }

        private static class PublicApiSnapshot
        {
            public static string Create(Assembly assembly)
            {
                var builder = new StringBuilder();
                var types = assembly.GetExportedTypes()
                    .Where(type => type.Namespace != null &&
                        type.Namespace.StartsWith("MQTTnet.AspNetCore.Routing", StringComparison.Ordinal))
                    .OrderBy(type => GetDisplayName(type), StringComparer.Ordinal)
                    .ToArray();

                foreach (var type in types)
                {
                    builder.AppendLine(GetTypeDeclaration(type));
                    foreach (var member in GetMembers(type))
                    {
                        builder.Append("  ");
                        builder.AppendLine(member);
                    }

                    builder.AppendLine();
                }

                return builder.ToString();
            }

            private static IEnumerable<string> GetMembers(Type type)
            {
                const BindingFlags Flags =
                    BindingFlags.Public |
                    BindingFlags.Instance |
                    BindingFlags.Static |
                    BindingFlags.DeclaredOnly;

                foreach (var constructor in type.GetConstructors(Flags).OrderBy(GetConstructorSignature, StringComparer.Ordinal))
                {
                    yield return GetConstructorSignature(constructor);
                }

                foreach (var field in type.GetFields(Flags).OrderBy(field => field.Name, StringComparer.Ordinal))
                {
                    yield return $"{GetStaticPrefix(field)}field {FormatType(field.FieldType)} {field.Name}";
                }

                foreach (var property in type.GetProperties(Flags).OrderBy(GetPropertySignature, StringComparer.Ordinal))
                {
                    yield return GetPropertySignature(property);
                }

                foreach (var eventInfo in type.GetEvents(Flags).OrderBy(item => item.Name, StringComparer.Ordinal))
                {
                    yield return $"{GetStaticPrefix(eventInfo)}event {FormatType(eventInfo.EventHandlerType!)} {eventInfo.Name}";
                }

                foreach (var method in type.GetMethods(Flags)
                             .Where(method => !method.IsSpecialName)
                             .OrderBy(GetMethodSignature, StringComparer.Ordinal))
                {
                    yield return GetMethodSignature(method);
                }
            }

            private static string GetTypeDeclaration(Type type)
            {
                var builder = new StringBuilder();
                builder.Append(GetTypeKind(type));
                builder.Append(' ');
                builder.Append(GetDisplayName(type));

                var genericArguments = type.IsGenericTypeDefinition
                    ? type.GetGenericArguments()
                    : Array.Empty<Type>();
                if (genericArguments.Length > 0)
                {
                    var constraints = genericArguments
                        .Select(GetGenericConstraint)
                        .Where(constraint => constraint.Length > 0)
                        .ToArray();
                    if (constraints.Length > 0)
                    {
                        builder.Append(' ');
                        builder.Append(string.Join(" ", constraints));
                    }
                }

                return builder.ToString();
            }

            private static string GetTypeKind(Type type)
            {
                if (type.IsInterface)
                {
                    return "interface";
                }

                if (type.IsEnum)
                {
                    return "enum";
                }

                if (typeof(MulticastDelegate).IsAssignableFrom(type.BaseType))
                {
                    return "delegate";
                }

                if (type.IsValueType)
                {
                    return "struct";
                }

                if (type.IsAbstract && type.IsSealed)
                {
                    return "static class";
                }

                if (type.IsAbstract)
                {
                    return "abstract class";
                }

                if (type.IsSealed)
                {
                    return "sealed class";
                }

                return "class";
            }

            private static string GetConstructorSignature(ConstructorInfo constructor)
            {
                return $"{GetStaticPrefix(constructor)}.ctor({FormatParameters(constructor.GetParameters())})";
            }

            private static string GetPropertySignature(PropertyInfo property)
            {
                var accessors = new List<string>();
                if (property.GetMethod?.IsPublic == true)
                {
                    accessors.Add("get");
                }

                if (property.SetMethod?.IsPublic == true)
                {
                    accessors.Add("set");
                }

                return $"{GetStaticPrefix(property)}property {FormatType(property.PropertyType)} {property.Name} {{ {string.Join("; ", accessors)}; }}";
            }

            private static string GetMethodSignature(MethodInfo method)
            {
                return $"{GetStaticPrefix(method)}method {FormatType(method.ReturnType)} {GetMethodName(method)}({FormatParameters(method.GetParameters())})";
            }

            private static string GetMethodName(MethodInfo method)
            {
                if (!method.IsGenericMethodDefinition)
                {
                    return method.Name;
                }

                return method.Name + "<" + string.Join(", ", method.GetGenericArguments().Select(argument => argument.Name)) + ">";
            }

            private static string FormatParameters(ParameterInfo[] parameters)
            {
                return string.Join(", ", parameters.Select(parameter =>
                {
                    var prefix = parameter.GetCustomAttribute<ParamArrayAttribute>() == null ? string.Empty : "params ";
                    return $"{prefix}{FormatType(parameter.ParameterType)} {parameter.Name}";
                }));
            }

            private static string GetStaticPrefix(MemberInfo member)
            {
                return member switch
                {
                    FieldInfo field when field.IsStatic => "static ",
                    PropertyInfo property when property.GetMethod?.IsStatic == true || property.SetMethod?.IsStatic == true => "static ",
                    MethodBase method when method.IsStatic => "static ",
                    EventInfo eventInfo when eventInfo.AddMethod?.IsStatic == true => "static ",
                    _ => string.Empty
                };
            }

            private static string GetDisplayName(Type type)
            {
                if (type.IsGenericType)
                {
                    var typeName = (type.FullName ?? type.Name);
                    var tickIndex = typeName.IndexOf('`');
                    if (tickIndex >= 0)
                    {
                        typeName = typeName.Substring(0, tickIndex);
                    }

                    var genericArguments = type.IsGenericTypeDefinition
                        ? type.GetGenericArguments().Select(argument => argument.Name)
                        : type.GetGenericArguments().Select(FormatType);
                    return typeName + "<" + string.Join(", ", genericArguments) + ">";
                }

                return type.FullName ?? type.Name;
            }

            private static string FormatType(Type type)
            {
                if (type == typeof(void))
                {
                    return "void";
                }

                if (type.IsByRef)
                {
                    return FormatType(type.GetElementType()!) + "&";
                }

                if (type.IsArray)
                {
                    return FormatType(type.GetElementType()!) + "[]";
                }

                if (type.IsGenericParameter)
                {
                    return type.Name;
                }

                var nullableType = Nullable.GetUnderlyingType(type);
                if (nullableType != null)
                {
                    return FormatType(nullableType) + "?";
                }

                if (type.IsGenericType)
                {
                    var typeName = type.GetGenericTypeDefinition().FullName ?? type.Name;
                    var tickIndex = typeName.IndexOf('`');
                    if (tickIndex >= 0)
                    {
                        typeName = typeName.Substring(0, tickIndex);
                    }

                    return typeName + "<" + string.Join(", ", type.GetGenericArguments().Select(FormatType)) + ">";
                }

                return type.FullName ?? type.Name;
            }

            private static string GetGenericConstraint(Type genericArgument)
            {
                var constraints = genericArgument.GetGenericParameterConstraints()
                    .Select(FormatType)
                    .ToList();
                var attributes = genericArgument.GenericParameterAttributes;

                if ((attributes & GenericParameterAttributes.ReferenceTypeConstraint) != 0)
                {
                    constraints.Insert(0, "class");
                }

                if ((attributes & GenericParameterAttributes.NotNullableValueTypeConstraint) != 0)
                {
                    constraints.Insert(0, "struct");
                }

                if ((attributes & GenericParameterAttributes.DefaultConstructorConstraint) != 0)
                {
                    constraints.Add("new()");
                }

                if (constraints.Count == 0)
                {
                    return string.Empty;
                }

                return $"where {genericArgument.Name} : {string.Join(", ", constraints)}";
            }
        }
    }
}
