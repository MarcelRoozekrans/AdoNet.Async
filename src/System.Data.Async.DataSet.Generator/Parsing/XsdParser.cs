using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using System.Data.Async.DataSet.Generator.Model;

namespace System.Data.Async.DataSet.Generator.Parsing;

internal static class XsdParser
{
    private static readonly XNamespace Xs = "http://www.w3.org/2001/XMLSchema";
    private static readonly XNamespace Msdata = "urn:schemas-microsoft-com:xml-msdata";
    private static readonly XNamespace Codegen = "urn:schemas-microsoft-com:xml-msprop";

    public static DataSetModel Parse(string xsdContent)
    {
        var doc = XDocument.Parse(xsdContent);
        var schema = doc.Root!;
        var dsElement = schema.Descendants(Xs + "element")
            .First(e => string.Equals((string?)e.Attribute(Msdata + "IsDataSet"), "true", StringComparison.OrdinalIgnoreCase));

        var name = (string)dsElement.Attribute("name")!;
        var locale = (string?)dsElement.Attribute(Msdata + "Locale");
        var caseSensitive = ParseBool(dsElement, Msdata + "CaseSensitive");
        var enforceConstraints = ParseBool(dsElement, Msdata + "EnforceConstraints", defaultValue: true);

        var complexType = dsElement.Element(Xs + "complexType")!;
        var choice = complexType.Element(Xs + "choice") ?? complexType.Element(Xs + "sequence");

        var tables = ImmutableArray.CreateBuilder<TableModel>();
        var uniqueKeys = new Dictionary<string, (string TableName, ImmutableArray<string> Columns, bool IsPrimaryKey)>(StringComparer.Ordinal);

        if (choice != null)
        {
            foreach (var tableElement in choice.Elements(Xs + "element"))
            {
                tables.Add(ParseTable(tableElement));
            }
        }

        // Parse xs:unique constraints
        foreach (var unique in dsElement.Elements(Xs + "unique"))
        {
            var uName = (string)unique.Attribute("name")!;
            var isPk = ParseBool(unique, Msdata + "PrimaryKey");
            var selector = unique.Element(Xs + "selector")!;
            var tableName = ExtractTableNameFromXPath((string)selector.Attribute("xpath")!);
            var columns = ParseFieldColumns(unique);

            uniqueKeys[uName] = (tableName, columns, isPk);

            if (isPk)
            {
                UpdateTablePrimaryKey(tables, tableName, columns);
            }
        }

        // Parse xs:key constraints (also used as unique references)
        foreach (var key in dsElement.Elements(Xs + "key"))
        {
            var kName = (string)key.Attribute("name")!;
            var isPk = ParseBool(key, Msdata + "PrimaryKey");
            var selector = key.Element(Xs + "selector")!;
            var tableName = ExtractTableNameFromXPath((string)selector.Attribute("xpath")!);
            var columns = ParseFieldColumns(key);

            uniqueKeys[kName] = (tableName, columns, isPk);

            if (isPk)
            {
                UpdateTablePrimaryKey(tables, tableName, columns);
            }
        }

        // Parse xs:keyref -> relations and FK constraints
        var relations = ImmutableArray.CreateBuilder<RelationModel>();
        var fkConstraints = ImmutableArray.CreateBuilder<ForeignKeyConstraintModel>();

        foreach (var keyref in dsElement.Elements(Xs + "keyref"))
        {
            var krName = (string)keyref.Attribute("name")!;
            var refer = (string)keyref.Attribute("refer")!;
            var constraintOnly = ParseBool(keyref, Msdata + "ConstraintOnly");
            var nested = ParseBool(keyref, Msdata + "IsNested");
            var updateRule = (string?)keyref.Attribute(Msdata + "UpdateRule") ?? "Cascade";
            var deleteRule = (string?)keyref.Attribute(Msdata + "DeleteRule") ?? "Cascade";
            var acceptRejectRule = (string?)keyref.Attribute(Msdata + "AcceptRejectRule") ?? "None";
            var typedParent = (string?)keyref.Attribute(Codegen + "typedParent");
            var typedChildren = (string?)keyref.Attribute(Codegen + "typedChildren");

            var selector = keyref.Element(Xs + "selector")!;
            var childTable = ExtractTableNameFromXPath((string)selector.Attribute("xpath")!);
            var childColumns = ParseFieldColumns(keyref);

            if (!uniqueKeys.TryGetValue(refer, out var parentInfo))
            {
                continue;
            }

            fkConstraints.Add(new ForeignKeyConstraintModel(
                krName, parentInfo.TableName, parentInfo.Columns,
                childTable, childColumns,
                updateRule, deleteRule, acceptRejectRule));

            if (!constraintOnly)
            {
                relations.Add(new RelationModel(
                    krName, parentInfo.TableName, parentInfo.Columns,
                    childTable, childColumns,
                    nested, constraintOnly, typedParent, typedChildren));
            }
        }

        // Build unique constraints list (non-PK)
        var updatedTables = tables.Select(t =>
        {
            var ucs = uniqueKeys.Values
                .Where(u => string.Equals(u.TableName, t.Name, StringComparison.Ordinal) && !u.IsPrimaryKey)
                .Select(u => new UniqueConstraintModel(t.Name + "_Unique", t.Name, u.Columns, false))
                .ToImmutableArray();
            return t with { UniqueConstraints = ucs };
        }).ToImmutableArray();

        return new DataSetModel(
            name, null, locale, caseSensitive, enforceConstraints,
            updatedTables, relations.ToImmutable(), fkConstraints.ToImmutable());
    }

