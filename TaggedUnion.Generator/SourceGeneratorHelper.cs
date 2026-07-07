using System.Text;
using Microsoft.CodeAnalysis;

using static Macaron.TaggedUnion.UnionCaseStorageKind;
using static Microsoft.CodeAnalysis.SymbolDisplayFormat;

namespace Macaron.TaggedUnion;

public static class SourceGenerationHelper
{
    public const string Indent = "    ";

    public static string GetPartialTypeDeclarationString(INamedTypeSymbol typeSymbol)
    {
        var typeKind = GetTypeKindString(typeSymbol);
        var typeName = typeSymbol.ToDisplayString(MinimallyQualifiedFormat);

        return $"partial {typeKind} {typeName}";

        #region Local Functions
        static string GetTypeKindString(INamedTypeSymbol typeSymbol)
        {
            if (typeSymbol.IsRecord)
            {
                return typeSymbol.TypeKind is TypeKind.Struct ? "record struct" : "record" ;
            }

            return typeSymbol.TypeKind switch
            {
                TypeKind.Class => "class",
                TypeKind.Struct => "struct",
                TypeKind.Interface => "interface",
                _ => throw new InvalidOperationException($"Invalid type kind: {typeSymbol.TypeKind}")
            };
        }
        #endregion
    }

    public static void WriteOperatorOverloads(
        string leadingWhitespace,
        StringBuilder builder,
        string typeName
    )
    {
        builder.AppendLine($"{leadingWhitespace}#region Operator Overloads");
        builder.AppendLine($"{leadingWhitespace}public static bool operator ==({typeName} left, {typeName} right)");
        builder.AppendLine($"{leadingWhitespace}{{");
        builder.AppendLine($"{leadingWhitespace}{Indent}return left.Equals(right);");
        builder.AppendLine($"{leadingWhitespace}}}");
        builder.AppendLine();
        builder.AppendLine($"{leadingWhitespace}public static bool operator !=({typeName} left, {typeName} right)");
        builder.AppendLine($"{leadingWhitespace}{{");
        builder.AppendLine($"{leadingWhitespace}{Indent}return !left.Equals(right);");
        builder.AppendLine($"{leadingWhitespace}}}");
        builder.AppendLine($"{leadingWhitespace}#endregion");
    }

    public static void WriteConversionOperators(string leadingWhitespace, StringBuilder builder, UnionContext context)
    {
        var typeName = context.TypeName;
        var caseContexts = context.CaseContexts;

        AppendLine("#region Conversion Operators");

        for (var i = 0; i < caseContexts.Length; i++)
        {
            var caseContext = caseContexts[i];
            var caseTypeName = caseContext.FullyQualifiedTypeName;

            // implicit
            AppendLine($"public static implicit operator {typeName}({caseTypeName} value)");
            AppendLine("{");
            AppendLine($"{Indent}return new {typeName}(value);");
            AppendLine("}");
            AppendLine();

            // explicit
            var nullableAnnotation = caseContext.StorageKind == Reference ? "?" : "";

            AppendLine($"public static explicit operator {caseTypeName}({typeName} value)");
            AppendLine("{");
            AppendLine($"{Indent}if (value.TryGetValue(out {caseTypeName}{nullableAnnotation} result))");
            AppendLine($"{Indent}{{");
            AppendLine($"{Indent}{Indent}return result;");
            AppendLine($"{Indent}}}");
            AppendLine();
            AppendLine($"{Indent}throw new global::System.InvalidCastException($\"Unable to cast object of type '{{typeof({typeName})}}' to type '{{typeof({caseTypeName})}}'.\");");
            AppendLine("}");

            if (i < caseContexts.Length - 1)
            {
                AppendLine();
            }
        }

        AppendLine("#endregion");

        #region Local Functions
        void AppendLine(string text = "")
        {
            if (text.Length > 0)
            {
                builder.Append(leadingWhitespace);
                builder.AppendLine(text);
            }
            else
            {
                builder.AppendLine("");
            }
        }
        #endregion
    }

