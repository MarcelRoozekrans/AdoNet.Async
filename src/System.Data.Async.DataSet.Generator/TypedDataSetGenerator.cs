using Microsoft.CodeAnalysis;

namespace System.Data.Async.DataSet.Generator;

[Generator(LanguageNames.CSharp)]
public sealed class TypedDataSetGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Pipeline will be implemented in subsequent tasks
    }
}
