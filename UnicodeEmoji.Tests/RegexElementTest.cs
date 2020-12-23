using System.Collections.Immutable;
using NUnit.Framework;

namespace UnicodeEmoji.Tests
{
    public class RegexElementTest
    {
        private RegexElement.SingleCharacter Char(char c) => new RegexElement.SingleCharacter(c);

        [Test]
        public void OrToCharacterSet()
        {
            RegexElement.Or elem = new(ImmutableHashSet.Create<RegexElement>(
                Char('a'), Char('b'), Char('c')));
            Assert.AreEqual("a|b|c", elem.ToRegex());
            Assert.AreEqual("[a-c]", elem.Optimize().ToRegex());
        }

        [Test]
        public void OrWithNothingToMaybe()
        {
            RegexElement elem = new RegexElement.Or(ImmutableHashSet.Create<RegexElement>(
                Char('a'), new RegexElement.Nothing()));
            string regex = elem.ToRegex();
            Assert.IsTrue(regex == "|a" || regex == "a|");
            Assert.AreEqual("a?", elem.Optimize().ToRegex());
        }

        [Test]
        public void NestedMaybe()
        {
            RegexElement elem = new RegexElement.Maybe(new RegexElement.Maybe(Char('a')));
            Assert.AreEqual("(?:a?)?", elem.ToRegex());
            Assert.AreEqual("a?", elem.Optimize().ToRegex());
        }

        [Test]
        public void NestedSequences()
        {
            RegexElement elem = new RegexElement.Sequence(ImmutableList.Create<RegexElement>(
                new RegexElement.Sequence(ImmutableList.Create<RegexElement>(Char('a'), Char('b'))),
                new RegexElement.Nothing(),
                new RegexElement.Sequence(ImmutableList.Create<RegexElement>(Char('c'), Char('d'))),
                new RegexElement.Nothing()));
            Assert.AreEqual("abcd", elem.ToRegex());
            RegexElement optimized = elem.Optimize();
            Assert.AreEqual("abcd", optimized.ToRegex());
            Assert.IsInstanceOf<RegexElement.Sequence>(optimized);
            Assert.AreEqual(
                ImmutableList.Create<RegexElement>(Char('a'), Char('b'), Char('c'), Char('d')),
                ((RegexElement.Sequence) optimized).Elements);
        }
    }
}
