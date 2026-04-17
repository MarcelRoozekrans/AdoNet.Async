using System.Data.Async.DataSet;
using FluentAssertions;
using Xunit;

namespace System.Data.Async.DataSet.Generator.Integration.Tests;

/// <summary>
/// Integration tests for DataSets whose table columns are declared as xs:attribute
/// elements in the XSD schema rather than xs:element within xs:sequence.
/// Regression coverage for issue #52.
/// </summary>
public class AttributeColumnsTypedDataSetTests
{
    [Fact]
    public void Can_Create_AttributeColumnsDS()
    {
        using var ds = new AsyncAttributeColumnsDS();
        ds.Entry.Should().NotBeNull();
    }

    [Fact]
    public void Entry_Table_Has_Column_Properties()
    {
        using var ds = new AsyncAttributeColumnsDS();
        ds.Entry.RegionIdColumn.Should().NotBeNull();
        ds.Entry.CodeColumn.Should().NotBeNull();
        ds.Entry.DescriptionColumn.Should().NotBeNull();
        ds.Entry.LabelColumn.Should().NotBeNull();
        ds.Entry.TrackingIdColumn.Should().NotBeNull();
        ds.Entry.TagColumn.Should().NotBeNull();
    }

    [Fact]
    public async Task Explicit_Optional_Attribute_Column_AllowDBNull_True()
    {
        using var ds = new AsyncAttributeColumnsDS();
        // use="optional" explicitly set — must be nullable, same as absent use
        ds.Entry.LabelColumn.AllowDBNull.Should().BeTrue();
        var row = await ds.Entry.AddEntryRowAsync(1, "NL");
        row.IsLabelNull().Should().BeTrue();
        await row.SetLabelAsync("west");
        row.Label.Should().Be("west");
    }

    [Fact]
    public async Task Can_Add_Entry_Row()
    {
        using var ds = new AsyncAttributeColumnsDS();
        var row = await ds.Entry.AddEntryRowAsync(1, "NL");
        row.Should().BeOfType<AsyncAttributeColumnsDSEntryRow>();
        row.RegionId.Should().Be(1);
        row.Code.Should().Be("NL");
    }

    [Fact]
    public void Required_Attribute_Column_AllowDBNull_False()
    {
        using var ds = new AsyncAttributeColumnsDS();
        ds.Entry.RegionIdColumn.AllowDBNull.Should().BeFalse();
        ds.Entry.CodeColumn.AllowDBNull.Should().BeFalse();
    }

    [Fact]
    public void Optional_Attribute_Column_AllowDBNull_True()
    {
        using var ds = new AsyncAttributeColumnsDS();
        ds.Entry.DescriptionColumn.AllowDBNull.Should().BeTrue();
    }

    [Fact]
    public async Task Optional_Attribute_Column_IsNull_When_Not_Set()
    {
        using var ds = new AsyncAttributeColumnsDS();
        var row = await ds.Entry.AddEntryRowAsync(1, "NL");
        row.IsDescriptionNull().Should().BeTrue();
    }

    [Fact]
    public async Task Optional_Attribute_Column_Set_And_Read()
    {
        using var ds = new AsyncAttributeColumnsDS();
        var row = await ds.Entry.AddEntryRowAsync(1, "NL");
        await row.SetDescriptionAsync("Netherlands");
        row.IsDescriptionNull().Should().BeFalse();
        row.Description.Should().Be("Netherlands");
    }

    [Fact]
    public async Task DataType_Override_Guid_Attribute_Column()
    {
        using var ds = new AsyncAttributeColumnsDS();
        ds.Entry.TrackingIdColumn.DataType.Should().Be<Guid>();
        var row = await ds.Entry.AddEntryRowAsync(1, "NL");
        var guid = Guid.NewGuid();
        await row.SetTrackingIdAsync(guid);
        row.TrackingId.Should().Be(guid);
    }

    [Fact]
    public async Task NullValue_Replacement_Attribute_Column()
    {
        using var ds = new AsyncAttributeColumnsDS();
        var row = await ds.Entry.AddEntryRowAsync(1, "NL");
        // Tag has codegen:nullValue="N/A" — null returns "N/A" instead of throwing
        row.IsTagNull().Should().BeTrue();
        row.Tag.Should().Be("N/A");
    }

    [Fact]
    public async Task NullValue_Replacement_Attribute_Column_SetNull()
    {
        using var ds = new AsyncAttributeColumnsDS();
        var row = await ds.Entry.AddEntryRowAsync(1, "NL");
        await row.SetTagAsync("some-tag");
        row.Tag.Should().Be("some-tag");
        await row.SetTagNullAsync();
        row.Tag.Should().Be("N/A");
    }

    [Fact]
    public async Task Composite_PK_FindBy_Works()
    {
        using var ds = new AsyncAttributeColumnsDS();
        await ds.Entry.AddEntryRowAsync(1, "NL");
        await ds.Entry.AddEntryRowAsync(1, "DE");
        await ds.Entry.AddEntryRowAsync(2, "NL");

        var found = ds.Entry.FindByRegionIdCode(1, "DE");
        found.Should().NotBeNull();
        found!.RegionId.Should().Be(1);
        found.Code.Should().Be("DE");
    }

    [Fact]
    public async Task Composite_PK_FindBy_Returns_Null_When_Not_Found()
    {
        using var ds = new AsyncAttributeColumnsDS();
        await ds.Entry.AddEntryRowAsync(1, "NL");

        var notFound = ds.Entry.FindByRegionIdCode(99, "XX");
        notFound.Should().BeNull();
    }

    [Fact]
    public async Task Entry_Count_Correct()
    {
        using var ds = new AsyncAttributeColumnsDS();
        await ds.Entry.AddEntryRowAsync(1, "NL");
        await ds.Entry.AddEntryRowAsync(1, "DE");
        ds.Entry.Count.Should().Be(2);
    }

    [Fact]
    public void Clone_Returns_Typed_AttributeColumnsDS()
    {
        using var ds = new AsyncAttributeColumnsDS();
        using var clone = ds.Clone();
        clone.Should().NotBeNull();
        clone.Should().BeOfType<AsyncAttributeColumnsDS>();
        clone.Entry.Should().NotBeNull();
    }

    [Fact]
    public async Task Row_Is_AsyncDataRow()
    {
        using var ds = new AsyncAttributeColumnsDS();
        await ds.Entry.AddEntryRowAsync(1, "NL");
        AsyncDataRow untyped = ds.Entry[0];
        untyped.Should().BeOfType<AsyncAttributeColumnsDSEntryRow>();
    }

    [Fact]
    public async Task Remove_Row()
    {
        using var ds = new AsyncAttributeColumnsDS();
        var row = await ds.Entry.AddEntryRowAsync(1, "NL");
        ds.Entry.Count.Should().Be(1);
        await ds.Entry.RemoveEntryRowAsync(row);
        ds.Entry.Count.Should().Be(0);
    }
}
