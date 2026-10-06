using FluentValidation;
using NzbDrone.Core.Annotations;

namespace NzbDrone.Core.ImportLists.Tmdb.Account;

public class TmdbAccountSettingsValidator : TmdbSettingsBaseValidator<TmdbAccountSettings>
{
    public TmdbAccountSettingsValidator()
    {
        RuleFor(c => c.AccountId).NotEmpty().WithMessage("Enter your TMDB account ID for account lists.");
    }
}

public class TmdbAccountSettings : TmdbSettingsBase<TmdbAccountSettings>
{
    private static readonly TmdbAccountSettingsValidator Validator = new();

    public TmdbAccountSettings()
        : base(Validator)
    {
        AccountListType = (int)TmdbAccountListType.Watchlist;
    }

    [FieldDefinition(1, Label = "ImportListsTmdbSettingsAccountListType", HelpText = "ImportListsTmdbSettingsAccountListTypeHelpText", Type = FieldType.Select, SelectOptions = typeof(TmdbAccountListType))]
    public int AccountListType { get; set; }
}
