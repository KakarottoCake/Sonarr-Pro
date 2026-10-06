using FluentValidation;
using NzbDrone.Core.Annotations;
using NzbDrone.Core.Validation;

namespace NzbDrone.Core.ImportLists.Tmdb;

public abstract class TmdbSettingsBaseValidator<TSettings> : AbstractValidator<TSettings>
    where TSettings : TmdbSettingsBase<TSettings>
{
    protected TmdbSettingsBaseValidator()
    {
        RuleFor(c => c.BaseUrl).ValidRootUrl();

        RuleFor(c => c.AuthToken).NotEmpty()
            .OverridePropertyName("AuthToken")
            .WithMessage("Enter your TMDB API Read Access Token");
    }
}

public abstract class TmdbSettingsBase<TSettings> : ImportListSettingsBase<TSettings>
    where TSettings : TmdbSettingsBase<TSettings>
{
    private readonly TmdbSettingsBaseValidator<TSettings> _validator;

    protected TmdbSettingsBase(TmdbSettingsBaseValidator<TSettings> validator)
    {
        _validator = validator;
    }

    public override string BaseUrl { get; set; } = "https://api.themoviedb.org";

    [FieldDefinition(20, Label = "ImportListsTmdbSettingsAccountId", Type = FieldType.Textbox, Advanced = true)]
    public string AccountId { get; set; }

    [FieldDefinition(0, Label = "ImportListsSettingsAccessToken", Type = FieldType.Textbox, Privacy = PrivacyLevel.ApiKey, HelpText = "ImportListsTmdbAccessTokenHelp")]
    public string AuthToken { get; set; }

    public override NzbDroneValidationResult Validate()
    {
        return new NzbDroneValidationResult(_validator.Validate((TSettings)this));
    }
}
