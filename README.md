# File Organizer CLI

A robust command-line tool for organizing files, analyzing CSV data, and converting JSON-stat datasets to CSV format. Built with C#, .NET 10, and Spectre.Console for a rich terminal experience.

## Features

- **File Organization**: Automatically sort files into directories by extension with dry-run support.
- **CSV Analysis**: Stream and analyze gzipped CSV files with automatic numeric column detection and statistics (mean, min, max).
- **JSON-stat Import**: Convert JSON-stat formatted datasets to flattened CSV for easier analysis.
- **Dependency Injection**: Clean architecture with DI support for logging and services.
- **User-Friendly CLI**: Color-coded output with helpful command examples and error messages.

## Installation

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Windows, macOS, or Linux

### Setup

Clone the repository and restore dependencies:

```bash
cd "c:\code\Filbehandler CLI"
dotnet restore
dotnet build
```

## Usage

### Show Help

```bash
dotnet run -- --help
```

### 1. Organize Files

Organize files in a directory by extension (or custom grouping).

**Syntax:**
```bash
dotnet run -- organize [OPTIONS]
```

**Options:**
- `--path, -p <PATH>` — Directory to organize (default: current directory)
- `--dry-run, -d` — Don't move files; only log what would happen

**Examples:**

```bash
# Dry-run: show what would be organized
dotnet run -- organize --path "C:\Downloads" --dry-run

# Actually organize files
dotnet run -- organize --path "C:\Downloads"

# Organize current directory
dotnet run -- organize -d
```

**How it works:**
- Scans the target directory for files.
- Groups files by extension or custom strategy.
- Moves files into subdirectories (creates them if needed).
- With `--dry-run`, logs actions without moving files.

---

### 2. Analyze CSV Files

Analyze a gzipped CSV file and generate statistics.

**Syntax:**
```bash
dotnet run -- analyze-csv <FILE>
```

**Arguments:**
- `<FILE>` — Path to a gzipped CSV file (`.csv.gz`)

**Examples:**

```bash
# Analyze gzipped CSV with statistics
dotnet run -- analyze-csv "C:\data\enheter_2025.csv.gz"

# Output includes per-column stats: count, mean, min, max for numeric columns
```

