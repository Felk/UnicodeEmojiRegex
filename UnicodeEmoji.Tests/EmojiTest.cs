using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace UnicodeEmoji.Tests
{
    public class EmojiTest
    {
        /// <summary>
        /// Some emojis containing two people and skin tone modifiers have a dedicated emoji for when both people
        /// have the same skin tone. This means that the explicit skin tone forms skip those combinations, causing there
        /// to never be a fixed set of common skin tone prefixes and suffixes, since for each LHS skin tone it needs to
        /// omit a separate RHS skin tone later on.
        /// To avoid the resulting regex to get unnecessarily complicated for this reason, these pseudo-emojis may get
        /// added to the list of possible emoji. The optimizations will then be able to collapse all possibilities into
        /// one branch with all available skin tone modifiers as common prefixes and suffixes. I was able to shrink
        /// the resulting regex to ~66% length using this hack.
        /// </summary>
        private static readonly IImmutableList<EmojiTestEntry> EmojisToFixModifierSymmetry = ImmutableList.Create(
            // @formatter:off
            new EmojiTestEntry(ImmutableList.Create<int>(0x1F9D1, 0x1F3FB, 0x200D, 0x2764, 0xFE0F, 0x200D, 0x1F9D1, 0x1F3FB), "fully-qualified", "couple with heart: person, person, light skin tone, light skin tone"),
            new EmojiTestEntry(ImmutableList.Create<int>(0x1F9D1, 0x1F3FC, 0x200D, 0x2764, 0xFE0F, 0x200D, 0x1F9D1, 0x1F3FC), "fully-qualified", "couple with heart: person, person, medium-light skin tone, medium-light skin tone"),
            new EmojiTestEntry(ImmutableList.Create<int>(0x1F9D1, 0x1F3FD, 0x200D, 0x2764, 0xFE0F, 0x200D, 0x1F9D1, 0x1F3FD), "fully-qualified", "couple with heart: person, person, medium skin tone, medium skin tone"),
            new EmojiTestEntry(ImmutableList.Create<int>(0x1F9D1, 0x1F3FE, 0x200D, 0x2764, 0xFE0F, 0x200D, 0x1F9D1, 0x1F3FE), "fully-qualified", "couple with heart: person, person, medium-dark skin tone, medium-dark skin tone"),
            new EmojiTestEntry(ImmutableList.Create<int>(0x1F9D1, 0x1F3FF, 0x200D, 0x2764, 0xFE0F, 0x200D, 0x1F9D1, 0x1F3FF), "fully-qualified", "couple with heart: person, person, dark skin tone, dark skin tone"),

            new EmojiTestEntry(ImmutableList.Create<int>(0x1F9D1, 0x1F3FB, 0x200D, 0x2764, 0x200D, 0x1F9D1, 0x1F3FB), "minimally-qualified", "couple with heart: person, person, light skin tone, light skin tone"),
            new EmojiTestEntry(ImmutableList.Create<int>(0x1F9D1, 0x1F3FC, 0x200D, 0x2764, 0x200D, 0x1F9D1, 0x1F3FC), "minimally-qualified", "couple with heart: person, person, medium-light skin tone, medium-light skin tone"),
            new EmojiTestEntry(ImmutableList.Create<int>(0x1F9D1, 0x1F3FD, 0x200D, 0x2764, 0x200D, 0x1F9D1, 0x1F3FD), "minimally-qualified", "couple with heart: person, person, medium skin tone, medium skin tone"),
            new EmojiTestEntry(ImmutableList.Create<int>(0x1F9D1, 0x1F3FE, 0x200D, 0x2764, 0x200D, 0x1F9D1, 0x1F3FE), "minimally-qualified", "couple with heart: person, person, medium-dark skin tone, medium-dark skin tone"),
            new EmojiTestEntry(ImmutableList.Create<int>(0x1F9D1, 0x1F3FF, 0x200D, 0x2764, 0x200D, 0x1F9D1, 0x1F3FF), "minimally-qualified", "couple with heart: person, person, dark skin tone, dark skin tone"),

            new EmojiTestEntry(ImmutableList.Create<int>(0x1F469, 0x1F3FB, 0x200D, 0x1F91D, 0x200D, 0x1F469, 0x1F3FB), "fully-qualified", "women holding hands: light skin tone, light skin tone"),
            new EmojiTestEntry(ImmutableList.Create<int>(0x1F469, 0x1F3FC, 0x200D, 0x1F91D, 0x200D, 0x1F469, 0x1F3FC), "fully-qualified", "women holding hands: medium-light skin tone, medium-light skin tone"),
            new EmojiTestEntry(ImmutableList.Create<int>(0x1F469, 0x1F3FD, 0x200D, 0x1F91D, 0x200D, 0x1F469, 0x1F3FD), "fully-qualified", "women holding hands: medium skin tone, medium skin tone"),
            new EmojiTestEntry(ImmutableList.Create<int>(0x1F469, 0x1F3FE, 0x200D, 0x1F91D, 0x200D, 0x1F469, 0x1F3FE), "fully-qualified", "women holding hands: medium-dark skin tone, medium-dark skin tone"),
            new EmojiTestEntry(ImmutableList.Create<int>(0x1F469, 0x1F3FF, 0x200D, 0x1F91D, 0x200D, 0x1F469, 0x1F3FF), "fully-qualified", "women holding hands: dark skin tone, dark skin tone"),

            new EmojiTestEntry(ImmutableList.Create<int>(0x1F469, 0x1F3FB, 0x200D, 0x1F91D, 0x200D, 0x1F468, 0x1F3FB), "fully-qualified", "woman and man holding hands: light skin tone, light skin tone"),
            new EmojiTestEntry(ImmutableList.Create<int>(0x1F469, 0x1F3FC, 0x200D, 0x1F91D, 0x200D, 0x1F468, 0x1F3FC), "fully-qualified", "woman and man holding hands: medium-light skin tone, medium-light skin tone"),
            new EmojiTestEntry(ImmutableList.Create<int>(0x1F469, 0x1F3FD, 0x200D, 0x1F91D, 0x200D, 0x1F468, 0x1F3FD), "fully-qualified", "woman and man holding hands: medium skin tone, medium skin tone"),
            new EmojiTestEntry(ImmutableList.Create<int>(0x1F469, 0x1F3FE, 0x200D, 0x1F91D, 0x200D, 0x1F468, 0x1F3FE), "fully-qualified", "woman and man holding hands: medium-dark skin tone, medium-dark skin tone"),
            new EmojiTestEntry(ImmutableList.Create<int>(0x1F469, 0x1F3FF, 0x200D, 0x1F91D, 0x200D, 0x1F468, 0x1F3FF), "fully-qualified", "woman and man holding hands: dark skin tone, dark skin tone"),

            new EmojiTestEntry(ImmutableList.Create<int>(0x1F468, 0x1F3FB, 0x200D, 0x1F91D, 0x200D, 0x1F468, 0x1F3FB), "fully-qualified", "men holding hands: light skin tone, light skin tone"),
            new EmojiTestEntry(ImmutableList.Create<int>(0x1F468, 0x1F3FC, 0x200D, 0x1F91D, 0x200D, 0x1F468, 0x1F3FC), "fully-qualified", "men holding hands: medium-light skin tone, medium-light skin tone"),
            new EmojiTestEntry(ImmutableList.Create<int>(0x1F468, 0x1F3FD, 0x200D, 0x1F91D, 0x200D, 0x1F468, 0x1F3FD), "fully-qualified", "men holding hands: medium skin tone, medium skin tone"),
            new EmojiTestEntry(ImmutableList.Create<int>(0x1F468, 0x1F3FE, 0x200D, 0x1F91D, 0x200D, 0x1F468, 0x1F3FE), "fully-qualified", "men holding hands: medium-dark skin tone, medium-dark skin tone"),
            new EmojiTestEntry(ImmutableList.Create<int>(0x1F468, 0x1F3FF, 0x200D, 0x1F91D, 0x200D, 0x1F468, 0x1F3FF), "fully-qualified", "men holding hands: dark skin tone, dark skin tone"),

            new EmojiTestEntry(ImmutableList.Create<int>(0x1F9D1, 0x1F3FB, 0x200D, 0x2764, 0xFE0F, 0x200D, 0x1F48B, 0x200D, 0x1F9D1, 0x1F3FB), "fully-qualified", "kiss: person, person, light skin tone, light skin tone"),
            new EmojiTestEntry(ImmutableList.Create<int>(0x1F9D1, 0x1F3FC, 0x200D, 0x2764, 0xFE0F, 0x200D, 0x1F48B, 0x200D, 0x1F9D1, 0x1F3FC), "fully-qualified", "kiss: person, person, medium-light skin tone, medium-light skin tone"),
            new EmojiTestEntry(ImmutableList.Create<int>(0x1F9D1, 0x1F3FD, 0x200D, 0x2764, 0xFE0F, 0x200D, 0x1F48B, 0x200D, 0x1F9D1, 0x1F3FD), "fully-qualified", "kiss: person, person, medium skin tone, medium skin tone"),
            new EmojiTestEntry(ImmutableList.Create<int>(0x1F9D1, 0x1F3FE, 0x200D, 0x2764, 0xFE0F, 0x200D, 0x1F48B, 0x200D, 0x1F9D1, 0x1F3FE), "fully-qualified", "kiss: person, person, medium-dark skin tone, medium-dark skin tone"),
            new EmojiTestEntry(ImmutableList.Create<int>(0x1F9D1, 0x1F3FF, 0x200D, 0x2764, 0xFE0F, 0x200D, 0x1F48B, 0x200D, 0x1F9D1, 0x1F3FF), "fully-qualified", "kiss: person, person, dark skin tone, dark skin tone"),

            new EmojiTestEntry(ImmutableList.Create<int>(0x1F9D1, 0x1F3FB, 0x200D, 0x2764, 0x200D, 0x1F48B, 0x200D, 0x1F9D1, 0x1F3FB), "minimally-qualified", "kiss: person, person, light skin tone, light skin tone"),
            new EmojiTestEntry(ImmutableList.Create<int>(0x1F9D1, 0x1F3FC, 0x200D, 0x2764, 0x200D, 0x1F48B, 0x200D, 0x1F9D1, 0x1F3FC), "minimally-qualified", "kiss: person, person, medium-light skin tone, medium-light skin tone"),
            new EmojiTestEntry(ImmutableList.Create<int>(0x1F9D1, 0x1F3FD, 0x200D, 0x2764, 0x200D, 0x1F48B, 0x200D, 0x1F9D1, 0x1F3FD), "minimally-qualified", "kiss: person, person, medium skin tone, medium skin tone"),
            new EmojiTestEntry(ImmutableList.Create<int>(0x1F9D1, 0x1F3FE, 0x200D, 0x2764, 0x200D, 0x1F48B, 0x200D, 0x1F9D1, 0x1F3FE), "minimally-qualified", "kiss: person, person, medium-dark skin tone, medium-dark skin tone"),
            new EmojiTestEntry(ImmutableList.Create<int>(0x1F9D1, 0x1F3FF, 0x200D, 0x2764, 0x200D, 0x1F48B, 0x200D, 0x1F9D1, 0x1F3FF), "minimally-qualified", "kiss: person, person, dark skin tone, dark skin tone")
            // @formatter:on
        );

        [Test]
        public void RegexAllEmojis()
        {
            var emojiTest = Emoji.GetEmojiTest();
            IEnumerable<string> emojiStrings = emojiTest
                .Select(e => string.Join("", e.Codepoints.Select(char.ConvertFromUtf32)));
            string regex = Dafsa.FromWordsMinimized(emojiStrings.ToImmutableList()).ToRegex();
            Console.WriteLine(regex);
        }

        [Test]
        public void RegexAllEmojisSimplified()
        {
            var emojiTest = Emoji.GetEmojiTest();
            IEnumerable<string> emojiStrings = emojiTest
                .Concat(EmojisToFixModifierSymmetry)
                .Select(e => string.Join("", e.Codepoints.Select(char.ConvertFromUtf32)));
            string regex = Dafsa.FromWordsMinimized(emojiStrings.ToImmutableList()).ToRegex();
            Console.WriteLine(regex);
        }

        [Test]
        public void RegexFullyQualifiedEmojis()
        {
            var emojiTest = Emoji.GetEmojiTest();
            IEnumerable<string> emojiStrings = emojiTest
                .Where(e => e.Status == "fully-qualified")
                .Select(e => string.Join("", e.Codepoints.Select(char.ConvertFromUtf32)));
            string regex = Dafsa.FromWordsMinimized(emojiStrings.ToImmutableList()).ToRegex();
            Console.WriteLine(regex);
        }

        [Test]
        public void RegexFullyQualifiedEmojisSimplified()
        {
            var emojiTest = Emoji.GetEmojiTest();
            IEnumerable<string> emojiStrings = emojiTest
                .Concat(EmojisToFixModifierSymmetry)
                .Where(e => e.Status == "fully-qualified")
                .Select(e => string.Join("", e.Codepoints.Select(char.ConvertFromUtf32)));
            string regex = Dafsa.FromWordsMinimized(emojiStrings.ToImmutableList()).ToRegex();
            Console.WriteLine(regex);
        }

        [Test]
        public void RegexPossiblyEmoji()
        {
            Regex possibleEmojiRegex = Emoji.GetPossibleEmojiRegex(Emoji.GetEmojiData());
            Console.WriteLine(possibleEmojiRegex);
        }
    }
}
