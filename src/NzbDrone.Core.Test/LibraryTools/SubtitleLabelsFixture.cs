using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Parser;

namespace NzbDrone.Core.Test.LibraryTools
{
    [TestFixture]
    public class SubtitleLabelsFixture
    {
        [TestCase("Show.S01E01.en-US.srt", "en-US")]
        [TestCase("Show.S01E01.pt-BR.forced.srt", "pt-BR")]
        [TestCase("en-US.srt", "en-US")]
        [TestCase("nb.srt", "nb")]
        [TestCase("Show.S01E01.no.srt", "no")]
        [TestCase("Show.S01E01.eng.srt", "en")]
        [TestCase("Show-S01E01-fr-cc.srt", "fr")]
        public void preserves_variants_without_confusing_accessibility_tags(string filename, string expected)
        {
            LanguageParser.ParseSubtitleLanguageCode(filename).Should().Be(expected);
        }

        [Test]
        public void subtitle_episode_numbers_are_not_duplicated_as_a_label()
        {
            var result = LanguageParser.ParseSubtitleLanguageInformation("Show.S01E01.en.srt");
            result.Title.Should().BeNull();
            result.LanguageCode.Should().Be("en");
        }
    }
}