    public static void WriteFields(string leadingWhitespace, StringBuilder builder, UnionContext context)
    {
        var caseContexts = context.CaseContexts;

        AppendLine("#region Fields");
        AppendLine("private readonly byte _tag;");

        if (caseContexts.Any(x => x.StorageKind == Reference))
        {
            AppendLine("private readonly object? _reference;");
        }

        AppendLine("#endregion");

        #region Local Functions
        void AppendLine(string text = "")
        {
            if (text.Length > 0)
            {
                builder.Append(leadingWhitespace);
                builder.AppendLine(text);
            }
            else
            {
                builder.AppendLine("");
            }
        }
        #endregion
    }

    public static void WriteConstructors(string leadingWhitespace, StringBuilder builder, UnionContext context)
    {
        var typeName = context.TypeName;
        var caseContexts = context.CaseContexts;

        AppendLine("#region Constructors");

        for (var i = 0; i < caseContexts.Length; i++)
        {
            var caseContext = caseContexts[i];

            AppendLine($"public {typeName}({caseContext.FullyQualifiedTypeName} value)");
            AppendLine("{");

            if (caseContext.StorageKind == Reference)
            {
                AppendLine($"{Indent}if ((object?)value == null)");
                AppendLine($"{Indent}{{");
                AppendLine($"{Indent}{Indent}throw new global::System.ArgumentNullException(nameof(value));");
                AppendLine($"{Indent}}}");
                AppendLine();
            }

            AppendLine($"{Indent}_tag = {caseContext.Tag};");

            if (caseContext.StorageKind == Reference)
            {
                AppendLine($"{Indent}_reference = value;");
            }

            AppendLine("}");

            if (i < caseContexts.Length - 1)
            {
                AppendLine();
            }
        }

        AppendLine("#endregion");

        #region Local Functions
        void AppendLine(string text = "")
        {
            if (text.Length > 0)
            {
                builder.Append(leadingWhitespace);
                builder.AppendLine(text);
            }
            else
            {
                builder.AppendLine("");
            }
        }
        #endregion
    }

    public static void WriteProperties(
        string leadingWhitespace,
        StringBuilder builder,
        UnionContext context
    )
    {
        AppendLine("#region Properties");
        AppendLine("public bool HasValue => _tag != 0;");
        AppendLine();
        AppendLine("public object? Value => _tag switch");
        AppendLine("{");
        AppendLine($"{Indent}0 => null,");

        foreach (var caseContext in context.CaseContexts)
        {
            var storageKind = caseContext.StorageKind;

            switch (storageKind)
            {
                case Reference:
                {
                    AppendLine($"{Indent}{caseContext.Tag} => _reference,");

                    break;
                }
                default:
                    throw new InvalidOperationException($"Invalid storage kind: {storageKind}");
            }
        }

        AppendLine($"{Indent}_ => throw new global::System.InvalidOperationException($\"Invalid tag: {{_tag}}\"),");
        AppendLine("};");
        AppendLine("#endregion");

        #region Local Functions
        void AppendLine(string text = "")
        {
            if (text.Length > 0)
            {
                builder.Append(leadingWhitespace);
                builder.AppendLine(text);
            }
            else
            {
                builder.AppendLine("");
            }
        }
        #endregion
    }

