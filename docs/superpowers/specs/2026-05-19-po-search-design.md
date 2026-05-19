# Custom API: `crowe_POSearch` — Design

**Date:** 2026-05-19
**Solution:** `MillTestReportPlugins` (publisher prefix `crowe`)
**Environment:** CMA (`cmacopilot.crm.dynamics.com`)
**Related:** [2026-05-18-find-best-vendor-match-design.md](2026-05-18-find-best-vendor-match-design.md) — same pattern, same assembly

## Purpose

Dataverse Custom API + C# plugin that searches the F&O virtual table
`mserp_purchpurchaseorderheaderv2entity` for the best-matching active purchase
orders given a vendor account number (required) and an optional customer
reference. Returns a JSON string of ranked matches scored 0–100. Intended for
matching incoming MTR (Mill Test Report) documents to existing POs.

## Inputs

| Name | Type | Required | Default |
|---|---|---|---|
| `VendorNum` | String | yes | — |
| `CustomerRef` | String | no | null |
| `TopN` | Integer | no | 10 (max 100) |

## Output

`POs` — String (JSON array), sorted desc by `rank`, trimmed to `TopN`:

```json
[
  {
    "rank": 100,
    "poid": "<mserp_purchpurchaseorderheaderv2entityid GUID>",
    "purchaseordernumber": "PO-001234",
    "vendoraccount": "10V00002",
    "vendorname": "Allegheny Technologies",
    "customerreference": "MTR-2024-789",
    "company": "USMF"
  }
]
```

`poid` is the real PO record GUID so callers can resolve back to the source.

## Field mapping

| JSON field | Dataverse column |
|---|---|
| `poid` | `mserp_purchpurchaseorderheaderv2entityid` |
| `purchaseordernumber` | `mserp_purchaseordernumber` |
| `vendoraccount` | `mserp_ordervendoraccountnumber` |
| `vendorname` | `mserp_purchaseordername` (F&O displays as "Vendor name") |
| `customerreference` | `mserp_vendororderreference` (F&O displays as "Customer reference") |
| `company` | `mserp_dataareaid` (Company Code, e.g. "USMF") |

## Active-PO filter

```
mserp_purchaseorderstatus IN (200000001, 200000002, 200000003)
```

| Value | Label | Included |
|---|---|---|
| 200000000 | None (draft) | excluded |
| 200000001 | Open order | included |
| 200000002 | Received | included |
| 200000003 | Invoiced | included |
| 200000004 | Canceled | excluded |

## Algorithm — weighted token + Jaro-Winkler hybrid

Mirrors the vendor search pattern.

**Pre-filter** (cuts the virtual-table row set before in-memory scoring):

1. `mserp_purchaseorderstatus` IN active set.
2. AND (`mserp_ordervendoraccountnumber` contains `VendorNum`
        OR — if `CustomerRef` is provided — `mserp_vendororderreference` contains `CustomerRef`).
3. Cap at 500 rows.
4. Fallback chain if 0 rows: Contains → BeginsWith → unfiltered top-500 (active filter retained).

**Scoring** per candidate, all sub-scores 0.0–1.0:

```
VendorScore      = max( JaroWinkler(VendorNum, po.vendoraccount),
                        TokenJaccard(VendorNum, po.vendoraccount) )

CustomerRefScore = if CustomerRef null → 0
                   else 0.5 * JaroWinkler(CustomerRef, po.vendororderreference)
                      + 0.5 * TokenJaccard(CustomerRef, po.vendororderreference)
```

**Combined rank:**

| Inputs supplied | Weights |
|---|---|
| VendorNum only | `rank = VendorScore × 100` |
| VendorNum + CustomerRef | `(0.5 × VendorScore + 0.5 × CustomerRefScore) × 100` |

Rounded to integer. Sorted desc, tie-broken by `mserp_purchaseordernumber` desc.

## Components going into `MillTestReportPlugins`

1. Updated plugin assembly `Crowe.MTRHub.Plugins` (now contains 2 plugin types)
2. New plugin type: `Crowe.MTRHub.Plugins.POSearchPlugin`
3. New Custom API: `crowe_POSearch`
4. Three Custom API request parameters: `VendorNum`, `CustomerRef`, `TopN`
5. One Custom API response property: `POs` (String / JSON)

Custom API config (creation-only fields per the lessons learned on vendor search):
- `bindingtype = 0` (Global)
- `isfunction = false` (Action)
- `isprivate = false`
- `workflowsdkstepenabled = true` (Power Automate visibility)
- `allowedcustomprocessingsteptype = 0` (None — matches the visible-in-Power-CAT pattern)

## Code layout (additions to the existing project)

```
Plugins/Crowe.MTRHub.Plugins/Plugin/
  POSearchPlugin.cs              (NEW — thin entry point)
  Literals.cs                    (EXTENDED — adds PurchaseOrderV2 + CustomApi.POSearch)
  CRUD/
    PORetrieveOperations.cs      (NEW — mirrors RetrieveOperations for POs)
  Helpers/
    POSearchHelper.cs            (NEW — mirrors VendorMatchHelper)
  Infrastructure/
    ServiceContainer.cs          (EXTENDED — adds PORetrieve lazy property)
```

`StringSimilarity.cs` is reused as-is — no new algorithm work.

## Diagnostic tracing

`ITracingService.Trace` at:
- Entry, with masked inputs (VendorNum, CustomerRef?, TopN)
- After pre-filter: candidate count + elapsed ms
- After scoring: top 3 ranks (sanity check)
- At exit: total elapsed ms + result count

## Known risks (same as vendor search)

`mserp_*` tables are virtual entities federated from F&O. Substring filters on
virtual tables sometimes have operator limits. The pre-filter sticks to
`Contains` / `BeginsWith` / `Equals` only. Cascading fallback chain covers
operator failures. Smoke test against real CMA data is part of rollout.
