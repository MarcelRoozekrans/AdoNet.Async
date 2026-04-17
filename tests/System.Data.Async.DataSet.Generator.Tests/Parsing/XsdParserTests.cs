using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using System.Data.Async.DataSet.Generator.Model;
using System.Data.Async.DataSet.Generator.Parsing;
using Xunit;

namespace System.Data.Async.DataSet.Generator.Tests.Parsing;

public class XsdParserTests
{
    private static string LoadSchema(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Schemas", fileName);
        return File.ReadAllText(path);
    }

    private static DataSetModel ParseSimple()
    {
        return XsdParser.Parse(LoadSchema("Simple.xsd"));
    }

    [Fact]
    public void Parse_Simple_DataSetName()
    {
        var model = ParseSimple();
        model.Name.Should().Be("OrdersDS");
    }

    [Fact]
    public void Parse_Simple_Locale()
    {
        var model = ParseSimple();
        model.Locale.Should().Be("en-US");
    }

    [Fact]
    public void Parse_Simple_Tables()
    {
        var model = ParseSimple();
        model.Tables.Should().HaveCount(2);
        model.Tables.Select(t => t.Name).Should().BeEquivalentTo("Customer", "Order");
    }

    [Fact]
    public void Parse_Simple_Customer_Columns()
    {
        var model = ParseSimple();
        var customer = model.Tables.First(t => string.Equals(t.Name, "Customer", StringComparison.Ordinal));
        customer.Columns.Should().HaveCount(3);

        var customerId = customer.Columns.First(c => string.Equals(c.Name, "CustomerId", StringComparison.Ordinal));
        customerId.ClrTypeName.Should().Be("System.Int32");
        customerId.AllowDBNull.Should().BeFalse();

        var name = customer.Columns.First(c => string.Equals(c.Name, "Name", StringComparison.Ordinal));
        name.ClrTypeName.Should().Be("System.String");
        name.AllowDBNull.Should().BeFalse();

        var email = customer.Columns.First(c => string.Equals(c.Name, "Email", StringComparison.Ordinal));
        email.ClrTypeName.Should().Be("System.String");
        email.AllowDBNull.Should().BeTrue();
    }

    [Fact]
    public void Parse_Simple_AutoIncrement()
    {
        var model = ParseSimple();
        var order = model.Tables.First(t => string.Equals(t.Name, "Order", StringComparison.Ordinal));
        var orderId = order.Columns.First(c => string.Equals(c.Name, "OrderId", StringComparison.Ordinal));

        orderId.AutoIncrement.Should().BeTrue();
        orderId.AutoIncrementSeed.Should().Be(1);
        orderId.AutoIncrementStep.Should().Be(1);
    }

    [Fact]
    public void Parse_Simple_NullValueBehavior_Empty()
    {
        var model = ParseSimple();
        var order = model.Tables.First(t => string.Equals(t.Name, "Order", StringComparison.Ordinal));
        var notes = order.Columns.First(c => string.Equals(c.Name, "Notes", StringComparison.Ordinal));

        notes.AllowDBNull.Should().BeTrue();
        notes.NullValueBehavior.Kind.Should().Be(NullValueBehaviorKind.ReturnEmpty);
    }

    [Fact]
    public void Parse_Simple_PrimaryKeys()
    {
        var model = ParseSimple();
        var customer = model.Tables.First(t => string.Equals(t.Name, "Customer", StringComparison.Ordinal));
        customer.PrimaryKeyColumnNames.Should().BeEquivalentTo("CustomerId");
    }

    [Fact]
    public void Parse_Simple_Relations()
    {
        var model = ParseSimple();
        model.Relations.Should().HaveCount(1);

        var rel = model.Relations[0];
        rel.Name.Should().Be("FK_Customer_Order");
        rel.ParentTableName.Should().Be("Customer");
        rel.ParentColumnNames.Should().BeEquivalentTo("CustomerId");
        rel.ChildTableName.Should().Be("Order");
        rel.ChildColumnNames.Should().BeEquivalentTo("CustomerId");
    }

