# Project Instructions

## User Customizations

**This section is yours. Anything you write here is preserved across plugin updates.**

Use this section for project-specific guidance, rules, conventions, or notes that should apply to this project only. Content below this section (starting with `## Dataverse MCP`) is managed by the crowe-ppce plugin and will be regenerated when the plugin updates — do not customize those sections, your edits will be overwritten.

If you want to override or extend guidance from a managed section, do it here with wording like "For this project, when working with Dataverse forms, also..." — this section has priority because it's closer to the top and is your project-specific context.

_(Add your project notes below this line)_

---

## Dataverse MCP

This project has the CroweAT Dataverse MCP server configured. It provides 167 tools for managing Microsoft Dataverse environments — tables, columns, forms, views, security roles, flows, apps, solutions, web resources, command bar, plugins, Power Pages, option sets, data migration, record merge, and more.

All tools are prefixed with `dataverse_`. Always use `environment_name` to target an environment (call `dataverse_list_environments` to see available options).

### Connecting to Environments

```
# See all available environments
dataverse_list_environments

# All tools accept environment_name — just pass the name from the list above
dataverse_list_tables  environment_name: "Sandbox_AI-PoCs"
```

### Authentication (REQUIRED before using tools)

The first time you target an environment, run the auth flow. `dataverse_authenticate` branches on the env's `requireUserAuth` flag:

**Case A — env requires user auth (most client and production envs):**

1. Call `dataverse_authenticate` with the `environment_name`
2. The tool returns a sign-in URL — tell the user to open it in their browser and sign in with their Microsoft account
3. After the user confirms they signed in, call `dataverse_verify_auth` with the `auth_request_id`
4. If the response status is `pending`, the user hasn't finished yet — wait and retry `dataverse_verify_auth`
5. On success, store the result in `.dataverse-auth.json` in the project root:

```json
{
  "environment-name-here": {
    "aad_object_id": "the-returned-aad-object-id",
    "email": "user@example.com",
    "display_name": "User Name",
    "verified_at": "2026-01-01T00:00:00Z"
  }
}
```

**Case B — env has user auth disabled (`requireUserAuth=false`, e.g., some client tenants where the MCP runs SP-only):**

`dataverse_authenticate` returns `status: "not_required"` without a sign-in URL. To enable the MCP permission-layer enforcement (which keys on email), prompt the user directly:

> *"This environment doesn't require a Microsoft sign-in through the MCP. What email do you use to access this environment? I'll store it locally so your permission-layer access can be enforced."*

Store the email in `.dataverse-auth.json` under the env's entry:

```json
{
  "environment-name-here": {
    "email": "user@example.com",
    "auth_mode": "manual",
    "captured_at": "2026-01-01T00:00:00Z"
  }
}
```

This is trust-based — the user could type any email. The permission layer defaults to allow when no matching record exists, so a wrong/fake email just gives default access. For graduation to verified identity, the env's orchestrator record would need `requireUserAuth=true`.

### Per-Tool-Call Injection

**On every tool call**, read `.dataverse-auth.json` for the target `environment_name` and include both fields from the stored entry:

- `user_aad_object_id` — from the stored entry (omit if only `email` was captured via Case B)
- `user_email` — from the stored entry (always include if present)

If the file doesn't exist or has no entry for the target environment, run the appropriate auth flow above first.

**Per-environment auth:** Each environment is authenticated independently. Signing in for one environment does NOT authenticate you for others. Users may sign in with different Microsoft accounts for different environments.

### Tool Selection Guide — When to Use What

#### Single vs Bulk Operations

| Scenario | Use This | NOT This |
|----------|----------|----------|
| Creating 1-3 tables | `dataverse_create_table` (one at a time) | `bulk_create_tables` |
| Creating 4+ tables at once | `dataverse_bulk_create_tables` (returns immediately with operation_id, poll with `check_bulk_operation`) | Multiple `create_table` calls |
| Creating 4+ columns on one table | `dataverse_bulk_create_columns` (returns immediately with operation_id, poll with `check_bulk_operation`) | Multiple `create_column` calls |
| Creating a form with known layout | `dataverse_build_form` (tabs + sections + fields in one call) | `create_form` + `add_form_tab` + `add_form_fields` separately |
| Adding fields to an existing form | `dataverse_add_form_fields` | `build_form` (that creates a NEW form) |
| Creating/updating 4+ records | `dataverse_batch_operations` (max 1000, can be transactional) | Multiple `create_record` calls |
| Creating 1-3 records | `dataverse_create_record` | `batch_operations` (overkill) |

#### Reading vs Querying

