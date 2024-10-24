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
            Assert.That(elem.ToRegex(), Is.EqualTo("a|b|c"));
            Assert.That(elem.Optimize().ToRegex(), Is.EqualTo("[a-c]"));
        }

        [Test]
        public void OrWithNothingToMaybe()
        {
            RegexElement elem = new RegexElement.Or(ImmutableHashSet.Create<RegexElement>(
                Char('a'), new RegexElement.Nothing()));
            string regex = elem.ToRegex();
            Assert.That(regex is "|a" or "a|", Is.True);
            Assert.That(elem.Optimize().ToRegex(), Is.EqualTo("a?"));
        }

        [Test]
        public void NestedMaybe()
        {
            RegexElement elem = new RegexElement.Maybe(new RegexElement.Maybe(Char('a')));
            Assert.That(elem.ToRegex(), Is.EqualTo("(?:a?)?"));
            Assert.That(elem.Optimize().ToRegex(), Is.EqualTo("a?"));
        }

        [Test]
        public void NestedSequences()
        {
            RegexElement elem = new RegexElement.Sequence(ImmutableList.Create<RegexElement>(
                new RegexElement.Sequence(ImmutableList.Create<RegexElement>(Char('a'), Char('b'))),
                new RegexElement.Nothing(),
                new RegexElement.Sequence(ImmutableList.Create<RegexElement>(Char('c'), Char('d'))),
                new RegexElement.Nothing()));
            Assert.That(elem.ToRegex(), Is.EqualTo("abcd"));
            RegexElement optimized = elem.Optimize();
            Assert.That(optimized.ToRegex(), Is.EqualTo("abcd"));
            Assert.That(optimized, Is.InstanceOf<RegexElement.Sequence>());
            Assert.That(((RegexElement.Sequence) optimized).Elements, Is.EqualTo(
                ImmutableList.Create<RegexElement>(Char('a'), Char('b'), Char('c'), Char('d'))));
        }
    }
}