**Features:**
- Streams data from gzipped files without loading entire file into memory.
- Auto-detects numeric vs. text columns.
- Computes per-column statistics incrementally (using Welford's algorithm for mean).
- Displays results in a formatted table.

**Output Example:**
```
Analyzing C:\data\enheter_2025.csv.gz...
Rows: 50000
┌──────────────┬────────┬───────┬─────────┬─────────┬─────────┐
│ Column Name  │ Type   │ Count │ Mean    │ Min     │ Max     │
├──────────────┼────────┼───────┼─────────┼─────────┼─────────┤
│ id           │ Numeric│ 50000 │ 25000.5 │ 1.00    │ 50000.0 │
│ name         │ Text   │ 0     │ 0.00    │ ∞       │ -∞      │
└──────────────┴────────┴───────┴─────────┴─────────┴─────────┘
```

---

### 3. Import JSON-stat to CSV

Convert a JSON-stat formatted dataset to CSV.

**Syntax:**
```bash
dotnet run -- import-jsonstat <INPUT> <OUTPUT>
```

**Arguments:**
- `<INPUT>` — Path to JSON-stat file (e.g., `data.json`)
- `<OUTPUT>` — Path for the resulting CSV file

**Examples:**

```bash
# Convert JSON-stat to CSV
dotnet run -- import-jsonstat "C:\data\data.json" "C:\data\output.csv"
```

**How it works:**

1. Reads the JSON-stat dataset structure (dimensions, categories, values).
2. Expands dimensions into CSV columns.
3. Maps the value array to rows using cartesian product ordering.
4. Writes flattened CSV with proper escaping for special characters.

**JSON-stat Format Example:**
```json
{
  "dataset": {
    "dimension": {
      "region": { "category": { "index": ["NO", "SE", "DK"] } },
      "year": { "category": { "index": [2020, 2021, 2022] } }
    },
    "value": [100, 110, 120, 200, 210, 220, 300, 310, 320]
  }
}
```

**Output CSV:**
```csv
region,year,value
NO,2020,100
NO,2021,110
NO,2022,120
SE,2020,200
SE,2021,210
SE,2022,220
DK,2020,300
DK,2021,310
DK,2022,320
```

---

## Project Structure

```
Filbehandler CLI/
├── Program.cs                    # Main entry point with CLI command routing
├── Filbehandler CLI.csproj       # Project configuration and package references
├── Models/
│   ├── FileOptions.cs            # Configuration options for organizer
│   └── FileAction.cs             # Represents a file move action (source → target)
├── Services/
│   ├── FileOrganizer.cs          # Core logic for organizing files
│   ├── IDryRunService.cs         # Interface for dry-run logging
│   ├── DryRunService.cs          # Dry-run implementation using ILogger
│   ├── CsvAnalyzer.cs            # Streaming CSV analyzer for gzipped files
│   └── JsonStatImporter.cs       # JSON-stat to CSV converter
├── bin/                          # Compiled binaries
├── obj/                          # Intermediate build objects
└── README.md                     # This file
```

---

## Architecture

### Dependency Injection

The project uses **Microsoft.Extensions.DependencyInjection** for loose coupling:

```csharp
services.AddSingleton<IFileOrganizer, FileOrganizer>();
services.AddSingleton<IDryRunService, DryRunService>();
services.AddLogging(cfg => cfg.AddConsole());
```

This allows services to be easily swapped, tested, and extended.

### Streaming Pattern

The **CsvAnalyzer** uses streaming to handle large files efficiently:
- Opens gzipped files with `GZipStream`.
- Reads rows incrementally without buffering the entire file.
- Computes statistics on-the-fly using Welford's algorithm.

### JSON-stat Handling

The **JsonStatImporter** flattens the hierarchical JSON-stat structure:
1. Extracts dimension metadata and category mappings.
2. Computes multipliers for linear-to-multidimensional index conversion.
3. Iterates through all category combinations (cartesian product).
4. Maps each combination to the corresponding value in the array.

---

## Key Classes

### CsvAnalyzer

Analyzes gzipped CSV files and computes per-column statistics.

**Key Methods:**
- `AnalyzeGzippedCsvAsync(string gzPath, CancellationToken ct)` — Streams and analyzes a gzipped CSV.

**Key Properties:**
- `CsvColumnStats.IsNumeric` — Indicates if a column contains numeric data.
- `CsvStats.Columns` — List of column statistics.

### JsonStatImporter

Converts JSON-stat datasets to CSV.

**Key Methods:**
- `ConvertJsonStatToCsvAsync(string jsonPath, string csvOutPath)` — Converts JSON-stat to CSV.

**Implementation Details:**
- Handles both array and object-based category indices.
- Escapes special characters in CSV output (quotes, commas, newlines).
- Supports label mappings for human-readable column names.

### FileOrganizer

Organizes files into subdirectories based on configuration.

**Key Methods:**
- `PreviewAsync(FileOptions options, CancellationToken ct)` — Shows what would be organized.
- `ExecuteAsync(FileOptions options, CancellationToken ct)` — Actually organizes files.

---

## Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| Microsoft.Extensions.Hosting | 8.0.0 | Dependency injection and configuration |
| Microsoft.Extensions.Logging.Abstractions | 8.0.0 | Logging abstractions |
| CsvHelper | 30.0.1 | CSV parsing |
| Spectre.Console | 0.49.1 | Rich terminal output |

---

## Error Handling

The CLI provides clear error messages:

```bash
# File not found
dotnet run -- analyze-csv "nonexistent.csv.gz"
# Output: Error: File not found: nonexistent.csv.gz

# Missing arguments
dotnet run -- import-jsonstat "only_input.json"
# Output: Error: import-jsonstat requires input and output paths
```

---

## Examples

### Example 1: Organize Downloads Folder (Dry-Run)

```bash
cd "c:\code\Filbehandler CLI"
dotnet run -- organize --path "C:\Users\YourName\Downloads" --dry-run
```

This shows which files would be moved without actually moving them.

### Example 2: Analyze Norwegian Business Registry Data

```bash
# Data from https://data.brreg.no/enhetsregisteret/
dotnet run -- analyze-csv "enheter_2025-04-22T04-24-53.118828464.csv.gz"
```

Shows statistics on the business registry dataset.

### Example 3: Convert Statistics Norway Data

```bash
# Data from https://ssb.no/
dotnet run -- import-jsonstat "data.json" "statistics.csv"
```

Converts JSON-stat formatted statistics to CSV for analysis in Excel or other tools.

---

## Development

### Building

```bash
dotnet build
```

### Running Tests (Planned)

```bash
dotnet test
```

### Adding New Features

1. **New Service**: Create a class in `Services/` and register it in `Program.cs` DI.
2. **New Command**: Add a handler function in `Program.cs` and wire it to the CLI.
3. **New Model**: Add a class in `Models/` for data structures.

---

## Performance Considerations

- **Streaming CSV**: Large files (>1GB) are handled efficiently without loading into memory.
- **JSON-stat Conversion**: O(n) where n = total category combinations. Pre-computed multipliers avoid redundant calculations.
- **Logging**: Async logging in `DryRunService` prevents I/O blocking.

---

## Troubleshooting

| Issue | Solution |
|-------|----------|
| "File not found" | Verify the file path is correct and the file exists. |
| "Permission denied" | Check file permissions and run as administrator if needed. |
| Slow CSV analysis | Large files may take time; this is normal for streaming analysis. |
| Out of memory on JSON-stat | JSON-stat conversion pre-loads the entire file; use smaller datasets if needed. |

---

## License

This project is provided as-is for educational and personal use.

---

## Author

Created as part of a C# learning project focusing on file handling, data parsing, and CLI design.

---

## Future Enhancements

- [ ] Unit tests for all services.
- [ ] Support for additional file formats (JSON, XML, Parquet).
- [ ] Batch processing of multiple files.
- [ ] Configuration file support (appsettings.json).
- [ ] Progress bars for long-running operations.
- [ ] Export statistics to JSON or Excel.
- [ ] Custom file grouping strategies (by date, size, pattern).
