namespace FrankyCLI;

/// <summary>
/// Ensures all template ESMs found in the Starfield data folder are active (*) in Plugins.txt
/// before the Mutagen GameEnvironment is built, so they appear in the load order.
///
/// On disposal, deactivates only the entries we activated.
/// Entries that were already starred before we ran are left starred.
///
/// Usage:
///   using var _ = PluginsActivator.ActivateTemplates();
///   using (var env = GameEnvironment.Typical.Builder(...).Build()) { ... }
/// </summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class PluginsActivator : IDisposable
{
    private readonly string _pluginsPath;

    // Filenames (case-insensitive) that we starred — were unstarred before, need unstaring on cleanup.
    private readonly HashSet<string> _newlyStarred;

    // Filenames that weren't in Plugins.txt at all — we added them, need removing on cleanup.
    private readonly HashSet<string> _newlyAdded;

    private PluginsActivator(string pluginsPath, string dataPath)
    {
        _pluginsPath = pluginsPath;
        _newlyStarred = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _newlyAdded   = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!File.Exists(pluginsPath))
        {
            Console.WriteLine($"[PluginsActivator] Warning: Plugins.txt not found at {pluginsPath}");
            return;
        }

        if (!Directory.Exists(dataPath))
        {
            Console.WriteLine($"[PluginsActivator] Warning: Data folder not found at {dataPath}");
            return;
        }

        // Find all template ESMs in the data folder.
        var templateFiles = Directory.GetFiles(dataPath, "*.esm")
            .Select(Path.GetFileName)
            .Where(f => f!.Contains("template", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (templateFiles.Count == 0)
            return;

        var lines = File.ReadAllLines(pluginsPath).ToList();

        // Build the set of filenames that already have at least one starred entry.
        var alreadyStarred = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in lines)
        {
            if (line.StartsWith('*'))
                alreadyStarred.Add(line[1..].Trim());
        }

        bool modified = false;

        foreach (var tmpl in templateFiles)
        {
            if (alreadyStarred.Contains(tmpl!))
                continue; // already active — leave it alone

            Console.WriteLine($"[PluginsActivator] Activating: {tmpl}");

            // Find the first unstarred entry for this file and star it.
            bool found = false;
            for (int i = 0; i < lines.Count; i++)
            {
                var raw = lines[i];
                if (raw.StartsWith('#')) continue;
                var name = raw.TrimStart('*').Trim();
                if (!string.Equals(name, tmpl, StringComparison.OrdinalIgnoreCase)) continue;
                if (!raw.StartsWith('*'))
                {
                    lines[i] = '*' + name;
                    _newlyStarred.Add(name);
                    modified = true;
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                // Not in Plugins.txt at all — insert before any trailing blank lines.
                int insertAt = lines.Count;
                while (insertAt > 0 && string.IsNullOrWhiteSpace(lines[insertAt - 1]))
                    insertAt--;
                lines.Insert(insertAt, '*' + tmpl);
                _newlyAdded.Add(tmpl!);
                modified = true;
            }
        }

        if (modified)
            File.WriteAllLines(pluginsPath, lines);
    }

    public static PluginsActivator ActivateTemplates()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var pluginsPath  = Path.Combine(localAppData, "Starfield", "Plugins.txt");
        var dataPath     = FindStarfieldDataPath();
        return new PluginsActivator(pluginsPath, dataPath ?? "");
    }

    /// <summary>
    /// Finds the Starfield Data folder via the Steam registry entry.
    /// Falls back to the default Steam install path if the registry key is absent.
    /// </summary>
    private static string? FindStarfieldDataPath()
    {
        // Steam sets this key when Starfield (App ID 1716740) is installed.
        using var appKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 1716740");
        if (appKey?.GetValue("InstallLocation") is string installPath)
            return Path.Combine(installPath, "Data");

        // Fallback: find Steam's root and assume the default library location.
        using var steamKey =
            Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam")
            ?? Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Valve\Steam");
        if (steamKey?.GetValue("InstallPath") is string steamPath)
            return Path.Combine(steamPath, "steamapps", "common", "Starfield", "Data");

        return null;
    }

    public void Dispose()
    {
        if (_newlyStarred.Count == 0 && _newlyAdded.Count == 0)
            return;

        if (!File.Exists(_pluginsPath))
            return;

        var lines = File.ReadAllLines(_pluginsPath).ToList();
        bool modified = false;

        for (int i = lines.Count - 1; i >= 0; i--)
        {
            var line = lines[i];
            if (line.StartsWith('#')) continue;

            var name = line.TrimStart('*').Trim();

            if (_newlyAdded.Contains(name) && line.StartsWith('*'))
            {
                // We added this entry entirely — remove it.
                lines.RemoveAt(i);
                Console.WriteLine($"[PluginsActivator] Removed: {name}");
                modified = true;
            }
            else if (_newlyStarred.Contains(name) && line.StartsWith('*'))
            {
                // We starred this entry — unstar it.
                lines[i] = name;
                Console.WriteLine($"[PluginsActivator] Deactivated: {name}");
                modified = true;
            }
        }

        if (modified)
            File.WriteAllLines(_pluginsPath, lines);
    }
}
