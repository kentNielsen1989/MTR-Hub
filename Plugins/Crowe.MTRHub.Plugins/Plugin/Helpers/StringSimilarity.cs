using System;
using System.Collections.Generic;

namespace Crowe.MTRHub.Plugins.Helpers
{
    public static class StringSimilarity
    {
        public static double JaroWinkler(string s1, string s2)
        {
            if (string.IsNullOrEmpty(s1) || string.IsNullOrEmpty(s2)) return 0.0;
            double jaro = Jaro(s1, s2);
            if (jaro < 0.7) return jaro;

            int prefix = 0;
            int maxPrefix = Math.Min(4, Math.Min(s1.Length, s2.Length));
            for (int i = 0; i < maxPrefix; i++)
            {
                if (char.ToLowerInvariant(s1[i]) == char.ToLowerInvariant(s2[i])) prefix++;
                else break;
            }
            return jaro + prefix * 0.1 * (1.0 - jaro);
        }

        private static double Jaro(string s1, string s2)
        {
            int len1 = s1.Length;
            int len2 = s2.Length;
            if (len1 == 0 || len2 == 0) return 0.0;

            int matchDistance = Math.Max(len1, len2) / 2 - 1;
            if (matchDistance < 0) matchDistance = 0;

            var s1Matches = new bool[len1];
            var s2Matches = new bool[len2];
            int matches = 0;

            for (int i = 0; i < len1; i++)
            {
                int start = Math.Max(0, i - matchDistance);
                int end = Math.Min(i + matchDistance + 1, len2);
                for (int j = start; j < end; j++)
                {
                    if (s2Matches[j]) continue;
                    if (char.ToLowerInvariant(s1[i]) != char.ToLowerInvariant(s2[j])) continue;
                    s1Matches[i] = true;
                    s2Matches[j] = true;
                    matches++;
                    break;
                }
            }
            if (matches == 0) return 0.0;

            int transpositions = 0;
            int k = 0;
            for (int i = 0; i < len1; i++)
            {
                if (!s1Matches[i]) continue;
                while (!s2Matches[k]) k++;
                if (char.ToLowerInvariant(s1[i]) != char.ToLowerInvariant(s2[k])) transpositions++;
                k++;
            }
            double m = matches;
            return (m / len1 + m / len2 + (m - transpositions / 2.0) / m) / 3.0;
        }

        public static double TokenJaccard(string s1, string s2)
        {
            if (string.IsNullOrWhiteSpace(s1) || string.IsNullOrWhiteSpace(s2)) return 0.0;
            var t1 = TokenSet(s1);
            var t2 = TokenSet(s2);
            if (t1.Count == 0 || t2.Count == 0) return 0.0;

            int intersect = 0;
            foreach (var token in t1)
                if (t2.Contains(token)) intersect++;
            int union = t1.Count + t2.Count - intersect;
            return union == 0 ? 0.0 : (double)intersect / union;
        }

        public static HashSet<string> TokenSet(string s)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(s)) return set;
            foreach (var raw in Tokenize(s))
            {
                if (!string.IsNullOrEmpty(raw)) set.Add(raw);
            }
            return set;
        }

        public static List<string> Tokenize(string s)
        {
            var tokens = new List<string>();
            if (string.IsNullOrWhiteSpace(s)) return tokens;

            var current = new System.Text.StringBuilder();
            foreach (char c in s)
            {
                if (char.IsLetterOrDigit(c))
                {
                    current.Append(char.ToLowerInvariant(c));
                }
                else
                {
                    if (current.Length > 0)
                    {
                        tokens.Add(current.ToString());
                        current.Clear();
                    }
                }
            }
            if (current.Length > 0) tokens.Add(current.ToString());
            return tokens;
        }

        private static readonly HashSet<string> Stopwords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "inc", "llc", "co", "corp", "corporation", "company", "the", "and",
            "ltd", "limited", "lp", "llp", "plc", "gmbh", "ag", "sa", "spa",
            "of", "for", "a", "an"
        };

        public static List<string> DistinctiveTokens(string s, int max)
        {
            var all = Tokenize(s);
            var filtered = new List<string>();
            foreach (var t in all)
            {
                if (t.Length < 2) continue;
                if (Stopwords.Contains(t)) continue;
                filtered.Add(t);
            }
            if (filtered.Count == 0) filtered = all;

            filtered.Sort((a, b) => b.Length.CompareTo(a.Length));
            if (filtered.Count > max) filtered = filtered.GetRange(0, max);
            return filtered;
        }
    }
}