    // --- Advanced XSD tests ---

    [Fact]
    public void Parse_Advanced_CaseSensitive()
    {
        var model = XsdParser.Parse(LoadSchema("Advanced.xsd"));
        model.CaseSensitive.Should().BeTrue();
        model.EnforceConstraints.Should().BeTrue();
    }

    [Fact]
    public void Parse_Advanced_TypedName_Override()
    {
        var model = XsdParser.Parse(LoadSchema("Advanced.xsd"));
        var category = model.Tables.First(t => string.Equals(t.Name, "Category", StringComparison.Ordinal));
        category.TypedName.Should().Be("CategoryEntry");
        category.TypedPlural.Should().Be("Categories");
    }

    [Fact]
    public void Parse_Advanced_NullValue_Null()
    {
        var model = XsdParser.Parse(LoadSchema("Advanced.xsd"));
        var desc = model.Tables.First(t => string.Equals(t.Name, "Category", StringComparison.Ordinal))
            .Columns.First(c => string.Equals(c.Name, "Description", StringComparison.Ordinal));
        desc.NullValueBehavior.Kind.Should().Be(NullValueBehaviorKind.ReturnNull);
    }

    [Fact]
    public void Parse_Advanced_NullValue_Replacement()
    {
        var model = XsdParser.Parse(LoadSchema("Advanced.xsd"));
        var notes = model.Tables.First(t => string.Equals(t.Name, "Product", StringComparison.Ordinal))
            .Columns.First(c => string.Equals(c.Name, "Notes", StringComparison.Ordinal));
        notes.NullValueBehavior.Kind.Should().Be(NullValueBehaviorKind.ReplacementValue);
        notes.NullValueBehavior.ReplacementValue.Should().Be("N/A");
    }

    [Fact]
    public void Parse_Advanced_DefaultValue()
    {
        var model = XsdParser.Parse(LoadSchema("Advanced.xsd"));
        var price = model.Tables.First(t => string.Equals(t.Name, "Product", StringComparison.Ordinal))
            .Columns.First(c => string.Equals(c.Name, "Price", StringComparison.Ordinal));
        price.DefaultValue.Should().Be("0");
    }

    [Fact]
    public void Parse_Advanced_ReadOnly()
    {
        var model = XsdParser.Parse(LoadSchema("Advanced.xsd"));
        var stock = model.Tables.First(t => string.Equals(t.Name, "Product", StringComparison.Ordinal))
            .Columns.First(c => string.Equals(c.Name, "Stock", StringComparison.Ordinal));
        stock.ReadOnly.Should().BeTrue();
    }

    [Fact]
    public void Parse_Advanced_Expression()
    {
        var model = XsdParser.Parse(LoadSchema("Advanced.xsd"));
        var total = model.Tables.First(t => string.Equals(t.Name, "Product", StringComparison.Ordinal))
            .Columns.First(c => string.Equals(c.Name, "TotalValue", StringComparison.Ordinal));
        total.Expression.Should().Be("Price * Stock");
        total.ReadOnly.Should().BeTrue();
    }

    [Fact]
    public void Parse_Advanced_DataType_Override()
    {
        var model = XsdParser.Parse(LoadSchema("Advanced.xsd"));
        var sku = model.Tables.First(t => string.Equals(t.Name, "Product", StringComparison.Ordinal))
            .Columns.First(c => string.Equals(c.Name, "Sku", StringComparison.Ordinal));
        sku.ClrTypeName.Should().Be("System.Guid");
    }

    [Fact]
    public void Parse_Advanced_TypedParent_TypedChildren()
    {
        var model = XsdParser.Parse(LoadSchema("Advanced.xsd"));
        var rel = model.Relations.First(r => string.Equals(r.Name, "FK_Category_Product", StringComparison.Ordinal));
        rel.TypedParent.Should().Be("Category");
        rel.TypedChildren.Should().Be("GetProducts");
    }

    // --- Namespace-prefixed XPath tests (issue #42) ---