    public static void WriteEquatableInterface(
        string leadingWhitespace,
        StringBuilder builder,
        UnionContext context
    )
    {
        var typeName = context.TypeName;

        AppendLine($"#region IEquatable<{typeName}> Interface");
        AppendLine($"public bool Equals({typeName} other)");
        AppendLine("{");
        AppendLine($"{Indent}if (_tag != other._tag)");
        AppendLine($"{Indent}{{");
        AppendLine($"{Indent}{Indent}return false;");
        AppendLine($"{Indent}}}");
        AppendLine();
        AppendLine($"{Indent}return _tag switch");
        AppendLine($"{Indent}{{");
        AppendLine($"{Indent}{Indent}0 => true,");

        foreach (var caseContext in context.CaseContexts)
        {
            var storageKind = caseContext.StorageKind;
            var caseTypeName = caseContext.FullyQualifiedTypeName;

            switch (storageKind)
            {
                case Reference:
                {
                    AppendLine($"{Indent}{Indent}{caseContext.Tag} => global::System.Collections.Generic.EqualityComparer<{caseTypeName}>.Default.Equals(({caseTypeName})_reference!, ({caseTypeName})other._reference!),");

                    break;
                }
                default:
                    throw new InvalidOperationException($"Invalid storage kind: {storageKind}");
            }
        }

        AppendLine($"{Indent}{Indent}_ => throw new global::System.InvalidOperationException($\"Invalid tag: {{_tag}}\"),");
        AppendLine($"{Indent}}};");
        AppendLine("}");
        AppendLine("#endregion");

        #region Local Functions
        void AppendLine(string text = "")
        {
            if (text.Length > 0)
            {
                builder.Append(leadingWhitespace);
                builder.AppendLine(text);
            }
            else
            {
                builder.AppendLine("");
            }
        }
        #endregion
    }

    public static void WriteOverrides(
        string leadingWhitespace,
        StringBuilder builder,
        UnionContext context
    )
    {
        var typeName = context.TypeName;

        AppendLine("#region Overrides");

        // Equals
        AppendLine("public override bool Equals(object? obj)");
        AppendLine("{");
        AppendLine($"{Indent}return obj is {typeName} other && Equals(other);");
        AppendLine("}");
        AppendLine();

        // GetHashCode
        AppendLine("public override int GetHashCode()");
        AppendLine("{");
        AppendLine($"{Indent}return _tag switch");
        AppendLine($"{Indent}{{");
        AppendLine($"{Indent}{Indent}0 => 0,");

        foreach (var caseContext in context.CaseContexts)
        {
            var storageKind = caseContext.StorageKind;

            switch (storageKind)
            {
                case Reference:
                {
                    AppendLine($"{Indent}{Indent}{caseContext.Tag} => global::System.HashCode.Combine(_tag, _reference),");

                    break;
                }
                default:
                    throw new InvalidOperationException($"Invalid storage kind: {storageKind}");
            }
        }

        AppendLine($"{Indent}{Indent}_ => throw new global::System.InvalidOperationException($\"Invalid tag: {{_tag}}\"),");
        AppendLine($"{Indent}}};");
        AppendLine("}");
        AppendLine();

        // ToString
        AppendLine("public override string ToString()");
        AppendLine("{");
        AppendLine($"{Indent}return _tag switch");
        AppendLine($"{Indent}{{");
        AppendLine($"{Indent}{Indent}0 => \"<Uninitialized>\",");

        foreach (var caseContext in context.CaseContexts)
        {
            var storageKind = caseContext.StorageKind;

            switch (storageKind)
            {
                case Reference:
                {
                    AppendLine($"{Indent}{Indent}{caseContext.Tag} => $\"{{_reference}}\",");

                    break;
                }
                default:
                    throw new InvalidOperationException($"Invalid storage kind: {storageKind}");
            }
        }

        AppendLine($"{Indent}{Indent}_ => throw new global::System.InvalidOperationException($\"Invalid tag: {{_tag}}\"),");
        AppendLine($"{Indent}}};");
        AppendLine("}");

        AppendLine("#endregion");

        #region Local Functions
        void AppendLine(string text = "")
        {
            if (text.Length > 0)
            {
                builder.Append(leadingWhitespace);
                builder.AppendLine(text);
            }
            else
            {
                builder.AppendLine("");
            }
        }
        #endregion
    }

