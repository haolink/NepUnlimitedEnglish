using System;
using System.Linq;
using System.Collections.Generic;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Newtonsoft.Json.Linq;

namespace NepUnlPatcher;

/// <summary>
/// Converts an already-parsed <see cref="AssetTypeValueField"/> tree to and from JSON, the way
/// AssetStudio shows a MonoBehaviour: fields as properties, lists as arrays. AssetsTools.NET does
/// the actual type tree parsing and the raw byte (de)serialisation; this only
/// maps its generic value tree to <see cref="JToken"/> and back.
/// </summary>
internal static class AssetValueJson
{
    /// <summary>Dumps a field (and everything below it) as JSON.</summary>
    /// <param name="field">The field; the root field for a whole object.</param>
    /// <returns>The value as JSON.</returns>
    public static JToken Read(AssetTypeValueField field)
    {
        AssetTypeTemplateField template = field.TemplateField;

        if (IsVectorWrapper(field, out AssetTypeValueField? arrayChild))
        {
            return Read(arrayChild);
        }

        if (template.IsArray)
        {
            if (IsByteArray(template))
            {
                byte[] bytes = template.ValueType == AssetValueType.ByteArray
                    ? field.Value!.AsByteArray
                    : field.Children.Select(c => c.Value!.AsByte).ToArray();
                return new JValue(bytes);
            }

            JArray array = new JArray();
            foreach (AssetTypeValueField child in field.Children)
            {
                array.Add(Read(child));
            }

            return array;
        }

        if (template.ValueType != AssetValueType.None)
        {
            return template.ValueType switch
            {
                AssetValueType.Bool => new JValue(field.Value!.AsBool),
                AssetValueType.Int8 => new JValue(field.Value!.AsSByte),
                AssetValueType.UInt8 => new JValue(field.Value!.AsByte),
                AssetValueType.Int16 => new JValue(field.Value!.AsShort),
                AssetValueType.UInt16 => new JValue(field.Value!.AsUShort),
                AssetValueType.Int32 => new JValue(field.Value!.AsInt),
                AssetValueType.UInt32 => new JValue(field.Value!.AsUInt),
                AssetValueType.Int64 => new JValue(field.Value!.AsLong),
                AssetValueType.UInt64 => new JValue(field.Value!.AsULong),
                AssetValueType.Float => new JValue(field.Value!.AsFloat),
                AssetValueType.Double => new JValue(field.Value!.AsDouble),
                AssetValueType.String => new JValue(field.Value!.AsString),
                AssetValueType.ByteArray => new JValue(field.Value!.AsByteArray),
                _ => throw new NotSupportedException($"{template.Name}: managed references are not supported"),
            };
        }

        JObject fields = new JObject();
        foreach (AssetTypeValueField child in field.Children)
        {
            fields.Add(child.TemplateField.Name, Read(child));
        }

        return fields;
    }

