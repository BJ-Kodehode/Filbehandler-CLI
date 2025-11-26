/// <summary>
/// Represents a proposed file movement action.
/// Used in file organization to track source and target file paths.
/// 
/// Example:
///   Source: C:\Downloads\document.pdf
///   Target: C:\Downloads\pdf\document.pdf
/// </summary>
/// <param name="Source">Full path to the source file.</param>
/// <param name="Target">Full path where the file should be moved.</param>
public record FileAction(string Source, string Target);
