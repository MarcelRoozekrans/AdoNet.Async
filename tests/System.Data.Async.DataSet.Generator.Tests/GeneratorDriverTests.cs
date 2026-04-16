using System.Collections.Immutable;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace System.Data.Async.DataSet.Generator.Tests;

public class GeneratorDriverTests
{
    private static string LoadSchema(string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Schemas", name);
        return File.ReadAllText(path);
    }

    private static GeneratorDriverRunResult RunGenerator(string xsdContent, string fileName = "Test.xsd")
    {
        // Create a minimal compilation — the generator only needs to emit source,
        // it doesn't need the source to compile in the test
        var compilation = CSharpCompilation.Create("TestAssembly",
            references: new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });

        var generator = new TypedDataSetGenerator();
        var driver = CSharpGeneratorDriver.Create(generator)
            .AddAdditionalTexts(ImmutableArray.Create<AdditionalText>(
                new InMemoryAdditionalText(fileName, xsdContent)));

        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(
            compilation, out _, out _);

        return driver.GetRunResult();
    }

    [Fact]
    public void Simple_Xsd_Generates_Expected_File_Count()
    {
        var result = RunGenerator(LoadSchema("Simple.xsd"), "Simple.xsd");
        result.Diagnostics.Should().BeEmpty();
        // 1 DataSet + 2 tables * 3 files (DataTable, DataRow, Events) = 7
        result.GeneratedTrees.Should().HaveCount(7);
    }

    [Fact]
    public void Simple_Xsd_DataSet_File_Contains_Class()
    {
        var result = RunGenerator(LoadSchema("Simple.xsd"), "Simple.xsd");
        var dsTree = result.GeneratedTrees.FirstOrDefault(t => t.FilePath.Contains("AsyncDataSet.g.cs"));
        dsTree.Should().NotBeNull();
        var text = dsTree!.GetText().ToString();
        text.Should().Contain("class AsyncOrdersDS");
    }

    [Fact]
    public void Simple_Xsd_Generates_Customer_Table()
    {
        var result = RunGenerator(LoadSchema("Simple.xsd"), "Simple.xsd");
        var tableTree = result.GeneratedTrees.FirstOrDefault(t => t.FilePath.Contains("Customer.AsyncDataTable.g.cs"));
        tableTree.Should().NotBeNull();
        var text = tableTree!.GetText().ToString();
        text.Should().Contain("class AsyncOrdersDSCustomerDataTable");
    }

    [Fact]
    public void Simple_Xsd_Generates_Customer_Row()
    {
        var result = RunGenerator(LoadSchema("Simple.xsd"), "Simple.xsd");
        var rowTree = result.GeneratedTrees.FirstOrDefault(t => t.FilePath.Contains("Customer.AsyncDataRow.g.cs"));
        rowTree.Should().NotBeNull();
        var text = rowTree!.GetText().ToString();
        text.Should().Contain("class AsyncOrdersDSCustomerRow");
    }

    [Fact]
    public void Invalid_Xsd_Reports_Diagnostic()
    {
        var result = RunGenerator("<invalid>not valid xsd</invalid>", "Bad.xsd");
        result.Diagnostics.Should().ContainSingle()
            .Which.Id.Should().Be("ADAG001");
    }

    [Fact]
    public void NamespacePrefixed_Xsd_Generates_No_Diagnostics()
    {
        var result = RunGenerator(LoadSchema("NamespacePrefixed.xsd"), "NamespacePrefixed.xsd");
        result.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void NamespacePrefixed_Xsd_DataSet_Contains_No_Namespace_Prefix()
    {
        var result = RunGenerator(LoadSchema("NamespacePrefixed.xsd"), "NamespacePrefixed.xsd");
        var dsTree = result.GeneratedTrees.FirstOrDefault(t => t.FilePath.Contains("AsyncDataSet.g.cs"));
        dsTree.Should().NotBeNull();
        var text = dsTree!.GetText().ToString();
        text.Should().NotContain("mstns:");
        text.Should().Contain("tableCATEGORY.CATIDColumn");
        text.Should().Contain("tableSUBCATEGORY.CATIDColumn");
    }

    [Fact]
    public void Non_Xsd_Files_Are_Ignored()
    {
        var compilation = CSharpCompilation.Create("TestAssembly",
            references: new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });

        var generator = new TypedDataSetGenerator();
        var driver = CSharpGeneratorDriver.Create(generator)
            .AddAdditionalTexts(ImmutableArray.Create<AdditionalText>(
                new InMemoryAdditionalText("test.json", "{}")));

        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(
            compilation, out _, out _);

        var result = driver.GetRunResult();
        result.GeneratedTrees.Should().BeEmpty();
        result.Diagnostics.Should().BeEmpty();
    }

    private sealed class InMemoryAdditionalText : AdditionalText
    {
        private readonly string _text;
        public override string Path { get; }

        public InMemoryAdditionalText(string path, string text)
        {
            Path = path;
            _text = text;
        }

        public override SourceText? GetText(CancellationToken cancellationToken = default)
            => SourceText.From(_text);
    }
}