    /// <summary>
    /// Applies JSON onto a field (and everything below it), overwriting its current value/children
    /// in place.
    /// </summary>
    /// <param name="field">The field to overwrite; the root field for a whole object.</param>
    /// <param name="json">The new value.</param>
    public static void Write(AssetTypeValueField field, JToken json)
    {
        AssetTypeTemplateField template = field.TemplateField;

        if (IsVectorWrapper(field, out AssetTypeValueField? arrayChild))
        {
            Write(arrayChild, json);
            return;
        }

        if (template.IsArray)
        {
            if (IsByteArray(template))
            {
                byte[] bytes = Expect(json, template, JTokenType.Bytes, JTokenType.String).ToObject<byte[]>()!;
                if (template.ValueType == AssetValueType.ByteArray)
                {
                    field.Value = new AssetTypeValue(bytes, asString: false);
                    field.Children = new List<AssetTypeValueField>(0);
                }
                else
                {
                    AssetTypeTemplateField elementTemplate = template.Children[1];
                    field.Children = bytes.Select(b => new AssetTypeValueField
                    {
                        TemplateField = elementTemplate,
                        Value = new AssetTypeValue(b),
                        Children = new List<AssetTypeValueField>(0),
                    }).ToList();
                }

                return;
            }

            JArray array = (JArray)Expect(json, template, JTokenType.Array);
            AssetTypeTemplateField elemTemplate = template.Children[1];
            List<AssetTypeValueField> children = new List<AssetTypeValueField>(array.Count);
            foreach (JToken item in array)
            {
                AssetTypeValueField child = ValueBuilder.DefaultValueFieldFromTemplate(elemTemplate);
                Write(child, item);
                children.Add(child);
            }

            field.Children = children;
            return;
        }

        if (template.ValueType != AssetValueType.None)
        {
            field.Value = template.ValueType switch
            {
                AssetValueType.Bool => new AssetTypeValue((bool)Expect(json, template, JTokenType.Boolean)),
                AssetValueType.Int8 => new AssetTypeValue((sbyte)Expect(json, template, JTokenType.Integer)),
                AssetValueType.UInt8 => new AssetTypeValue((byte)Expect(json, template, JTokenType.Integer)),
                AssetValueType.Int16 => new AssetTypeValue((short)Expect(json, template, JTokenType.Integer)),
                AssetValueType.UInt16 => new AssetTypeValue((ushort)Expect(json, template, JTokenType.Integer)),
                AssetValueType.Int32 => new AssetTypeValue((int)Expect(json, template, JTokenType.Integer)),
                AssetValueType.UInt32 => new AssetTypeValue((uint)Expect(json, template, JTokenType.Integer)),
                AssetValueType.Int64 => new AssetTypeValue((long)Expect(json, template, JTokenType.Integer)),
                AssetValueType.UInt64 => new AssetTypeValue((ulong)Expect(json, template, JTokenType.Integer)),
                AssetValueType.Float => new AssetTypeValue((float)Expect(json, template, JTokenType.Float, JTokenType.Integer)),
                AssetValueType.Double => new AssetTypeValue((double)Expect(json, template, JTokenType.Float, JTokenType.Integer)),
                AssetValueType.String => new AssetTypeValue((string)Expect(json, template, JTokenType.String)!),
                AssetValueType.ByteArray => new AssetTypeValue(Expect(json, template, JTokenType.Bytes, JTokenType.String).ToObject<byte[]>()!, asString: false),
                _ => throw new NotSupportedException($"{template.Name}: managed references are not supported"),
            };
            return;
        }

        JObject fields = (JObject)Expect(json, template, JTokenType.Object);
        foreach (JProperty property in fields.Properties())
        {
            if (!template.Children.Any(c => c.Name == property.Name))
            {
                throw new InvalidDataException($"{template.Name}: has no field '{property.Name}'");
            }
        }

        foreach (AssetTypeValueField child in field.Children)
        {
            JToken value = fields[child.TemplateField.Name]
                ?? throw new InvalidDataException($"{template.Name}: field '{child.TemplateField.Name}' is missing");
            Write(child, value);
        }
    }

    /// <summary>
    /// Unity's on-disk layout for every vector/List[T] field: an outer struct with
    /// exactly one child literally named "Array", which carries the actual size+data array. Both
    /// AssetStudio and this project's previous type tree walker present that outer field directly
    /// as the array in JSON, skipping the wrapper.
    /// </summary>
    private static bool IsVectorWrapper(AssetTypeValueField field, out AssetTypeValueField arrayChild)
    {
        if (!field.TemplateField.IsArray && field.Children.Count == 1 && field.Children[0].TemplateField is { IsArray: true, Name: "Array" })
        {
            arrayChild = field.Children[0];
            return true;
        }

        arrayChild = null!;
        return false;
    }

    /// <summary>
    /// Arrays of plain bytes are kept as bytes (much smaller than a JSON array of numbers),
    /// whether the type tree calls them "TypelessData" (ByteArray value type) or a plain unaligned
    /// UInt8/char vector - matching the project's previous convention.
    /// </summary>
    private static bool IsByteArray(AssetTypeTemplateField template) 
    {
        return template.ValueType == AssetValueType.ByteArray ||
               (template.Children.Count == 2 && template.Children[1].ValueType == AssetValueType.UInt8 && !template.Children[1].IsAligned);
    }

    /// <summary>
    /// Returns <paramref name="value"/> if it is of one of the <paramref name="allowed"/> JSON kinds.
    /// Throws an <see cref="InvalidDataException"/> otherwise.
    /// </summary>
    private static JToken Expect(JToken value, AssetTypeTemplateField template, params JTokenType[] allowed)
    {
        return allowed.Contains(value.Type)
            ? value
            : throw new InvalidDataException($"{template.Name}: expected {string.Join(" or ", allowed)}, got {value.Type}");
    }
}