| Scenario | Use This |
|----------|----------|
| Get one record by GUID | `dataverse_get_record` with `select` and optional `expand` |
| Find records by filter | `dataverse_query_records` with `filter`, `select`, `orderby`, `top` |
| Complex queries (aggregates, joins) | `dataverse_query_fetchxml` with raw FetchXML |

#### Lookup Columns (IMPORTANT)

You CANNOT create a lookup column directly with `create_column`. Lookups are created by creating a relationship:
- `dataverse_create_one_to_many` — creates the relationship AND the lookup column together
- `dataverse_create_many_to_many` — creates an N:N relationship with an intersect table

#### Forms: Build vs Modify

- `dataverse_build_form` — Creates a COMPLETE new form from scratch (tabs, sections, fields all at once). Use for initial form creation.
- `dataverse_create_form` + `add_form_tab` + `add_form_section` + `add_form_fields` — Use when modifying or incrementally building on an existing form.
- Each field can only appear ONCE per form — `add_form_fields` auto-skips duplicates.

### Required Ordering

Dataverse has dependencies. Follow this order:

1. **Publisher** — `create_publisher` (if needed — usually reuse existing)
2. **Solution** — `create_solution` (linked to publisher)
3. **Tables** — `create_table` or `bulk_create_tables` (pass `solution_name`)
4. **Columns** — `create_column` or `bulk_create_columns` (after table exists)
5. **Relationships** — `create_one_to_many` / `create_many_to_many` (after both tables exist)
6. **Global Choices** — `create_global_choice` (before columns that reference them)
7. **Views** — `create_view` (after columns exist — view references column names)
8. **Forms** — `build_form` or `create_form` (after columns exist — form references column names)
9. **App** — `create_app` with `tables` param (after tables, forms, views exist)
10. **Security Roles** — `create_security_role` + `add_role_privilege` (anytime)
11. **Publish** — Most tools auto-publish, but run `publish_all` after bulk schema changes

### Publisher and Solution — NEVER Assume

When creating ANY schema component (tables, columns, relationships, choices, etc.) for the first time in a conversation targeting an environment you have not worked in yet:

1. **Ask the user which publisher prefix to use** — do NOT assume or guess. The default Microsoft-generated publisher (prefix 'cr' + random chars) is almost never correct. Projects use specific publisher prefixes (e.g., 'contoso_', 'myclient_') that determine all schema name prefixes.
2. **Ask which solution to add components to** — or whether to create a new one. Use `dataverse_list_publishers` and `dataverse_list_solutions` to show the user what already exists in the environment.
3. **Always pass `solution_name`** on every create operation — tables, columns, relationships, views, forms, web resources, etc. Components created without a solution context end up in the Default Solution and are hard to manage.

Once the user confirms the publisher/solution for a given environment in the conversation, reuse it for all subsequent operations without re-asking.

### Key Constraints and Gotchas

- **Schema names are immutable** — once a table or column is created, its logical name cannot be changed. Choose carefully.
- **Column types are immutable** — you cannot change a String column to an Integer after creation. Must delete and recreate.
- **Always pass `solution_name`** when creating schema components (tables, columns, relationships) to ensure they're solution-aware.
- **Views need both columns AND filters defined** — a view without columns shows nothing. Use `create_view` with `columns` array.
- **Only Public views (querytype=0) can be created/deleted** — QuickFind, Lookup, and Associated views are system-managed.
- **Cannot delete the default view** — set another view as default first via `update_view` with `is_default: true`.
- **Cloud flows don't need publishing** — state changes (activate/deactivate) take effect immediately. Unlike forms/views.
- **Flow deletion requires Draft state** — deactivate a flow before deleting it.
- **System users cannot be created/deleted** — they're provisioned from Azure AD. You can only read them and assign/remove roles.

### Development Best Practices

#### Always Verify Field Names Before Use

Dataverse field logical names are frequently non-obvious (e.g., `prefix_chargecode` not `prefix_code`, `quotestatecode` not `statecode` on `quotedetail`). **Never guess field names** based on display names or assumptions. Before referencing any field in code, forms, views, or queries:

- Call `dataverse_list_columns(table_name)` or `dataverse_get_column(table_name, column_name)` to verify the exact logical name
- For option set/choice fields, always query the actual integer values — never assume them
- For lookup fields, verify the relationship exists via `dataverse_list_relationships(table_name)`

#### Forms & Views After Schema Changes

When creating new tables, ensure the default views (Active, Inactive, Quick Find, Lookup) have meaningful columns — not just the auto-generated primary name column. When adding new fields to existing tables, evaluate whether they should be added to relevant views and forms. Don't leave users with views that show only a Name column when there are useful fields available.

- **Update existing system-generated views** (Active, Inactive, etc.) — do not create duplicates
- **Quick Find view**: set the search columns to the name field plus key lookup fields
- **Lookup view**: keep it concise — name plus 1-2 identifying fields
- **Main form**: ensure new fields are placed in logical tab/section groupings

