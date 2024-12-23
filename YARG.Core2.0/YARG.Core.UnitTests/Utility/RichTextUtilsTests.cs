using NUnit.Framework;
using YARG.Core.Utility;

namespace YARG.Core.UnitTests.Utility
{
    public class RichTextUtilsTests
    {
        internal static (string name, string hex)[] COLOR_NAMES =
        [
            ("aqua",      "#00ffff"),
            ("black",     "#000000"),
            ("blue",      "#0000ff"),
            ("brown",     "#a52a2a"),
            ("cyan",      "#00ffff"),
            ("darkblue",  "#0000a0"),
            ("fuchsia",   "#ff00ff"),
            ("green",     "#008000"),
            ("grey",      "#808080"),
            ("lightblue", "#add8e6"),
            ("lime",      "#00ff00"),
            ("magenta",   "#ff00ff"),
            ("maroon",    "#800000"),
            ("navy",      "#000080"),
            ("olive",     "#808000"),
            ("orange",    "#ffa500"),
            ("purple",    "#800080"),
            ("red",       "#ff0000"),
            ("silver",    "#c0c0c0"),
            ("teal",      "#008080"),
            ("white",     "#ffffff"),
            ("yellow",    "#ffff00"),
        ];

        private static readonly (string TagText, string Test, RichTextTags Tags)[] TEXT_TO_TAG;

        static RichTextUtilsTests()
        {
            var tags = RichTextUtils.RICH_TEXT_TAGS;
            TEXT_TO_TAG = new (string TagText, string Test, RichTextTags Tags)[tags.Length];
            for (var i = 0; i < tags.Length; i++)
            {
                TEXT_TO_TAG[i] = (
                    tags[i].Text,
                    $"Some <{tags[i].Text}=50vb>formatting</{tags[i].Text}> with trailing text\n",
                    tags[i].tag
                );
            }
        }

        [TestCase]
        public void ReplacesTags()
        {
            const string expectedText = "Some formatting with trailing text\n";
            Assert.Multiple(() =>
            {
                foreach (var (tagText, testText, tag) in TEXT_TO_TAG)
                {
                    string stripped = RichTextUtils.StripRichTextTags(testText, tag);
                    Assert.That(stripped, Is.EqualTo(expectedText), $"Tag '{tagText}' was not stripped!");
                }
            });
        }

        // Separate test since the implementation is different
        [TestCase]
        public void ReplacesAllTags()
        {
            string expectedText = "";
            string fullTest = "";
            foreach (var (tagText, testText, tag) in TEXT_TO_TAG)
            {
                expectedText += "Some formatting with trailing text\n";
                fullTest += testText;
            }

            string stripped = RichTextUtils.StripRichTextTags(fullTest);
            Assert.That(stripped, Is.EqualTo(expectedText), "Some tags were not stripped!");
        }

        [TestCase]
        public void ReplacesColors()
        {
            Assert.Multiple(() =>
            {
                foreach (var (name, hex) in COLOR_NAMES)
                {
                    string expectedText = $"Some <color={hex}>formatting</color> with trailing text";
                    string testText = $"Some <color={name}>formatting</color> with trailing text";

                    string stripped = RichTextUtils.ReplaceColorNames(testText);
                    Assert.That(stripped, Is.EqualTo(expectedText), $"Color name '{name}' was not replaced!");
                }
            });
        }
    }
}