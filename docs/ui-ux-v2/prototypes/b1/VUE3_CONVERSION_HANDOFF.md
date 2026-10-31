# Handoff: B1 HTML prototype → Vue 3 (PROPOSED; DO NOT IMPLEMENT YET)

**Gate:** Requires separate PO authorization for any changes to `src/frontend/`. This is a design specification, not a coding instruction with execution permission.

## 1. Principles and frozen boundaries

- **Keep:** Vue 3 + TypeScript + Vite, Pinia, Vue Router, Tailwind CSS 4, current Reka/shadcn-vue-style primitives, existing functions in `src/frontend/src/api/*` and backend/API/business/security contracts.
- HTML `prototype.js` uses **fake demo arrays** and **must NOT be copied as business logic**. Production Vue components must consume real typed API results and preserve server-side authorization.
- No backend work, API version changes, database migrations, persistent chat history, user-selected arbitrary external context, new tenant/global admin capabilities, or outbound source business writes.
- **Platform identity:** S01 design requires separate platform/tenant principals and sessions; `main` still has legacy routing patterns. Do not infer Platform Admin = Tenant Admin or introduce platform routes in this visual conversion. Platform login/console is out of scope.
- **Source links:** current `CitationCard.vue` has no click-through because the answer citation response lacks a source navigation contract. Prototype source selections merely highlight matching evidence; no real deep-link.
- **AI confidence:** current API is `'high'|'medium'|'low'`, not a numeric percentage. Never display fabricated accuracy or guarantee from demo confidence.
- **Context:** Jira/CRM/ERP cards are examples of **unavailable** context, NOT proof of live connector integration/ACL freshness.
- **Workflow:** no action execution changes; external business writes remain read-only/default-deny per approved security direction.
- Scope `All` means all **authorized** sources, not all enterprise data. Frontend never bypasses server ACL.

## 2. Visual Design System freeze candidates

The user selected **B1 Enterprise Blue** as overall color direction; per-screen detailed review is pending. Tokens (from `styles.css`):

| UI token | Hex | Vue mapping concept |
| --- | --- | --- |
| Primary | `#3159E9` | `--primary` |
| Primary hover | `#2447C8` | interactive emphasis |
| Primary-soft | `#EDF1FF` | navigation active, helper badges |
| Canvas | `#EDF2FA` | outer application shell |
| Surface | `#FFFFFF` | content/card |
| Sidebar | `#F7F9FD` | app navigation |
| Border | `#E3E9F3` | structural separators |
| Foreground | `#1B2941` | headings/body emphasis |
| Muted text | `#72829A` | secondary labels |
| Success / warning / error | `#187654` / `#A66B16` / `#BB4242` | existing `StatusBadge` semantic variants |

Do not blindly overwrite current `src/frontend/src/assets/main.css`: reconcile new B1 tokens with existing light/dark tokens and code styles during implementation review. Source uses system font fallback without CDN. Responsive and visual accessibility require actual implementation verification.

## 3. UI-to-code mapping

### UX-01 — AI Workspace (`/ai`)

**Existing page:** `src/frontend/src/pages/ai/AiQuestionPage.vue`  
**Reuse:** `AnswerPanel.vue`, `CitationCard.vue`, `ScopeSelector.vue`, `EmptyState.vue`, `ErrorState.vue`, `LlmWarningBanner.vue`, `useFolderTree`, `useToast`.  
**Existing contract:** `src/frontend/src/api/ai.ts`:
- `askQuestion({question,scopeType,folderId?,documentId?}, accessToken)` → `AskQuestionResponse` with `interactionId, answer, needsClarification, confidence, missingInformation[], conflicts[], suggestedFollowUps[], citations[]`.
- `AiScopeType='All'|'Folder'|'Document'`, selectors from approved/indexed documents and authorized folder tree.
- `submitAiFeedback(interactionId,{value,note},accessToken)` via `src/frontend/src/api/feedback.ts`.
- Clarification, source conflict, missing information, empty citations, 401/403, network/500, loading and validation must remain visible.
- Prototype question sample/citations are **fictional**. The live answer must display only response data, never the fake step list.

**Design specific:** sticky-ish composer with Enter submit/Shift+Enter newline, collapsible source/context region recommended for narrow devices, right panel citations **not clickable** until an approved navigation contract exists. No conversation history or streaming assumed.