#### Diagnostic Logging

When creating or modifying web resources or plugins, include diagnostic logging until the feature is verified working:

- **JavaScript web resources**: Use `console.log` / `console.error` with a `[FeatureName]` prefix at initialization, API calls, responses, and error paths
- **C# plugins**: Use `ITracingService.Trace(...)` at method entry, key decision points, and method exit. Include elapsed time for performance-sensitive operations
- Do NOT remove diagnostic logging until the user confirms the feature is working correctly

### Universal Operation Pattern

Every Dataverse operation follows this decision framework:

1. **Do you already have current metadata for the component(s) you will touch?** If no, retrieve it (`list_columns`, `get_form`, `list_relationships`, etc.). If yes (already fetched in this conversation), skip the retrieval — do not re-fetch unnecessarily.
2. **Does the component already exist?** Before creating tables, columns, views, forms, flows, web resources, or security roles, check if they already exist in the target solution. Update existing components — do not recreate them.
3. **After modification:** Use targeted publish (`publish_components`) on the specific components you touched. Only use `publish_all` when you changed 4+ distinct components simultaneously. Cloud flows do not need publishing — state changes take effect immediately.

**The principle: never guess metadata, but never re-fetch what you already know.**

### Destructive Operations — ALWAYS Confirm First

Before executing ANY operation that deletes, removes, or overwrites a Dataverse component, you MUST get explicit user confirmation. **Create and update operations are NOT affected by this rule** — only destructive actions that can cause data or component loss.

**Tools that require confirmation:**

- **Delete tools:** `delete_table`, `delete_column`, `delete_record`, `delete_view`, `delete_form`, `delete_flow`, `delete_web_resource`, `delete_relationship`, `delete_global_choice`, `delete_choice_option`, `delete_security_role`, `delete_bpf`, `delete_ribbon_command`, `delete_env_variable`, `delete_plugin_assembly`, `delete_plugin_step`, `delete_plugin_step_image`
- **Remove tools:** `remove_form_field`, `remove_view_column`, `remove_app_component`, `remove_form_event`, `remove_role_privilege`, `remove_security_role`, `remove_team_member`
- **Overwrite-risk tools:** `update_sitemap` (replaces full XML), `import_solution` (can overwrite forms, views, sitemaps, security roles), `update_form` with full formxml replacement, `update_web_resource` (replaces content), `batch_operations` containing Delete actions

**Before confirming with the user, state clearly:**

1. The exact component being affected (name, ID, table it belongs to)
2. What will be changed or lost — and if the component is something other components may depend on (e.g., "this table has 3 lookup relationships pointing to it", "this is the active sitemap for the app", "this view is referenced by an entity list")
3. Whether the operation is reversible or permanent

**NEVER auto-chain destructive operations.** If troubleshooting leads you to believe something should be deleted or removed, STOP and present your reasoning to the user. Do not delete as part of a multi-step fix without explicit per-step approval.

**Solution imports deserve extra caution** — they can overwrite sitemaps, forms, views, and security roles in the target environment. Always `export_solution` the current version as a backup before importing.

### All 167 Tools by Category

**Environment & Auth (4):** `list_environments`, `test_connection`, `authenticate`, `verify_auth`

**Schema (24):** `list_tables`, `get_table`, `create_table`, `update_table`, `delete_table`, `list_columns`, `get_column`, `create_column`, `update_column`, `delete_column`, `list_relationships`, `create_one_to_many`, `create_many_to_many`, `delete_relationship`, `list_global_choices`, `create_global_choice`, `get_global_choice`, `delete_global_choice`, `add_choice_option`, `update_choice_option`, `delete_choice_option`, `publish_all`, `query_fetchxml`

**Bulk (4):** `bulk_create_tables`, `bulk_create_columns`, `check_bulk_operation`, `build_form` — bulk table/column creation uses Durable Functions (returns operation_id immediately, poll with `check_bulk_operation` for progress)

**Records (8):** `create_record`, `get_record`, `update_record`, `upsert_record`, `delete_record`, `associate_records`, `query_records`, `batch_operations`

**Data Migration (2):** `analyze_migration`, `migrate_table` — cross-environment data migration with GUID preservation, dependency ordering, date preservation, and state transitions via Durable Functions

**Record Merge (2):** `preview_merge`, `merge_records` — duplicate record deduplication with side-by-side comparison, master recommendation, and Dataverse Merge action (account, contact, lead, incident)

**Security (14):** `list_security_roles`, `get_role_privileges`, `create_security_role`, `delete_security_role`, `add_role_privilege`, `remove_role_privilege`, `assign_security_role`, `remove_security_role`, `list_users`, `get_user`, `list_teams`, `create_team`, `add_team_member`, `remove_team_member`

