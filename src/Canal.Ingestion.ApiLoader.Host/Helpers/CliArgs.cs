namespace Canal.Ingestion.ApiLoader.Host.Helpers;

/// <summary>
/// Minimal CLI argument parser. No external dependencies.
/// Splits args into positional values and --option key/value pairs.
/// </summary>
public sealed class CliArgs
{
    private readonly List<string> _positionals = [];
    private readonly Dictionary<string, string?> _options = new(StringComparer.OrdinalIgnoreCase);

    public CliArgs(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg.StartsWith("--"))
            {
                var key = arg[2..]; // strip --
                // Peek at next arg: if it exists and isn't another option, it's the value
                if (i + 1 < args.Length && !args[i + 1].StartsWith("--"))
                    _options[key] = args[++i];
                else
                    _options[key] = null; // flag with no value
            }
            else if (arg.StartsWith('-') && arg.Length == 2)
            {
                // Short alias like -e, -s, -v
                var key = arg[1..];
                if (i + 1 < args.Length && !args[i + 1].StartsWith('-'))
                    _options[key] = args[++i];
                else
                    _options[key] = null;
            }
            else
            {
                _positionals.Add(arg);
            }
        }
    }

    /// <summary>Get a positional arg by index (0-based). Returns null if out of range.</summary>
    public string? Positional(int index) => index < _positionals.Count ? _positionals[index] : null;

    /// <summary>Get an --option value by name (case-insensitive, without the -- prefix).</summary>
    public string? Option(string name) => _options.TryGetValue(name, out var v) ? v : null;

    /// <summary>Check if a flag (--flag with no value) is present.</summary>
    public bool Flag(string name) => _options.ContainsKey(name);

    /// <summary>Get an --option value parsed as int, or null if missing/unparsable.</summary>
    public int? IntOption(string name) { var s = Option(name); return s is not null && int.TryParse(s, out var i) ? i : null; }
}
