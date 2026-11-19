using System.IO;
using System.Linq;

namespace TinyScholar.Switcher;

// All file access is centralized here. The web interface cannot execute arbitrary file operations.
public sealed class LoadoutEngine
{
    private static readonly string[] PackCodes =
    {
        "EP01_WorldAdventures",
        "EP02_Ambitions",
        "EP03_LateNight",
        "EP04_Generations",
        "EP05_Pets",
        "EP06_Showtime",
        "EP07_Supernatural",
        "EP08_Seasons",
        "EP09_UniversityLife",
        "EP10_IslandParadise",
        "EP11_IntoTheFuture",
        "SP01_HighEndLoft",
        "SP02_FastLane",
        "SP03_OutdoorLiving",
        "SP04_TownLife",
        "SP05_MasterSuite",
        "SP06_KatyPerry",
        "SP07_Diesel",
        "SP08_70s80s90s",
        "SP09_Movie",
        "DT01_Riverview",
        "DT02_BarnacleBay",
        "DT03_HiddenSprings",
        "DT04_LunarLakes",
        "DT05_LuckyPalms",
        "DT06_SunlitTides",
        "DT07_MonteVista",
        "DT08_AuroraSkies",
        "DT09_DragonValley",
        "DT10_MidnightHollow",
        "DT11_RoaringHeights"
    };
    public static IReadOnlyCollection<string> AllowedCodes { get; } =
        new HashSet<string>(PackCodes, StringComparer.Ordinal);

    private static readonly StringComparison Compare = StringComparison.OrdinalIgnoreCase;
    public record Inspection(bool ModsReady, string[] PathProblems, string[] ActiveCodes, string? Error = null);
    public record FileMatch(string Code, string Path);
    public record Duplicate(string Name, List<FileMatch> Occurrences);
    public record Analysis(string[] MissingCodes, string[] RemovedCodes, string[] ReplacingCodes,
                           List<Duplicate> Duplicates);
    public record ApplyResult(bool Success, string Message, string? BackupPath);

    public Inspection Inspect(string storage, string gameMods)
    {
        storage=storage.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        gameMods=gameMods.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var problems = new List<string>();
        var active = new List<string>();
        bool modsReady = false;
        try
        {
            if (string.IsNullOrWhiteSpace(storage) || !Directory.Exists(storage))
                problems.Add("STORAGE folder not found.");
            else if (!PackCodes.Any(code =>
                Directory.Exists(Path.Combine(storage, code, "Packages")) ||
                Directory.Exists(Path.Combine(storage, code, "Overrides"))))
                problems.Add("STORAGE has no recognized pack folders containing Packages or Overrides.");

            modsReady = !string.IsNullOrWhiteSpace(gameMods) && Directory.Exists(gameMods);
            if (!modsReady) problems.Add("Active Mods folder not found.");
            else if (!Directory.Exists(Path.Combine(gameMods, "Packages")) ||
                     !Directory.Exists(Path.Combine(gameMods, "Overrides")))
            {
                problems.Add("Active Mods folder is missing Packages or Overrides.");
                modsReady = false;
            }
            if (modsReady)
            {
                foreach (var code in PackCodes)
                    if (Directory.Exists(Path.Combine(gameMods, "Packages", code)) ||
                        Directory.Exists(Path.Combine(gameMods, "Overrides", code))) active.Add(code);
            }
        }
        catch (Exception ex) { return new Inspection(false,problems.ToArray(),[],ex.Message); }
        return new Inspection(modsReady, problems.ToArray(), active.ToArray());
    }

