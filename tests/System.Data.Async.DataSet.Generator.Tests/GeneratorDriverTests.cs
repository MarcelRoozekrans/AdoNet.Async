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

    // --- xs:attribute column tests (issue #52) ---

    [Fact]
    public void AttributeColumns_Xsd_Generates_No_Diagnostics()
    {
        var result = RunGenerator(LoadSchema("AttributeColumns.xsd"), "AttributeColumns.xsd");
        result.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void AttributeColumns_Xsd_DataTable_Contains_Column_Fields()
    {
        var result = RunGenerator(LoadSchema("AttributeColumns.xsd"), "AttributeColumns.xsd");
        var tableTree = result.GeneratedTrees.FirstOrDefault(t => t.FilePath.Contains("Entry.AsyncDataTable.g.cs"));
        tableTree.Should().NotBeNull();
        var text = tableTree!.GetText().ToString();
        // All three xs:attribute columns must be declared
        text.Should().Contain("columnRegionId");
        text.Should().Contain("columnCode");
        text.Should().Contain("columnDescription");
    }

    [Fact]
    public void AttributeColumns_Xsd_AddRowAsync_Has_Required_Parameters()
    {
        var result = RunGenerator(LoadSchema("AttributeColumns.xsd"), "AttributeColumns.xsd");
        var tableTree = result.GeneratedTrees.FirstOrDefault(t => t.FilePath.Contains("Entry.AsyncDataTable.g.cs"));
        tableTree.Should().NotBeNull();
        var text = tableTree!.GetText().ToString();
        // AddEntryRowAsync must NOT have a leading comma — must have typed params for required cols
        text.Should().Contain("AddEntryRowAsync(");
        text.Should().NotContain("AddEntryRowAsync(,");
        text.Should().Contain("int regionId");
        text.Should().Contain("string code");
    }

    [Fact]
    public void AttributeColumns_Xsd_Composite_FindBy_Generated()
    {
        var result = RunGenerator(LoadSchema("AttributeColumns.xsd"), "AttributeColumns.xsd");
        var tableTree = result.GeneratedTrees.FirstOrDefault(t => t.FilePath.Contains("Entry.AsyncDataTable.g.cs"));
        tableTree.Should().NotBeNull();
        var text = tableTree!.GetText().ToString();
        text.Should().Contain("FindByRegionIdCode(");
    }

    // --- Mixed xs:element + xs:attribute column tests ---

    [Fact]
    public void MixedColumns_Xsd_Generates_No_Diagnostics()
    {
        var result = RunGenerator(LoadSchema("MixedColumns.xsd"), "MixedColumns.xsd");
        result.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void MixedColumns_Xsd_DataTable_Contains_All_Four_Column_Fields()
    {
        var result = RunGenerator(LoadSchema("MixedColumns.xsd"), "MixedColumns.xsd");
        var tableTree = result.GeneratedTrees.FirstOrDefault(t => t.FilePath.Contains("Item.AsyncDataTable.g.cs"));
        tableTree.Should().NotBeNull();
        var text = tableTree!.GetText().ToString();
        // 2 xs:element + 2 xs:attribute
        text.Should().Contain("columnId");
        text.Should().Contain("columnName");
        text.Should().Contain("columnSource");
        text.Should().Contain("columnPriority");
    }

    [Fact]
    public void AttributeColumns_Xsd_Guid_Attribute_Generated_With_Correct_Type()
    {
        var result = RunGenerator(LoadSchema("AttributeColumns.xsd"), "AttributeColumns.xsd");
        var tableTree = result.GeneratedTrees.FirstOrDefault(t => t.FilePath.Contains("Entry.AsyncDataTable.g.cs"));
        tableTree.Should().NotBeNull();
        var text = tableTree!.GetText().ToString();
        // msdata:DataType="System.Guid" attribute column
        text.Should().Contain("typeof(global::System.Guid)");
    }

    [Fact]
    public void AttributeColumns_Xsd_NullValue_Replacement_In_Row()
    {
        var result = RunGenerator(LoadSchema("AttributeColumns.xsd"), "AttributeColumns.xsd");
        var rowTree = result.GeneratedTrees.FirstOrDefault(t => t.FilePath.Contains("Entry.AsyncDataRow.g.cs"));
        rowTree.Should().NotBeNull();
        var text = rowTree!.GetText().ToString();
        // codegen:nullValue="N/A" on Tag attribute
        text.Should().Contain("\"N/A\"");
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

    // -------------------------------------------------------------------------
    // Column metadata emitter tests (ColumnMetadata.xsd)
    // -------------------------------------------------------------------------

    [Fact]
    public void ColumnMetadata_AutoIncrement_Column_ExcludedFromAddRowParams()
    {
        var result = RunGenerator(LoadSchema("ColumnMetadata.xsd"), "ColumnMetadata.xsd");
        var tableTree = result.GeneratedTrees.FirstOrDefault(t => t.FilePath.Contains("Item.AsyncDataTable.g.cs"));
        tableTree.Should().NotBeNull();
        var text = tableTree!.GetText().ToString();
        // ItemId is AutoIncrement — must NOT appear in AddItemRowAsync parameters
        text.Should().Contain("AddItemRowAsync(");
        text.Should().NotContain("AddItemRowAsync(int itemId");
    }

    [Fact]
    public void ColumnMetadata_ReadOnly_Column_ExcludedFromAddRowParams()
    {
        var result = RunGenerator(LoadSchema("ColumnMetadata.xsd"), "ColumnMetadata.xsd");
        var tableTree = result.GeneratedTrees.FirstOrDefault(t => t.FilePath.Contains("Item.AsyncDataTable.g.cs"));
        var text = tableTree!.GetText().ToString();
        // CreatedAt is ReadOnly — must NOT appear as a parameter
        text.Should().NotContain("createdAt");
    }

    [Fact]
    public void ColumnMetadata_Expression_Column_ExcludedFromAddRowParams()
    {
        var result = RunGenerator(LoadSchema("ColumnMetadata.xsd"), "ColumnMetadata.xsd");
        var tableTree = result.GeneratedTrees.FirstOrDefault(t => t.FilePath.Contains("Item.AsyncDataTable.g.cs"));
        var text = tableTree!.GetText().ToString();
        // DisplayName is Expression — must NOT appear as a parameter
        text.Should().NotContain("displayName");
    }

    [Fact]
    public void ColumnMetadata_Hidden_Column_ExcludedFromAddRowParams()
    {
        var result = RunGenerator(LoadSchema("ColumnMetadata.xsd"), "ColumnMetadata.xsd");
        var tableTree = result.GeneratedTrees.FirstOrDefault(t => t.FilePath.Contains("Item.AsyncDataTable.g.cs"));
        var text = tableTree!.GetText().ToString();
        // InternalCode is hiddenColumn — must NOT appear as a parameter
        text.Should().NotContain("internalCode");
    }

    [Fact]
    public void ColumnMetadata_Caption_EmittedInInitClass()
    {
        var result = RunGenerator(LoadSchema("ColumnMetadata.xsd"), "ColumnMetadata.xsd");
        var tableTree = result.GeneratedTrees.FirstOrDefault(t => t.FilePath.Contains("Item.AsyncDataTable.g.cs"));
        var text = tableTree!.GetText().ToString();
        text.Should().Contain("columnNotes.Caption = \"Internal Notes\";");
    }

    [Fact]
    public void ColumnMetadata_MaxLength_EmittedInInitClass()
    {
        var result = RunGenerator(LoadSchema("ColumnMetadata.xsd"), "ColumnMetadata.xsd");
        var tableTree = result.GeneratedTrees.FirstOrDefault(t => t.FilePath.Contains("Item.AsyncDataTable.g.cs"));
        var text = tableTree!.GetText().ToString();
        text.Should().Contain("columnNotes.MaxLength = 500;");
    }

    [Fact]
    public void ColumnMetadata_DefaultValue_EmittedInInitClass()
    {
        var result = RunGenerator(LoadSchema("ColumnMetadata.xsd"), "ColumnMetadata.xsd");
        var tableTree = result.GeneratedTrees.FirstOrDefault(t => t.FilePath.Contains("Item.AsyncDataTable.g.cs"));
        var text = tableTree!.GetText().ToString();
        text.Should().Contain("columnStatus.DefaultValue = \"Active\";");
    }

    [Fact]
    public void ColumnMetadata_AutoIncrementSeedAndStep_EmittedInInitClass()
    {
        var result = RunGenerator(LoadSchema("ColumnMetadata.xsd"), "ColumnMetadata.xsd");
        var tableTree = result.GeneratedTrees.FirstOrDefault(t => t.FilePath.Contains("Item.AsyncDataTable.g.cs"));
        var text = tableTree!.GetText().ToString();
        text.Should().Contain("columnItemId.AutoIncrementSeed = 10;");
        text.Should().Contain("columnItemId.AutoIncrementStep = 5;");
    }

    // -------------------------------------------------------------------------
    // No primary key: no FindBy method emitted
    // -------------------------------------------------------------------------

    [Fact]
    public void NoPrimaryKey_Xsd_Generates_No_Diagnostics()
    {
        var result = RunGenerator(LoadSchema("NoPrimaryKey.xsd"), "NoPrimaryKey.xsd");
        result.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void NoPrimaryKey_Table_Has_No_FindBy_Method()
    {
        var result = RunGenerator(LoadSchema("NoPrimaryKey.xsd"), "NoPrimaryKey.xsd");
        var tableTree = result.GeneratedTrees.FirstOrDefault(t => t.FilePath.Contains("Log.AsyncDataTable.g.cs"));
        tableTree.Should().NotBeNull();
        var text = tableTree!.GetText().ToString();
        text.Should().NotContain("FindBy");
    }

    [Fact]
    public void NoPrimaryKey_Table_Still_Has_AddRowMethod()
    {
        var result = RunGenerator(LoadSchema("NoPrimaryKey.xsd"), "NoPrimaryKey.xsd");
        var tableTree = result.GeneratedTrees.FirstOrDefault(t => t.FilePath.Contains("Log.AsyncDataTable.g.cs"));
        var text = tableTree!.GetText().ToString();
        text.Should().Contain("AddLogRowAsync(");
        text.Should().Contain("string message");
        text.Should().Contain("int severity");
    }

    // -------------------------------------------------------------------------
    // ConstraintOnly: no parent-row param in AddRowAsync for child table
    // -------------------------------------------------------------------------

    [Fact]
    public void ConstraintOnly_Generates_No_Diagnostics()
    {
        var result = RunGenerator(LoadSchema("ConstraintOnlyRelation.xsd"), "ConstraintOnlyRelation.xsd");
        result.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void ConstraintOnly_Product_AddRow_Has_No_Parent_Row_Param()
    {
        var result = RunGenerator(LoadSchema("ConstraintOnlyRelation.xsd"), "ConstraintOnlyRelation.xsd");
        var tableTree = result.GeneratedTrees.FirstOrDefault(t => t.FilePath.Contains("Product.AsyncDataTable.g.cs"));
        tableTree.Should().NotBeNull();
        var text = tableTree!.GetText().ToString();
        // ConstraintOnly FK means no navigation relation — CategoryId is just a plain column param
        text.Should().NotContain("parentCategoryRow");
        text.Should().Contain("int categoryId");
    }

    // -------------------------------------------------------------------------
    // External type reference: columns emitted despite type="mstns:Foo" (issue #55)
    // -------------------------------------------------------------------------

    [Fact]
    public void ExternalTypeRef_Generates_No_Diagnostics()
    {
        var result = RunGenerator(LoadSchema("ExternalTypeRef.xsd"), "ExternalTypeRef.xsd");
        result.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void ExternalTypeRef_Order_ColumnFields_Emitted()
    {
        var result = RunGenerator(LoadSchema("ExternalTypeRef.xsd"), "ExternalTypeRef.xsd");
        var tableTree = result.GeneratedTrees.FirstOrDefault(t => t.FilePath.Contains("Order.AsyncDataTable.g.cs"));
        tableTree.Should().NotBeNull();
        var text = tableTree!.GetText().ToString();
        text.Should().Contain("columnOrderId");
        text.Should().Contain("columnCustomerId");
        text.Should().Contain("columnAmount");
        text.Should().Contain("columnNote");
    }

    [Fact]
    public void ExternalTypeRef_Order_AddRowAsync_RequiredParams_Only()
    {
        var result = RunGenerator(LoadSchema("ExternalTypeRef.xsd"), "ExternalTypeRef.xsd");
        var tableTree = result.GeneratedTrees.FirstOrDefault(t => t.FilePath.Contains("Order.AsyncDataTable.g.cs"));
        var text = tableTree!.GetText().ToString();
        // OrderId, CustomerId, Amount are required; Note is nullable (optional param)
        text.Should().Contain("int orderId");
        text.Should().Contain("int customerId");
        text.Should().Contain("decimal amount");
        text.Should().Contain("string? note = null");
    }

    [Fact]
    public void ExternalTypeRef_Tag_AttributeColumnFields_Emitted()
    {
        var result = RunGenerator(LoadSchema("ExternalTypeRef.xsd"), "ExternalTypeRef.xsd");
        var tableTree = result.GeneratedTrees.FirstOrDefault(t => t.FilePath.Contains("Tag.AsyncDataTable.g.cs"));
        tableTree.Should().NotBeNull();
        var text = tableTree!.GetText().ToString();
        text.Should().Contain("columnTagId");
        text.Should().Contain("columnLabel");
        text.Should().Contain("columnColor");
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