    [Fact]
    public void Parse_NamespacePrefixed_Tables()
    {
        var model = XsdParser.Parse(LoadSchema("NamespacePrefixed.xsd"));
        model.Tables.Should().HaveCount(2);
        model.Tables.Select(t => t.Name).Should().BeEquivalentTo("CATEGORY", "SUBCATEGORY");
    }

    [Fact]
    public void Parse_NamespacePrefixed_PrimaryKeys_StripPrefix()
    {
        var model = XsdParser.Parse(LoadSchema("NamespacePrefixed.xsd"));
        var category = model.Tables.First(t => string.Equals(t.Name, "CATEGORY", StringComparison.Ordinal));
        category.PrimaryKeyColumnNames.Should().BeEquivalentTo("CATID");
    }

    [Fact]
    public void Parse_NamespacePrefixed_Relations_StripPrefix()
    {
        var model = XsdParser.Parse(LoadSchema("NamespacePrefixed.xsd"));
        model.Relations.Should().HaveCount(1);

        var rel = model.Relations[0];
        rel.Name.Should().Be("CATEGORYSUBCATEGORY");
        rel.ParentTableName.Should().Be("CATEGORY");
        rel.ParentColumnNames.Should().BeEquivalentTo("CATID");
        rel.ChildTableName.Should().Be("SUBCATEGORY");
        rel.ChildColumnNames.Should().BeEquivalentTo("CATID");
    }

    [Fact]
    public void Parse_NamespacePrefixed_ForeignKeyConstraints_StripPrefix()
    {
        var model = XsdParser.Parse(LoadSchema("NamespacePrefixed.xsd"));
        model.ForeignKeyConstraints.Should().HaveCount(1);

        var fk = model.ForeignKeyConstraints[0];
        fk.ParentTableName.Should().Be("CATEGORY");
        fk.ParentColumnNames.Should().BeEquivalentTo("CATID");
        fk.ChildTableName.Should().Be("SUBCATEGORY");
        fk.ChildColumnNames.Should().BeEquivalentTo("CATID");
    }

    // --- xs:attribute column tests (issue #52) ---

    [Fact]
    public void Parse_AttributeColumns_Required_Attribute_Is_Not_Nullable()
    {
        var model = XsdParser.Parse(LoadSchema("AttributeColumns.xsd"));
        var entry = model.Tables.First(t => string.Equals(t.Name, "Entry", StringComparison.Ordinal));
        var regionId = entry.Columns.First(c => string.Equals(c.Name, "RegionId", StringComparison.Ordinal));
        regionId.ClrTypeName.Should().Be("System.Int32");
        regionId.AllowDBNull.Should().BeFalse();
    }

    [Fact]
    public void Parse_AttributeColumns_Optional_Attribute_Is_Nullable()
    {
        var model = XsdParser.Parse(LoadSchema("AttributeColumns.xsd"));
        var entry = model.Tables.First(t => string.Equals(t.Name, "Entry", StringComparison.Ordinal));
        var description = entry.Columns.First(c => string.Equals(c.Name, "Description", StringComparison.Ordinal));
        description.ClrTypeName.Should().Be("System.String");
        description.AllowDBNull.Should().BeTrue();
    }

    [Fact]
    public void Parse_AttributeColumns_CompositePrimaryKey_AtPrefixStripped()
    {
        var model = XsdParser.Parse(LoadSchema("AttributeColumns.xsd"));
        var entry = model.Tables.First(t => string.Equals(t.Name, "Entry", StringComparison.Ordinal));
        // xs:field xpath="@RegionId" and xpath="@Code" — the @ prefix must be stripped
        entry.PrimaryKeyColumnNames.Should().BeEquivalentTo("RegionId", "Code");
    }

    [Fact]
    public void Parse_AttributeColumns_TableName_From_NamespacePrefixedSelector()
    {
        var model = XsdParser.Parse(LoadSchema("AttributeColumns.xsd"));
        // selector xpath=".//mstns:Entry" — namespace prefix stripped, table matched correctly
        model.Tables.Should().HaveCount(1);
        model.Tables[0].Name.Should().Be("Entry");
    }

