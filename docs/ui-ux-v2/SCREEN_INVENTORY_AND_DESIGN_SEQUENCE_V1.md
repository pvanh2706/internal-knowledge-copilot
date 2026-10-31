# Enterprise AI Copilot — UI/UX Redesign v2: Screen Inventory & Design Sequence

**Repository:** pvanh2706/internal-knowledge-copilot  
**Document version:** v1.0 — inventory baseline proposal  
**Date:** 2026-11-01  
**Status:** PROPOSED — PO to review the detailed screen inventory and sequence  
**Direction:** Enterprise Knowledge Workspace (Style B / AI Workspace at center) — PO accepted as UX direction in conversation, NOT a finalized per-screen UI specification  
**Source snapshot:** main @ 9b9bf5bad7733ecb7eef8e7a8d9cf60ef2fcb3b3 (GitHub tree and source read on 2026-11-01)  
**Scope of this commit:** Documentation only. No product source, backend, API, database, security policy, assets, or frontend runtime changes.

## 1. Purpose and authorization

The PO requested a complete inventory of the existing frontend screens, committed to GitHub, so that UX concepts can be designed and approved **one screen at a time**. This document records what exists, what needs a design artifact, and recommended order. It is NOT authorization to implement UI code, delete legacy UI, merge a PR, or deploy.

Design only: preserve approved backend/business/API/security contracts. A visual mockup must never silently introduce backend capabilities, cross-tenant authority, outbound business writes, or new data exposure. Product-owned business actions remain in source systems.

## 2. Inventory methodology and counting rules

Reviewed the main branch tree; src/frontend/src/router/index.ts; all 23 non-test Vue page components under src/frontend/src/pages; app shell, navigation, auth store and API client/contracts; LUỒNG_GIAO_DIỆN.md; docs/KẾ_HOẠCH_THIẾT_KẾ_LẠI_GIAO_DIỆN.md; S01 approved identity/security design; and open PR #8/#9 metadata and changed-file lists.

**Count: 23 existing routed Vue screens / 23 route records / 23 page components.** src/frontend/src/pages/workflow/Phase8Pages.test.ts is a test, NOT a screen. Reusable components, dialogs, master-detail panels, empty/error/loading states are distinct design states, NOT extra routes. Proposed platform screens have no implementation on main and are tracked separately below.

Current route access labels in the table describe **existing client route meta**, not proof of backend authorization:
- Auth: requiresAuth.
- Reviewer+: requiresReviewer (current frontend allows Reviewer or Admin).
- Admin: requiresAdmin.
- Public: route does not require authentication.
- Current main uses one tenant auth store and a common app shell; this is NOT an approved platform/tenant security boundary.

### 2.1 Current screen inventory — 23/23