    public static void WriteMethods(
        string leadingWhitespace,
        StringBuilder builder,
        UnionContext context
    )
    {
        AppendLine("#region Methods");

        // TryGet
        foreach (var caseContext in context.CaseContexts)
        {
            var storageKind = caseContext.StorageKind;
            var caseTypeName = caseContext.FullyQualifiedTypeName;
            var nullableAnnotation = caseContext.StorageKind == Reference ? "?" : "";

            AppendLine($"public bool TryGetValue([global::System.Diagnostics.CodeAnalysis.NotNullWhen(returnValue: true)] out {caseTypeName}{nullableAnnotation} value)");
            AppendLine("{");
            AppendLine($"{Indent}if (_tag != {caseContext.Tag})");
            AppendLine($"{Indent}{{");
            AppendLine($"{Indent}{Indent}value = default;");
            AppendLine();
            AppendLine($"{Indent}{Indent}return false;");
            AppendLine($"{Indent}}}");
            AppendLine($"{Indent}else");
            AppendLine($"{Indent}{{");

            switch (storageKind)
            {
                case Reference:
                {
                    AppendLine($"{Indent}{Indent}value = ({caseTypeName})_reference!;");

                    break;
                }
                default:
                    throw new InvalidOperationException($"Invalid storage kind: {storageKind}");
            }

            AppendLine();
            AppendLine($"{Indent}{Indent}return true;");
            AppendLine($"{Indent}}}");

            AppendLine("}");
            AppendLine();
        }

        // Switch
        AppendLine("public void Switch(");

        for (var i = 0; i < context.CaseContexts.Length; i++)
        {
            var caseContext = context.CaseContexts[i];
            var separator = i < context.CaseContexts.Length - 1 ? "," : "";

            AppendLine($"{Indent}global::System.Action<{caseContext.FullyQualifiedTypeName}> {caseContext.ParamName}{separator}");
        }

        AppendLine(")");
        AppendLine("{");
        AppendLine($"{Indent}switch (_tag)");
        AppendLine($"{Indent}{{");
        AppendLine($"{Indent}{Indent}case 0: throw new global::System.InvalidOperationException(\"Value not initialized.\");");

        foreach (var caseContext in context.CaseContexts)
        {
            var storageKind = caseContext.StorageKind;
            var typeName = caseContext.FullyQualifiedTypeName;
            var paramName = caseContext.ParamName;

            switch (storageKind)
            {
                case Reference:
                {
                    AppendLine($"{Indent}{Indent}case {caseContext.Tag}: {paramName}(({typeName})_reference!); return;");

                    break;
                }
                default:
                    throw new InvalidOperationException($"Invalid storage kind: {storageKind}");
            }
        }

        AppendLine($"{Indent}{Indent}default: throw new global::System.InvalidOperationException($\"Invalid tag: {{_tag}}\");");
        AppendLine($"{Indent}}}");
        AppendLine("}");
        AppendLine();

        // Match
        AppendLine("public TResult Match<TResult>(");

        for (var i = 0; i < context.CaseContexts.Length; i++)
        {
            var caseContext = context.CaseContexts[i];
            var separator = i < context.CaseContexts.Length - 1 ? "," : "";

            AppendLine($"{Indent}global::System.Func<{caseContext.FullyQualifiedTypeName}, TResult> {caseContext.ParamName}{separator}");
        }

        AppendLine(")");
        AppendLine("{");
        AppendLine($"{Indent}return _tag switch");
        AppendLine($"{Indent}{{");
        AppendLine($"{Indent}{Indent}0 => throw new global::System.InvalidOperationException(\"Value not initialized.\"),");

        foreach (var caseContext in context.CaseContexts)
        {
            var storageKind = caseContext.StorageKind;
            var typeName = caseContext.FullyQualifiedTypeName;
            var paramName = caseContext.ParamName;


            switch (storageKind)
            {
                case Reference:
                {
                    AppendLine($"{Indent}{Indent}{caseContext.Tag} => {paramName}(({typeName})_reference!),");

                    break;
                }
                default:
                    throw new InvalidOperationException($"Invalid storage kind: {storageKind}");
            }
        }

        AppendLine($"{Indent}{Indent}_ => throw new global::System.InvalidOperationException($\"Invalid tag: {{_tag}}\"),");
        AppendLine($"{Indent}}};");
        AppendLine("}");

        AppendLine("#endregion");

        #region Local Functions
        void AppendLine(string text = "")
        {
            if (text.Length > 0)
            {
                builder.Append(leadingWhitespace);
                builder.AppendLine(text);
            }
            else
            {
                builder.AppendLine("");
            }
        }
        #endregion
    }
}