    public Analysis Prepare(string storage, string gameMods, IEnumerable<string> selected)
    {
        string[] codes = ValidateCodes(selected);
        (storage, gameMods) = ValidateRoots(storage, gameMods);
        var missing = new List<string>();
        var replacing = new List<string>();
        var removed = new List<string>();
        var selectedSet = new HashSet<string>(codes, StringComparer.Ordinal);
        foreach (var code in codes)
        {
            if (!HasSource(storage, code)) missing.Add(code);
            if (HasActive(gameMods, code)) replacing.Add(code);
        }
        foreach (var code in PackCodes)
            if (!selectedSet.Contains(code) && HasActive(gameMods, code)) removed.Add(code);

        var names = new Dictionary<string, List<FileMatch>>(StringComparer.OrdinalIgnoreCase);
        // Duplicate warnings include Base Game source files just as in v0.84.
        foreach (var code in new[] { "EP00_BaseGame" }.Concat(codes.Except(missing)))
            foreach (string category in new[] { "Packages", "Overrides" })
            {
                string folder = Path.Combine(storage, code, category);
                if (!Directory.Exists(folder)) continue;
                AssertDirectoryNotLink(Path.Combine(storage, code));
                EnsureNoReparsePoints(folder);
                foreach (string file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
                {
                    var filename = Path.GetFileName(file);
                    if (!names.TryGetValue(filename, out var occurrences))
                        names[filename] = occurrences = new List<FileMatch>();
                    occurrences.Add(new FileMatch(code, file));
                }
            }
        var duplicates = names.Where(pair => pair.Value.Count > 1)
            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(pair => new Duplicate(pair.Key, pair.Value)).ToList();
        return new Analysis(missing.ToArray(), removed.ToArray(), replacing.ToArray(), duplicates);
    }

    public ApplyResult Apply(string storage, string gameMods, IEnumerable<string> selected)
    {
        string? staging = null, backup = null;
        bool backupReady = false, changesStarted = false;
        try
        {
            var codes = ValidateCodes(selected);
            (storage, gameMods) = ValidateRoots(storage, gameMods);
            foreach (var code in codes)
                if (!HasSource(storage, code))
                    throw new IOException("The selected source pack is missing: " + code +
                        ". Re-check the loadout before applying.");

            // Never touch a target whose managed files contain junctions/symlinks.
            foreach (var code in PackCodes)
                foreach (var category in new[] { "Packages", "Overrides" })
                {
                    var managed = Path.Combine(gameMods, category, code);
                    if (Directory.Exists(managed)) EnsureNoReparsePoints(managed);
                }

            staging = Path.Combine(Path.GetTempPath(), "TinyScholar_S3_Stage_" + Guid.NewGuid().ToString("N"));
            backup = Path.Combine(Path.GetTempPath(), "TinyScholar_S3_Backup_" +
                DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_") + Guid.NewGuid().ToString("N").Substring(0,8));
            Directory.CreateDirectory(staging);
            Directory.CreateDirectory(backup);

            // Phase 1: stage all selected folders. Any error here leaves active Mods untouched.
            foreach (var code in codes)
                foreach (var category in new[] { "Packages", "Overrides" })
                {
                    string source = Path.Combine(storage, code, category);
                    if (Directory.Exists(source))
                    {
                        AssertDirectoryNotLink(Path.Combine(storage, code));
                        CopyTree(source, Path.Combine(staging, category, code));
                    }
                }

            // Phase 2: verify and back up every previously active managed pack folder.
            foreach (var code in PackCodes)
                foreach (var category in new[] { "Packages", "Overrides" })
                {
                    string source = Path.Combine(gameMods, category, code);
                    if (Directory.Exists(source))
                        CopyTree(source, Path.Combine(backup, category, code));
                }
            backupReady = true;

            // Phase 3: change ONLY exact, known pack codes under Packages and Overrides.
            changesStarted = true;
            foreach (var code in PackCodes)
                foreach (var category in new[] { "Packages", "Overrides" })
                    DeleteTree(Path.Combine(gameMods, category, code));
            foreach (var code in codes)
                foreach (var category in new[] { "Packages", "Overrides" })
                {
                    var source = Path.Combine(staging, category, code);
                    if (Directory.Exists(source))
                        CopyTree(source, Path.Combine(gameMods, category, code));
                }

            // Completion is successful even if temporary cleanup is blocked by antivirus.
            TryDeleteTree(staging);
            if (TryDeleteTree(backup))
                return new ApplyResult(true,
                    "The temporary backup has been removed after the successful switch.", null);
            return new ApplyResult(true,
                "Your loadout is complete, but Windows did not allow the temporary backup to be deleted. " +
                "You can remove it after confirming your Mods folders: " + backup, backup);
        }
        catch (Exception error)
        {
            string recovery;
            if (changesStarted && backupReady && backup != null)
            {
                try
                {
                    // Restore every previously active managed folder from the complete backup.
                    foreach (var code in PackCodes)
                        foreach (var category in new[] { "Packages", "Overrides" })
                            DeleteTree(Path.Combine(gameMods, category, code));
                    foreach (var code in PackCodes)
                        foreach (var category in new[] { "Packages", "Overrides" })
                        {
                            var previous = Path.Combine(backup, category, code);
                            if (Directory.Exists(previous))
                                CopyTree(previous, Path.Combine(gameMods, category, code));
                        }
                    recovery = "The switcher also attempted to restore the previous managed folders.";
                }
                catch (Exception)
                {
                    recovery = "Automatic restoration did not complete. Keep the backup folder and ask for help.";
                }
            }
            else recovery = "The active Mods folders were not intentionally cleared. Temporary staging files may remain here:\n" + staging;
            if (staging != null) TryDeleteTree(staging);
            if (!backupReady && backup != null) TryDeleteTree(backup);
            return new ApplyResult(false,
                "An error occurred while applying the loadout:\n\n" + error.Message +
                "\n\n" + recovery, backupReady ? backup : null);
        }
    }

    private static string[] ValidateCodes(IEnumerable<string> selected)
    {
        if (selected == null) throw new ArgumentException("No pack selection supplied.");
        string[] codes = selected.ToArray();
        if (codes.Length > PackCodes.Length || codes.Any(code => !AllowedCodes.Contains(code)) ||
            codes.Distinct(StringComparer.Ordinal).Count() != codes.Length)
            throw new ArgumentException("The pack selection contains an unknown or repeated code.");
        return codes;
    }

    private static (string Storage, string Mods) ValidateRoots(string storage, string mods)
    {
        if (string.IsNullOrWhiteSpace(storage) || string.IsNullOrWhiteSpace(mods))
            throw new IOException("Select both the STORAGE folder and active Mods folder.");
        storage = NormalizePath(storage);
        mods = NormalizePath(mods);
        if (!Directory.Exists(storage)) throw new DirectoryNotFoundException("STORAGE folder not found: " + storage);
        if (!Directory.Exists(mods)) throw new DirectoryNotFoundException("Active Mods folder not found: " + mods);
        if (!Path.GetFileName(mods).Equals("Mods", Compare))
            throw new IOException("The active Mods path must point to a folder named Mods, not a parent or system directory.");
        if (IsAtOrBelow(storage, mods) || IsAtOrBelow(mods, storage))
            throw new IOException("The STORAGE and active Mods folders must not contain one another.");
        if (!Directory.Exists(Path.Combine(mods, "Packages")) ||
            !Directory.Exists(Path.Combine(mods, "Overrides")))
            throw new IOException("Your active Mods folder must already have both Packages and Overrides directories.");
        AssertDirectoryNotLink(storage);
        AssertDirectoryNotLink(mods);
        AssertDirectoryNotLink(Path.Combine(mods, "Packages"));
        AssertDirectoryNotLink(Path.Combine(mods, "Overrides"));
        return (storage, mods);
    }

    private static bool IsFullyQualified(string path)
    {
        if(Path.DirectorySeparatorChar=='/')return path.StartsWith("/",StringComparison.Ordinal);
        return (path.Length>=3 && char.IsLetter(path[0]) && path[1]==':' && (path[2]=='\\'||path[2]=='/'))
            || (path.StartsWith(@"\\",StringComparison.Ordinal) && path.TrimStart('\\').Split('\\').Length>=2);
    }
    private static string NormalizePath(string path)
    {
        path = path.Trim();
        if (path.StartsWith(@"\\?\", StringComparison.Ordinal) ||
            path.StartsWith(@"\\.\", StringComparison.Ordinal))
            throw new IOException("Device paths are not supported.");
        if (!IsFullyQualified(path))
            throw new IOException("Choose a fully qualified folder path (for example, C:\\Modding\\Mods).");
        string full = Path.GetFullPath(path);
        if (string.Equals(Path.GetPathRoot(full)?.TrimEnd(Path.DirectorySeparatorChar),
            full.TrimEnd(Path.DirectorySeparatorChar), Compare))
            throw new IOException("Please select a normal folder, not the drive root.");
        return full.TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
    }

    private static bool IsAtOrBelow(string parent, string target) =>
        target.Equals(parent, Compare) ||
        target.StartsWith(parent.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, Compare);

    private static bool HasSource(string storage, string code) =>
        Directory.Exists(Path.Combine(storage, code, "Packages")) ||
        Directory.Exists(Path.Combine(storage, code, "Overrides"));

    private static bool HasActive(string mods, string code) =>
        Directory.Exists(Path.Combine(mods, "Packages", code)) ||
        Directory.Exists(Path.Combine(mods, "Overrides", code));

    private static void EnsureNoReparsePoints(string path)
    {
        if (!Directory.Exists(path)) return;
        // Never follow directory symlinks/junctions during copying or deletion.
        AssertDirectoryNotLink(path);
        var directory = new DirectoryInfo(path);
        foreach (var file in directory.EnumerateFiles("*", SearchOption.TopDirectoryOnly))
            if ((file.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("A file link cannot be copied safely: " + file.FullName);
        foreach (var sub in directory.EnumerateDirectories("*", SearchOption.TopDirectoryOnly))
            EnsureNoReparsePoints(sub.FullName);
    }

    private static void AssertDirectoryNotLink(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("A folder is a symbolic link or junction and cannot be modified safely: " + path);
    }

    private static void CopyTree(string from, string to)
    {
        AssertDirectoryNotLink(from);
        Directory.CreateDirectory(to);
        foreach (var file in Directory.EnumerateFiles(from, "*", SearchOption.TopDirectoryOnly))
        {
            if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("A linked file cannot be copied safely: " + file);
            File.Copy(file, Path.Combine(to, Path.GetFileName(file)), true);
        }
        foreach (var sub in Directory.EnumerateDirectories(from, "*", SearchOption.TopDirectoryOnly))
            CopyTree(sub, Path.Combine(to, Path.GetFileName(sub)));
    }

    private static void DeleteTree(string path)
    {
        if (!Directory.Exists(path)) return;
        EnsureNoReparsePoints(path);
        Directory.Delete(path, recursive: true);
    }

    private static bool TryDeleteTree(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return true;
        try { DeleteTree(path!); return true; }
        catch { return false; }
    }
}