| ID | Route | Current Vue page | Existing client access | Screen-specific design inventory and important states |
| --- | --- | --- | --- | --- |
| UX-01 | /ai | pages/ai/AiQuestionPage.vue | Auth | AI question composer; scope popover (All/Folder/Document); loading/answer/error/empty; confidence, missing information, source conflicts; citations; follow-up prompts; good/bad feedback. Target: AI Workspace (not assumed multi-turn chat). |
| UX-02 | /documents | pages/documents/DocumentListPage.vue | Auth | Search/status/folder filters; documents table; selected document detail and versions; upload document dialog; upload new version; download original; generate wiki draft where allowed; empty/error/loading. |
| UX-03 | /wiki | pages/wiki/WikiDraftPage.vue | Reviewer+ | Draft list and detail; content/related documents; publication scope; company visibility confirmation; publish/reject with rejection reason and confirmation; empty/loading/errors. This is reviewer wiki-draft management, NOT a separate wiki reader route. |
| UX-04 | /review | pages/review/DocumentReviewPage.vue | Reviewer+ | Pending document queue; selected document/version review; approve; reject with reason/confirm; no pending items, stale version, operation failure. |
| UX-05 | /feedback | pages/review/FeedbackReviewPage.vue | Reviewer+ | Incorrect feedback table; interaction/answer/citation detail; quality issue; correction draft/approve/reject; evaluation case creation; reviewer status/notes; empty/errors. |
| UX-06 | /retrieval-explain | pages/review/RetrievalExplainPage.vue | Reviewer+ | Query and scope selector; rewritten query/filter statistics; candidate table; selected final context; source evidence; no context/error/loading states. |
| UX-07 | /evaluation | pages/review/EvaluationPage.vue | Reviewer+ | Evaluation cases; run creation/name; runs list; selected run results; pass/fail and empty/error states. |
| UX-08 | /knowledge-index | pages/review/KnowledgeIndexPage.vue | Reviewer+ | Ledger/keyword chunk metrics; source-type summary; batch size; rebuild index and its result/error/confirmation states. |
| UX-09 | /knowledge-sources | pages/review/KnowledgeSourcePage.vue | Reviewer+ | Application filter; knowledge source table; external object table; metadata JSON/detail; empty, error and permission states. |
| UX-10 | /workflow/recommendations | pages/workflow/RecommendationListPage.vue | Auth | Application/object filters; recommendation list/detail; supporting citations; user feedback; action-request draft controls with policy restrictions; no assumption that outbound execution is enabled. |
| UX-11 | /workflow/actions | pages/workflow/ActionApprovalQueuePage.vue | Auth | Action queue filters/status; selected request/detail/payload/validation; approve/reject note; legacy execute affordance currently in source must be treated as security-dependent, default-deny for external writes. |
| UX-12 | / | pages/DashboardPage.vue | Auth | Reviewer/admin KPI dashboard; ordinary user sees access-limited empty state; top cited sources; skeleton/errors. Proposed separate Insights/Operations experience — redirect-to-AI is NOT approved/implemented. |
| UX-13 | /login | pages/auth/LoginPage.vue | Public | Tenant code, email/password; validation, failed login, expired session and password-change redirect; tenant login, NOT platform admin login. |
| UX-14 | /change-password | pages/auth/ChangePasswordPage.vue | Auth | Current/new password, validation, success/failure, required password change. Tenant flow, NOT platform admin credential change. |
| UX-15 | /admin/users | pages/admin/UserManagementPage.vue | Admin | Users table, role/team context, create user dialog, initial password, validation/empty/error. Do not conflate tenant admins with platform admins. |
| UX-16 | /admin/teams | pages/admin/TeamManagementPage.vue | Admin | Teams table, create team dialog, description, feedback and empty state. |
| UX-17 | /admin/folders | pages/admin/FolderManagementPage.vue | Reviewer+ | Folder tree, selected folder details, change parent/name, team permissions, create dialog, deletion confirmation. Note route is Reviewer+, not Admin-only. |
| UX-18 | /admin/ai-settings | pages/admin/AiSettingsPage.vue | Admin | Settings status; Basic/Advanced tabs; provider and model, endpoint, embedding, keys; save/test and errors; no sensitive key display in prototype. |
| UX-19 | /admin/integrations | pages/admin/IntegrationManagementPage.vue | Admin | Application filter, connection table, create connection dialog, auth mode/status, secret reference and metadata fields; avoid fake connector-ready states. |
| UX-20 | /admin/applications | pages/admin/ApplicationManagementPage.vue | Admin | Tenant filter, application table, create/edit dialog, type/status/base URL, archive confirmation; platform-vs-tenant access MUST follow approved backend policies at implementation. |
| UX-21 | /admin/audit-logs | pages/admin/AuditLogPage.vue | Admin | Audit log table; action/target filters; empty/error/loading; distinguish existing tenant audit from future platform audit. |
| UX-22 | /admin/data-reset | pages/admin/DataResetPage.vue | Admin | Dangerous operation screen, availability/status, typed confirmation, operation result/errors; never normalize this as an ordinary button. |
| UX-23 | /admin/tenants | pages/admin/TenantManagementPage.vue | Admin (legacy, security-sensitive) | Tenant table and create/edit/archive dialog. The current client route is legacy; canonical platform management and separate platform session are defined by S01. Design must be reviewed against S01 security cutover, NOT copied as tenant-Admin global access. |

**Verification:** Every row corresponds to exactly one route and one .vue page in main; no screen is inferred from a mockup alone.

### 2.2 Global UI/design surfaces (not separate page count)

