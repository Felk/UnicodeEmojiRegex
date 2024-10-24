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
            Assert.That(Dafsa.FromWordsMinimized(strs).ToRegex(), Is.EqualTo("[12][ab]"));
        }

        [Test]
        public void OptimalOptionals()
        {
            ImmutableList<string> strs = ImmutableList.Create<string>("ab", "bc", "b", "abc");
            Assert.That(Dafsa.FromWordsMinimized(strs).ToRegex(), Is.EqualTo("a?bc?"));
        }

        [Test]
        public void MinimalTopLevelOptions()
        {
            ImmutableList<string> strs = ImmutableList.Create<string>("ab", "bc", "b", "abc", "ac");
            // The regex 'a?bc?|ac' would be nicer of course, but the outcome of the current algorithm depends on the
            // state elimination order, which is just top-down right now.
            // That happens to result in this messy looking but equally correct regex.
            // Assert.That(DafsaUtils.CreateRegexFromWords(strs), Is.EqualTo("a?bc?|ac"));
            Assert.That(Dafsa.FromWordsMinimized(strs).ToRegex(), Is.EqualTo("(?:a?b|a)c|a?b"));
        }

        [Test]
        public void TieredSuffix()
        {
            ImmutableList<string> strs = ImmutableList.Create<string>("ab1", "ab2", "ac3", "ac4");
            Assert.That(Dafsa.FromWordsMinimized(strs).ToRegex(), Is.EqualTo("a(?:b[12]|c[34])"));
        }

        [Test]
        public void NestedOptional()
        {
            ImmutableList<string> strs = ImmutableList.Create<string>("ad", "abd", "abcd");
            Assert.That(Dafsa.FromWordsMinimized(strs).ToRegex(), Is.EqualTo("a(?:bc?)?d"));
        }

        [Test]
        public void EmojiWithOptionalModifiers()
        {
            ImmutableList<string> strs = ImmutableList.Create<string>(
                "👋", "👋🏻", "👋🏿",
                "🤚", "🤚🏻", "🤚🏿");
            const string expectedRegex = @"(?:\uD83D\uDC4B|\uD83E\uDD1A)(?:\uD83C[\uDFFB\uDFFF])?";
            Assert.That(Dafsa.FromWordsMinimized(strs).ToRegex(), Is.EqualTo(expectedRegex));
        }

        [Test]
        public void QualifiedAndUnqualifiedEmoji()
        {
            string regex = Dafsa.FromWordsMinimized(ImmutableList.Create<string>("👁️‍🗨️", "👁‍🗨️")).ToRegex();
            Assert.That(regex, Is.EqualTo(@"\uD83D\uDC41\uFE0F?\u200D\uD83D\uDDE8\uFE0F"));
        }

        [Test]
        public void CharacterSetRange()
        {
            ImmutableList<string> strs = ImmutableList.Create<string>("a", "b", "c");
            Assert.That(Dafsa.FromWordsMinimized(strs).ToRegex(), Is.EqualTo("[a-c]"));
        }

        [Test]
        public void SequenceInfix()
        {
            ImmutableList<string> strs = ImmutableList.Create<string>(
                "1aa", "1bb", "aa", "bb", "aa2", "bb2", "1aa2", "1bb2");
            Assert.That(Dafsa.FromWordsMinimized(strs).ToRegex(), Is.EqualTo("1?(?:aa|bb)2?"));
        }

        [Test, Ignore("not sure how to cleanly fix this yet")]
        public void CharacterSetInfix()
        {
            ImmutableList<string> strs = ImmutableList.Create<string>("1a", "1b", "a", "b", "a2", "b2", "1a2", "1b2");
            Assert.That(Dafsa.FromWordsMinimized(strs).ToRegex(), Is.EqualTo("1?[ab]2?"));
        }

        [Test]
        public void PossiblyLongestFirst()
        {
            ImmutableList<string> strs = ImmutableList.Create<string>("a123", "a1", "a6", "a45");
            Assert.That(Dafsa.FromWordsMinimized(strs).ToRegex(), Is.EqualTo("a(?:1(?:23)?|45|6)"));
        }
    }
}