    [Fact]
    public void Parse_AttributeColumns_Explicit_Optional_Is_Nullable()
    {
        var model = XsdParser.Parse(LoadSchema("AttributeColumns.xsd"));
        var entry = model.Tables.First(t => string.Equals(t.Name, "Entry", StringComparison.Ordinal));
        // use="optional" explicitly set — must be nullable
        var label = entry.Columns.First(c => string.Equals(c.Name, "Label", StringComparison.Ordinal));
        label.AllowDBNull.Should().BeTrue();
    }

    [Fact]
    public void Parse_AttributeColumns_DataType_Override_Guid()
    {
        var model = XsdParser.Parse(LoadSchema("AttributeColumns.xsd"));
        var entry = model.Tables.First(t => string.Equals(t.Name, "Entry", StringComparison.Ordinal));
        // msdata:DataType="System.Guid" on xs:attribute
        var trackingId = entry.Columns.First(c => string.Equals(c.Name, "TrackingId", StringComparison.Ordinal));
        trackingId.ClrTypeName.Should().Be("System.Guid");
        trackingId.AllowDBNull.Should().BeTrue();
    }

    [Fact]
    public void Parse_AttributeColumns_NullValue_Replacement()
    {
        var model = XsdParser.Parse(LoadSchema("AttributeColumns.xsd"));
        var entry = model.Tables.First(t => string.Equals(t.Name, "Entry", StringComparison.Ordinal));
        // codegen:nullValue="N/A" on xs:attribute
        var tag = entry.Columns.First(c => string.Equals(c.Name, "Tag", StringComparison.Ordinal));
        tag.NullValueBehavior.Kind.Should().Be(NullValueBehaviorKind.ReplacementValue);
        tag.NullValueBehavior.ReplacementValue.Should().Be("N/A");
    }

    [Fact]
    public void Parse_AttributeColumns_AllSixColumns_Parsed()
    {
        var model = XsdParser.Parse(LoadSchema("AttributeColumns.xsd"));
        var entry = model.Tables.First(t => string.Equals(t.Name, "Entry", StringComparison.Ordinal));
        entry.Columns.Should().HaveCount(6);
        entry.Columns.Select(c => c.Name).Should().BeEquivalentTo(
            "RegionId", "Code", "Description", "Label", "TrackingId", "Tag");
    }

    // --- Mixed xs:element + xs:attribute columns (issue #52 — comprehensive coverage) ---

    [Fact]
    public void Parse_MixedColumns_ParsesFourColumns()
    {
        var model = XsdParser.Parse(LoadSchema("MixedColumns.xsd"));
        var item = model.Tables.First(t => string.Equals(t.Name, "Item", StringComparison.Ordinal));
        // 2 xs:element + 2 xs:attribute = 4 total
        item.Columns.Should().HaveCount(4);
    }

    [Fact]
    public void Parse_MixedColumns_Element_Columns_Present()
    {
        var model = XsdParser.Parse(LoadSchema("MixedColumns.xsd"));
        var item = model.Tables.First(t => string.Equals(t.Name, "Item", StringComparison.Ordinal));
        item.Columns.Select(c => c.Name).Should().Contain("Id").And.Contain("Name");
        item.Columns.First(c => string.Equals(c.Name, "Id", StringComparison.Ordinal)).ClrTypeName.Should().Be("System.Int32");
    }

    [Fact]
    public void Parse_MixedColumns_Attribute_Columns_Present()
    {
        var model = XsdParser.Parse(LoadSchema("MixedColumns.xsd"));
        var item = model.Tables.First(t => string.Equals(t.Name, "Item", StringComparison.Ordinal));
        item.Columns.Select(c => c.Name).Should().Contain("Source").And.Contain("Priority");
    }

    [Fact]
    public void Parse_MixedColumns_Required_Attribute_Not_Nullable()
    {
        var model = XsdParser.Parse(LoadSchema("MixedColumns.xsd"));
        var item = model.Tables.First(t => string.Equals(t.Name, "Item", StringComparison.Ordinal));
        var source = item.Columns.First(c => string.Equals(c.Name, "Source", StringComparison.Ordinal));
        source.AllowDBNull.Should().BeFalse();
    }

