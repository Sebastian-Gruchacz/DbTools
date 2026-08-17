namespace Anonymyzer.ConfigEditor.Infrastructure;

using System.IO;
using System.Text;
using Anonymyzer.Configuration;
using Newtonsoft.Json;

internal sealed class ConfigurationFileService
{
    private static readonly JsonSerializerSettings SerializerSettings = new()
    {
        Formatting = Formatting.Indented
    };

    public AnonymizationConfiguration Load(string path)
    {
        string json = File.ReadAllText(path, Encoding.UTF8);
        AnonymizationConfiguration configuration = JsonConvert.DeserializeObject<AnonymizationConfiguration>(json, SerializerSettings)
            ?? throw new JsonSerializationException("Configuration file is empty.");
        ConfigurationValidator.EnsureValid(configuration);
        return configuration;
    }

    public void Save(string path, AnonymizationConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ConfigurationValidator.EnsureValid(configuration);

        string fullPath = Path.GetFullPath(path);
        string? directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string temporaryPath = fullPath + ".tmp";
        try
        {
            string json = JsonConvert.SerializeObject(configuration, SerializerSettings);
            File.WriteAllText(temporaryPath, json + Environment.NewLine, new UTF8Encoding(false));
            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