    private static ImmutableArray<string> ParseFieldColumns(XElement parent)
    {
        var builder = ImmutableArray.CreateBuilder<string>();
        foreach (var field in parent.Elements(Xs + "field"))
        {
            builder.Add(ExtractColumnNameFromXPath((string)field.Attribute("xpath")!));
        }

        return builder.ToImmutable();
    }

    private static void UpdateTablePrimaryKey(ImmutableArray<TableModel>.Builder tables, string tableName, ImmutableArray<string> columns)
    {
        for (int i = 0; i < tables.Count; i++)
        {
            if (string.Equals(tables[i].Name, tableName, StringComparison.Ordinal))
            {
                tables[i] = tables[i] with { PrimaryKeyColumnNames = columns };
                break;
            }
        }
    }

    private static TableModel ParseTable(XElement tableElement)
    {
        var tableName = (string)tableElement.Attribute("name")!;
        var typedName = (string?)tableElement.Attribute(Codegen + "typedName");
        var typedPlural = (string?)tableElement.Attribute(Codegen + "typedPlural");

        var complexType = tableElement.Element(Xs + "complexType");
        var columns = ImmutableArray.CreateBuilder<ColumnModel>();

        if (complexType != null)
        {
            var sequence = complexType.Element(Xs + "sequence");
            if (sequence != null)
            {
                foreach (var col in sequence.Elements(Xs + "element"))
                {
                    columns.Add(ParseColumn(col));
                }
            }
        }

        return new TableModel(
            tableName, typedName, typedPlural,
            columns.ToImmutable(),
            ImmutableArray<string>.Empty,
            ImmutableArray<UniqueConstraintModel>.Empty);
    }

    private static ColumnModel ParseColumn(XElement colElement)
    {
        var colName = (string)colElement.Attribute("name")!;
        var xsdType = (string?)colElement.Attribute("type");
        var dataTypeOverride = (string?)colElement.Attribute(Msdata + "DataType");
        var clrType = dataTypeOverride ?? (xsdType != null ? XsdTypeMapper.TryMap(xsdType) : null) ?? "System.String";

        var minOccurs = (string?)colElement.Attribute("minOccurs");
        var allowDbNull = string.Equals(minOccurs, "0", StringComparison.Ordinal);

        var readOnly = ParseBool(colElement, Msdata + "ReadOnly");
        var expression = (string?)colElement.Attribute(Msdata + "Expression");
        var autoIncrement = ParseBool(colElement, Msdata + "AutoIncrement");
        var seed = ParseLong(colElement, Msdata + "AutoIncrementSeed", 0);
        var step = ParseLong(colElement, Msdata + "AutoIncrementStep", 1);
        var defaultValue = (string?)colElement.Attribute(Msdata + "DefaultValue");
        var caption = (string?)colElement.Attribute(Msdata + "Caption");
        var ordinal = ParseNullableInt(colElement, Msdata + "Ordinal");
        var maxLength = ParseNullableInt(colElement, "maxLength");
        var isHidden = ParseBool(colElement, Msdata + "hiddenColumn");

        var nullValueRaw = (string?)colElement.Attribute(Codegen + "nullValue");
        var nullBehavior = ParseNullValueBehavior(nullValueRaw, allowDbNull);

        return new ColumnModel(
            colName, clrType, allowDbNull, readOnly, expression,
            autoIncrement, seed, step, defaultValue, caption,
            ordinal, maxLength, isHidden, nullBehavior);
    }

    private static NullValueBehavior ParseNullValueBehavior(string? raw, bool allowDbNull)
    {
        if (!allowDbNull || raw == null)
        {
            return new NullValueBehavior(NullValueBehaviorKind.Throw);
        }

        return raw switch
        {
            "_throw" => new NullValueBehavior(NullValueBehaviorKind.Throw),
            "_null" => new NullValueBehavior(NullValueBehaviorKind.ReturnNull),
            "_empty" or "" => new NullValueBehavior(NullValueBehaviorKind.ReturnEmpty),
            _ => new NullValueBehavior(NullValueBehaviorKind.ReplacementValue, raw),
        };
    }

    private static bool ParseBool(XElement el, XName attr, bool defaultValue = false)
    {
        var val = (string?)el.Attribute(attr);
        return val != null ? string.Equals(val, "true", StringComparison.OrdinalIgnoreCase) : defaultValue;
    }

    private static long ParseLong(XElement el, XName attr, long defaultValue)
    {
        var val = (string?)el.Attribute(attr);
        return val != null && long.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : defaultValue;
    }

    private static int? ParseNullableInt(XElement el, XName attr)
    {
        var val = (string?)el.Attribute(attr);
        return val != null && int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : null;
    }

    private static string ExtractTableNameFromXPath(string xpath)
    {
        var idx = xpath.LastIndexOf('/');
        return idx >= 0 ? xpath.Substring(idx + 1) : xpath;
    }

    private static string ExtractColumnNameFromXPath(string xpath)
    {
        var idx = xpath.LastIndexOf('/');
        return idx >= 0 ? xpath.Substring(idx + 1) : xpath;
    }
}
