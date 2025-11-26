using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Utility for converting JSON-stat formatted datasets to CSV.
/// JSON-stat uses a hierarchical structure with dimensions (categories) and a value array.
/// This importer flattens the structure by expanding dimensions and mapping the value array to rows.
/// 
/// JSON-stat Specification: https://json-stat.org/format/
/// </summary>
public static class JsonStatImporter
{
    /// <summary>
    /// Converts a JSON-stat dataset file to a flattened CSV file.
    /// Handles dimension expansion and value mapping using cartesian product ordering.
    /// </summary>
    /// <param name="jsonPath">Path to JSON-stat file containing a dataset.</param>
    /// <param name="csvOutPath">Path where the CSV output will be written.</param>
    /// <exception cref="InvalidDataException">Thrown if JSON-stat structure is invalid or dimensions/values missing.</exception>
    /// <remarks>
    /// The JSON-stat value array is ordered so that the last dimension changes fastest (row-major order).
    /// This method pre-computes multipliers to efficiently map linear indices to multi-dimensional coordinates.
    /// </remarks>
    public static async Task ConvertJsonStatToCsvAsync(string jsonPath, string csvOutPath)
    {
        using var fs = File.OpenRead(jsonPath);
        using var doc = await JsonDocument.ParseAsync(fs);
        var root = doc.RootElement;

        // Get dataset (handles both top-level and nested dataset property)
        var dataset = root.TryGetProperty("dataset", out var ds) ? ds : root;

        if (!dataset.TryGetProperty("dimension", out var dims))
            throw new InvalidDataException("JSON-stat dataset.dimension not found");

        // Determine the order of dimensions (critical for correct value mapping)
        string[] dimOrder = GetDimensionOrder(dataset, dims);

        // Extract categories and labels for each dimension
        var dimKeys = new List<string[]>();
        var dimLabels = new List<Dictionary<string, string?>>();

        foreach (var dimName in dimOrder)
        {
            var dimElem = dims.GetProperty(dimName);
            var cat = dimElem.GetProperty("category");

            // Get list of category keys (indices)
            string[] keys = GetCategoryKeys(cat);
            dimKeys.Add(keys);

            // Get label map (human-readable names for categories)
            var labels = new Dictionary<string, string?>();
            if (cat.TryGetProperty("label", out var labElem) && labElem.ValueKind == JsonValueKind.Object)
            {
                foreach (var p in labElem.EnumerateObject())
                    labels[p.Name] = p.Value.ValueKind == JsonValueKind.Null ? null : p.Value.GetString();
            }
            dimLabels.Add(labels);
        }

        // Read values array (the actual numeric/data values)
        if (!dataset.TryGetProperty("value", out var valuesElem) || valuesElem.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("JSON-stat dataset.value not found or not an array");

        var values = valuesElem.EnumerateArray().ToArray();

        // Pre-compute multipliers for efficient linear-to-multidimensional index conversion
        // This avoids repeated division operations when iterating through combinations
        int n = dimKeys.Count;
        var sizes = dimKeys.Select(k => k.Length).ToArray();
        var multipliers = new int[n];
        for (int i = 0; i < n; i++)
        {
            int m = 1;
            for (int j = i + 1; j < n; j++) m *= sizes[j];
            multipliers[i] = m;
        }

        // Write CSV header row
        using var writer = new StreamWriter(csvOutPath);
        var headerCols = string.Join(",", dimOrder.Select(d => EscapeCsv(d)).Concat(new[] { "value" }));
        await writer.WriteLineAsync(headerCols);

        // Iterate through all category combinations (cartesian product)
        // Total combinations = size[0] * size[1] * ... * size[n-1]
        var indices = new int[n];
        long total = 1;
        foreach (var s in sizes) total *= s;

        for (long r = 0; r < total; r++)
        {
            // Convert linear index r to multi-dimensional index coordinates
            long rem = r;
            for (int i = 0; i < n; i++)
            {
                int idx = (int)(rem / multipliers[i]);
                indices[i] = idx;
                rem = rem % multipliers[i];
            }

            // Compute linear index into the values array using the same multiplier scheme
            long linear = 0;
            for (int i = 0; i < n; i++) linear += indices[i] * multipliers[i];

            // Get value at this linear position (or null if out of bounds)
            string? valueStr = null;
            if (linear >= 0 && linear < values.Length)
            {
                var v = values[linear];
                if (v.ValueKind == JsonValueKind.Null) valueStr = string.Empty;
                else if (v.ValueKind == JsonValueKind.Number) valueStr = v.GetRawText();
                else valueStr = v.ToString();
            }

            // Build CSV row: use labels if available (for readability), otherwise use keys
            var cols = new List<string>(n + 1);
            for (int i = 0; i < n; i++)
            {
                var key = dimKeys[i][indices[i]];
                if (dimLabels[i].TryGetValue(key, out var lab) && lab != null)
                    cols.Add(EscapeCsv(lab));
                else
                    cols.Add(EscapeCsv(key));
            }
            cols.Add(EscapeCsv(valueStr ?? string.Empty));

            await writer.WriteLineAsync(string.Join(',', cols));
        }
    }

    /// <summary>
    /// Escapes a string for safe CSV output (quotes and commas require wrapping in quotes).
    /// </summary>
    private static string EscapeCsv(string s)
    {
        if (s.Contains('"') || s.Contains(',') || s.Contains('\n') || s.Contains('\r'))
            return '"' + s.Replace("\"", "\"\"") + '"';
        return s;
    }

    /// <summary>
    /// Extracts category keys from a JSON-stat category object.
    /// Handles both array and object-based index formats.
    /// </summary>
    private static string[] GetCategoryKeys(JsonElement cat)
    {
        // Try array-based index (most common)
        if (cat.TryGetProperty("index", out var idx))
        {
            if (idx.ValueKind == JsonValueKind.Array)
            {
                return idx.EnumerateArray().Select(x => x.GetString() ?? string.Empty).ToArray();
            }
            else if (idx.ValueKind == JsonValueKind.Object)
            {
                // Object-based index uses property names as keys
                return idx.EnumerateObject().Select(p => p.Name).ToArray();
            }
        }

        // Fallback: use label keys if index not present
        if (cat.TryGetProperty("label", out var lab) && lab.ValueKind == JsonValueKind.Object)
        {
            return lab.EnumerateObject().Select(p => p.Name).ToArray();
        }

        return Array.Empty<string>();
    }

    /// <summary>
    /// Determines the order of dimensions from the JSON-stat dataset.
    /// Dimension order is critical because the value array is indexed based on this order.
    /// </summary>
    private static string[] GetDimensionOrder(JsonElement dataset, JsonElement dims)
    {
        // Prefer dataset.id array (explicit ordering)
        if (dataset.TryGetProperty("id", out var idElem) && idElem.ValueKind == JsonValueKind.Array)
        {
            return idElem.EnumerateArray().Select(x => x.GetString() ?? string.Empty).ToArray();
        }

        // Fallback: use dimension.id array
        if (dims.TryGetProperty("id", out var dimsId) && dimsId.ValueKind == JsonValueKind.Array)
        {
            return dimsId.EnumerateArray().Select(x => x.GetString() ?? string.Empty).ToArray();
        }

        // Last resort: use property order, excluding reserved/metadata keys
        var reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "id", "size", "role" };
        return dims.EnumerateObject().Where(p => !reserved.Contains(p.Name)).Select(p => p.Name).ToArray();
    }
}
