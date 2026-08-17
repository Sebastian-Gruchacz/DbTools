namespace Anonymyzer.ConfigEditor.ViewModels;

using System.ComponentModel;
using System.Runtime.CompilerServices;
using Anonymyzer.Configuration;

internal sealed class ColumnViewModel : INotifyPropertyChanged
{
    private readonly ColumnProcessingOptions _model;
    private readonly IReadOnlyList<GeneratorProfileConfiguration> _profiles;
    private string _sample = "—";

    public ColumnViewModel(
        ColumnProcessingOptions model,
        IReadOnlyList<GeneratorProfileConfiguration> profiles,
        IReadOnlyList<SemanticRoleGroup> semanticRoleGroups)
    {
        _model = model;
        _profiles = profiles;
        SemanticRoleGroups = IncludeStoredCustomRole(semanticRoleGroups, model.SemanticRole);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? ConfigurationChanged;

    public int Ordinal => _model.Ordinal;
    public string ColumnName => _model.ColumnName;
    public string DataType => _model.DataType;
    public string TypeDisplay => _model.DataType.Equals("Text", StringComparison.OrdinalIgnoreCase)
        ? $"{_model.DataType} ({FormatLength(_model.MaxLength)})"
        : _model.DataType;
    public ColumnProcessingOptions Model => _model;
    public IReadOnlyList<SemanticRoleGroup> SemanticRoleGroups { get; }
    public string CandidateMark => _model.Detection.IsCandidate ? "●" : string.Empty;
    public string CandidateDetails => _model.Detection.IsCandidate
        ? $"{_model.Detection.SuggestedRole} ({_model.Detection.Confidence:P0}, {_model.Detection.MatchedRule})"
        : "No automatic candidate match.";
    public string Sample
    {
        get => _sample;
        private set
        {
            if (_sample == value)
            {
                return;
            }

            _sample = value;
            OnPropertyChanged();
        }
    }

    public void SetSample(string sample) => Sample = sample;

    public bool Enabled
    {
        get => _model.Enabled;
        set
        {
            if (_model.Enabled == value)
            {
                return;
            }

            _model.Enabled = value;
            OnPropertyChanged();
            OnConfigurationChanged();
        }
    }

    public string SemanticRoleDisplay => FindSemanticRole(_model.SemanticRole) is { } role
        ? string.IsNullOrEmpty(role.Option.Value)
            ? "— No semantic role"
            : $"{role.Group.DisplayName} › {role.Option.DisplayName}"
        : _model.SemanticRole;

    public string SemanticRoleValue => _model.SemanticRole;

    public void SelectSemanticRole(SemanticRoleOption option)
    {
        if (_model.SemanticRole == option.Value)
        {
            return;
        }

        _model.SemanticRole = option.Value;
        OnPropertyChanged(nameof(SemanticRoleDisplay));
        OnPropertyChanged(nameof(SemanticRoleValue));
        OnConfigurationChanged();
    }

    public string GenerationGroupId
    {
        get => _model.GenerationGroupId;
        set
        {
            string groupId = value ?? string.Empty;
            if (_model.GenerationGroupId == groupId)
            {
                return;
            }

            _model.GenerationGroupId = groupId;
            OnPropertyChanged();
            OnConfigurationChanged();
        }
    }

    public string GeneratorType
    {
        get => _model.Generator.GeneratorType;
        set
        {
            string generatorType = value ?? string.Empty;
            if (_model.Generator.GeneratorType == generatorType)
            {
                return;
            }

            _model.Generator.GeneratorType = generatorType;
            GeneratorProfileConfiguration? profile = FindProfile(_model.Generator.ProfileId);
            if (profile is not null && !profile.GeneratorType.Equals(generatorType, StringComparison.OrdinalIgnoreCase))
            {
                _model.Generator.ProfileId = string.Empty;
                _model.Generator.GeneratorVersion = string.Empty;
                OnPropertyChanged(nameof(ProfileId));
            }

            OnPropertyChanged();
            OnConfigurationChanged();
        }
    }

    public string ProfileId
    {
        get => _model.Generator.ProfileId;
        set
        {
            string profileId = value ?? string.Empty;
            if (_model.Generator.ProfileId == profileId)
            {
                return;
            }

            _model.Generator.ProfileId = profileId;
            GeneratorProfileConfiguration? profile = FindProfile(profileId);
            if (profile is not null)
            {
                _model.Generator.GeneratorType = profile.GeneratorType;
                _model.Generator.GeneratorVersion = profile.GeneratorVersion;
                OnPropertyChanged(nameof(GeneratorType));
            }

            OnPropertyChanged();
            OnConfigurationChanged();
        }
    }

    private GeneratorProfileConfiguration? FindProfile(string profileId)
    {
        return _profiles.FirstOrDefault(profile => profile.Id.Equals(profileId, StringComparison.OrdinalIgnoreCase));
    }

    private (SemanticRoleGroup Group, SemanticRoleOption Option)? FindSemanticRole(string value)
    {
        foreach (SemanticRoleGroup group in SemanticRoleGroups)
        {
            SemanticRoleOption? option = group.Options.FirstOrDefault(candidate => candidate.Value == value);
            if (option is not null)
            {
                return (group, option);
            }
        }

        return null;
    }

    private static IReadOnlyList<SemanticRoleGroup> IncludeStoredCustomRole(
        IReadOnlyList<SemanticRoleGroup> groups,
        string storedRole)
    {
        if (string.IsNullOrWhiteSpace(storedRole)
            || groups.SelectMany(group => group.Options).Any(option => option.Value == storedRole))
        {
            return groups;
        }

        return groups
            .Append(new SemanticRoleGroup(
                "Custom / legacy",
                [new SemanticRoleOption(storedRole, storedRole)]))
            .ToArray();
    }

    private static string FormatLength(int maxLength) =>
        maxLength <= 0 || maxLength >= 1_073_741_823 ? "MAX" : maxLength.ToString();

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void OnConfigurationChanged()
    {
        ConfigurationChanged?.Invoke(this, EventArgs.Empty);
    }
}
