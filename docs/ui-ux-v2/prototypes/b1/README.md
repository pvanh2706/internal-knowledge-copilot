# UI/UX Redesign v2 — B1 Enterprise Blue

**Status:** PROPOSED — design review required before any implementation.  
**Date:** 2026-11-01  
**Source code baseline inspected:** `main` @ `9b9bf5bad7733ecb7eef8e7a8d9cf60ef2fcb3b3`  
**Delivery mode:** HTML design assets in `docs/` only. **No frontend/backend implementation is authorized.**

## Goal and approved design direction

The PO selected **B1 — Enterprise Blue**, within the previously accepted Style B / Enterprise Knowledge Workspace direction. We apply the proposed hybrid method: prototype a few representative screens, approve reusable Design System patterns, then separately authorize a small Vue 3 conversion before scaling to the remaining UI.

This iteration intentionally covers exactly THREE representative screens rather than all 23:

| Prototype ID | Current route | Existing Vue screen | Design coverage |
| --- | --- | --- | --- |
| UX-01 | `/ai` | `src/frontend/src/pages/ai/AiQuestionPage.vue` | AI-first 3-column desktop layout; source evidence; confidence category; scope selector; answer, empty, loading, clarification, denied, no source, error; feedback; input composer |
| UX-02 | `/documents` | `src/frontend/src/pages/documents/DocumentListPage.vue` | Search, folder/status filters, document table, selected detail/versions, upload/new-version dialog, empty states |
| UX-04 | `/review` | `src/frontend/src/pages/review/DocumentReviewPage.vue` | Reviewer queue, status search/filter, selected version, approve confirmation, reject reason, mock decision state, empty queue |

**All 23 routes remain in the inventory at:** [SCREEN_INVENTORY_AND_DESIGN_SEQUENCE_V1.md](../../SCREEN_INVENTORY_AND_DESIGN_SEQUENCE_V1.md) (proposed in PR #10, not merged as of creating this branch). These 3 designs are exemplars for future screen designs; they do not replace the full inventory.

## Open the prototype

- **One-file preview:** `index-standalone.html` — download this file and open it in Chrome/Edge. No dependencies, server or npm needed.
- **Maintainable source for AI conversion:** `index.html` + `styles.css` + `prototype.js` — keep these three files together and open `index.html` in the browser.
- Navigation in HTML: AI Workspace, Tài liệu, Duyệt tài liệu (or URL fragments `#ai`, `#documents`, `#review`).
- Prototype interactions are **local simulations only**. No persistent data, network calls, session or file upload. A selected document is demo-only; sample feedback, citations and decisions do not reflect actual enterprise policies.

## Design style — B1

- Primary accent `#3159E9`, hover `#2447C8`, active soft background `#EDF1FF`.
- Canvas `#EDF2FA`; white content; muted sidebar `#F7F9FD`; border `#E3E9F3`; text `#1B2941`; neutral details `#72829A`.
- UI has reusable sidebar + tenant label + header, cards, data table, master-detail, status badge, dialog, toast, empty/loading/denied/error visuals.
- Desktop AI screen uses left navigation, centered answer/composer, right evidence/context sidebar.
- Responsive <=750px: navigation becomes toggled overlay and AI right panel follows content; table scrolls horizontally. Final mobile/tablet behavior requires PO review.
- Typography: Inter when installed; system sans fallback; no network font load.
- Accessibility: labeled inputs, button/focus styles, keyboard selection for table rows, Escape closes dialog/sidebar, reduced-motion rules, visible feedback.
- This HTML is a **visual and behavior prototype**, not an accessibility certificate. During Vue implementation, complete dialog focus-trapping, screen-reader announcements and keyboard audit.

## Recommended acceptance checkpoint (PO)

1. Approve/adjust B1 palette, typography, density and sidebar/topbar.
2. Review UX-01: right-side citations, empty/answer/error states and composer position.
3. Review UX-02: filters, table, selected document detail, upload modal.
4. Review UX-04: queue/detail layout, decision confirmations and mandatory rejection reason.
5. Identify any layout issues on desktop and narrow screens.
6. Accept B1 Design System baseline and three screens **for conversion planning only**.

**Implementation Gate:** After PO explicitly authorizes, prepare a bounded Sol task to convert only shared visual components + UX-01/UX-02/UX-04 using existing Vue 3/Reka/Tailwind/Pinia and approved contracts. Do not merge code or create backend endpoints without separate permission.

## Files

- `index-standalone.html` — one-file reviewable HTML (generated from same source).
- `index.html` — structured semantic page layout for three screens.
- `styles.css` — Design System B1 and responsive styling.
- `prototype.js` — offline demo interactions (never reuse as production business logic).
- `VUE3_CONVERSION_HANDOFF.md` — per-screen API/component mapping, source-of-truth contract and review checklist.

**Important:** GitHub's source-file viewer displays HTML as code. To try the interaction, download the standalone HTML via the Raw link and open it in a local browser; publishing a live site or GitHub Pages is not authorized by this PR.
