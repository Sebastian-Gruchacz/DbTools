namespace Anonymyzer.Base.LanguagePacks;

using System.Reflection;
using Newtonsoft.Json;

public sealed class LanguagePackInstallationService
{
    private const string SettingsFileName = "language-packs.json";
    private const string PendingRemovalsFileName = "language-pack-removals.json";
    private readonly string _installationDirectory;
    private readonly IReadOnlyList<ILanguagePack> _builtInPacks;
    private readonly HashSet<string> _disabledIds;
    private string? _settingsWarning;
    private List<LanguagePackInstallation> _installations;

    public LanguagePackInstallationService(
        IEnumerable<ILanguagePack> builtInPacks,
        string? installationDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(builtInPacks);
        _builtInPacks = builtInPacks.ToArray();
        _installationDirectory = installationDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Anonymyzer",
            "LanguagePacks");
        ApplyPendingRemovals();
        _disabledIds = LoadDisabledIds();
        _installations = Discover();
    }

    public IReadOnlyList<LanguagePackInstallation> Installations => _installations;

    public IReadOnlyList<string> LoadWarnings { get; private set; } = [];

    public IReadOnlyList<ILanguagePack> ActivePacks => _installations
        .Where(installation => installation.IsEnabled)
        .Select(installation => installation.Pack)
        .ToArray();

    public LanguagePackInstallation Install(string sourceAssemblyPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceAssemblyPath);
        string sourcePath = Path.GetFullPath(sourceAssemblyPath);
        if (!File.Exists(sourcePath) || !Path.GetExtension(sourcePath).Equals(".dll", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Select an existing .dll language-pack assembly.");
        }

        ILanguagePack pack = LoadSinglePack(sourcePath);
        _ = new LanguagePackCatalog(_installations.Select(item => item.Pack).Append(pack));
        Directory.CreateDirectory(_installationDirectory);
        string safeId = string.Concat(pack.Descriptor.Id.Select(character =>
            Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
        string safeVersion = string.Concat(pack.Descriptor.Version.Select(character =>
            Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
        string destinationPath = Path.Combine(_installationDirectory, $"{safeId}-{safeVersion}.dll");
        File.Copy(sourcePath, destinationPath, overwrite: false);

        var installation = new LanguagePackInstallation(pack, "Installed", destinationPath, IsEnabled: true);
        _installations = _installations.Append(installation)
            .OrderBy(item => item.Pack.Descriptor.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        _disabledIds.Remove(pack.Descriptor.Id);
        SaveDisabledIds();
        return installation;
    }

    public bool SetEnabled(string packId, bool enabled)
    {
        LanguagePackInstallation installation = _installations.Single(item =>
            item.Pack.Descriptor.Id.Equals(packId, StringComparison.OrdinalIgnoreCase));
        if (installation.IsEnabled == enabled)
        {
            return false;
        }

        if (enabled)
        {
            _disabledIds.Remove(packId);
        }
        else
        {
            _disabledIds.Add(packId);
        }

        _installations = _installations
            .Select(item => item.Pack.Descriptor.Id.Equals(packId, StringComparison.OrdinalIgnoreCase)
                ? item with { IsEnabled = enabled }
                : item)
            .ToList();
        SaveDisabledIds();
        return true;
    }

    public bool ScheduleUninstall(string packId)
    {
        LanguagePackInstallation installation = _installations.Single(item =>
            item.Pack.Descriptor.Id.Equals(packId, StringComparison.OrdinalIgnoreCase));
        if (!installation.Origin.Equals("Installed", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(installation.AssemblyPath))
        {
            return false;
        }

        string fileName = Path.GetFileName(installation.AssemblyPath);
        HashSet<string> pending = LoadPendingRemovalFileNames();
        pending.Add(fileName);
        SavePendingRemovalFileNames(pending);
        _installations = _installations.Where(item => !ReferenceEquals(item, installation)).ToList();
        _disabledIds.Remove(packId);
        SaveDisabledIds();
        return true;
    }

    private List<LanguagePackInstallation> Discover()
    {
        var warnings = new List<string>();
        if (_settingsWarning is not null)
        {
            warnings.Add(_settingsWarning);
        }
        var packs = _builtInPacks
            .Select(pack => new LanguagePackInstallation(
                pack,
                "Built-in",
                null,
                !_disabledIds.Contains(pack.Descriptor.Id)))
            .ToList();
        _ = new LanguagePackCatalog(_builtInPacks);

        if (Directory.Exists(_installationDirectory))
        {
            foreach (string assemblyPath in Directory.EnumerateFiles(_installationDirectory, "*.dll"))
            {
                try
                {
                    ILanguagePack pack = LoadSinglePack(assemblyPath);
                    _ = new LanguagePackCatalog(packs.Select(item => item.Pack).Append(pack));
                    packs.Add(new LanguagePackInstallation(
                        pack,
                        "Installed",
                        assemblyPath,
                        !_disabledIds.Contains(pack.Descriptor.Id)));
                }
                catch (Exception exception)
                {
                    warnings.Add($"{Path.GetFileName(assemblyPath)}: {exception.Message}");
                }
            }
        }

        LoadWarnings = warnings;
        return packs.OrderBy(item => item.Pack.Descriptor.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    private HashSet<string> LoadDisabledIds()
    {
        string path = Path.Combine(_installationDirectory, SettingsFileName);
        if (!File.Exists(path))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            string[] values = JsonConvert.DeserializeObject<string[]>(File.ReadAllText(path)) ?? [];
            return new HashSet<string>(values, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception exception)
        {
            AppendSettingsWarning($"Cannot read language-pack settings '{path}': {exception.Message}");
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private void ApplyPendingRemovals()
    {
        HashSet<string> pending = LoadPendingRemovalFileNames();
        if (pending.Count == 0)
        {
            return;
        }

        var remaining = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string fileName in pending)
        {
            try
            {
                string path = ResolveInstalledFileName(fileName);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception exception)
            {
                remaining.Add(fileName);
                AppendSettingsWarning($"Cannot remove language pack '{fileName}': {exception.Message}");
            }
        }

        SavePendingRemovalFileNames(remaining);
    }

    private HashSet<string> LoadPendingRemovalFileNames()
    {
        string path = Path.Combine(_installationDirectory, PendingRemovalsFileName);
        if (!File.Exists(path))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            string[] values = JsonConvert.DeserializeObject<string[]>(File.ReadAllText(path)) ?? [];
            return new HashSet<string>(values.Select(value => Path.GetFileName(value)), StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception exception)
        {
            AppendSettingsWarning($"Cannot read pending language-pack removals '{path}': {exception.Message}");
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private void SavePendingRemovalFileNames(IReadOnlyCollection<string> fileNames)
    {
        Directory.CreateDirectory(_installationDirectory);
        string path = Path.Combine(_installationDirectory, PendingRemovalsFileName);
        if (fileNames.Count == 0)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            return;
        }

        File.WriteAllText(path, JsonConvert.SerializeObject(fileNames.OrderBy(value => value), Formatting.Indented));
    }

    private string ResolveInstalledFileName(string fileName)
    {
        if (!Path.GetFileName(fileName).Equals(fileName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Pending removal contains an invalid file name.");
        }

        string root = Path.GetFullPath(_installationDirectory) + Path.DirectorySeparatorChar;
        string path = Path.GetFullPath(Path.Combine(_installationDirectory, fileName));
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Pending removal points outside the language-pack directory.");
        }

        return path;
    }

    private void AppendSettingsWarning(string warning)
    {
        _settingsWarning = string.IsNullOrWhiteSpace(_settingsWarning)
            ? warning
            : $"{_settingsWarning} | {warning}";
    }

    private void SaveDisabledIds()
    {
        Directory.CreateDirectory(_installationDirectory);
        string path = Path.Combine(_installationDirectory, SettingsFileName);
        File.WriteAllText(path, JsonConvert.SerializeObject(_disabledIds.OrderBy(value => value), Formatting.Indented));
    }

    private static ILanguagePack LoadSinglePack(string assemblyPath)
    {
        Assembly assembly = Assembly.LoadFrom(Path.GetFullPath(assemblyPath));
        Type[] packTypes = assembly.GetExportedTypes()
            .Where(type => typeof(ILanguagePack).IsAssignableFrom(type) && type is { IsAbstract: false, IsInterface: false })
            .ToArray();
        if (packTypes.Length != 1)
        {
            throw new InvalidOperationException(
                $"Assembly must export exactly one ILanguagePack implementation; found {packTypes.Length}.");
        }

        return Activator.CreateInstance(packTypes[0]) as ILanguagePack
               ?? throw new InvalidOperationException("Cannot create the language-pack entry point.");
    }
}
