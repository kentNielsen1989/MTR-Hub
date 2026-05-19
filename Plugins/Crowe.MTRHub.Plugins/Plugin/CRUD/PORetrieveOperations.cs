using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace Crowe.MTRHub.Plugins.CRUD
{
    public class PORetrieveOperations
    {
        private readonly ServiceConnection _cnx;

        public PORetrieveOperations(ServiceConnection cnx)
        {
            _cnx = cnx;
        }

        private static ColumnSet DefaultCols => new ColumnSet(
            Literals.PurchaseOrderV2.Id,
            Literals.PurchaseOrderV2.PurchaseOrderNumber,
            Literals.PurchaseOrderV2.OrderVendorAccountNumber,
            Literals.PurchaseOrderV2.PurchaseOrderName,
            Literals.PurchaseOrderV2.VendorOrderReference,
            Literals.PurchaseOrderV2.CompanyCode);

        public List<Entity> GetCandidatePOs(string vendorNum, string customerRef)
        {
            var byId = new Dictionary<Guid, Entity>();
            var primary = TryContainsBranch(vendorNum, customerRef);
            if (primary.Count == 0)
            {
                _cnx.Trace.Trace("Contains branch returned 0 rows. Falling back to BeginsWith.");
                primary = TryBeginsWithBranch(vendorNum, customerRef);
            }
            if (primary.Count == 0)
            {
                _cnx.Trace.Trace("BeginsWith returned 0 rows. Falling back to unfiltered top {0}.",
                    Literals.MaxCandidatePullSize);
                primary = GetTopActive();
            }
            AccumulateById(byId, primary);
            return byId.Values.ToList();
        }

        private List<Entity> TryContainsBranch(string vendorNum, string customerRef)
        {
            var q = NewActiveQuery();
            var or = new FilterExpression(LogicalOperator.Or);
            if (!string.IsNullOrWhiteSpace(vendorNum))
                or.AddCondition(Literals.PurchaseOrderV2.OrderVendorAccountNumber,
                                ConditionOperator.Like, "%" + vendorNum + "%");
            if (!string.IsNullOrWhiteSpace(customerRef))
                or.AddCondition(Literals.PurchaseOrderV2.VendorOrderReference,
                                ConditionOperator.Like, "%" + customerRef + "%");
            if (or.Conditions.Count == 0) return new List<Entity>();
            q.Criteria.AddFilter(or);
            return SafeRetrieveMultiple(q, "POContains");
        }

        private List<Entity> TryBeginsWithBranch(string vendorNum, string customerRef)
        {
            var q = NewActiveQuery();
            var or = new FilterExpression(LogicalOperator.Or);
            if (!string.IsNullOrWhiteSpace(vendorNum))
            {
                string prefix = vendorNum.Length > 3 ? vendorNum.Substring(0, 3) : vendorNum;
                or.AddCondition(Literals.PurchaseOrderV2.OrderVendorAccountNumber,
                                ConditionOperator.BeginsWith, prefix);
            }
            if (!string.IsNullOrWhiteSpace(customerRef))
            {
                string prefix = customerRef.Length > 3 ? customerRef.Substring(0, 3) : customerRef;
                or.AddCondition(Literals.PurchaseOrderV2.VendorOrderReference,
                                ConditionOperator.BeginsWith, prefix);
            }
            if (or.Conditions.Count == 0) return new List<Entity>();
            q.Criteria.AddFilter(or);
            return SafeRetrieveMultiple(q, "POBeginsWith");
        }

        private List<Entity> GetTopActive()
        {
            var q = NewActiveQuery();
            q.AddOrder(Literals.PurchaseOrderV2.PurchaseOrderNumber, OrderType.Descending);
            return SafeRetrieveMultiple(q, "POUnfiltered");
        }

        private static QueryExpression NewActiveQuery()
        {
            var q = new QueryExpression(Literals.PurchaseOrderV2.EntityName)
            {
                ColumnSet = DefaultCols,
                TopCount = Literals.MaxCandidatePullSize,
                NoLock = true
            };
            q.Criteria = new FilterExpression(LogicalOperator.And);
            q.Criteria.AddCondition(Literals.PurchaseOrderV2.PurchaseOrderStatus,
                                    ConditionOperator.In,
                                    Literals.PurchaseOrderV2.StatusOpenOrder,
                                    Literals.PurchaseOrderV2.StatusReceived,
                                    Literals.PurchaseOrderV2.StatusInvoiced);
            return q;
        }

        private List<Entity> SafeRetrieveMultiple(QueryExpression q, string label)
        {
            try
            {
                var result = _cnx.Service.RetrieveMultiple(q);
                _cnx.Trace.Trace("POQuery[{0}] returned {1} rows.", label, result.Entities.Count);
                return result.Entities.ToList();
            }
            catch (Exception ex)
            {
                _cnx.Trace.Trace("POQuery[{0}] threw: {1}. Returning empty list.", label, ex.Message);
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
