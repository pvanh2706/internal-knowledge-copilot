# Enterprise AI Copilot — Decision Log

**Version:** 0.2  
**Document status:** PO APPROVED CONTENT — Documentation Publication PR pending merge  
**Last updated:** 2026-10-29  
**Owner:** Product Owner

## 1. Purpose

Version-controlled registry of PO-approved product and architecture decisions. A decision's APPROVED status does not grant source-code implementation or PR merge permission.

## 2. Decision Registry

| ID | Decision | Category | Status |
|---|---|---|---|
| [DEC-0001](DEC-0001.md) | B as Primary Product | Product Strategy | APPROVED |
| [DEC-0002](DEC-0002.md) | Enterprise Context-Aware Direction | Product Strategy | APPROVED |
| [DEC-0003](DEC-0003.md) | Source System Ownership | Product Architecture | APPROVED |
| [DEC-0004](DEC-0004.md) | Strict Security Boundary (Option A) | Security Architecture | APPROVED |
| [DEC-0005](DEC-0005.md) | Platform Admin vs Tenant Admin (D1=B) | Identity & Access Architecture | APPROVED |
| [DEC-0006](DEC-0006.md) | Centralized Authorization and Revalidation (D2=B) | Security & Retrieval Architecture | APPROVED |
| [DEC-0007](DEC-0007.md) | Incremental Enterprise Hardening (D3=B) | Platform Architecture | APPROVED |

DEC-0001..03 have been merged to `main`. DEC-0004..07 were approved by PO in conversation on 2026-10-29, awaiting review and authorized merge of this Documentation Publication PR.

## 3. Review Registry

| Review | PO status | Remediation |
|---|---|---|
| AR-B-01 — Architecture Baseline | APPROVED | Baseline only |
| AR-B-02 — Security & Integration Boundary, SEC-01..07 | APPROVED | OPEN |
| AR-B-03 — Technical Debt, TD-01..06 | APPROVED | OPEN |
| AR-B-04 — Consolidation, To-Be, S01..05 preliminary plan | APPROVED | Implementation NOT AUTHORIZED |

[Consolidated Architecture Review](../architecture/AR-B-01_TO_AR-B-04_ARCHITECTURE_REVIEW.md).

## 4. Decision Lifecycle

- **PROPOSED:** Under PO consideration.
- **APPROVED:** PO accepts decision scope/content; not equivalent to IMPLEMENTED, VERIFIED or production-ready.
- **SUPERSEDED:** Replaced by a newer linked decision; keep history.

Documentation publication, implementation authorization and merge permission are separate gates.

## 5. Decision Record Requirements

ID/title, category, status, context, options, selected decision, rationale, consequences/trade-offs, related decisions and approval authority/date. Changes of major architecture require review and a new/superseding record where appropriate.

## 6. Governance Rules

1. Only PO approves product/architecture decisions.
2. ChatGPT analyzes and reviews; coding agents execute only within explicitly authorized scope.
3. Source, DB and runtime modifications require separate PO approval.
4. GitHub changes go through branch/PR; never merge without PO approval.
5. Accepted findings remain OPEN until verified remediation; don't conflate analysis with testing.

## 7. References

- [Project Context](../../PROJECT_CONTEXT.md)
- [Project Status](../../PROJECT_STATUS.md)
- [Architecture Review](../architecture/AR-B-01_TO_AR-B-04_ARCHITECTURE_REVIEW.md)
- [AI Handoff](../../BÀN_GIAO_AI.md)
