using System;
using System.Collections.Generic;
using System.Linq;

namespace BlackBrowser
{
    public class AutocompleteSuggestion
    {
        public string Key { get; set; }
        public string Title { get; set; }
        public string Url { get; set; }
        public string Category { get; set; } // "history", "bookmark", "search", "domain"
        public int Weight { get; set; }
    }

    /// <summary>
    /// Computer Science Concept: Trie (Prefix Tree) with Top-K Ranking
    /// Enables O(L) prefix retrieval where L is query length, regardless of dictionary size.
    /// Uses frequency weights to rank matching suggestions.
    /// </summary>
    public class PrefixTrie
    {
        private class TrieNode
        {
            public Dictionary<char, TrieNode> Children = new Dictionary<char, TrieNode>();
            public List<AutocompleteSuggestion> Suggestions = new List<AutocompleteSuggestion>();
            public bool IsEndOfWord = false;
        }

        private readonly TrieNode _root = new TrieNode();
        private readonly object _lock = new object();

        public void Insert(string text, string title, string url, string category, int weight = 1)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            string key = text.Trim().ToLowerInvariant();

            AutocompleteSuggestion suggestion = new AutocompleteSuggestion
            {
                Key = key,
                Title = title ?? text,
                Url = url ?? text,
                Category = category ?? "search",
                Weight = weight
            };

            lock (_lock)
            {
                TrieNode current = _root;
                foreach (char c in key)
                {
                    if (!current.Children.ContainsKey(c))
                    {
                        current.Children[c] = new TrieNode();
                    }
                    current = current.Children[c];
                    AddSortedSuggestion(current.Suggestions, suggestion, 8);
                }
                current.IsEndOfWord = true;
            }
        }

        public List<AutocompleteSuggestion> SearchPrefix(string prefix, int maxResults = 6)
        {
            if (string.IsNullOrWhiteSpace(prefix)) return new List<AutocompleteSuggestion>();
            string key = prefix.Trim().ToLowerInvariant();

            lock (_lock)
            {
                TrieNode current = _root;
                foreach (char c in key)
                {
                    if (!current.Children.ContainsKey(c))
                    {
                        return new List<AutocompleteSuggestion>(); // No matching prefix
                    }
                    current = current.Children[c];
                }

                // Return pre-ranked top suggestions stored at prefix node
                return current.Suggestions.Take(maxResults).ToList();
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _root.Children.Clear();
                _root.Suggestions.Clear();
            }
        }

        private static void AddSortedSuggestion(List<AutocompleteSuggestion> list, AutocompleteSuggestion item, int limit)
        {
            // Avoid duplicate URLs in same node bucket
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Url.Equals(item.Url, StringComparison.OrdinalIgnoreCase))
                {
                    if (item.Weight > list[i].Weight) list[i].Weight = item.Weight;
                    return;
                }
            }

            int insertIdx = list.Count;
            for (int i = 0; i < list.Count; i++)
            {
                if (item.Weight > list[i].Weight)
                {
                    insertIdx = i;
                    break;
                }
            }

            list.Insert(insertIdx, item);
            if (list.Count > limit)
            {
                list.RemoveAt(list.Count - 1);
            }
        }
    }
}
