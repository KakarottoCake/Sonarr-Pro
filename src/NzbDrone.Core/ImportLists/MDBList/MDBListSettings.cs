using System;
using System.Text.RegularExpressions;
using FluentValidation;
using NzbDrone.Core.Annotations;
using NzbDrone.Core.Validation;

namespace NzbDrone.Core.ImportLists.MDBList
{
    public class MDBListSettingsValidator : AbstractValidator<MDBListSettings>
    {
        public MDBListSettingsValidator()
        {
            RuleFor(s => s.ApiKey).NotEmpty();
            RuleFor(s => s.ListUrl).Must(url => MDBListSettings.GetListPath(url) != null).WithMessage("Paste an MDBList URL such as https://mdblist.com/lists/username/list-name");
        }
    }

    public class MDBListSettings : ImportListSettingsBase<MDBListSettings>
    {
        private static readonly MDBListSettingsValidator Validator = new();

        public override string BaseUrl { get; set; } = "https://api.mdblist.com";

        [FieldDefinition(0, Label = "MDBListListUrl", HelpText = "MDBListListUrlHelp")]
        public string ListUrl { get; set; }

        [FieldDefinition(1, Label = "ApiKey", HelpText = "MDBListApiKeyHelp", Privacy = PrivacyLevel.ApiKey)]
        public string ApiKey { get; set; }

        public override NzbDroneValidationResult Validate() => new(Validator.Validate(this));

        public static string GetListPath(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https" || (uri.Host != "mdblist.com" && uri.Host != "www.mdblist.com"))
            {
                return null;
            }

            var match = Regex.Match(uri.AbsolutePath, @"^/lists/([a-zA-Z0-9_-]+)/([a-zA-Z0-9_-]+)/?$", RegexOptions.None, TimeSpan.FromSeconds(1));
            return match.Success ? $"lists/{match.Groups[1].Value}/{match.Groups[2].Value}/items/show" : null;
        }
    }
}