**Solutions (8):** `list_publishers`, `create_publisher`, `list_solutions`, `get_solution`, `create_solution`, `add_solution_component`, `export_solution`, `import_solution`

**Forms (10):** `list_forms`, `get_form`, `create_form`, `update_form`, `delete_form`, `add_form_tab`, `add_form_section`, `add_form_fields`, `remove_form_field`, `update_form_field`

**Views (8):** `list_views`, `get_view`, `create_view`, `update_view`, `delete_view`, `add_view_columns`, `remove_view_column`, `deactivate_view`

**Apps (9):** `list_apps`, `get_app`, `create_app`, `update_app`, `add_app_component`, `remove_app_component`, `get_sitemap`, `update_sitemap`, `publish_app`

**Web Resources (9):** `list_web_resources`, `get_web_resource`, `create_web_resource`, `update_web_resource`, `delete_web_resource`, `add_form_event`, `remove_form_event`, `list_form_events`, `add_form_web_resource`

**Command Bar (7):** `list_ribbon_commands`, `get_ribbon_command`, `create_ribbon_command`, `update_ribbon_command`, `delete_ribbon_command`, `hide_ribbon_command`, `get_ribbon_xml`

**Flows (10):** `list_flows`, `get_flow`, `create_flow`, `update_flow`, `delete_flow`, `activate_flow`, `deactivate_flow`, `get_flow_connections`, `share_flow`, `cancel_flow_runs`

**Connection Refs (4):** `list_connection_references`, `get_connection_reference`, `create_connection_reference`, `update_connection_reference`

**Env Variables (5):** `list_env_variables`, `get_env_variable`, `create_env_variable`, `update_env_variable`, `delete_env_variable`

**Flow Runs (3):** `list_flow_runs`, `get_flow_run`, `resubmit_flow_run`

**Business Rules (4):** `list_business_rules`, `get_business_rule`, `activate_business_rule`, `deactivate_business_rule`

**BPFs (6):** `list_bpfs`, `get_bpf`, `create_bpf`, `activate_bpf`, `deactivate_bpf`, `delete_bpf`

**Plugins (14):** `list_plugin_assemblies`, `get_plugin_assembly`, `register_plugin_assembly`, `update_plugin_assembly`, `delete_plugin_assembly`, `list_plugin_steps`, `get_plugin_step`, `register_plugin_step`, `update_plugin_step`, `enable_plugin_step`, `disable_plugin_step`, `delete_plugin_step`, `add_plugin_step_image`, `delete_plugin_step_image`

**Power Pages (2):** `list_power_pages_sites`, `get_power_pages_site`

**Targeted Publish (1):** `publish_components`

---

## OOB Entity Awareness

Before creating ANY custom table, check if a standard Dataverse table serves the purpose:

| OOB Table | Purpose | Use Instead Of Custom... |
|-----------|---------|--------------------------|
| `account` | Companies, organizations, vendors | "Company", "Organization", "Vendor" tables |
| `contact` | People (customers, partners, employees) | "Person", "Customer", "Client" tables |
| `incident` | Cases, tickets, service requests | "Case", "Ticket", "Issue", "Request" tables |
| `opportunity` | Deals, sales opportunities | "Deal", "Proposal", "Quote" tables |
| `lead` | Prospects, inquiries | "Prospect", "Inquiry" tables |
| `task`, `appointment`, `phonecall`, `email` | Activities (polymorphic activity party) | Custom activity-like tables |
| `systemuser` | Internal users (CANNOT be created via API — provisioned from Azure AD) | Custom "Employee" or "Staff" tables for users |
| `team`, `businessunit` | Organizational structure and ownership | Custom grouping tables |

**Rule:** If the user asks for a "Company" table, clarify whether `account` should be used. If they ask for a "Ticket" table, clarify whether `incident` fits. If OOB fits, use it — extend with custom columns rather than recreating the entity. OOB tables come with built-in relationships, views, forms, and platform integrations that custom tables lack.

## Platform Constraints Quick Reference

These are hard platform limitations — not best practices, but things that cannot be done via API or MCP tools:

- **Business rules:** Cannot be created via API (error 0x80045037 UIDataGenerationFailed). MCP can only list, activate, deactivate, and delete existing business rules. Must be created through the Dataverse UI.
- **Lookup columns:** Cannot be created directly with `dataverse_create_column`. Lookups are created as a side-effect of creating a relationship via `dataverse_create_one_to_many`. Always use the relationship tool.
- **Schema names are immutable:** Once a table or column is created, its logical name CANNOT be changed. The display name can be updated, but the schema name is permanent. Choose names carefully.
- **Column types are immutable:** You cannot change a String column to an Integer (or any other type change) after creation. Must delete and recreate the column (which loses all data in that column).
- **System users cannot be created or deleted:** Users are provisioned from Azure AD only. MCP can list users, get user details, and assign/remove security roles — but cannot create or delete user records.
- **Default views cannot be deleted:** Every table has a default Active view. You cannot delete it. To change which view is default, use `dataverse_update_view` with `is_default: true` on another view first.
- **Only Public views can be created/deleted:** QuickFind (querytype=4), Lookup (querytype=64), and Associated (querytype=2) views are system-managed. Only Public views (querytype=0) can be created or deleted via API.
- **Flow deletion requires Draft state:** You must deactivate a flow (`dataverse_deactivate_flow`) before deleting it. Attempting to delete an active flow will fail.
- **BPF uniquename must include publisher prefix:** Business Process Flow unique names that omit the publisher prefix will fail silently on activation.
- **Plugin types are not auto-discovered:** When calling `dataverse_register_plugin_assembly`, you MUST provide an explicit `plugin_types` array listing every plugin class. The tool does not scan the assembly.
- **Forms: one field per form:** Each field can appear only once on a form. `dataverse_add_form_fields` auto-skips duplicates, but be aware when designing form layouts.

## Cross-Cutting Concern Triggers

When you modify one Dataverse component, other components may be affected. Check these:

**When you modify or delete a column:**
- Views displaying this column (may need column removed/updated)
- Forms showing this column (field may need repositioning or removal)
- Cloud flows that trigger on or reference this column in conditions/actions
- Plugins with filtering attributes that include this column
- Web resources (JS) that reference this column by logical name
- Business rules that use this column in conditions or actions

**When you modify a form:**
- Web resources registered as form libraries (event handlers may reference changed fields)
- Business rules bound to this form
- Subgrids and quick view forms embedded in this form (verify their data sources still exist)
- PCF controls bound to fields on this form

**When you modify or delete a table:**
- All relationships (1:N, N:N) pointing to or from this table
- Views, forms, dashboards, and the app sitemap referencing this table
- Power Pages entity lists and entity forms configured for this table
- Cloud flows triggered by this table's records
- Plugins registered on this table's messages

**When you delete anything:**
- ALWAYS list what depends on the component FIRST
- Present the dependency list to the user before proceeding
- See the Destructive Operation Guardrails section for the full confirmation protocol

## Azure DevOps MCP

This project also has the CroweAT Azure DevOps MCP server configured. It provides 97 tools for managing Azure DevOps — work items, repos, pull requests, pipelines, wikis, test plans, test execution, iterations, capacity, and more.

All tools are prefixed with ADO domain names (e.g., `wit_`, `repo_`, `pipelines_`, `wiki_`, `work_`, `testplan_`, `search_`, `advsec_`). Auth tools: `ado_authenticate`, `ado_list_environments`.

### Connecting to ADO Environments

```
# See all available ADO environments (each maps to an org + project)
ado_list_environments

# All tools accept environment_name + ado_pat
wit_query_work_items  environment_name: "ADO MCP Demo"  ado_pat: "your-pat"
```

### Authentication (REQUIRED before using ADO tools)

ADO uses Personal Access Tokens (PATs) for authentication. Each user provides their own PAT per ADO organization.

1. Call `ado_list_environments` to see available ADO environments
2. Ask the user for their PAT for the target organization
   - They can create one at `https://dev.azure.com/{org}/_usersSettings/tokens`
   - Recommended scopes: Work Items (Read/Write), Code (Read/Write), Build (Read), Project and Team (Read)
3. Call `ado_authenticate` with `environment_name` and `ado_pat` to validate
4. On success, store the result in `.ado-auth.json` in the project root:

```json
{
  "ADO MCP Demo": {
    "org_url": "https://dev.azure.com/SP-ADO-MCP-Demo",
    "project": "ADO MCP Demo",
    "pat": "the-validated-pat",
    "user_display_name": "User Name",
    "authenticated_at": "2026-01-01T00:00:00Z"
  }
}
```

**On every subsequent ADO tool call**, read `.ado-auth.json` and include `environment_name` + `ado_pat` from the stored entry. If the file doesn't exist or has no entry for the target environment, run the authentication flow first.

**Per-org PATs:** A PAT is scoped to an ADO organization. If multiple environments share the same org URL, the same PAT works for all of them. `ado_authenticate` returns an `also_valid_for` list showing other environments the PAT covers.

**Project resolution:** Each ADO environment in the orchestrator maps to a specific project. You do NOT need to pass `project` separately — it comes from the orchestrator config. The `project` parameter is available as an override for cross-project queries.

### All 97 ADO Tools by Category

**Auth & Orchestrator (2):** `ado_list_environments`, `ado_authenticate`

