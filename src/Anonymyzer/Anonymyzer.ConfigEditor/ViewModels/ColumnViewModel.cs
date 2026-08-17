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
        IReadOnlyList<GeneratorProfileConfiguration> profiles)
    {
        _model = model;
        _profiles = profiles;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public int Ordinal => _model.Ordinal;
    public string ColumnName => _model.ColumnName;
    public string DataType => _model.DataType;
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
        set => _model.Enabled = value;
    }

    public string SemanticRole
    {
        get => _model.SemanticRole;
        set => _model.SemanticRole = value ?? string.Empty;
    }

    public string GenerationGroupId
    {
        get => _model.GenerationGroupId;
        set => _model.GenerationGroupId = value ?? string.Empty;
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
        }
    }

    private GeneratorProfileConfiguration? FindProfile(string profileId)
    {
        return _profiles.FirstOrDefault(profile => profile.Id.Equals(profileId, StringComparison.OrdinalIgnoreCase));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
