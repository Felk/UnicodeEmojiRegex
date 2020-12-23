using System.Collections.Immutable;
using NUnit.Framework;

namespace UnicodeEmoji.Tests
{
    public class DafsaTest
    {
        [Test]
        public void PrefixAndSuffix()
        {
            ImmutableList<string> strs = ImmutableList.Create<string>("1a", "1b", "2a", "2b");
            Assert.AreEqual("[12][ab]", Dafsa.FromWordsMinimized(strs).ToRegex());
        }

        [Test]
        public void OptimalOptionals()
        {
            ImmutableList<string> strs = ImmutableList.Create<string>("ab", "bc", "b", "abc");
            Assert.AreEqual("a?bc?", Dafsa.FromWordsMinimized(strs).ToRegex());
        }

        [Test]
        public void MinimalTopLevelOptions()
        {
            ImmutableList<string> strs = ImmutableList.Create<string>("ab", "bc", "b", "abc", "ac");
            // The regex 'a?bc?|ac' would be nicer of course, but the outcome of the current algorithm depends on the
            // state elimination order, which is just top-down right now.
            // That happens to result in this messy looking but equally correct regex.
            // Assert.AreEqual("a?bc?|ac", DafsaUtils.CreateRegexFromWords(strs));
            Assert.AreEqual("(?:a?b|a)c|a?b", Dafsa.FromWordsMinimized(strs).ToRegex());
        }

        [Test]
        public void TieredSuffix()
        {
            ImmutableList<string> strs = ImmutableList.Create<string>("ab1", "ab2", "ac3", "ac4");
            Assert.AreEqual("a(?:b[12]|c[34])", Dafsa.FromWordsMinimized(strs).ToRegex());
        }

        [Test]
        public void NestedOptional()
        {
            ImmutableList<string> strs = ImmutableList.Create<string>("ad", "abd", "abcd");
            Assert.AreEqual("a(?:bc?)?d", Dafsa.FromWordsMinimized(strs).ToRegex());
        }

        [Test]
        public void EmojiWithOptionalModifiers()
        {
            ImmutableList<string> strs = ImmutableList.Create<string>(
                "👋", "👋🏻", "👋🏿",
                "🤚", "🤚🏻", "🤚🏿");
            const string expectedRegex = @"(?:\uD83D\uDC4B|\uD83E\uDD1A)(?:\uD83C[\uDFFB\uDFFF])?";
            Assert.AreEqual(expectedRegex, Dafsa.FromWordsMinimized(strs).ToRegex());
        }

        [Test]
        public void QualifiedAndUnqualifiedEmoji()
        {
            string regex = Dafsa.FromWordsMinimized(ImmutableList.Create<string>("👁️‍🗨️", "👁‍🗨️")).ToRegex();
            Assert.AreEqual(@"\uD83D\uDC41\uFE0F?\u200D\uD83D\uDDE8\uFE0F", regex);
        }

        [Test]
        public void CharacterSetRange()
        {
            ImmutableList<string> strs = ImmutableList.Create<string>("a", "b", "c");
            Assert.AreEqual("[a-c]", Dafsa.FromWordsMinimized(strs).ToRegex());
        }

        [Test]
        public void SequenceInfix()
        {
            ImmutableList<string> strs = ImmutableList.Create<string>(
                "1aa", "1bb", "aa", "bb", "aa2", "bb2", "1aa2", "1bb2");
            Assert.AreEqual("1?(?:aa|bb)2?", Dafsa.FromWordsMinimized(strs).ToRegex());
        }

        [Test, Ignore("not sure how to cleanly fix this yet")]
        public void CharacterSetInfix()
        {
            ImmutableList<string> strs = ImmutableList.Create<string>("1a", "1b", "a", "b", "a2", "b2", "1a2", "1b2");
            Assert.AreEqual("1?[ab]2?", Dafsa.FromWordsMinimized(strs).ToRegex());
        }

        [Test]
        public void PossiblyLongestFirst()
        {
            ImmutableList<string> strs = ImmutableList.Create<string>("a123", "a1", "a6", "a45");
            Assert.AreEqual("a(?:1(?:23)?|45|6)", Dafsa.FromWordsMinimized(strs).ToRegex());
        }
    }
}
