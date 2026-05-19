using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Crowe.MTRHub.Plugins.CRUD;
using Crowe.MTRHub.Plugins.Infrastructure;
using Microsoft.Xrm.Sdk;

namespace Crowe.MTRHub.Plugins.Helpers
{
    public class VendorMatchHelper
    {
        private readonly ServiceConnection _cnx;
        private readonly ServiceContainer _container;

        public VendorMatchHelper(ServiceConnection cnx, ServiceContainer container)
        {
            _cnx = cnx;
            _container = container;
        }

        public string FindMatches(string name, string address, string zip, int topN)
        {
            var tokens = StringSimilarity.DistinctiveTokens(name, 2);
            _cnx.Trace.Trace("Distinctive name tokens: [{0}]", string.Join(", ", tokens));

            var candidates = _container.Retrieve.GetCandidateVendors(tokens, zip);
            _cnx.Trace.Trace("Total unique candidates after union: {0}", candidates.Count);

            var scored = ScoreCandidates(candidates, name, address, zip);

            var top = scored
                .OrderByDescending(r => r.Rank)
                .ThenBy(r => r.VendorName ?? string.Empty)
                .Take(topN)
                .ToList();

            if (top.Count > 0)
            {
                var preview = top.Take(3)
                    .Select(r => string.Format("{0}={1}", r.Rank, Truncate(r.VendorName, 40)));
                _cnx.Trace.Trace("Top {0} of {1} results: {2}",
                    Math.Min(3, top.Count), top.Count, string.Join(" | ", preview));
            }
            else
            {
                _cnx.Trace.Trace("No matches found.");
            }

            return ToJsonArray(top);
        }

        private List<ScoredVendor> ScoreCandidates(IList<Entity> candidates, string name, string address, string zip)
        {
            bool hasAddress = !string.IsNullOrWhiteSpace(address);
            bool hasZip = !string.IsNullOrWhiteSpace(zip);

            double wName, wAddr, wZip;
            ComputeWeights(hasAddress, hasZip, out wName, out wAddr, out wZip);
            _cnx.Trace.Trace("Weights: name={0:F2} addr={1:F2} zip={2:F2}", wName, wAddr, wZip);

            var results = new List<ScoredVendor>(candidates.Count);
            foreach (var v in candidates)
            {
                double nameScore = ComputeNameScore(name, v);
                double addrScore = hasAddress ? ComputeAddressScore(address, v) : 0.0;
                double zipScore = hasZip ? ComputeZipScore(zip, v) : 0.0;

                double combined = (wName * nameScore) + (wAddr * addrScore) + (wZip * zipScore);
                int rank = (int)Math.Round(combined * 100.0);
                if (rank < 0) rank = 0;
                if (rank > 100) rank = 100;

                results.Add(new ScoredVendor
                {
                    Rank = rank,
                    VendorId = v.Id,
                    VendorName = v.GetAttributeValue<string>(Literals.VendorV2.OrganizationName),
                    VendorAccount = v.GetAttributeValue<string>(Literals.VendorV2.VendorAccount),
                    Address = v.GetAttributeValue<string>(Literals.VendorV2.FormattedAddress)
                });
            }
            return results;
        }

        private static void ComputeWeights(bool hasAddr, bool hasZip, out double wName, out double wAddr, out double wZip)
        {
            if (hasAddr && hasZip) { wName = 0.60; wAddr = 0.25; wZip = 0.15; return; }
            if (hasAddr)            { wName = 0.70; wAddr = 0.30; wZip = 0.00; return; }
            if (hasZip)             { wName = 0.75; wAddr = 0.00; wZip = 0.25; return; }
            wName = 1.00; wAddr = 0.00; wZip = 0.00;
        }

