using System;
using System.Collections;
using System.Collections.Generic;

namespace BlackBrowser
{
    /// <summary>
    /// Computer Science Concept: Bloom Filter
    /// Space-efficient probabilistic data structure used to test set membership in O(k) time.
    /// Provides zero false negatives: if it returns false, the item is definitely not in the set.
    /// </summary>
    public class BloomFilter
    {
        private readonly int _size;
        private readonly int _hashFunctionCount;
        private readonly BitArray _bitArray;

        public BloomFilter(int expectedElements = 500, double falsePositiveRate = 0.01)
        {
            if (expectedElements <= 0) expectedElements = 100;
            if (falsePositiveRate <= 0 || falsePositiveRate >= 1) falsePositiveRate = 0.01;

            // Optimal bit array size m = - (n * ln(p)) / (ln(2)^2)
            _size = (int)Math.Ceiling(-1.0 * expectedElements * Math.Log(falsePositiveRate) / (Math.Log(2) * Math.Log(2)));
            _size = Math.Max(64, _size);

            // Optimal hash function count k = (m / n) * ln(2)
            _hashFunctionCount = (int)Math.Round((double)_size / expectedElements * Math.Log(2));
            _hashFunctionCount = Math.Max(2, Math.Min(8, _hashFunctionCount));

            _bitArray = new BitArray(_size);
        }

        public void Add(string item)
        {
            if (string.IsNullOrEmpty(item)) return;
            string key = item.ToLowerInvariant();

            int h1 = GetFnv1aHash(key);
            int h2 = GetMurmurishHash(key);

            // Kirsch-Mitzenmacher optimization: generate k hashes using h(i) = h1 + i * h2
            for (int i = 0; i < _hashFunctionCount; i++)
            {
                int combinedHash = (int)((uint)(h1 + i * h2) % (uint)_size);
                _bitArray.Set(combinedHash, true);
            }
        }

        public bool MightContain(string item)
        {
            if (string.IsNullOrEmpty(item)) return false;
            string key = item.ToLowerInvariant();

            int h1 = GetFnv1aHash(key);
            int h2 = GetMurmurishHash(key);

            for (int i = 0; i < _hashFunctionCount; i++)
            {
                int combinedHash = (int)((uint)(h1 + i * h2) % (uint)_size);
                if (!_bitArray.Get(combinedHash))
                    return false; // Definitely not present
            }
            return true; // Might be present (pass to secondary verification)
        }

        private static int GetFnv1aHash(string text)
        {
            unchecked
            {
                uint hash = 2166136261;
                for (int i = 0; i < text.Length; i++)
                {
                    hash ^= text[i];
                    hash *= 16777619;
                }
                return (int)hash;
            }
        }

        private static int GetMurmurishHash(string text)
        {
            unchecked
            {
                uint hash = 0x811C9DC5;
                for (int i = 0; i < text.Length; i++)
                {
                    hash = (hash << 5) - hash + text[i];
                    hash = (hash << 13) | (hash >> 19);
                    hash ^= 0x5bd1e995;
                }
                return (int)hash;
            }
        }
    }
}
