using System.Text.Json;

namespace DragonKMG.ToolGateway;

/// <summary>
/// Validates the deliberately small JSON Schema subset supported by the MCP gateway.
/// Unsupported keywords fail closed instead of being silently ignored.
/// </summary>
public static class McpInputSchemaValidator
{
    private static readonly HashSet<string> SupportedKeywords = new(StringComparer.Ordinal)
    {
        "type", "properties", "required", "additionalProperties", "items", "enum",
        "minimum", "maximum", "minLength", "maxLength", "minItems", "maxItems", "description", "title"
    };

    public static void ValidateSchema(string schemaJson)
    {
        if (string.IsNullOrWhiteSpace(schemaJson) || schemaJson.Length > 64 * 1024)
            throw new McpHttpException("invalid_schema", "MCP tool schema is empty or too large.");
        try
        {
            using var document = JsonDocument.Parse(schemaJson, new JsonDocumentOptions { MaxDepth = 16 });
            ValidateSchemaElement(document.RootElement, 0);
        }
        catch (JsonException)
        {
            throw new McpHttpException("invalid_schema", "MCP tool schema is not valid JSON.");
        }
    }

    public static void ValidateArguments(string schemaJson, JsonElement arguments)
    {
        ValidateSchema(schemaJson);
        try
        {
            using var document = JsonDocument.Parse(schemaJson, new JsonDocumentOptions { MaxDepth = 16 });
            ValidateValue(document.RootElement, arguments, "$", 0);
        }
        catch (JsonException)
        {
            throw new McpHttpException("invalid_arguments", "MCP tool arguments do not match the declared input schema.");
        }
    }