        private static double ComputeNameScore(string input, Entity v)
        {
            string org = v.GetAttributeValue<string>(Literals.VendorV2.OrganizationName) ?? string.Empty;
            string sn = v.GetAttributeValue<string>(Literals.VendorV2.SearchName) ?? string.Empty;

            double jwOrg = StringSimilarity.JaroWinkler(input, org);
            double jwSn = StringSimilarity.JaroWinkler(input, sn);
            double tokOrg = StringSimilarity.TokenJaccard(input, org);
            double tokSn = StringSimilarity.TokenJaccard(input, sn);

            return Math.Max(Math.Max(jwOrg, jwSn), Math.Max(tokOrg, tokSn));
        }

        private static double ComputeAddressScore(string input, Entity v)
        {
            string addr = v.GetAttributeValue<string>(Literals.VendorV2.FormattedAddress) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(addr)) return 0.0;
            double jw = StringSimilarity.JaroWinkler(input, addr);
            double tok = StringSimilarity.TokenJaccard(input, addr);
            return 0.5 * jw + 0.5 * tok;
        }

        private static double ComputeZipScore(string input, Entity v)
        {
            string z = v.GetAttributeValue<string>(Literals.VendorV2.ZipCode);
            if (string.IsNullOrWhiteSpace(z)) return 0.0;
            string a = input.Trim();
            string b = z.Trim();
            if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase)) return 1.0;
            int prefixLen = Math.Min(3, Math.Min(a.Length, b.Length));
            if (prefixLen > 0 &&
                string.Equals(a.Substring(0, prefixLen), b.Substring(0, prefixLen),
                              StringComparison.OrdinalIgnoreCase))
                return 0.5;
            return 0.0;
        }

        private static string ToJsonArray(IList<ScoredVendor> rows)
        {
            var sb = new StringBuilder(64 + rows.Count * 200);
            sb.Append('[');
            for (int i = 0; i < rows.Count; i++)
            {
                if (i > 0) sb.Append(',');
                var r = rows[i];
                sb.Append('{');
                AppendIntField(sb, Literals.CustomApi.JsonField.Rank, r.Rank);              sb.Append(',');
                AppendStringField(sb, Literals.CustomApi.JsonField.VendorId, r.VendorId.ToString("D"));      sb.Append(',');
                AppendStringField(sb, Literals.CustomApi.JsonField.VendorName, r.VendorName);                sb.Append(',');
                AppendStringField(sb, Literals.CustomApi.JsonField.VendorAccount, r.VendorAccount);          sb.Append(',');
                AppendStringField(sb, Literals.CustomApi.JsonField.Address, r.Address);
                sb.Append('}');
            }
            sb.Append(']');
            return sb.ToString();
        }

        private static void AppendIntField(StringBuilder sb, string key, int value)
        {
            sb.Append('"').Append(key).Append("\":")
              .Append(value.ToString(CultureInfo.InvariantCulture));
        }

        private static void AppendStringField(StringBuilder sb, string key, string value)
        {
            sb.Append('"').Append(key).Append("\":\"");
            EscapeJsonString(sb, value);
            sb.Append('"');
        }

        private static void EscapeJsonString(StringBuilder sb, string s)
        {
            if (string.IsNullOrEmpty(s)) return;
            foreach (char c in s)
            {
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"':  sb.Append("\\\""); break;
                    case '\b': sb.Append("\\b");  break;
                    case '\f': sb.Append("\\f");  break;
                    case '\n': sb.Append("\\n");  break;
                    case '\r': sb.Append("\\r");  break;
                    case '\t': sb.Append("\\t");  break;
                    default:
                        if (c < ' ')
                            sb.AppendFormat(CultureInfo.InvariantCulture, "\\u{0:x4}", (int)c);
                        else
                            sb.Append(c);
                        break;
                }
            }
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Length <= max ? s : s.Substring(0, max) + "…";
        }

        private class ScoredVendor
        {
            public int Rank { get; set; }
            public Guid VendorId { get; set; }
            public string VendorName { get; set; }
            public string VendorAccount { get; set; }
            public string Address { get; set; }
        }
    }
}
