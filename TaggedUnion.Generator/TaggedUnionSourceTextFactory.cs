using System.Text;
using Microsoft.CodeAnalysis.Text;

namespace Macaron.TaggedUnion;

internal static class TaggedUnionSourceTextFactory
{
    #region Static Methods
    public static SourceText GenerateSourceText(UnionContext context)
    {
        var writer = new TaggedUnionSourceWriter(context);
        var source = writer.Generate();

        return SourceText.From(source, Encoding.UTF8);
    }
    #endregion
}
