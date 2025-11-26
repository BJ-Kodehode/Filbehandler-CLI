using System;
using System.IO;
using System.IO.Compression;
using System.Globalization;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CsvHelper;

/// <summary>
/// Represents accumulated statistics for a single CSV column.
/// Uses Welford's algorithm for online mean computation to avoid overflow/underflow.
/// </summary>
public class CsvColumnStats
{
    /// <summary>Column name from CSV header.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Number of numeric values processed (resets to 0 if non-numeric values found).</summary>
    public long Count { get; set; }

    /// <summary>Running mean computed incrementally via Welford's algorithm.</summary>
    public double Mean { get; set; }

    /// <summary>Minimum numeric value encountered; PositiveInfinity if no values yet.</summary>
    public double Min { get; set; } = double.PositiveInfinity;

    /// <summary>Maximum numeric value encountered; NegativeInfinity if no values yet.</summary>
    public double Max { get; set; } = double.NegativeInfinity;

    /// <summary>True if all values processed so far have been numeric; false if any non-numeric value found.</summary>
    public bool IsNumeric { get; set; } = true;

    /// <summary>
    /// Adds a numeric value to the running statistics using Welford's online algorithm.
    /// This approach is numerically stable and avoids storing all values in memory.
    /// </summary>
    /// <param name="v">The numeric value to add.</param>
    public void Add(double v)
    {
        Count++;
        double delta = v - Mean;
        Mean += delta / Count;
        if (v < Min) Min = v;
        if (v > Max) Max = v;
    }
}

/// <summary>
/// Container for aggregated CSV analysis results from all columns.
/// </summary>
public class CsvStats
{
    /// <summary>Total number of data rows processed (excluding header).</summary>
    public long RowCount { get; set; }

    /// <summary>List of statistics for each column.</summary>
    public List<CsvColumnStats> Columns { get; } = new();
}

/// <summary>
/// Static utility for streaming analysis of gzipped CSV files.
/// Handles decompression, header parsing, and per-column numeric statistics without loading entire file into memory.
/// </summary>
public static class CsvAnalyzer
{
    /// <summary>
    /// Analyzes a gzipped CSV file and computes per-column statistics.
    /// Streams data incrementally for memory efficiency on large files.
    /// </summary>
    /// <param name="gzPath">Path to .gz CSV file.</param>
    /// <param name="ct">Cancellation token for long-running operation.</param>
    /// <returns>CsvStats containing per-column analysis results.</returns>
    /// <exception cref="FileNotFoundException">Thrown if file does not exist.</exception>
    public static async Task<CsvStats> AnalyzeGzippedCsvAsync(string gzPath, CancellationToken ct = default)
    {
        if (!File.Exists(gzPath)) throw new FileNotFoundException("CSV file not found", gzPath);

        using var fs = File.OpenRead(gzPath);
        using var gz = new GZipStream(fs, CompressionMode.Decompress);
        using var sr = new StreamReader(gz);
        using var csv = new CsvReader(sr, CultureInfo.InvariantCulture);

        var stats = new CsvStats();

        // Read first row to get headers
        if (!await csv.ReadAsync()) return stats;
        csv.ReadHeader();
        var header = csv.HeaderRecord ?? Array.Empty<string>();

        // Initialize column stats for each header
        foreach (var h in header)
        {
            stats.Columns.Add(new CsvColumnStats { Name = h });
        }

        // Stream remaining rows, updating statistics incrementally
        while (await csv.ReadAsync())
        {
            ct.ThrowIfCancellationRequested();
            stats.RowCount++;

            // Process each field in the row
            for (int i = 0; i < header.Length; i++)
            {
                var field = csv.GetField(i);
                if (string.IsNullOrEmpty(field)) continue;

                var col = stats.Columns[i];
                if (col.IsNumeric)
                {
                    // Try to parse as numeric value
                    if (double.TryParse(field, NumberStyles.Any, CultureInfo.InvariantCulture, out var v))
                    {
                        col.Add(v);
                    }
                    else
                    {
                        // Mark column as non-numeric and clear stats
                        col.IsNumeric = false;
                        col.Count = 0;
                        col.Mean = 0;
                        col.Min = double.PositiveInfinity;
                        col.Max = double.NegativeInfinity;
                    }
                }
            }
        }

        return stats;
    }
}