    private static void ValidateSchemaElement(JsonElement schema, int depth)
    {
        if (depth > 12 || schema.ValueKind != JsonValueKind.Object)
            throw InvalidSchema();
        foreach (var property in schema.EnumerateObject())
            if (!SupportedKeywords.Contains(property.Name)) throw new McpHttpException("unsupported_schema", "MCP tool schema uses a keyword the gateway does not support.");

        var type = RequiredString(schema, "type");
        if (type is not ("object" or "array" or "string" or "number" or "integer" or "boolean" or "null"))
            throw InvalidSchema();

        if (schema.TryGetProperty("enum", out var enumValues))
        {
            if (enumValues.ValueKind != JsonValueKind.Array || enumValues.GetArrayLength() is < 1 or > 100)
                throw InvalidSchema();
            foreach (var value in enumValues.EnumerateArray())
                if (value.ValueKind is JsonValueKind.Object or JsonValueKind.Array or JsonValueKind.Undefined)
                    throw new McpHttpException("unsupported_schema", "MCP enum values must be JSON scalars.");
        }

        if (type == "object")
        {
            if (!schema.TryGetProperty("properties", out var properties) || properties.ValueKind != JsonValueKind.Object)
                throw InvalidSchema();
            if (properties.EnumerateObject().Count() > 100) throw InvalidSchema();
            if (schema.TryGetProperty("additionalProperties", out var additional) && additional.ValueKind != JsonValueKind.False)
                throw new McpHttpException("unsupported_schema", "MCP object schemas must set additionalProperties to false.");
            foreach (var property in properties.EnumerateObject())
            {
                if (property.Name.Length is < 1 or > 128 || property.Name.Any(char.IsControl)) throw InvalidSchema();
                ValidateSchemaElement(property.Value, depth + 1);
            }
            if (schema.TryGetProperty("required", out var required))
            {
                if (required.ValueKind != JsonValueKind.Array) throw InvalidSchema();
                var declared = properties.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal);
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var item in required.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.String || !declared.Contains(item.GetString()!) || !seen.Add(item.GetString()!))
                        throw InvalidSchema();
                }
            }
        }
        else if (type == "array")
        {
            if (!schema.TryGetProperty("items", out var items)) throw InvalidSchema();
            ValidateSchemaElement(items, depth + 1);
            ValidateIntegerBound(schema, "minItems", 0, 10000);
            ValidateIntegerBound(schema, "maxItems", 0, 10000);
        }
        else if (type == "string")
        {
            ValidateIntegerBound(schema, "minLength", 0, 100000);
            ValidateIntegerBound(schema, "maxLength", 0, 100000);
        }
        else if (type is "number" or "integer")
        {
            ValidateNumberBound(schema, "minimum");
            ValidateNumberBound(schema, "maximum");
        }
        foreach (var textKeyword in new[] { "description", "title" })
            if (schema.TryGetProperty(textKeyword, out var text) && (text.ValueKind != JsonValueKind.String || text.GetString()!.Length > 512)) throw InvalidSchema();
    }

    private static void ValidateValue(JsonElement schema, JsonElement value, string path, int depth)
    {
        if (depth > 12) throw InvalidArguments();
        var type = schema.GetProperty("type").GetString()!;
        var matches = type switch
        {
            "object" => value.ValueKind == JsonValueKind.Object,
            "array" => value.ValueKind == JsonValueKind.Array,
            "string" => value.ValueKind == JsonValueKind.String,
            "number" => value.ValueKind == JsonValueKind.Number,
            "integer" => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _),
            "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
            "null" => value.ValueKind == JsonValueKind.Null,
            _ => false
        };
        if (!matches) throw InvalidArguments();

        if (schema.TryGetProperty("enum", out var values) && !values.EnumerateArray().Any(candidate => JsonElement.DeepEquals(candidate, value)))
            throw InvalidArguments();

        if (type == "object")
        {
            var properties = schema.GetProperty("properties");
            foreach (var property in value.EnumerateObject())
            {
                if (!properties.TryGetProperty(property.Name, out var propertySchema)) throw InvalidArguments();
                ValidateValue(propertySchema, property.Value, path + "." + property.Name, depth + 1);
            }
            if (schema.TryGetProperty("required", out var required))
                foreach (var item in required.EnumerateArray())
                    if (!value.TryGetProperty(item.GetString()!, out _)) throw InvalidArguments();
        }
        else if (type == "array")
        {
            var count = value.GetArrayLength();
            CheckInteger(value.GetArrayLength(), schema, "minItems", "maxItems");
            foreach (var item in value.EnumerateArray()) ValidateValue(schema.GetProperty("items"), item, path + "[]", depth + 1);
        }
        else if (type == "string")
        {
            var length = value.GetString()!.Length;
            CheckInteger(length, schema, "minLength", "maxLength");
        }
        else if (type is "number" or "integer")
        {
            var number = value.GetDouble();
            if (schema.TryGetProperty("minimum", out var min) && number < min.GetDouble()) throw InvalidArguments();
            if (schema.TryGetProperty("maximum", out var max) && number > max.GetDouble()) throw InvalidArguments();
        }
    }

    private static void CheckInteger(int actual, JsonElement schema, string minName, string maxName)
    {
        if (schema.TryGetProperty(minName, out var min) && actual < min.GetInt32()) throw InvalidArguments();
        if (schema.TryGetProperty(maxName, out var max) && actual > max.GetInt32()) throw InvalidArguments();
    }

    private static void ValidateIntegerBound(JsonElement schema, string keyword, int min, int max)
    {
        if (schema.TryGetProperty(keyword, out var value) &&
            (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var number) || number < min || number > max)) throw InvalidSchema();
    }

    private static void ValidateNumberBound(JsonElement schema, string keyword)
    {
        if (schema.TryGetProperty(keyword, out var value) && (value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out _))) throw InvalidSchema();
    }

    private static string RequiredString(JsonElement objectElement, string name) =>
        objectElement.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()!
            : throw InvalidSchema();

    private static McpHttpException InvalidSchema() => new("invalid_schema", "MCP tool schema is invalid or outside the supported subset.");
    private static McpHttpException InvalidArguments() => new("invalid_arguments", "MCP tool arguments do not match the declared input schema.");
}
