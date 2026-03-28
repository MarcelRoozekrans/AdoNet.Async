using Microsoft.CodeAnalysis;
using System.Data.Async.DataSet.Generator.Emit;
using System.Data.Async.DataSet.Generator.Model;
using System.Data.Async.DataSet.Generator.Parsing;

namespace System.Data.Async.DataSet.Generator;

[Generator(LanguageNames.CSharp)]
public sealed class TypedDataSetGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var xsdFiles = context.AdditionalTextsProvider
            .Where(static file => file.Path.EndsWith(".xsd", StringComparison.OrdinalIgnoreCase));

#pragma warning disable EPS06 // Hidden struct copy — IncrementalValuesProvider is a readonly struct by design
        var models = xsdFiles.Select(static (file, ct) =>
#pragma warning restore EPS06
            {
                var text = file.GetText(ct)?.ToString();
                if (text == null) return default;

                try
                {
                    var model = XsdParser.Parse(text);
                    return (Model: model, Error: (string?)null, FilePath: file.Path);
                }
                catch (Exception ex)
                {
                    return (Model: (DataSetModel?)null, Error: ex.ToString(), FilePath: file.Path);
                }
            });

        context.RegisterSourceOutput(models, static (spc, result) =>
        {
            if (result.Error != null)
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    Diagnostics.InvalidXsd, Location.None, result.FilePath, result.Error));
                return;
            }

            var model = result.Model;
            if (model == null) return;

            // Emit DataSet class
            spc.AddSource($"{model.Name}.AsyncDataSet.g.cs", DataSetEmitter.Emit(model));

            // Emit per-table types
            foreach (var table in model.Tables)
            {
                spc.AddSource($"{model.Name}.{table.Name}.AsyncDataTable.g.cs",
                    DataTableEmitter.Emit(model.Name, table, model.Relations, model.Tables));

                spc.AddSource($"{model.Name}.{table.Name}.AsyncDataRow.g.cs",
                    DataRowEmitter.Emit(model.Name, table, model.Relations, model.Tables));

                spc.AddSource($"{model.Name}.{table.Name}.Events.g.cs",
                    EventArgsEmitter.Emit(model.Name, table));
            }
        });
    }
}
