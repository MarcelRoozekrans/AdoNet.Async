using Microsoft.CodeAnalysis;

namespace System.Data.Async.DataSet.Generator;

internal static class Diagnostics
{
    public static readonly DiagnosticDescriptor InvalidXsd = new(
        id: "ADAG001",
        title: "Invalid XSD schema",
        messageFormat: "Failed to parse XSD file '{0}': {1}",
        category: "AdoNet.Async.DataSet.Generator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}