**Core (2):** `core_list_projects`, `core_list_project_teams`

**Work Items (14):** `wit_create_work_item`, `wit_create_work_items_batch`, `wit_get_work_item`, `wit_get_work_items_batch_by_ids`, `wit_update_work_item`, `wit_query_work_items`, `wit_my_work_items`, `wit_get_work_item_type`, `wit_link_work_items`, `wit_add_work_item_comment`, `wit_list_work_item_comments`, `wit_list_work_item_revisions`, `wit_get_work_items_for_iteration`, `wit_get_query`, `wit_get_query_results_by_id`

**Backlogs & Iterations (8):** `wit_list_backlogs`, `wit_list_backlog_work_items`, `work_list_iterations`, `work_list_team_iterations`, `work_create_iterations`, `work_assign_iterations`, `work_get_team_capacity`, `work_update_team_capacity`, `work_get_iteration_capacities`

**Repos (17):** `repo_list_repos_by_project`, `repo_create_repository`, `repo_get_repo_by_name_or_id`, `repo_list_branches_by_repo`, `repo_list_my_branches_by_repo`, `repo_get_branch_by_name`, `repo_create_branch`, `repo_list_refs`, `repo_list_tags`, `repo_list_forks`, `repo_get_commit`, `repo_search_commits`, `repo_get_file_content`, `repo_list_pull_requests_by_repo_or_project`, `repo_get_pull_request_by_id`, `repo_create_pull_request`, `repo_update_pull_request`

**PR Threads (5):** `repo_list_pull_request_threads`, `repo_list_pull_request_thread_comments`, `repo_create_pull_request_thread`, `repo_update_pull_request_thread`, `repo_reply_to_comment`, `repo_update_pull_request_reviewers`

**Pipelines (12):** `pipelines_list`, `pipelines_run`, `pipelines_get_run`, `pipelines_list_runs`, `pipelines_list_artifacts`, `pipelines_create_pipeline`, `pipelines_get_builds`, `pipelines_get_build_status`, `pipelines_get_build_changes`, `pipelines_get_build_definitions`, `pipelines_get_build_definition_revisions`, `pipelines_get_build_log`, `pipelines_get_build_log_by_id`, `pipelines_update_build_stage`

**Wikis (5):** `wiki_list_wikis`, `wiki_get_wiki`, `wiki_list_pages`, `wiki_get_page`, `wiki_get_page_content`, `wiki_create_or_update_page`

**Test Plans & Execution (15):** `testplan_list_test_plans`, `testplan_create_test_plan`, `testplan_list_test_suites`, `testplan_create_test_suite`, `testplan_list_test_cases`, `testplan_create_test_case`, `testplan_add_test_cases_to_suite`, `testplan_update_test_case_steps`, `testplan_show_test_results_from_build_id`, `testplan_get_test_points`, `testplan_create_test_run`, `testplan_add_test_results`, `testplan_complete_test_run`, `testplan_add_result_attachment`, `testplan_set_automation_status`

**Search (3):** `search_code`, `search_wiki`, `search_workitem`

**Advanced Security (2):** `advsec_get_alerts`, `advsec_get_alert_details`

---

## Playwright MCP (Automated Browser Testing)

This project has the Microsoft Playwright MCP server configured for automated browser testing. It provides 59+ tools for navigating web pages, clicking elements, filling forms, taking screenshots, and verifying content.

### IMPORTANT: When to Use Playwright

**DO NOT** use Playwright MCP tools just because the user mentions 'testing' or 'test'. In normal development, 'test this' or 'verify this works' means validating via MCP API calls (Dataverse queries, checking records, etc.) — NOT opening a browser.

**ONLY use Playwright** when the user explicitly asks for:
- Browser-based UI testing or automation
- The `/test-app` skill
- Playwright test scripts or specs
- Visual verification of a form, page, or PCF control in a browser
- Power Pages site testing

For all other testing/validation (did this record create correctly? does this view have the right columns? is this flow active?) — use the Dataverse MCP and ADO MCP tools directly. Those are API calls, not browser automation.

### Overview

Use the `/test-app` skill to orchestrate automated browser testing of Dataverse Model-Driven Apps and Power Pages sites. The skill reads ADO test cases, generates Playwright test scripts, executes them (interactive or headless), and reports results back to ADO test plans.

### Key Capabilities

- **Interactive mode:** Agent drives the browser via Playwright MCP tools while QA watches
- **Suite mode:** `npx playwright test` runs headless, results parsed and reported to ADO
- **Auth:** Test user credentials stored in Key Vault (same KV as orchestrator), Entra ID login automated via storageState
- **Evidence:** Screenshots captured and attached to ADO test results
- **Traceability:** Test specs tagged with `@[testCaseId]`, linked to User Stories via TestedBy, results in ADO test runs