    [Fact]
    public void Parse_MixedColumns_Optional_Attribute_Nullable()
    {
        var model = XsdParser.Parse(LoadSchema("MixedColumns.xsd"));
        var item = model.Tables.First(t => string.Equals(t.Name, "Item", StringComparison.Ordinal));
        var priority = item.Columns.First(c => string.Equals(c.Name, "Priority", StringComparison.Ordinal));
        priority.AllowDBNull.Should().BeTrue();
    }

    [Fact]
    public void Parse_MixedColumns_PrimaryKey_From_Element_Field()
    {
        var model = XsdParser.Parse(LoadSchema("MixedColumns.xsd"));
        var item = model.Tables.First(t => string.Equals(t.Name, "Item", StringComparison.Ordinal));
        // xs:field xpath="mstns:Id" — element reference (no @), namespace prefix stripped
        item.PrimaryKeyColumnNames.Should().BeEquivalentTo("Id");
    }

    // --- FK relation where child columns are xs:attributes (issue #52 — FK coverage) ---

    [Fact]
    public void Parse_AttributeFk_Child_Columns_Parsed()
    {
        var model = XsdParser.Parse(LoadSchema("AttributeFkColumns.xsd"));
        var entry = model.Tables.First(t => string.Equals(t.Name, "Entry", StringComparison.Ordinal));
        entry.Columns.Should().HaveCount(2);
        entry.Columns.Select(c => c.Name).Should().BeEquivalentTo("RegionId", "Code");
    }

    [Fact]
    public void Parse_AttributeFk_Relation_ChildColumns_AtPrefixStripped()
    {
        var model = XsdParser.Parse(LoadSchema("AttributeFkColumns.xsd"));
        model.Relations.Should().HaveCount(1);
        var rel = model.Relations[0];
        rel.Name.Should().Be("FK_Region_Entry");
        rel.ParentTableName.Should().Be("Region");
        // xs:field xpath="mstns:RegionId" on parent → "RegionId" (namespace stripped)
        rel.ParentColumnNames.Should().BeEquivalentTo("RegionId");
        rel.ChildTableName.Should().Be("Entry");
        // xs:field xpath="@RegionId" on child → "RegionId" (@ stripped)
        rel.ChildColumnNames.Should().BeEquivalentTo("RegionId");
    }

    [Fact]
    public void Parse_AttributeFk_ForeignKeyConstraint_ChildColumns_AtPrefixStripped()
    {
        var model = XsdParser.Parse(LoadSchema("AttributeFkColumns.xsd"));
        model.ForeignKeyConstraints.Should().HaveCount(1);
        var fk = model.ForeignKeyConstraints[0];
        fk.ParentTableName.Should().Be("Region");
        fk.ParentColumnNames.Should().BeEquivalentTo("RegionId");
        fk.ChildTableName.Should().Be("Entry");
        // @ prefix must be stripped from attribute-based FK column reference
        fk.ChildColumnNames.Should().BeEquivalentTo("RegionId");
    }

    [Fact]
    public void Parse_AttributeFk_Entry_CompositePrimaryKey_AtPrefixStripped()
    {
        var model = XsdParser.Parse(LoadSchema("AttributeFkColumns.xsd"));
        var entry = model.Tables.First(t => string.Equals(t.Name, "Entry", StringComparison.Ordinal));
        entry.PrimaryKeyColumnNames.Should().BeEquivalentTo("RegionId", "Code");
    }

    // -------------------------------------------------------------------------
    // External type reference (issue #55): type="mstns:TypeName" on table element
    // -------------------------------------------------------------------------

    [Fact]
    public void Parse_ExternalTypeRef_TwoTablesFound()
    {
        var model = XsdParser.Parse(LoadSchema("ExternalTypeRef.xsd"));
        model.Tables.Should().HaveCount(2);
    }

