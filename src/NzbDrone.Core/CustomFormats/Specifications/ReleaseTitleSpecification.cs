using System;
using System.Linq;
using System.Text.RegularExpressions;
using NzbDrone.Core.Annotations;

namespace NzbDrone.Core.CustomFormats
{
    public class ReleaseTitleSpecification : RegexSpecificationBase
    {
        public override int Order => 1;
        public override string ImplementationName => "Release Title";
        public override string InfoLink => "https://wiki.servarr.com/sonarr/settings#custom-formats-2";

        [FieldDefinition(2, Label = "FormatExcludeEpisodeTitles", HelpText = "FormatExcludeEpisodeTitlesHelp", Type = FieldType.Checkbox)]
        public bool ExcludeEpisodeTitles { get; set; }

        protected override bool IsSatisfiedByWithoutNegate(CustomFormatInput input)
        {
            return MatchString(WithoutEpisodeTitles(input.EpisodeInfo?.ReleaseTitle, input)) || MatchString(WithoutEpisodeTitles(input.Filename, input));
        }

        private string WithoutEpisodeTitles(string value, CustomFormatInput input)
        {
            if (!ExcludeEpisodeTitles || string.IsNullOrEmpty(value))
            {
                return value;
            }

            var identifier = Regex.Match(value, @"(?i)\bS\d{1,3}[ ._-]*E\d{1,3}(?:[ ._-]*E?\d{1,3})*\b", RegexOptions.None, TimeSpan.FromSeconds(1));
            if (!identifier.Success)
            {
                return value;
            }

            var start = identifier.Index + identifier.Length;
            var remainder = value.Substring(start);
            foreach (var title in input.EpisodeTitles.Where(t => !string.IsNullOrWhiteSpace(t) && t.Length > 3).OrderByDescending(t => t.Length))
            {
                var words = Regex.Split(title.Trim(), @"[\W_]+").Where(w => w.Length > 0).Select(Regex.Escape);
                var pattern = @"(?<!\w)" + string.Join(@"[\W_]+", words) + @"(?!\w)";
                remainder = new Regex(pattern, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1)).Replace(remainder, string.Empty, 1);
            }

            return string.Concat(value.AsSpan(0, start), remainder);
        }
    }
}
