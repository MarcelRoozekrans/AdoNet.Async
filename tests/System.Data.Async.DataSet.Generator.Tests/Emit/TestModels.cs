using System.Collections.Immutable;
using System.Data.Async.DataSet.Generator.Model;

namespace System.Data.Async.DataSet.Generator.Tests.Emit;

/// <summary>
/// Shared test model factory for emitter tests.
/// </summary>
internal static class TestModels
{
    public static ColumnModel Column(
        string name,
        string clrType = "System.String",
        bool allowNull = false,
        bool readOnly = false,
        string? expression = null,
        bool autoIncrement = false,
        long autoIncrementSeed = 0,
        long autoIncrementStep = 0,
        string? defaultValue = null,
        string? caption = null,
        int? ordinal = null,
        int? maxLength = null,
        bool isHidden = false,
        NullValueBehaviorKind nullBehavior = NullValueBehaviorKind.Throw,
        string? replacementValue = null)
    {
        return new ColumnModel(
            name, clrType, allowNull, readOnly, expression,
            autoIncrement, autoIncrementSeed, autoIncrementStep,
            defaultValue, caption, ordinal, maxLength, isHidden,
            new NullValueBehavior(nullBehavior, replacementValue));
    }

    public static TableModel CustomerTable => new TableModel(
        "Customer",
        null,
        null,
        ImmutableArray.Create(
            Column("CustomerId", "System.Int32"),
            Column("Name", "System.String"),
            Column("Email", "System.String", allowNull: true, nullBehavior: NullValueBehaviorKind.ReturnNull)
        ),
        ImmutableArray.Create("CustomerId"),
        ImmutableArray<UniqueConstraintModel>.Empty);

    public static TableModel OrderTable => new TableModel(
        "Order",
        null,
        null,
        ImmutableArray.Create(
            Column("OrderId", "System.Int32", autoIncrement: true, autoIncrementSeed: 1, autoIncrementStep: 1),
            Column("CustomerId", "System.Int32"),
            Column("OrderDate", "System.DateTime"),
            Column("Notes", "System.String", allowNull: true, nullBehavior: NullValueBehaviorKind.ReturnEmpty),
            Column("Total", "System.Decimal", readOnly: true, expression: "Price * Qty")
        ),
        ImmutableArray.Create("OrderId"),
        ImmutableArray<UniqueConstraintModel>.Empty);

    public static TableModel TypedCategoryTable => new TableModel(
        "Category",
        "CategoryEntry",
        "Categories",
        ImmutableArray.Create(
            Column("CategoryId", "System.Int32"),
            Column("Name", "System.String"),
            Column("Description", "System.String", allowNull: true, nullBehavior: NullValueBehaviorKind.ReturnNull)
        ),
        ImmutableArray.Create("CategoryId"),
        ImmutableArray<UniqueConstraintModel>.Empty);

    public static RelationModel CustomerOrderRelation => new RelationModel(
        "FK_Customer_Order",
        "Customer",
        ImmutableArray.Create("CustomerId"),
        "Order",
        ImmutableArray.Create("CustomerId"),
        false,
        false,
        null,
        null);

    public static RelationModel TypedCategoryProductRelation => new RelationModel(
        "FK_Category_Product",
        "Category",
        ImmutableArray.Create("CategoryId"),
        "Product",
        ImmutableArray.Create("CategoryId"),
        false,
        false,
        "Category",
        "GetProducts");

    public static DataSetModel SimpleDataSet => new DataSetModel(
        "OrdersDS",
        null,
        "en-US",
        false,
        true,
        ImmutableArray.Create(CustomerTable, OrderTable),
        ImmutableArray.Create(CustomerOrderRelation),
        ImmutableArray<ForeignKeyConstraintModel>.Empty);
}
