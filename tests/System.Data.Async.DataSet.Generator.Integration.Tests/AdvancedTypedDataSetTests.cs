using System.Data.Async.DataSet;
using FluentAssertions;
using Xunit;

namespace System.Data.Async.DataSet.Generator.Integration.Tests;

public class AdvancedTypedDataSetTests
{
    [Fact]
    public void TypedName_Override_Applied()
    {
        using var ds = new AsyncAdvancedDS();
        // Category has codegen:typedPlural="Categories" -> property named "Categories"
        ds.Categories.Should().NotBeNull();
        ds.Categories.Should().BeOfType<AsyncCategoriesDataTable>();
    }

    [Fact]
    public async Task Can_Add_CategoryEntry_Row()
    {
        using var ds = new AsyncAdvancedDS();
        // AddCategoryEntryRowAsync(categoryId, name, description)
        var row = await ds.Categories.AddCategoryEntryRowAsync(1, "Electronics", "Electronic goods");
        row.Should().BeOfType<AsyncCategoryEntryRow>();
        row.Name.Should().Be("Electronics");
    }

    [Fact]
    public async Task NullValue_Null_Returns_Null()
    {
        using var ds = new AsyncAdvancedDS();
        // Don't pass description — set it to null after adding
        var row = await ds.Categories.AddCategoryEntryRowAsync(1, "Electronics", "temp");
        await row.SetDescriptionNullAsync();
        // Description has codegen:nullValue="_null" — should return null, not throw
        row.Description.Should().BeNull();
    }

    [Fact]
    public async Task NullValue_Replacement_Returns_Value()
    {
        using var ds = new AsyncAdvancedDS();
        var cat = await ds.Categories.AddCategoryEntryRowAsync(1, "Electronics", "Electronic goods");
        // AddProductRowAsync(productId, parentCategoryRow, name, price, stock, sku, notes)
        var product = await ds.Product.AddProductRowAsync(1, cat, "Widget", 9.99m, 10, Guid.NewGuid(), "temp");
        await product.SetNotesNullAsync();
        // Notes has codegen:nullValue="N/A" — should return "N/A" not throw
        product.Notes.Should().Be("N/A");
    }

    [Fact]
    public async Task DefaultValue_Applied()
    {
        using var ds = new AsyncAdvancedDS();
        var cat = await ds.Categories.AddCategoryEntryRowAsync(1, "Electronics", "desc");
        var row = ds.Product.NewProductRow();
        await row.SetProductIdAsync(1);
        await row.SetCategoryIdAsync(1);
        await row.SetNameAsync("Widget");
        await row.SetStockAsync(0);
        // Don't set Price — it has DefaultValue="0"
        await ds.Product.Rows.AddAsync(row);
        row.Price.Should().Be(0m);
    }

    [Fact]
    public async Task TypedChildren_Override()
    {
        using var ds = new AsyncAdvancedDS();
        var cat = await ds.Categories.AddCategoryEntryRowAsync(1, "Electronics", "desc");
        await ds.Product.AddProductRowAsync(1, cat, "Widget", 9.99m, 10, Guid.NewGuid(), "note");
        // codegen:typedChildren="GetProducts"
        var products = cat.GetProducts();
        products.Should().HaveCount(1);
    }

    [Fact]
    public async Task TypedParent_Override()
    {
        using var ds = new AsyncAdvancedDS();
        var cat = await ds.Categories.AddCategoryEntryRowAsync(1, "Electronics", "desc");
        var product = await ds.Product.AddProductRowAsync(1, cat, "Widget", 9.99m, 10, Guid.NewGuid(), "note");
        // codegen:typedParent="Category" -> property is "CategoryRow"
        product.CategoryRow.Should().NotBeNull();
        product.CategoryRow!.Name.Should().Be("Electronics");
    }

    [Fact]
    public async Task DataType_Override_Guid()
    {
        using var ds = new AsyncAdvancedDS();
        var cat = await ds.Categories.AddCategoryEntryRowAsync(1, "Electronics", "desc");
        var product = await ds.Product.AddProductRowAsync(1, cat, "Widget", 9.99m, 10, Guid.Empty, "note");
        var guid = Guid.NewGuid();
        await product.SetSkuAsync(guid);
        product.Sku.Should().Be(guid);
    }

    [Fact]
    public void CaseSensitive_Applied()
    {
        using var ds = new AsyncAdvancedDS();
        var inner = (System.Data.DataSet)ds;
        inner.CaseSensitive.Should().BeTrue();
    }

    [Fact]
    public void EnforceConstraints_Applied()
    {
        using var ds = new AsyncAdvancedDS();
        var inner = (System.Data.DataSet)ds;
        inner.EnforceConstraints.Should().BeTrue();
    }
}
