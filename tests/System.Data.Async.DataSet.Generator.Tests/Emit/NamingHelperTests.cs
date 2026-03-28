using System.Collections.Immutable;
using FluentAssertions;
using System.Data.Async.DataSet.Generator.Emit;
using Xunit;

namespace System.Data.Async.DataSet.Generator.Tests.Emit;

public class NamingHelperTests
{
    [Fact]
    public void RowClassName_UsesTableName_WhenTypedNameIsNull()
    {
        NamingHelper.RowClassName("Customer", null).Should().Be("AsyncCustomerRow");
    }

    [Fact]
    public void RowClassName_UsesTypedName_WhenProvided()
    {
        NamingHelper.RowClassName("Customer", "CustomerEntry").Should().Be("AsyncCustomerEntryRow");
    }

    [Fact]
    public void TableClassName_UsesTableName_WhenTypedPluralIsNull()
    {
        NamingHelper.TableClassName("Order", null).Should().Be("AsyncOrderDataTable");
    }

    [Fact]
    public void TableClassName_UsesTypedPlural_WhenProvided()
    {
        NamingHelper.TableClassName("Order", "Orders").Should().Be("AsyncOrdersDataTable");
    }

    [Fact]
    public void DataSetClassName_PrepentsAsync()
    {
        NamingHelper.DataSetClassName("OrdersDS").Should().Be("AsyncOrdersDS");
    }

    [Fact]
    public void EventArgsClassName_UsesTableName_WhenTypedNameIsNull()
    {
        NamingHelper.EventArgsClassName("Customer", null).Should().Be("AsyncCustomerRowChangeEvent");
    }

    [Fact]
    public void EventArgsClassName_UsesTypedName_WhenProvided()
    {
        NamingHelper.EventArgsClassName("Customer", "Cust").Should().Be("AsyncCustRowChangeEvent");
    }

    [Fact]
    public void FindByMethodName_ConcatenatesColumns()
    {
        var cols = ImmutableArray.Create("FirstName", "LastName");
        NamingHelper.FindByMethodName(cols).Should().Be("FindByFirstNameLastName");
    }

    [Fact]
    public void FindByMethodName_SingleColumn()
    {
        var cols = ImmutableArray.Create("Id");
        NamingHelper.FindByMethodName(cols).Should().Be("FindById");
    }

    [Fact]
    public void SetterMethodName_AppendsAsync()
    {
        NamingHelper.SetterMethodName("Name").Should().Be("SetNameAsync");
    }

    [Fact]
    public void IsNullMethodName_ReturnsCorrectName()
    {
        NamingHelper.IsNullMethodName("Email").Should().Be("IsEmailNull");
    }

    [Fact]
    public void SetNullMethodName_ReturnsCorrectName()
    {
        NamingHelper.SetNullMethodName("Email").Should().Be("SetEmailNullAsync");
    }

    [Theory]
    [InlineData("System.String", "string")]
    [InlineData("System.Int32", "int")]
    [InlineData("System.Int64", "long")]
    [InlineData("System.Boolean", "bool")]
    [InlineData("System.Decimal", "decimal")]
    [InlineData("System.Double", "double")]
    [InlineData("System.Single", "float")]
    [InlineData("System.Byte[]", "byte[]")]
    [InlineData("System.DateTime", "global::System.DateTime")]
    [InlineData("System.Guid", "global::System.Guid")]
    [InlineData("System.TimeSpan", "global::System.TimeSpan")]
    [InlineData("MyCustom.Type", "global::MyCustom.Type")]
    public void ClrTypeToKeyword_MapsCorrectly(string clrType, string expected)
    {
        NamingHelper.ClrTypeToKeyword(clrType).Should().Be(expected);
    }

    [Fact]
    public void AddRowMethodName_UsesTypedName()
    {
        NamingHelper.AddRowMethodName("Customer", "Cust").Should().Be("AddCustRowAsync");
    }

    [Fact]
    public void RemoveRowMethodName_UsesTableName_WhenTypedNameIsNull()
    {
        NamingHelper.RemoveRowMethodName("Customer", null).Should().Be("RemoveCustomerRowAsync");
    }

    [Fact]
    public void NewRowMethodName_UsesTableName()
    {
        NamingHelper.NewRowMethodName("Order", null).Should().Be("NewOrderRow");
    }

    [Fact]
    public void GetChildRowsMethodName_UsesTypedChildren_WhenProvided()
    {
        NamingHelper.GetChildRowsMethodName("Order", "GetOrders").Should().Be("GetOrders");
    }

    [Fact]
    public void GetChildRowsMethodName_FallsBackToDefault()
    {
        NamingHelper.GetChildRowsMethodName("Order", null).Should().Be("GetOrderRows");
    }

    [Fact]
    public void ParentRowPropertyName_UsesTypedParent()
    {
        NamingHelper.ParentRowPropertyName("Customer", "Customer").Should().Be("CustomerRow");
    }

    [Fact]
    public void ParentRowPropertyName_FallsBackToTableName()
    {
        NamingHelper.ParentRowPropertyName("Customer", null).Should().Be("CustomerRow");
    }

    [Fact]
    public void SetParentRowMethodName_Correct()
    {
        NamingHelper.SetParentRowMethodName("Customer", null).Should().Be("SetCustomerRowAsync");
    }
}
