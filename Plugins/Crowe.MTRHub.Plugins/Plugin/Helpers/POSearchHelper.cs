using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Crowe.MTRHub.Plugins.Infrastructure;
using Microsoft.Xrm.Sdk;

namespace Crowe.MTRHub.Plugins.Helpers
{
    public class POSearchHelper
    {
        private readonly ServiceConnection _cnx;
        private readonly ServiceContainer _container;

        public POSearchHelper(ServiceConnection cnx, ServiceContainer container)
        {
            _cnx = cnx;
            _container = container;
        }

        public string FindMatches(string vendorNum, string customerRef, int topN)
        {
            var candidates = _container.PORetrieve.GetCandidatePOs(vendorNum, customerRef);
            _cnx.Trace.Trace("Total candidate POs: {0}", candidates.Count);

            var scored = ScoreCandidates(candidates, vendorNum, customerRef);

            var top = scored
                .OrderByDescending(r => r.Rank)
                .ThenByDescending(r => r.PurchaseOrderNumber ?? string.Empty)
                .Take(topN)
                .ToList();

            if (top.Count > 0)
            {
                var preview = top.Take(3)
                    .Select(r => string.Format("{0}={1}", r.Rank, Truncate(r.PurchaseOrderNumber, 40)));
                _cnx.Trace.Trace("Top {0} of {1} results: {2}",
                    Math.Min(3, top.Count), top.Count, string.Join(" | ", preview));
            }
            else
            {
                _cnx.Trace.Trace("No matches found.");
            }

            return ToJsonArray(top);
        }

        private List<ScoredPO> ScoreCandidates(IList<Entity> candidates, string vendorNum, string customerRef)
        {
            bool hasRef = !string.IsNullOrWhiteSpace(customerRef);

            double wVendor, wRef;
            if (hasRef) { wVendor = 0.5; wRef = 0.5; }
            else        { wVendor = 1.0; wRef = 0.0; }
            _cnx.Trace.Trace("Weights: vendor={0:F2} customerref={1:F2}", wVendor, wRef);

            var results = new List<ScoredPO>(candidates.Count);
            foreach (var po in candidates)
            {
                double vendorScore = ComputeVendorScore(vendorNum, po);
                double refScore = hasRef ? ComputeCustomerRefScore(customerRef, po) : 0.0;

                double combined = (wVendor * vendorScore) + (wRef * refScore);
                int rank = (int)Math.Round(combined * 100.0);
                if (rank < 0) rank = 0;
                if (rank > 100) rank = 100;

                results.Add(new ScoredPO
                {
                    Rank = rank,
                    POId = po.Id,
                    PurchaseOrderNumber = po.GetAttributeValue<string>(Literals.PurchaseOrderV2.PurchaseOrderNumber),
                    VendorAccount = po.GetAttributeValue<string>(Literals.PurchaseOrderV2.OrderVendorAccountNumber),
                    VendorName = po.GetAttributeValue<string>(Literals.PurchaseOrderV2.PurchaseOrderName),
                    CustomerReference = po.GetAttributeValue<string>(Literals.PurchaseOrderV2.VendorOrderReference),
                    Company = po.GetAttributeValue<string>(Literals.PurchaseOrderV2.CompanyCode)
                });
            }
            return results;
        }

        private static double ComputeVendorScore(string input, Entity po)
        {
            string acct = po.GetAttributeValue<string>(Literals.PurchaseOrderV2.OrderVendorAccountNumber) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(acct)) return 0.0;
            double jw = StringSimilarity.JaroWinkler(input, acct);
            double tok = StringSimilarity.TokenJaccard(input, acct);
            return Math.Max(jw, tok);
        }

        private static double ComputeCustomerRefScore(string input, Entity po)
        {
            string r = po.GetAttributeValue<string>(Literals.PurchaseOrderV2.VendorOrderReference) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(r)) return 0.0;
            double jw = StringSimilarity.JaroWinkler(input, r);
            double tok = StringSimilarity.TokenJaccard(input, r);
            return 0.5 * jw + 0.5 * tok;
        }

        private static string ToJsonArray(IList<ScoredPO> rows)
        {
            var sb = new StringBuilder(64 + rows.Count * 220);
            sb.Append('[');
            for (int i = 0; i < rows.Count; i++)
            {
                if (i > 0) sb.Append(',');
                var r = rows[i];
                sb.Append('{');
                AppendIntField(sb, Literals.CustomApi.POSearch.JsonField.Rank, r.Rank);                                       sb.Append(',');
                AppendStringField(sb, Literals.CustomApi.POSearch.JsonField.POId, r.POId.ToString("D"));                      sb.Append(',');
                AppendStringField(sb, Literals.CustomApi.POSearch.JsonField.PurchaseOrderNumber, r.PurchaseOrderNumber);      sb.Append(',');
                AppendStringField(sb, Literals.CustomApi.POSearch.JsonField.VendorAccount, r.VendorAccount);                  sb.Append(',');
                AppendStringField(sb, Literals.CustomApi.POSearch.JsonField.VendorName, r.VendorName);                        sb.Append(',');
                AppendStringField(sb, Literals.CustomApi.POSearch.JsonField.CustomerReference, r.CustomerReference);          sb.Append(',');
                AppendStringField(sb, Literals.CustomApi.POSearch.JsonField.Company, r.Company);
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

        private class ScoredPO
        {
            public int Rank { get; set; }
            public Guid POId { get; set; }
            public string PurchaseOrderNumber { get; set; }
            public string VendorAccount { get; set; }
            public string VendorName { get; set; }
            public string CustomerReference { get; set; }
            public string Company { get; set; }
        }
    }
}