    [Fact]
    public void Parse_ExternalTypeRef_Order_FourColumnsResolved()
    {
        var model = XsdParser.Parse(LoadSchema("ExternalTypeRef.xsd"));
        var order = model.Tables.First(t => string.Equals(t.Name, "Order", StringComparison.Ordinal));
        order.Columns.Should().HaveCount(4);
    }

    [Fact]
    public void Parse_ExternalTypeRef_Order_ColumnNames()
    {
        var model = XsdParser.Parse(LoadSchema("ExternalTypeRef.xsd"));
        var order = model.Tables.First(t => string.Equals(t.Name, "Order", StringComparison.Ordinal));
        order.Columns.Select(c => c.Name).Should().BeEquivalentTo("OrderId", "CustomerId", "Amount", "Note");
    }

    [Fact]
    public void Parse_ExternalTypeRef_Order_RequiredColumns_AllowDBNull_False()
    {
        var model = XsdParser.Parse(LoadSchema("ExternalTypeRef.xsd"));
        var order = model.Tables.First(t => string.Equals(t.Name, "Order", StringComparison.Ordinal));
        order.Columns.Where(c => !string.Equals(c.Name, "Note", StringComparison.Ordinal))
            .Should().AllSatisfy(c => c.AllowDBNull.Should().BeFalse());
    }

    [Fact]
    public void Parse_ExternalTypeRef_Order_NoteColumn_AllowDBNull_True()
    {
        var model = XsdParser.Parse(LoadSchema("ExternalTypeRef.xsd"));
        var order = model.Tables.First(t => string.Equals(t.Name, "Order", StringComparison.Ordinal));
        order.Columns.First(c => string.Equals(c.Name, "Note", StringComparison.Ordinal))
            .AllowDBNull.Should().BeTrue();
    }

    [Fact]
    public void Parse_ExternalTypeRef_Order_PrimaryKey()
    {
        var model = XsdParser.Parse(LoadSchema("ExternalTypeRef.xsd"));
        var order = model.Tables.First(t => string.Equals(t.Name, "Order", StringComparison.Ordinal));
        order.PrimaryKeyColumnNames.Should().BeEquivalentTo("OrderId");
    }

    [Fact]
    public void Parse_ExternalTypeRef_Tag_ThreeAttributeColumnsResolved()
    {
        // External type reference with xs:attribute columns (not xs:sequence)
        var model = XsdParser.Parse(LoadSchema("ExternalTypeRef.xsd"));
        var tag = model.Tables.First(t => string.Equals(t.Name, "Tag", StringComparison.Ordinal));
        tag.Columns.Should().HaveCount(3);
    }

    [Fact]
    public void Parse_ExternalTypeRef_Tag_RequiredAttributeColumns_AllowDBNull_False()
    {
        var model = XsdParser.Parse(LoadSchema("ExternalTypeRef.xsd"));
        var tag = model.Tables.First(t => string.Equals(t.Name, "Tag", StringComparison.Ordinal));
        tag.Columns.Where(c => !string.Equals(c.Name, "Color", StringComparison.Ordinal))
            .Should().AllSatisfy(c => c.AllowDBNull.Should().BeFalse());
    }

    [Fact]
    public void Parse_ExternalTypeRef_Tag_OptionalAttributeColumn_AllowDBNull_True()
    {
        var model = XsdParser.Parse(LoadSchema("ExternalTypeRef.xsd"));
        var tag = model.Tables.First(t => string.Equals(t.Name, "Tag", StringComparison.Ordinal));
        tag.Columns.First(c => string.Equals(c.Name, "Color", StringComparison.Ordinal))
            .AllowDBNull.Should().BeTrue();
    }

    [Fact]
    public void Parse_ExternalTypeRef_Tag_PrimaryKey_AtPrefixStripped()
    {
        var model = XsdParser.Parse(LoadSchema("ExternalTypeRef.xsd"));
        var tag = model.Tables.First(t => string.Equals(t.Name, "Tag", StringComparison.Ordinal));
        tag.PrimaryKeyColumnNames.Should().BeEquivalentTo("TagId");
    }
}
