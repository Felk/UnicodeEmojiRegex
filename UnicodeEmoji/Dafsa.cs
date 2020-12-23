using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;

namespace UnicodeEmoji
{
    internal record Edge (RegexElement Word, DafsaNode Node);

    internal class DafsaNode
    {
        public HashSet<Edge> ParentLinks { get; set; } = new();
        public HashSet<Edge> ChildrenLinks { get; set; } = new();
    }

    public class Dafsa
    {
        private readonly DafsaNode _rootNode;
        private readonly DafsaNode _leafNode;

        private Dafsa(DafsaNode rootNode, DafsaNode leafNode)
        {
            _rootNode = rootNode;
            _leafNode = leafNode;
        }

        /// <summary>
        /// Build prefix tree / trie: recursively group by first character.
        /// It's not a true prefix tree since we are already tying all ends into a single leaf node for convenience.
        /// </summary>
        /// <param name="inputWords">words to construct the pseudo prefix tree from</param>
        /// <returns>tuple of (root node, leaf node) of the constructed structure</returns>
        public static Dafsa FromPseudoPrefixTree(IEnumerable<string> inputWords)
        {
            DafsaNode leafNode = new();
            DafsaNode rootNode = new();

            void AddChildrenFromWords(DafsaNode node, IReadOnlyCollection<string> strs)
            {
                foreach (var grouping in strs
                    .Where(s => s.Length > 0)
                    .GroupBy(s => s[0]))
                {
                    RegexElement.SingleCharacter word = new(grouping.Key);
                    DafsaNode newChild = new() {ParentLinks = new HashSet<Edge> {new(word, node)}};
                    node.ChildrenLinks.Add(new Edge(word, newChild));
                    AddChildrenFromWords(newChild, grouping.Select(s => s.Substring(1)).ToList());
                }

                if (strs.Any(s => s.Length == 0))
                {
                    leafNode.ParentLinks.Add(new Edge(new RegexElement.Nothing(), node));
                    node.ChildrenLinks.Add(new Edge(new RegexElement.Nothing(), leafNode));
                }
            }

            AddChildrenFromWords(rootNode, inputWords.OrderBy(s => s).ToList());

            return new Dafsa(rootNode, leafNode);
        }

        /// <summary>
        /// Build a minimized DAFSA defined by a list of accepted words.
        /// </summary>
        /// <param name="inputWords">words that the DAFSA will accept</param>
        /// <returns>a DAFSA that accepts only the supplied words</returns>
        public static Dafsa FromWordsMinimized(IEnumerable<string> inputWords)
        {
            Dafsa dafsa = FromPseudoPrefixTree(inputWords);
            dafsa.Minimize();
            return dafsa;
        }

        /// <summary>
        /// Determines whether a supplied teststring is accepted by this DAFSA.
        /// </summary>
        /// <param name="teststring">string to test</param>
        /// <returns>true if it is accepted, false otherwise</returns>
        public bool IsMatch(string teststring)
        {
            DafsaNode currentNode = _rootNode;
            foreach (char c in teststring)
            {
                RegexElement.SingleCharacter matchChar = new(c);
                Edge? edge = currentNode.ChildrenLinks.FirstOrDefault(e => Equals(e.Word, matchChar));
                if (edge == null) return false;
                currentNode = edge.Node;
            }

            return currentNode.ChildrenLinks.Any(e => e.Word is RegexElement.Nothing && e.Node == _leafNode);
        }

