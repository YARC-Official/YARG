using NUnit.Framework;
using YARG.Core.UnitTests.Parsing;
using YARG.Core.Utility;

namespace YARG.Core.UnitTests.Utility
{
    public class SpanSplitterTests
    {
        [TestCase]
        public void SplitBehaviorTest()
        {
            string searchString = "abcd \n efgh \n ijkl";
            var splitter = searchString.SplitAsSpan(' ');
            Assert.That(splitter.GetNext().ToString(), Is.EqualTo("abcd"));
            Assert.That(splitter.GetNext().ToString(), Is.EqualTo("\n"));
            Assert.That(splitter.GetNext().ToString(), Is.EqualTo("efgh"));
            Assert.That(splitter.GetNext().ToString(), Is.EqualTo("\n"));
            Assert.That(splitter.GetNext().ToString(), Is.EqualTo("ijkl"));
            Assert.That(splitter.GetNext().ToString(), Is.Empty);
        }

        [TestCase]
        public void SplitTrimmedBehaviorTest()
        {
            string searchString = "abcd \n efgh \n ijkl";
            var splitter = searchString.SplitTrimmed(' ');
            Assert.That(splitter.GetNext().ToString(), Is.EqualTo("abcd"));
            Assert.That(splitter.GetNext().ToString(), Is.EqualTo("efgh"));
            Assert.That(splitter.GetNext().ToString(), Is.EqualTo("ijkl"));
            Assert.That(splitter.GetNext().ToString(), Is.Empty);
        }

        [TestCase]
        public void SplitTrimmedAsciiBehaviorTest()
        {
            string searchString = "abcd \n efgh \n ijkl";
            var splitter = searchString.SplitTrimmedAscii(' ');
            Assert.That(splitter.GetNext().ToString(), Is.EqualTo("abcd"));
            Assert.That(splitter.GetNext().ToString(), Is.EqualTo("efgh"));
            Assert.That(splitter.GetNext().ToString(), Is.EqualTo("ijkl"));
            Assert.That(splitter.GetNext().ToString(), Is.Empty);
        }

        [TestCase]
        public void SplitTrimmedLatin1BehaviorTest()
        {
            string searchString = "abcd \n efgh \n ijkl";
            var splitter = searchString.SplitTrimmedLatin1(' ');
            Assert.That(splitter.GetNext().ToString(), Is.EqualTo("abcd"));
            Assert.That(splitter.GetNext().ToString(), Is.EqualTo("efgh"));
            Assert.That(splitter.GetNext().ToString(), Is.EqualTo("ijkl"));
            Assert.That(splitter.GetNext().ToString(), Is.Empty);
        }

        [TestCase]
        public void SplitChartTest()
        {
            string searchString = ChartParseBehaviorTests.GenerateChartFile();
            Assert.DoesNotThrow(() =>
            {
                int lineCount = 0;
                var splitter = searchString.SplitTrimmed('\n');
                foreach (var line in splitter)
                    lineCount++;
            });
        }
    }
}