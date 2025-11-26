using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

public static class JsonStatImporter
{
    // Convert a JSON-stat file to CSV. This implements the common JSON-stat layout:
    // dataset.dimension contains dimensions and category information, and dataset.value is an array
    // with the values ordered so the last dimension changes fastest.
    public static async Task ConvertJsonStatToCsvAsync(string jsonPath, string csvOutPath)
    {
        using var fs = File.OpenRead(jsonPath);
        using var doc = await JsonDocument.ParseAsync(fs);
        var root = doc.RootElement;

        var dataset = root.TryGetProperty("dataset", out var ds) ? ds : root;

        if (!dataset.TryGetProperty("dimension", out var dims))
            throw new InvalidDataException("JSON-stat dataset.dimension not found");

        // Determine dimension order
        string[] dimOrder = GetDimensionOrder(dataset, dims);

        // For each dimension, get category keys and label map
        var dimKeys = new List<string[]>();
        var dimLabels = new List<Dictionary<string, string?>>();

        foreach (var dimName in dimOrder)
        {
            var dimElem = dims.GetProperty(dimName);
            var cat = dimElem.GetProperty("category");

            string[] keys = GetCategoryKeys(cat);
            dimKeys.Add(keys);

            var labels = new Dictionary<string, string?>();
            if (cat.TryGetProperty("label", out var labElem) && labElem.ValueKind == JsonValueKind.Object)
            {
                foreach (var p in labElem.EnumerateObject()) labels[p.Name] = p.Value.ValueKind == JsonValueKind.Null ? null : p.Value.GetString();
            }
            dimLabels.Add(labels);
        }

        // Read values array
        if (!dataset.TryGetProperty("value", out var valuesElem) || valuesElem.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("JSON-stat dataset.value not found or not an array");

        var values = valuesElem.EnumerateArray().ToArray();

        // Precompute dimension sizes and multipliers for linear index
        int n = dimKeys.Count;
        var sizes = dimKeys.Select(k => k.Length).ToArray();
        var multipliers = new int[n];
        for (int i = 0; i < n; i++)
        {
            int m = 1;
            for (int j = i + 1; j < n; j++) m *= sizes[j];
            multipliers[i] = m;
        }

        // Write CSV header
        using var writer = new StreamWriter(csvOutPath);
        var headerCols = string.Join(",", dimOrder.Select(d => EscapeCsv(d)).Concat(new[] { "value" }));
        await writer.WriteLineAsync(headerCols);

        // Iterate all combinations (cartesian product)
        var indices = new int[n];
        long total = 1;
        foreach (var s in sizes) total *= s;

        for (long r = 0; r < total; r++)
        {
            // compute multi-index for r
            long rem = r;
            for (int i = 0; i < n; i++)
            {
                int idx = (int)(rem / multipliers[i]);
                indices[i] = idx;
                rem = rem % multipliers[i];
            }

            // compute linear index into values array
            long linear = 0;
            for (int i = 0; i < n; i++) linear += indices[i] * multipliers[i];

            string? valueStr = null;
            if (linear >= 0 && linear < values.Length)
            {
                var v = values[linear];
                if (v.ValueKind == JsonValueKind.Null) valueStr = string.Empty;
                else if (v.ValueKind == JsonValueKind.Number) valueStr = v.GetRawText();
                else valueStr = v.ToString();
            }

            // build row: use labels if available, otherwise keys
            var cols = new List<string>(n + 1);
            for (int i = 0; i < n; i++)
            {
                var key = dimKeys[i][indices[i]];
                if (dimLabels[i].TryGetValue(key, out var lab) && lab != null) cols.Add(EscapeCsv(lab));
                else cols.Add(EscapeCsv(key));
            }
            cols.Add(EscapeCsv(valueStr ?? string.Empty));

            await writer.WriteLineAsync(string.Join(',', cols));
        }
    }

    private static string EscapeCsv(string s)
    {
        if (s.Contains('"') || s.Contains(',') || s.Contains('\n') || s.Contains('\r'))
            return '"' + s.Replace("\"", "\"\"") + '"';
        return s;
    }

    private static string[] GetCategoryKeys(JsonElement cat)
    {
        if (cat.TryGetProperty("index", out var idx))
        {
            if (idx.ValueKind == JsonValueKind.Array)
            {
                return idx.EnumerateArray().Select(x => x.GetString() ?? string.Empty).ToArray();
            }
            else if (idx.ValueKind == JsonValueKind.Object)
            {
                return idx.EnumerateObject().Select(p => p.Name).ToArray();
            }
        }

        if (cat.TryGetProperty("label", out var lab) && lab.ValueKind == JsonValueKind.Object)
        {
            return lab.EnumerateObject().Select(p => p.Name).ToArray();
        }

        return Array.Empty<string>();
    }

    private static string[] GetDimensionOrder(JsonElement dataset, JsonElement dims)
    {
        if (dataset.TryGetProperty("id", out var idElem) && idElem.ValueKind == JsonValueKind.Array)
        {
            return idElem.EnumerateArray().Select(x => x.GetString() ?? string.Empty).ToArray();
        }

        if (dims.TryGetProperty("id", out var dimsId) && dimsId.ValueKind == JsonValueKind.Array)
        {
            return dimsId.EnumerateArray().Select(x => x.GetString() ?? string.Empty).ToArray();
        }

        // Fallback: use the order of properties in "dimension" except reserved names
        var reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "id", "size", "role" };
        return dims.EnumerateObject().Where(p => !reserved.Contains(p.Name)).Select(p => p.Name).ToArray();
    }
}
