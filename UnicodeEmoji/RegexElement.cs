using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace UnicodeEmoji
{
    public abstract class RegexElement : IComparable<RegexElement>
    {
        private string? _regex;
        private bool _optimized;

        protected abstract Sequence AsSequence();
        protected abstract int MaxPossibleLength();

        protected abstract string _ToRegex();
        protected abstract RegexElement _Optimize();

        public override string ToString() => ToRegex();
        public override int GetHashCode() => ToRegex().GetHashCode();
        public override bool Equals(object? obj) => ToRegex().Equals((obj as RegexElement)?.ToRegex());

        public int CompareTo(RegexElement? other) =>
            string.Compare(ToRegex(), other?.ToRegex(), StringComparison.Ordinal);

        public string ToRegex()
        {
            _regex ??= _ToRegex();
            return _regex;
        }

        public RegexElement Optimize()
        {
            if (_optimized) return this;
            RegexElement elem = _Optimize();
            elem._optimized = true;
            return elem;
        }

        private RegexElement()
        {
            // all possible subclasses are defined within the class
        }

        private static string RenderChar(char c) =>
            c < 128 ? Regex.Escape(char.ToString(c)) : $@"\u{(ushort) c:X4}";

        public sealed class Nothing : RegexElement
        {
            protected override string _ToRegex() => "";
            protected override RegexElement _Optimize() => this;
            protected override Sequence AsSequence() => new(ImmutableList<RegexElement>.Empty);
            protected override int MaxPossibleLength() => 0;
        }

        public sealed class SingleCharacter : RegexElement
        {
            public char Character { get; }

            public SingleCharacter(char character)
            {
                Character = character;
            }

            protected override string _ToRegex() => RenderChar(Character);
            protected override RegexElement _Optimize() => this;
            protected override Sequence AsSequence() => new(ImmutableList.Create<RegexElement>(this));
            protected override int MaxPossibleLength() => 1;
        }

        public sealed class CharacterSet : RegexElement
        {
            public ImmutableHashSet<char> Characters { get; }

            public CharacterSet(ImmutableHashSet<char> characters)
            {
                Characters = characters;
            }

            protected override string _ToRegex()
            {
                var consecutives = new List<(char, char)>();
                foreach (char cur in Characters.OrderBy(c => c))
                {
                    if (consecutives.Count > 0 && consecutives[^1].Item2 == cur - 1)
                        consecutives[^1] = (consecutives[^1].Item1, cur);
                    else
                        consecutives.Add((cur, cur));
                }

                var sb = new StringBuilder();
                sb.Append('[');
                foreach ((char from, char to) in consecutives)
                {
                    if (from == to)
                        sb.Append(RenderChar(from));
                    else if (from == to - 1)
                        sb.Append(RenderChar(from) + RenderChar(to));
                    else
                        sb.Append(RenderChar(from) + '-' + RenderChar(to));
                }
                sb.Append(']');
                return sb.ToString();
            }

            protected override RegexElement _Optimize() =>
                Characters.Count switch
                {
                    0 => new Nothing(),
                    1 => new SingleCharacter(Characters.First()),
                    _ => this
                };

            protected override Sequence AsSequence() => new(ImmutableList.Create<RegexElement>(this));
            protected override int MaxPossibleLength() => 1;
        }

        public sealed class Sequence : RegexElement
        {
            public IImmutableList<RegexElement> Elements { get; }

            public Sequence(IImmutableList<RegexElement> elements)
            {
                Elements = elements;
            }

            protected override string _ToRegex() =>
                string.Join("", Elements.Select(elem =>
                {
                    bool needsParens = elem is Or;
                    return needsParens ? $"(?:{elem.ToRegex()})" : elem.ToRegex();
                }));

            protected override RegexElement _Optimize()
            {
                List<RegexElement> newElems = Elements.ToList();
                do
                {
                    newElems = newElems
                        .SelectMany(elem => elem is Sequence seq
                            ? (IEnumerable<RegexElement>) seq.Elements
                            : new List<RegexElement> {elem})
                        .Select(elem => elem.Optimize())
                        .Where(elem => elem is not Nothing)
                        .ToList();
                } while (newElems.Any(elem => elem is Sequence));

                return newElems.Count switch
                {
                    0 => new Nothing(),
                    1 => newElems.First(),
                    _ => new Sequence(newElems.ToImmutableList())
                };
            }

            protected override Sequence AsSequence() => this;
            protected override int MaxPossibleLength() => Elements.Sum(elem => elem.MaxPossibleLength());
        }

        public sealed class Maybe : RegexElement
        {
            public RegexElement Content { get; }

            public Maybe(RegexElement content)
            {
                Content = content;
            }

            protected override string _ToRegex()
            {
                bool needsParens = Content is Or || Content is Sequence || Content is Maybe;
                return needsParens ? $"(?:{Content.ToRegex()})?" : Content.ToRegex() + "?";
            }

            protected override RegexElement _Optimize()
            {
                var newElem = Content.Optimize();
                return newElem switch
                {
                    Nothing => new Nothing(),
                    Maybe => newElem,
                    _ => new Maybe(newElem)
                };
            }

            protected override Sequence AsSequence() => new(ImmutableList.Create<RegexElement>(this));
            protected override int MaxPossibleLength() => Content.MaxPossibleLength();
        }

        public sealed class Or : RegexElement
        {
            public IImmutableSet<RegexElement> Options { get; }

            public Or(IImmutableSet<RegexElement> options)
            {
                Options = options;
            }

            protected override string _ToRegex() =>
                string.Join('|', Options
                    .OrderBy(elem => (-elem.MaxPossibleLength(), elem.ToRegex()))
                    .Select(elem => elem.ToRegex())
                );

            private static List<RegexElement> OptimizeOptions(IReadOnlyCollection<RegexElement> origElems)
            {
                List<RegexElement> newElems = origElems.ToList();

                do
                {
                    // Need to unwind all options as flat as possible, to be able to reliably bisect on common prefixes,
                    // e.g. 'ax' and '[a-z]y' need to factor out the 'a' for the resulting regex to reliably match
                    // the longest possible string.
                    newElems = newElems
                        .SelectMany(elem =>
                            elem is Or or ? or.Options : (IEnumerable<RegexElement>) new List<RegexElement> {elem})
                        .SelectMany(elem =>
                            elem is CharacterSet characterSet
                                ? characterSet.Characters.Select(c => new SingleCharacter(c))
                                : (IEnumerable<RegexElement>) new List<RegexElement> {elem})
                        .Select(elem => elem.Optimize()) // e.g. flatten nested sequences
                        .ToList();
                } while (newElems.Any(elem => elem is Or || elem is CharacterSet));

                // Find common prefixes and suffixes to optimize the edges a bit,
                // e.g. --a--> and --ab--> can be simplified to --ab?-->
                int maxLength = newElems.Select(e => e.AsSequence()).Max(seq => seq.Elements.Count);
                int xfixLength = 1;
                while (xfixLength < maxLength)
                {
                    bool didOptimize = false;

                    IEnumerable<IGrouping<RegexElement, Sequence>> prefixGroup = newElems
                        .Select(e => e.AsSequence())
                        .GroupBy(seq => new Sequence(seq.Elements.Take(xfixLength).ToImmutableList()).Optimize());

                    List<RegexElement> prefixOptimized = new();
                    foreach (IGrouping<RegexElement, Sequence> group in prefixGroup)
                    {
                        if (group.Count() == 1)
                        {
                            prefixOptimized.Add(group.First().Optimize());
                            continue;
                        }

                        RegexElement prefix = group.Key;
                        IEnumerable<Sequence> wordsWithPrefix = group.ToList();
                        if (prefix is Nothing)
                        {
                            prefixOptimized.AddRange(wordsWithPrefix.Select(word => word.Optimize()));
                            continue;
                        }

                        IImmutableSet<RegexElement> wordsPrefixRemoved = wordsWithPrefix
                            .Select(seq => seq.Elements.Skip(xfixLength).ToList())
                            .Select(elems => (RegexElement) new Sequence(elems.ToImmutableList()))
                            .ToImmutableHashSet();

                        RegexElement restElem = new Or(wordsPrefixRemoved);
                        prefixOptimized.Add(new Sequence(ImmutableList.Create(prefix, restElem)).Optimize());
                        didOptimize = true;
                    }

                    newElems = prefixOptimized;

                    IEnumerable<IGrouping<RegexElement, Sequence>> suffixGroup = newElems
                        .Select(e => e.AsSequence())
                        .GroupBy(seq => new Sequence(
                            seq.Elements.Skip(seq.Elements.Count - xfixLength).ToImmutableList()).Optimize());

                    List<RegexElement> suffixOptimized = new();
                    foreach (IGrouping<RegexElement, Sequence> group in suffixGroup)
                    {
                        if (group.Count() == 1)
                        {
                            suffixOptimized.Add(group.First().Optimize());
                            continue;
                        }

                        RegexElement suffix = group.Key;
                        IEnumerable<Sequence> wordsWithSuffix = group.ToList();
                        if (suffix is Nothing)
                        {
                            suffixOptimized.AddRange(wordsWithSuffix.Select(word => word.Optimize()));
                            continue;
                        }

                        IImmutableSet<RegexElement> wordsSuffixRemoved = wordsWithSuffix
                            .Select(seq => seq.Elements.Take(seq.Elements.Count - xfixLength).ToList())
                            .Select(elems => (RegexElement) new Sequence(elems.ToImmutableList()))
                            .ToImmutableHashSet();

                        RegexElement restElem = new Or(wordsSuffixRemoved);
                        suffixOptimized.Add(new Sequence(ImmutableList.Create(restElem, suffix)).Optimize());
                        didOptimize = true;
                    }

                    newElems = suffixOptimized;

                    if (!didOptimize)
                    {
                        xfixLength++;
                    }
                }

                IEnumerable<char> fromSingleCharacters = newElems.OfType<SingleCharacter>().Select(c => c.Character);
                IEnumerable<char> fromCharacterSets = newElems.OfType<CharacterSet>().SelectMany(s => s.Characters);
                ImmutableHashSet<char> chars = fromCharacterSets.Concat(fromSingleCharacters).ToImmutableHashSet();
                RegexElement characterSetElem = new CharacterSet(chars).Optimize();

                newElems.RemoveAll(elem => elem is SingleCharacter || elem is CharacterSet);
                if (characterSetElem is not Nothing) newElems.Add(characterSetElem);

                return newElems;
            }

            protected override RegexElement _Optimize()
            {
                bool isOptional = false;

                List<RegexElement> newElems = Options.Select(e => e.Optimize()).ToList();
                for (var i = 0; i < newElems.Count; i++)
                {
                    if (newElems[i] is Maybe maybe)
                    {
                        isOptional = true;
                        newElems[i] = maybe.Content;
                    }
                }

                newElems = OptimizeOptions(newElems);

                if (newElems.RemoveAll(e => e is Nothing) > 0) isOptional = true;
                if (newElems.Count == 0) return new Nothing();
                if (newElems.Count == 1)
                    return isOptional
                        ? new Maybe(newElems.First()).Optimize()
                        : newElems.First();
                return isOptional
                    ? new Maybe(new Or(newElems.ToImmutableHashSet()))
                    : new Or(newElems.ToImmutableHashSet());
            }

            protected override Sequence AsSequence() => new(ImmutableList.Create<RegexElement>(this));
            protected override int MaxPossibleLength() => Options.Max(elem => elem.MaxPossibleLength());
        }
    }
}
