using NUnit.Framework;
using YARG.Core.Chart;

namespace YARG.Core.UnitTests.Parsing
{
    public class LyricTests
    {
        private const string STRIP_TEST_STRING = "\"a-b=c+d#e^f*g%h/i$j§k_l\"";

        [TestCase]
        public void StripForLyrics()
        {
            const string STRIPPED_LYRICS = "\"ab-cdefghij k l\"";
            Assert.That(LyricSymbols.StripForLyrics(STRIP_TEST_STRING), Is.EqualTo(STRIPPED_LYRICS));
        }

        [TestCase]
        public void StripForVocals()
        {
            const string STRIPPED_VOCALS = "a-b-cdefghij‿k l";
            Assert.That(LyricSymbols.StripForVocals(STRIP_TEST_STRING), Is.EqualTo(STRIPPED_VOCALS));
        }

        private const string TAG_TEST_STRING =
            "A <b>sample</b> <color=#00FF00>format</color>, with some > bad <font=Lato>formatting</font> and an <action>";

        [TestCase]
        public void StripTagsForLyrics()
        {
            const string STRIPPED_LYRICS =
                "A <b>sample</b> <color=#00FF00>format</color>, with some > bad formatting and an <action>";
            Assert.That(LyricSymbols.StripForLyrics(TAG_TEST_STRING), Is.EqualTo(STRIPPED_LYRICS));
        }

        [TestCase]
        public void StripTagsForVocals()
        {
            const string STRIPPED_VOCALS = "A sample format, with some > bad formatting and an <action>";
            Assert.That(LyricSymbols.StripForVocals(TAG_TEST_STRING), Is.EqualTo(STRIPPED_VOCALS));
        }
    }
}