- App shell: App.vue, layouts/AppLayout.vue, layouts/AuthLayout.vue.
- Navigation: components/app/AppSidebar.vue, AppTopbar.vue, navigation.ts, router/index.ts, role-related disabled/hidden/navigation states.
- AI components: AnswerPanel.vue, CitationCard.vue, ScopeSelector.vue, CorrectionCard.vue, LlmWarningBanner.vue.
- Shared business UI: DataTable.vue, DataTablePagination.vue, FilterBar.vue, FolderSelect.vue, FileUpload.vue, JsonViewer.vue, StatusBadge.vue, PageHeader.vue, StatCard.vue, EvalCaseForm.vue.
- Global feedback and states: ConfirmDialog.vue, ThemeToggle.vue, EmptyState.vue, ErrorState.vue, Toast/Sonner, loading skeleton and inline form validation.
- UI primitive library: components/ui/* (Reka/shadcn conventions), assets/main.css tokens, responsive/mobile states, accessibility and keyboard focus.
- Auth/transport boundary touchpoints for review ONLY: stores/auth.ts, api/http.ts, api/auth.ts. Never implement platform impersonation or permissions purely in UI.

### 2.3 Proposed or security-dependent screens — NOT in the 23

| Proposed concept | Why separate | Status / design restraint |
| --- | --- | --- |
| Platform Admin Login | S01 separates Platform Identity from Tenant Identity | Proposed visual target; not routed on main; separate login/session/client required before implementation. |
| Platform Admin Required Password Change | Restricted platform credential-change flow | Proposed visual target; no reuse of tenant auth token/store. |
| Platform Tenant Management | S01 approved canonical /api/platform/tenants authority | Proposed replacement for unsafe legacy global-tenant experience; must be aligned to implemented and verified contracts. |
| Platform Audit / Admin Operations | Separate platform audit and authority | Proposed only where corresponding approved APIs actually exist; no speculative screens for unapproved endpoints. |
| Connector-aware Jira/CRM context panel | Enterprise Context-Aware direction | Concept only; do not show connected/authorized data until source ACL and connector contracts are available. |
| Conversation history / true multi-turn chat | Not established by the current AskQuestionPayload/response contract | Visual exploration only; needs separate capability/API decision. |
| Click-through source viewer / direct source deep link | Current CitationCard explicitly states no click-through | Design affordance only; actual navigation depends on approved source identifiers and permission revalidation. |
| External action execution | S01 read-only/fail-closed policy | Do not present as generally available; may be hidden, disabled or explanatory depending on effective server state. |

**No Platform Bootstrap web page:** S01 chooses operator-controlled CLI, not public first-admin bootstrap. Do not design or invent one as a functional route.

## 3. Approved direction vs open visual choices

- **APPROVED direction (conversation 2026-11-01):** Redesign frontend presentation in Style B — Enterprise Knowledge Workspace; AI workspace at the center; evidence, source and business context are visibly distinct; preserve approved backend/security contracts; prototype and PO review before coding.
- **PROPOSED details to refine screen-by-screen:** B1 Enterprise Blue vs B2 Professional Teal accent, dark variant, typography/spacing, icons, responsive rules, sidebar collapse, exact layout proportions, empty-state copy, and which context panels are visible when unavailable.
- **NOT approved product capabilities:** New backend chat/session APIs, invented Jira/CRM data, fake scores or quantitative trust percentages, workflow writes, cross-tenant admin abilities.
- Source of truth for v1 design/history: docs/KẾ_HOẠCH_THIẾT_KẾ_LẠI_GIAO_DIỆN.md and LUỒNG_GIAO_DIỆN.md. They document the earlier July 2026 redesign, not final v2 screen decisions.
- The concept images from chat illustrate visual directions only; they are NOT code assets or verified runtime screenshots and are not committed by this inventory.

## 4. Per-screen design deliverable and acceptance checklist

For **each existing screen** produce one design review pack rather than a separate implementation gate:
1. Screenshot-sized desktop layout and an appropriate smaller/responsive view; preserve Style B navigation and visual hierarchy.
2. Full current UI/functional coverage: primary screen, modal/drawer/detail panels, tabs/filters/forms and destructive action states if present in source.
3. Loading, empty, error, denied/insufficient-permission, long text, missing source and success states where applicable.
4. User journeys, CTAs, labels in Vietnamese, keyboard/focus behavior and accessibility contrast.
5. Component reuse map, current route, existing API functions; explicitly distinguish live capability from concept/placeholder.
6. PO response record: Proposed → Approved / changes requested; approval of design does not authorize code.

Platform-related content requires a separate security review against S01 before implementation. No destructive action or outbound write is made available by style changes.

## 5. Recommended ONE-SCREEN-AT-A-TIME design order

**Foundation mockup (not a 24th routed screen):** Global App Shell, sidebar, header, AI Workspace three-column geometry, Light/Dark token variants, common forms/dialogs and states. Use this as a reusable visual frame.

The screen IDs in section 2.1 are also the **recommended design sequence**:
- **Wave I — AI & Knowledge (UX-01 through UX-03):** AI Workspace first; then Documents and Wiki so the citation/source/document visual language is consistent.
- **Wave II — Quality & source trust (UX-04 through UX-09):** Document review, Feedback & Correction, Retrieval Explain, Evaluation, Knowledge Index and Knowledge Sources.
- **Wave III — Assistive workflow & insights (UX-10 through UX-12):** Recommendations, Action Queue (read-only/default-deny semantics), then KPI Dashboard.
- **Wave IV — Identity & tenant tools (UX-13 through UX-22):** Tenant Login/Change Password, Users, Teams, Folders, AI Settings, Integrations, Applications, Audit, Data Reset.
- **Wave V — Security-dependent tenant platform boundary (UX-23 and proposed platform surfaces):** Legacy Tenant Management design is contingent on S01; platform login/administration is *not* part of 23-route implementation without authorization.

The PO can review designs sequentially while keeping one overarching UX direction; there is no need to create a separate architecture gate for each screen. Coding remains a separate explicit authorization after the relevant design baseline is accepted.

### First next deliverable (not executed in this documentation commit)

**UX-01 — AI Workspace**: prepare two detailed design variants within Style B (Enterprise Blue, Professional Teal), each with empty state, example question/result with clearly labeled fake data, existing scope controls, evidence/source cards, feedback, access-denied/error, and narrow layout. Propose a preferred variant for PO choice; do not imply real chat history, working source deep links, or live Jira/CRM.

## 6. Security and implementation guardrails

1. This inventory/PR changes docs only. Do not touch src/frontend, backend, migrations, build pipelines, token/cookie/storage handling or public routes.
2. Existing frontend is currently based on a tenant auth store and legacy requiresAdmin routing. **Never** treat that as the intended final authorization boundary. S01 requires separate platform vs tenant principals and sessions, with server enforcement authoritative.
3. PR #8 and #9 are OPEN, unmerged; their changed-file lists contain **no src/frontend changes** at inspection time. They must not be merged/changed as part of this UX documentation scope.
4. Keep /ai request payload fields and citation metadata contracts unchanged until an independently approved change exists. Existing citation component lacks a click-through source viewer.
5. Existing action approval/workflow pages cannot override default-deny external business execution policy, even where legacy source contains an Execute button.
6. No production claims from mockups; no invented connected-system state, numeric confidence percentage, conversational memory or source ACL proof.
7. Style B direction is separate from final per-screen approvals. No merge, code, AWS, identity PoC or production deployment is implied by this commit.

## 7. Evidence/reference paths (read-only review)

- src/frontend/src/router/index.ts
- src/frontend/src/components/app/navigation.ts
- src/frontend/src/pages/** (23 .vue pages enumerated in section 2.1)
- src/frontend/src/api/ai.ts; src/frontend/src/api/http.ts; src/frontend/src/stores/auth.ts
- src/frontend/src/components/app/AnswerPanel.vue; CitationCard.vue; ScopeSelector.vue
- src/frontend/src/App.vue; src/frontend/src/layouts/*
- LUỒNG_GIAO_DIỆN.md
- docs/KẾ_HOẠCH_THIẾT_KẾ_LẠI_GIAO_DIỆN.md
- docs/architecture/S01_TECHNICAL_DESIGN_BASELINE_V1.0.md
- docs/decisions/DECISION_LOG.md
- GitHub PR #8 (feature/s01-c-bootstrap-recovery) and PR #9 (feature/s01-c-security-remediation): OPEN and frontend unchanged per file lists.

**Review mode:** static source inspection. No browser UI run, npm install, lint, build or tests were executed for this docs-only inventory.

## 8. PO review request

Please approve or request changes to this **screen inventory and sequence only**. Upon acceptance, proceed with designing UX-01 visually, screen-by-screen. This documentation PR must remain unmerged until PO separately authorizes the merge. All product-code implementation remains NOT AUTHORIZED.