        /// <summary>
        /// Build regex from DAFSA by eliminating states until we are left with the root and leaf only.
        /// Note that the DAFSA passed in will be modified in-place, so it cannot be re-used afterwards.
        /// </summary>
        /// <returns>The regex constructed from the DAFSA</returns>
        public string ToRegex()
        {
            DafsaNode? GetNextNodeToEliminate() =>
                // need to traverse top-down to reliably bisect common prefixes,
                // otherwise the resulting regex might fail to find the longest possible match.
                _rootNode.ChildrenLinks
                    .OrderBy(edge => edge.Word) // non-sophisticated, but deterministic elimination order
                    .Select(edge => edge.Node)
                    .FirstOrDefault(node => node != _leafNode);

            DafsaNode? nodeToEliminate;
            while ((nodeToEliminate = GetNextNodeToEliminate()) != null)
            {
                // Add edges going around the node.
                foreach (var parentLink in nodeToEliminate.ParentLinks.OrderBy(edge => edge.Word))
                {
                    foreach (var childrenLink in nodeToEliminate.ChildrenLinks.OrderBy(edge => edge.Word))
                    {
                        IImmutableList<RegexElement> elems = ImmutableList.Create(parentLink.Word, childrenLink.Word);
                        RegexElement newWord = new RegexElement.Sequence(elems);
                        parentLink.Node.ChildrenLinks.Add(new Edge(newWord, childrenLink.Node));
                        childrenLink.Node.ParentLinks.Add(new Edge(newWord, parentLink.Node));
                    }
                }

                // Sever all incoming edges.
                foreach (DafsaNode parent in nodeToEliminate.ParentLinks.Select(edge => edge.Node).Distinct())
                    parent.ChildrenLinks.RemoveWhere(edge => edge.Node == nodeToEliminate);
                foreach (DafsaNode child in nodeToEliminate.ChildrenLinks.Select(edge => edge.Node).Distinct())
                    child.ParentLinks.RemoveWhere(edge => edge.Node == nodeToEliminate);

                // Merge all edges that have the same source and destination.
                foreach (DafsaNode parent in nodeToEliminate.ParentLinks.Select(edge => edge.Node).Distinct())
                {
                    foreach (DafsaNode child in nodeToEliminate.ChildrenLinks.Select(edge => edge.Node).Distinct())
                    {
                        IImmutableSet<RegexElement> words = parent.ChildrenLinks
                            .Where(edge => edge.Node == child)
                            .Select(edge => edge.Word)
                            .ToImmutableHashSet();

                        if (words.Count <= 1) continue;

                        // Remove existing edges
                        parent.ChildrenLinks.RemoveWhere(edge => edge.Node == child);
                        child.ParentLinks.RemoveWhere(edge => edge.Node == parent);

                        // Merge everything into a single "or"-edge
                        RegexElement elem = new RegexElement.Or(words).Optimize();
                        parent.ChildrenLinks.Add(new Edge(elem, child));
                        child.ParentLinks.Add(new Edge(elem, parent));
                    }
                }
            }

            Debug.Assert(_rootNode.ChildrenLinks.First().Node == _leafNode, "Intermediate states were removed");
            Debug.Assert(_rootNode.ChildrenLinks.Count == 1, "Only the leaf remains and multiple edges were merged");
            string finalRegex = _rootNode.ChildrenLinks.First().Word.Optimize().ToRegex();
            return finalRegex;
        }

        /// <summary>
        /// Minimize the DAFSA by traversing it bottom-up and eliminating all but one state from each equivalece group.
        /// Two states are equivalent if they have the same outgoing edges, meaning they have the same behaviour.
        /// </summary>
        private void Minimize()
        {
            static void Optimize(DafsaNode node)
            {
                if (!node.ParentLinks.Any()) return;
                foreach (var equivalenceGroup in node.ParentLinks
                    .Select(link => link.Node)
                    .Distinct()
                    .GroupBy(parent => parent.ChildrenLinks, HashSet<Edge>.CreateSetComparer())
                    .Where(group => group.Count() > 1))
                {
                    DafsaNode first = equivalenceGroup.First();
                    foreach (DafsaNode redundant in equivalenceGroup.Skip(1))
                    {
                        // reroute all of the redundant node's children
                        foreach (Edge childLinksOfRedundant in redundant.ChildrenLinks)
                        {
                            childLinksOfRedundant.Node.ParentLinks = childLinksOfRedundant.Node.ParentLinks
                                .Select(edge => edge.Node != redundant ? edge : new Edge(edge.Word, first))
                                .ToHashSet();
                            first.ChildrenLinks.Add(childLinksOfRedundant);
                        }

                        // reroute all of the redundant node's parents
                        foreach (Edge parentLinksOfRedundant in redundant.ParentLinks)
                        {
                            parentLinksOfRedundant.Node.ChildrenLinks = parentLinksOfRedundant.Node.ChildrenLinks
                                .Select(edge => edge.Node != redundant ? edge : new Edge(edge.Word, first))
                                .ToHashSet();
                            first.ParentLinks.Add(parentLinksOfRedundant);
                        }
                    }
                }

                // current node's parents have distinct behaviour now. Do the same recursively for all parents.
                foreach (Edge parentLink in node.ParentLinks) Optimize(parentLink.Node);
            }

            Optimize(_leafNode);
        }
    }
}
