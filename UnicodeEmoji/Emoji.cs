using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.FileProviders;

namespace UnicodeEmoji
{
    public record EmojiDataEntry((int, int) CodepointRange, string Property, string Comments);

    public record EmojiTestEntry(IImmutableList<int> Codepoints, string Status, string EmojiName);

    public static class Emoji
    {
        /// <summary>
        /// Builds a regular expression that matches a superset of all possibly valid emojis as specified in
        /// <a href="https://www.unicode.org/reports/tr51/#EBNF_and_Regex">Unicode® Technical Standard #51 Section 1.4.9 EBNF and Regex</a>
        /// </summary>
        public static Regex GetPossibleEmojiRegex(IImmutableList<EmojiDataEntry> emojiData)
        {
            string regionalIndicator = CodepointsToRegex(first: 0x1F1E6, last: 0x1F1FF);
            string emoji = CodepointsToRegex(GetCodepointsForProperty(emojiData, "Emoji"));
            string emojiModifier = CodepointsToRegex(GetCodepointsForProperty(emojiData, "Emoji_Modifier"));
            string tags = CodepointsToRegex(first: 0xE0020, last: 0xE007E);
            string terminateTag = CodepointsToRegex(new[] {0xE007F});
            return new Regex(@$"
                {regionalIndicator} {regionalIndicator}
                | {emoji}
                    (?: {emojiModifier}
                    | \uFE0F \u20E3?
                    | (?:{tags})+ {terminateTag} )?
                    (?: \u200D {emoji}
                        (?: {emojiModifier}
                        | \uFE0F \u20E3?
                        | (?:{tags})+ {terminateTag} )?
                    )*
            ", RegexOptions.Compiled | RegexOptions.IgnorePatternWhitespace);
        }

        private static string CodepointsToRegex(int first, int last) =>
            CodepointsToRegex(Enumerable.Range(first, last - first + 1));

        private static string CodepointsToRegex(IEnumerable<int> codepoints) =>
            Dafsa.FromWordsMinimized(codepoints.Select(char.ConvertFromUtf32)).ToRegex();

        private static IEnumerable<int> GetCodepointsForProperty(IEnumerable<EmojiDataEntry> data, string property)
        {
            foreach (EmojiDataEntry entry in data)
            {
                if (entry.Property != property) continue;
                (int from, int to) = entry.CodepointRange;
                for (; from <= to; from++) yield return from;
            }
        }

        public static IImmutableList<EmojiDataEntry> GetEmojiData() =>
            ReadEmbeddedFile("emoji-data.txt")
                .Select(Parsers.ParseEmojiDataLine)
                .ToImmutableList();

        public static IImmutableList<EmojiTestEntry> GetEmojiTest() =>
            ReadEmbeddedFile("emoji-test.txt")
                .Select(Parsers.ParseEmojiTestLine)
                .ToImmutableList();

        private static IEnumerable<string> ReadEmbeddedFile(string filename)
        {
            var embeddedProvider = new EmbeddedFileProvider(typeof(Emoji).Assembly);
            using Stream stream = embeddedProvider.GetFileInfo($"Resources/{filename}").CreateReadStream();

            using var streamReader = new StreamReader(stream);
            string? line;
            while ((line = streamReader.ReadLine()) != null)
            {
                line = line.Trim();
                if (line.Length == 0 || line.StartsWith('#')) continue;
                yield return line;
            }
        }
    }
}
