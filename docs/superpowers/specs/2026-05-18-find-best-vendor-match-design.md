# Custom API: `crowe_FindBestVendorMatch` — Design

**Date:** 2026-05-18
**Author:** Kent Nielson (designed with Claude)
**Solution:** `MillTestReportPlugins` (publisher prefix `crowe`)
**Environment:** CMA (`cmacopilot.crm.dynamics.com`)

## Purpose

Dataverse Custom API + C# plugin that searches the F&O virtual table
`mserp_vendvendorv2entity` for the best-matching vendors given a free-text name
(required) and optional address / zip / topN inputs. Returns a ranked list
scored 0–100.

## Inputs

| Name      | Type    | Required | Default     |
|-----------|---------|----------|-------------|
| `Name`    | String  | yes      | —           |
| `Address` | String  | no       | null        |
| `Zip`     | String  | no       | null        |
| `TopN`    | Integer | no       | 10 (max 100)|

## Output

`Vendors` — EntityCollection of in-memory `Entity("crowe_vendormatch")` rows:

| Attribute       | Type   | Source                            |
|-----------------|--------|-----------------------------------|
| `rank`          | int    | computed match score 0–100        |
| `vendorid`      | Guid   | `mserp_vendvendorv2entityid`      |
| `vendorname`    | string | `mserp_vendororganizationname`    |
| `vendoraccount` | string | `mserp_vendoraccountnumber`       |
| `address`       | string | `mserp_formattedprimaryaddress`   |

Sorted by `rank` descending. Trimmed to `TopN`.

## Algorithm — weighted token + Jaro-Winkler hybrid

**Pre-filter** (cuts the virtual-table row set before in-memory scoring):

1. Tokenize input `Name`. Drop stopwords (`inc`, `llc`, `co`, `corp`, `the`, `and`, `ltd`, `limited`).
2. Take the longest 1–2 distinctive tokens.
3. QueryExpression OR filter on `mserp_vendvendorv2entity`:
   - `mserp_vendororganizationname` contains token1
   - OR `mserp_vendorsearchname` contains token1
4. Cap at 500 rows.
5. Fallback if 0 rows: retry with `StartsWith` on first 3 chars.
6. Second fallback if still 0: pull top-500 by name asc with no filter.
7. If `Zip` provided, run a second query branch on exact `mserp_addresszipcode == Zip` and union the candidate set.

**Scoring** per candidate, all sub-scores 0.0–1.0:

```
NameScore    = max( JaroWinkler(input.Name, organizationname),
                    JaroWinkler(input.Name, searchname),
                    TokenJaccard(input.Name, organizationname) )

AddressScore = if Address null → 0
               else 0.5 * JaroWinkler(Address, formattedaddress)
                  + 0.5 * TokenJaccard(Address, formattedaddress)

ZipScore     = if Zip null            → 0
               input.Zip == vendorZip → 1.0
               first-3 chars match    → 0.5
               else                   → 0.0
```

**Combined rank** (depends on which optional inputs were supplied):

| Inputs supplied         | Weights                                       |
|-------------------------|-----------------------------------------------|
| Name only               | rank = NameScore × 100                        |
| Name + Address          | 0.7 × Name + 0.3 × Address                    |
| Name + Zip              | 0.75 × Name + 0.25 × Zip                      |
| Name + Address + Zip    | 0.6 × Name + 0.25 × Address + 0.15 × Zip      |

Rounded to integer. Sorted desc. Top N returned.

## Components going into `MillTestReportPlugins`

1. Plugin assembly: `Crowe.MTRHub.Plugins`
2. Plugin type: `Crowe.MTRHub.Plugins.FindBestVendorMatchPlugin`
3. Custom API: `crowe_FindBestVendorMatch`
4. Four Custom API request parameters
5. One Custom API response property (EntityCollection)

## Diagnostic logging

`ITracingService.Trace` at:

- Entry, with input params (Name, Address?, Zip?, TopN)
- After pre-filter: candidate count + elapsed ms
- After scoring: top 3 ranks (sanity check)
- At exit: total elapsed ms

Tracing retained until user confirms feature is working.

## Known risk

`mserp_*` tables are virtual entities federated from F&O. Substring filters
on virtual tables sometimes have operator limits. The pre-filter sticks to
`Contains` / `StartsWith` / `Equals` only. The cascading fallback chain
(Contains → StartsWith → unfiltered top-500) covers the case where any
operator behaves unexpectedly. A smoke test against real CMA data is part
of the rollout plan.