### Test Project Structure

Each ADO project gets a `{project}-tests` repo with:
- `playwright.config.ts` — environment-aware config with storageState auth
- `tests/auth.setup.ts` — Entra ID login setup project
- `pages/base.page.ts` — Page Object Model with formContext/Xrm helpers
- `tests/{area}/*.spec.ts` — test specs organized by area (forms, pcf, views, security, etc.)

### Prerequisites

- Chromium browser: `npx playwright install chromium` (one-time setup)
- ADO Test Plans requires 'Basic + Test Plans' access level on the ADO user
- Test user credentials in Key Vault: `test-user-{env-slug}-{role}-email` and `test-user-{env-slug}-{role}-password`
- Passwords with special characters (like #) must be quoted in .env files

### Common Playwright MCP Tools

**Navigation:** `browser_navigate`, `browser_navigate_back`, `browser_tabs`

**Interaction:** `browser_click`, `browser_type`, `browser_fill_form`, `browser_select_option`, `browser_hover`, `browser_press_key`

**Verification:** `browser_snapshot`, `browser_verify_text_visible`, `browser_verify_element_visible`, `browser_verify_value`, `browser_evaluate`

**Evidence:** `browser_take_screenshot`, `browser_console_messages`, `browser_network_requests`

**Session:** `browser_storage_state`, `browser_set_storage_state`, `browser_close`

## Graph MCP — Environment Knowledge Graph

The Graph MCP provides 10 tools for building and querying a dependency graph of any Dataverse environment. A single crawl captures the full metadata landscape — tables, columns, relationships, forms, views, flows, plugins, security roles, Power Pages, apps, solutions — and builds a connected knowledge graph.

All tools are prefixed with `graph_`. Use `environment_name` to target an environment.

### Why Use the Graph

The graph replaces the discovery phase (10-15 individual list/query calls) at the start of any task. Instead of querying tables, columns, forms, views, flows, and relationships separately, read the local graph file to understand the full dependency landscape instantly.

**Use the graph for:** understanding what exists, finding dependencies, impact analysis before changes, discovering what touches a table.
**Still use Dataverse MCP for:** full metadata details, creating/modifying components, reading record data, anything that needs live state.

### First-Time Setup

1. Call `graph_crawl` with the `environment_name` — this takes ~2 minutes
2. Poll with `graph_crawl_status` using the returned `operation_id` until status is `completed`
3. Call `graph_download_url` to get a temporary download URL, then save locally:

```bash
mkdir -p .claude/graphs
curl -o ".claude/graphs/{environment_name}.json" "{download_url}"
```

4. Subsequent `graph_query`/`graph_search`/`graph_impact` calls are instant
5. Local graph file enables offline reference and visualization generation

### Key Tools

| Tool | Purpose |
|------|---------|
| `graph_crawl` | Build/rebuild the environment graph (async — returns operation_id) |
| `graph_crawl_status` | Poll crawl progress |
| `graph_status` | Check if a graph exists and its age |
| `graph_query` | BFS traversal from a node — see what's connected within N hops |
| `graph_search` | Keyword search across all node names and labels |
| `graph_impact` | Trace upstream/downstream dependency chains from a node |
| `graph_summary` | Environment-wide stats, hotspot nodes, most connected tables |
| `graph_path` | Shortest path between two nodes |
| `graph_download_url` | Get a temporary URL to download the graph JSON locally |
| `graph_visualize` | Generate interactive vis.js HTML for a subgraph |
| `graph_erd` | Generate an ERD diagram for specific tables |

### Local Graph Cache

After a crawl completes, the graph JSON can be downloaded to `.claude/graphs/{environment_name}.json` for offline reference and visualization. The visualization templates in `.claude/graphs/` generate interactive HTML:

```
node .claude/graphs/generate-viz.mjs <environment_name> [focus_table]
```

### When to Re-Crawl

Re-crawl when significant schema changes have been made (new tables, major form restructuring, new flows). For day-to-day column additions or minor edits, the existing graph is sufficient.

## Azure AI Search MCP

The AI Search MCP provides 14 tools for searching and managing Azure AI Search indexes connected to SharePoint document libraries. Multi-environment support via orchestrator — each SharePoint site/folder gets its own index with automated provisioning.

All tools are prefixed with `search_`. Use `environment_name` to target a specific SharePoint index.

### Connecting to Search Environments

```
# See all available SharePoint search environments
search_list_environments

# Validate access to an environment
search_authenticate(environment_name: 'MySPSite')
```

### Key Capabilities

- **Hybrid/Semantic/Vector Search:** `search_query` with modes: hybrid (default), semantic, vector, keyword
- **Document Enumeration:** `search_list_documents` to list all indexed docs with snippet counts
- **Content Retrieval:** `search_get_document_snippets` to get full document content chunk-by-chunk
- **Automated Provisioning:** `search_provision_source` creates data source + index + skillset + indexer from an orchestrator record
- **Knowledge Base:** `search_retrieve` for cross-source agentic retrieval with citations via `search_create_knowledge_base` + `search_add_kb_source`

### Tool Categories

**Environment:** `search_list_environments`, `search_authenticate`

**Search:** `search_query`, `search_list_documents`, `search_get_document_snippets`, `search_get_index_stats`

**Provisioning:** `search_create_shared_skillset`, `search_provision_source`, `search_provision_status`, `search_deprovision_source`

**Knowledge Base:** `search_list_knowledge_bases`, `search_create_knowledge_base`, `search_add_kb_source`, `search_retrieve`

## Microsoft Fabric MCP

The Fabric MCP provides 123 tools for managing Microsoft Fabric workspaces, semantic models, reports, lakehouses, notebooks, pipelines, and more.

All tools are prefixed with `fabric_`. Use workspace names or IDs to target resources.

### Key Capabilities

- **Workspaces:** Create, list, update, delete workspaces; manage roles and capacity assignments
- **Semantic Models:** Full lifecycle — create, update schema (tables, columns, measures, relationships), refresh, manage permissions, execute DAX
- **Reports (PBIR):** Create, clone, update definitions; add/remove pages and visuals; rebind to different models
- **Lakehouses:** Create, manage tables, load data, create shortcuts, run maintenance
- **Notebooks & Pipelines:** Create, get/update definitions, run jobs, monitor status
- **Connections & Gateways:** Create connections, bind to gateways, manage datasources
- **Deployment Pipelines:** Create pipelines, assign stages, deploy between environments
- **Reference Guides:** `fabric_dax_reference`, `fabric_m_reference`, `fabric_tmsl_reference`, `fabric_pbir_reference` for in-context syntax help

## Large MCP Tool Responses

When reviewing large components (flows with many actions, complex formxml, full plugin assemblies, solution component lists), use this structured approach:

1. **Structure first:** Identify top-level sections. For flows: trigger type + action count + condition branches. For formxml: tab count + section count + field count. For plugins: assembly + step registrations. For solutions: component types + counts.
2. **Summarize:** Present the structural overview before diving into details.
3. **Targeted deep-dive:** Only parse in detail the sections relevant to the current task. Do not read every action in a 50-action flow if the user only asked about the trigger.
4. **If tool output overflows to file:** Use the Python extraction pattern below.

When an MCP tool returns a response that exceeds the context token limit, Claude Code automatically saves the full output to a temp `.json` or `.txt` file. The file format is a JSON array wrapper:

```json
[{"type": "text", "text": "{...escaped JSON string...}"}]
```

The actual response data is JSON-stringified inside the `text` field. **Do NOT use grep or Read directly on these files** — the content is double-encoded and will not match patterns reliably.

**To extract and query the data, always use Python:**

```bash
python -c "
import json
with open(r'<path>', 'r') as f:
    data = json.load(f)
obj = json.loads(data[0]['text'])
# Now work with obj normally, e.g.:
# for item in obj['environments']:
#     if 'search' in item['environment_name'].lower():
#         print(item)
"
```

This pattern works reliably for all MCP tool overflow files regardless of size.

## Skill Routing Rules

When performing these operations, invoke the corresponding skill for structured guidance:

| Operation | Skill |
|-----------|-------|
| Creating new tables or redesigning existing schema | `/design-schema` |
| Reviewing an existing complex component (flow, plugin, form, solution) | `/review-component` |
| An error occurs and first instinct is to delete/remove/recreate something | `/troubleshoot` |
| Completed a batch of UI changes and ready to verify | `/validate-ui` (offer to user, do not auto-run per-change) |
| Importing a solution into an environment | `/import-solution` |
| Building a new plugin or updating plugin code | `/build-plugins` |
| Creating or modifying cloud flows | `/build-power-automate-flows` |
| Building web resources or form scripts | `/build-web-resources` |
| Building PCF controls | `/build-pcf-controls` |
| Setting up Power Pages portal sites | `/build-power-pages` |
| Exporting or deploying solutions across environments | `/deploy-solution` |
| Designing a Power BI report | `/design-pbi-report` |
| Running automated browser tests | `/test-app` |
| Migrating data between environments | `/migrate-data` |
| Creating ADO work items from requirements | `/create-ado-work-items` |
| Extracting requirements from documents | `/extract-requirements` |

These skills provide detailed step-by-step procedures, guardrails against common mistakes, and validation checklists. Using them ensures consistent quality across all team members.