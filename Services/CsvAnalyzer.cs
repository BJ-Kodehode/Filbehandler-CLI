using System;
using System.IO;
using System.IO.Compression;
using System.Globalization;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CsvHelper;

public class CsvColumnStats
{
    public string Name { get; set; } = string.Empty;
    public long Count { get; set; }
    public double Mean { get; set; }
    public double Min { get; set; } = double.PositiveInfinity;
    public double Max { get; set; } = double.NegativeInfinity;
    public bool IsNumeric { get; set; } = true;

    public void Add(double v)
    {
        Count++;
        double delta = v - Mean;
        Mean += delta / Count;
        if (v < Min) Min = v;
        if (v > Max) Max = v;
    }
}

public class CsvStats
{
    public long RowCount { get; set; }
    public List<CsvColumnStats> Columns { get; } = new();
}

public static class CsvAnalyzer
{
    public static async Task<CsvStats> AnalyzeGzippedCsvAsync(string gzPath, CancellationToken ct = default)
    {
        if (!File.Exists(gzPath)) throw new FileNotFoundException("CSV file not found", gzPath);

        using var fs = File.OpenRead(gzPath);
        using var gz = new GZipStream(fs, CompressionMode.Decompress);
        using var sr = new StreamReader(gz);
        using var csv = new CsvReader(sr, CultureInfo.InvariantCulture);

        var stats = new CsvStats();

        if (!await csv.ReadAsync()) return stats;
        csv.ReadHeader();
        var header = csv.HeaderRecord ?? Array.Empty<string>();
        foreach (var h in header)
        {
            stats.Columns.Add(new CsvColumnStats { Name = h });
        }

        while (await csv.ReadAsync())
        {
            ct.ThrowIfCancellationRequested();
            stats.RowCount++;
            for (int i = 0; i < header.Length; i++)
            {
                var field = csv.GetField(i);
                if (string.IsNullOrEmpty(field)) continue;

                var col = stats.Columns[i];
                if (col.IsNumeric)
                {
                    if (double.TryParse(field, NumberStyles.Any, CultureInfo.InvariantCulture, out var v))
                    {
                        col.Add(v);
                    }
                    else
                    {
                        // mark as non-numeric and discard numeric summary
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
