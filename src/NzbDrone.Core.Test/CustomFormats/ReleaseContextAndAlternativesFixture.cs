using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.Languages;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.CustomFormats
{
    [TestFixture]
    public class ReleaseContextAndAlternativesFixture : CoreTest<CustomFormatCalculationService>
    {
        [Test]
        public void imported_and_compressed_pack_keeps_its_original_size_preference()
        {
            var small = new CustomFormat("Up to 10 GB", new SizeSpecification { Min = 0, Max = 10 });
            var medium = new CustomFormat("10 to 15 GB", new SizeSpecification { Min = 10, Max = 15 });
            Mocker.GetMock<ICustomFormatService>().Setup(s => s.All()).Returns(new List<CustomFormat> { small, medium });
            var file = new EpisodeFile { Size = 1.Gigabytes(), SourceReleaseSize = 12.Gigabytes(), ReleaseType = ReleaseType.SeasonPack };

            Subject.ParseCustomFormat(file, new Series()).Should().BeEquivalentTo(new[] { medium });
            file.Size = 100.Megabytes();
            Subject.ParseCustomFormat(file, new Series()).Should().BeEquivalentTo(new[] { medium });
        }

        [Test]
        public void existing_files_without_source_context_still_use_their_size()
        {
            var format = new CustomFormat("Small", new SizeSpecification { Min = 0, Max = 10 });
            Mocker.GetMock<ICustomFormatService>().Setup(s => s.All()).Returns(new List<CustomFormat> { format });
            Subject.ParseCustomFormat(new EpisodeFile { Size = 1.Gigabytes() }, new Series()).Should().ContainSingle();
        }

        [TestCase("Show Dual Audio", false, true)]
        [TestCase("Show", true, true)]
        [TestCase("Show", false, false)]
        public void alternative_condition_groups_allow_title_or_both_languages(string title, bool dualAudio, bool expected)
        {
            var format = new CustomFormat("Dual audio",
                new ReleaseTitleSpecification { Value = "Dual Audio", AlternativeGroup = 1 },
                new LanguageSpecification { Value = Language.Japanese.Id, Required = true, AlternativeGroup = 2 },
                new LanguageSpecification { Value = Language.English.Id, Required = true, AlternativeGroup = 2 });
            Mocker.GetMock<ICustomFormatService>().Setup(s => s.All()).Returns(new List<CustomFormat> { format });
            var file = new EpisodeFile
            {
                SourceReleaseTitle = title,
                Languages = dualAudio ? new List<Language> { Language.Japanese, Language.English } : new List<Language> { Language.Japanese }
            };

            Subject.ParseCustomFormat(file, new Series()).Contains(format).Should().Be(expected);
        }

        [Test]
        public void shared_conditions_are_required_even_when_an_alternative_matches()
        {
            var format = new CustomFormat("Small dual audio",
                new SizeSpecification { Min = 0, Max = 1 },
                new ReleaseTitleSpecification { Value = "Dual Audio", AlternativeGroup = 1 });
            Mocker.GetMock<ICustomFormatService>().Setup(s => s.All()).Returns(new List<CustomFormat> { format });
            Subject.ParseCustomFormat(new EpisodeFile { SceneName = "Show Dual Audio", Size = 2.Gigabytes() }, new Series()).Should().BeEmpty();
        }

        [TestCase(false, true)]
        [TestCase(true, false)]
        public void excludes_known_episode_titles_only_when_enabled(bool exclude, bool expected)
        {
            var specification = new ReleaseTitleSpecification { Value = "Dual[ .]Audio", ExcludeEpisodeTitles = exclude };
            var input = new CustomFormatInput
            {
                EpisodeInfo = new ParsedEpisodeInfo { ReleaseTitle = "Example.Show.S01E01.Dual.Audio.1080p" },
                EpisodeTitles = new List<string> { "Dual Audio" }
            };
            specification.IsSatisfiedBy(input).Should().Be(expected);
        }
    }
}