### UX-02 — Documents (`/documents`)

**Existing page:** `src/frontend/src/pages/documents/DocumentListPage.vue`  
**Existing contract:** `src/frontend/src/api/documents.ts`: `getDocuments`, `getDocument`, `uploadDocument`, `uploadDocumentVersion`, `downloadDocument`; wiki draft generation `src/frontend/src/api/wiki.ts` as currently supported and authorized.  
**Reuse:** `DataTable`, `FolderSelect`, `FileUpload`, `StatusBadge`, `ConfirmDialog`, `useToast`, existing document type/status enums.  
**Design specific:** filterable list and master-detail, selected document versions and uploaded file handling, validation, error, no results, no permissions. The sample list and fake upload dialog are not a specification to invent new fields or client-side file handling.

### UX-04 — Document Review (`/review`)

**Existing page:** `src/frontend/src/pages/review/DocumentReviewPage.vue`  
**Existing contract:** `getDocuments`, `getDocument`, `approveDocument`, `rejectDocument` in `src/frontend/src/api/documents.ts`.  
**Reuse:** `DataTable`, `StatusBadge`, `useConfirm`, `useToast`, `EmptyState`, `ErrorState`.  
**Design specific:** pending version list, selected version and evidence, approve confirmation, required reject reason, stale/updated status handling and final empty queue. **Never** send mock approval decisions or assume UI checks replace backend authorization.

## 4. Shared component boundaries (proposed only)

```
src/frontend/src/
  layouts/
    AppLayout.vue             # redesign visual shell only
  components/app/
    AppSidebar.vue            # navigation skin and mobile state
    AppTopbar.vue
    AnswerPanel.vue           # reuse API and business behavior
    CitationCard.vue
    ScopeSelector.vue
    DataTable.vue
    StatusBadge.vue
    ConfirmDialog.vue
  pages/
    ai/AiQuestionPage.vue
    documents/DocumentListPage.vue
    review/DocumentReviewPage.vue
```

Current `App.vue` switches AppLayout/AuthLayout via route meta. Keep existing route paths, names, auth guards and API function signatures as a default unless the PO separately approves a contract-safe change. Any security-related router/session changes require S01 boundary review.

## 5. Review scenarios for conversion trial

| Scenario | UX-01 | UX-02 | UX-04 |
| --- | --- | --- | --- |
| First load | Authorized scope and empty AI state | Document list with correct statuses | Pending review queue |
| Main success | Answer + actual citations + confidence category | Select document / versions | Approve or reject version |
| No data | No source / no answer | No documents / filtered empty | Empty pending queue |
| Failure | 401/403 and API failure, preserve safety | Upload/download and list failure | Stale version / denied action |
| Validation | Blank question, missing folder/document scope | Missing file/title/invalid form per current API | Reject reason mandatory |
| Responsive | Sidebar and source panel | Horizontal table & detail | Queue/detail stacking |
| Accessibility | Keyboard, aria labels, focus | Row selection keyboard | Modal/focus and error announcement |

Run `npm run lint`, `npm run build` and `npm test` within `src/frontend` **only after separate execution permission**. Add route, interaction and accessibility tests where appropriate. Review browser screenshots at desktop and narrow viewports.

## 6. Work package for a future Sol task — NOT AUTHORIZED

**Suggested model:** Sol, High effort for first conversion (shared shell, interactive AI, document/review business preservation).  
**Permitted scope after PO gate:** only B1 shared shell/components and these 3 pages, tests, independent branch/PR; no merge without PO.  
**Inputs:** `index-standalone.html` for visual inspection, `index.html`/`styles.css` for component boundaries, this handoff and approved GitHub contracts.  
**Output:** Vue UI visually faithful to approved prototype, no mock production data, API contract regression tests, screenshot evidence and a PR.  
**Acceptance:** PO first approves the exact B1 prototype and the separate frontend execution gate. This document does not give that authorization.

## 7. Current governance context

- 23-screen inventory is documented separately in `docs/ui-ux-v2/SCREEN_INVENTORY_AND_DESIGN_SEQUENCE_V1.md` on PR #10 (pending merge).
- S01-C security PR #8 and #9 are unmerged and not modified by this design.
- No production deployment, UI code change, cross-tenant privilege or backend/API change may be inferred from a design approval.
