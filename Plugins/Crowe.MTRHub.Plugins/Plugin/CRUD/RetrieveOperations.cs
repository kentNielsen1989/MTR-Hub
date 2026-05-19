using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace Crowe.MTRHub.Plugins.CRUD
{
    public class RetrieveOperations
    {
        private readonly ServiceConnection _cnx;

        public RetrieveOperations(ServiceConnection cnx)
        {
            _cnx = cnx;
        }

        private static ColumnSet DefaultCols => new ColumnSet(
            Literals.VendorV2.Id,
            Literals.VendorV2.OrganizationName,
            Literals.VendorV2.SearchName,
            Literals.VendorV2.FormattedAddress,
            Literals.VendorV2.ZipCode,
            Literals.VendorV2.VendorAccount);

        public List<Entity> GetCandidateVendors(IList<string> nameTokens, string zip)
        {
            var byId = new Dictionary<Guid, Entity>();

            if (nameTokens != null && nameTokens.Count > 0)
            {
                var tokenCandidates = TryTokenContains(nameTokens);
                if (tokenCandidates.Count == 0)
                {
                    _cnx.Trace.Trace("Token-contains returned 0 rows. Falling back to StartsWith.");
                    tokenCandidates = TryTokenStartsWith(nameTokens);
                }
                if (tokenCandidates.Count == 0)
                {
                    _cnx.Trace.Trace("StartsWith returned 0 rows. Falling back to unfiltered top {0}.",
                        Literals.MaxCandidatePullSize);
                    tokenCandidates = GetTopUnfiltered();
                }
                AccumulateById(byId, tokenCandidates);
            }

            if (!string.IsNullOrWhiteSpace(zip))
            {
                var zipCandidates = GetByZipExact(zip);
                _cnx.Trace.Trace("Zip-exact branch returned {0} rows.", zipCandidates.Count);
                AccumulateById(byId, zipCandidates);
            }

            return byId.Values.ToList();
        }

        private List<Entity> TryTokenContains(IList<string> tokens)
        {
            var query = new QueryExpression(Literals.VendorV2.EntityName)
            {
                ColumnSet = DefaultCols,
                TopCount = Literals.MaxCandidatePullSize,
                NoLock = true
            };
            var orFilter = new FilterExpression(LogicalOperator.Or);
            foreach (var t in tokens)
            {
                var like = "%" + t + "%";
                orFilter.AddCondition(Literals.VendorV2.OrganizationName, ConditionOperator.Like, like);
                orFilter.AddCondition(Literals.VendorV2.SearchName, ConditionOperator.Like, like);
            }
            query.Criteria = orFilter;

            return SafeRetrieveMultiple(query, "TokenContains");
        }

        private List<Entity> TryTokenStartsWith(IList<string> tokens)
        {
            var query = new QueryExpression(Literals.VendorV2.EntityName)
            {
                ColumnSet = DefaultCols,
                TopCount = Literals.MaxCandidatePullSize,
                NoLock = true
            };
            var orFilter = new FilterExpression(LogicalOperator.Or);
            foreach (var t in tokens)
            {
                var prefix = t.Length > 3 ? t.Substring(0, 3) : t;
                orFilter.AddCondition(Literals.VendorV2.OrganizationName, ConditionOperator.BeginsWith, prefix);
                orFilter.AddCondition(Literals.VendorV2.SearchName, ConditionOperator.BeginsWith, prefix);
            }
            query.Criteria = orFilter;

            return SafeRetrieveMultiple(query, "TokenStartsWith");
        }

        private List<Entity> GetByZipExact(string zip)
        {
            var query = new QueryExpression(Literals.VendorV2.EntityName)
            {
                ColumnSet = DefaultCols,
                TopCount = Literals.MaxCandidatePullSize,
                NoLock = true
            };
            query.Criteria.AddCondition(Literals.VendorV2.ZipCode, ConditionOperator.Equal, zip);
            return SafeRetrieveMultiple(query, "ZipEqual");
        }

        private List<Entity> GetTopUnfiltered()
        {
            var query = new QueryExpression(Literals.VendorV2.EntityName)
            {
                ColumnSet = DefaultCols,
                TopCount = Literals.MaxCandidatePullSize,
                NoLock = true
            };
            query.AddOrder(Literals.VendorV2.OrganizationName, OrderType.Ascending);
            return SafeRetrieveMultiple(query, "Unfiltered");
        }

        private List<Entity> SafeRetrieveMultiple(QueryExpression query, string label)
        {
            try
            {
                var result = _cnx.Service.RetrieveMultiple(query);
                _cnx.Trace.Trace("Query[{0}] returned {1} rows.", label, result.Entities.Count);
                return result.Entities.ToList();
            }
            catch (Exception ex)
            {
                _cnx.Trace.Trace("Query[{0}] threw: {1}. Returning empty list.", label, ex.Message);
                return new List<Entity>();
            }
        }

        private static void AccumulateById(IDictionary<Guid, Entity> sink, IEnumerable<Entity> rows)
        {
            foreach (var row in rows)
            {
                if (row.Id != Guid.Empty && !sink.ContainsKey(row.Id))
                {
                    sink[row.Id] = row;
                }
            }
        }
    }
}
