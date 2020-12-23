using System;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace UnicodeEmoji
{
    public static class Parsers
    {
        private static readonly Regex DataLinePattern = new(@"
                ^
                    (?<codepoints>[0-9A-Z]+(?:\.\.[0-9A-Z]+)?)
                    \s*;\s*
                    (?<property>[^\s]+)
                    \s*\#\s*
                    (?<comments>.*)
                $",
            RegexOptions.Compiled | RegexOptions.IgnorePatternWhitespace);

        public static EmojiDataEntry ParseEmojiDataLine(string line)
        {
            Match match = DataLinePattern.Match(line);
            if (!match.Success)
            {
                throw new ArgumentException(
                    $"line is malformed. it must match the regex '{DataLinePattern}'. Offending line: {line}");
            }

            string codepoints = match.Groups["codepoints"].Value;
            string property = match.Groups["property"].Value;
            string comments = match.Groups["comments"].Value.Trim();

            string[] codepointsSplit = codepoints.Split("..", count: 2);
            if (codepointsSplit.Length == 1)
            {
                int cp = int.Parse(codepointsSplit[0], NumberStyles.AllowHexSpecifier);
                return new EmojiDataEntry((cp, cp), property, comments);
            }
            else
            {
                int cpFrom = int.Parse(codepointsSplit[0], NumberStyles.AllowHexSpecifier);
                int cpTo = int.Parse(codepointsSplit[1], NumberStyles.AllowHexSpecifier);
                return new EmojiDataEntry((cpFrom, cpTo), property, comments);
            }
        }

        private static readonly Regex TestLinePattern = new(@"
                ^
                    (?<codepoints>[0-9A-Z]+(?:\s[0-9A-Z]+)*)
                    \s*;\s*
                    (?<status>[^\s]+)
                    \s*\#\s*
                    (?<emojiname>.*)
                $",
            RegexOptions.Compiled | RegexOptions.IgnorePatternWhitespace);

        public static EmojiTestEntry ParseEmojiTestLine(string line)
        {
            Match match = TestLinePattern.Match(line);
            if (!match.Success)
            {
                throw new ArgumentException(
                    $"line is malformed. it must match the regex '{TestLinePattern}'. Offending line: {line}");
            }

            string codepointStrs = match.Groups["codepoints"].Value;
            string status = match.Groups["status"].Value;
            string emojiname = match.Groups["emojiname"].Value.Trim();

            IImmutableList<int> codepoints = codepointStrs
                .Split(" ")
                .Select(str => int.Parse(str, NumberStyles.AllowHexSpecifier))
                .ToImmutableList();
            return new EmojiTestEntry(codepoints, status, emojiname);
        }
    }
}
