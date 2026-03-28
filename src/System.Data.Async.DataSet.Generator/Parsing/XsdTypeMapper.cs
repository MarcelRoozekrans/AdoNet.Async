using System;
using System.Collections.Generic;

namespace System.Data.Async.DataSet.Generator.Parsing;

internal static class XsdTypeMapper
{
    private static readonly Dictionary<string, string> Map = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["xs:string"] = "System.String",
        ["xs:int"] = "System.Int32",
        ["xs:integer"] = "System.Int64",
        ["xs:boolean"] = "System.Boolean",
        ["xs:dateTime"] = "System.DateTime",
        ["xs:decimal"] = "System.Decimal",
        ["xs:double"] = "System.Double",
        ["xs:float"] = "System.Single",
        ["xs:long"] = "System.Int64",
        ["xs:short"] = "System.Int16",
        ["xs:byte"] = "System.SByte",
        ["xs:unsignedByte"] = "System.Byte",
        ["xs:unsignedShort"] = "System.UInt16",
        ["xs:unsignedInt"] = "System.UInt32",
        ["xs:unsignedLong"] = "System.UInt64",
        ["xs:base64Binary"] = "System.Byte[]",
        ["xs:duration"] = "System.TimeSpan",
        ["xs:time"] = "System.DateTime",
        ["xs:date"] = "System.DateTime",
        ["xs:anyURI"] = "System.String",
        ["xs:QName"] = "System.String",
    };

    public static string? TryMap(string xsdType)
    {
        return Map.TryGetValue(xsdType, out var clrType) ? clrType : null;
    }
}
