# Enterprise Operations Notes

This note keeps enterprise-only planning separate from the first implementation pass.

## Secret Handling

- Store integration credentials and AI provider keys outside the application database for production.
- Use a cloud secret manager or vault-backed provider keyed by tenant and application.
- Keep only non-secret references in `integration_connections.secret_reference` and AI provider settings.
- Rotate secrets by creating a new version, testing it, then retiring the old version after active jobs drain.
- Audit who changed a secret reference, never the secret value.

## SQL Server Or PostgreSQL Migration

- SQLite is the current metadata database. Its suitability for any production workload must be demonstrated with load, concurrency, durability and recovery evidence; do not assume universal enterprise readiness.
- Evaluate SQLite, PostgreSQL and SQL Server from measured workload and operational requirements. PostgreSQL is a candidate for JSON-heavy metadata, indexing and reporting, not an automatically approved migration target.
- Under PO-approved DEC-0007 (D3=B), retain the modular monolith and benchmark before DB/queue/storage migration. Any major runtime migration requires separate architecture review and PO approval.
- Before migration, run EF migrations against an empty PostgreSQL/SQL Server database and a restored production-like dataset.
- Revisit filtered indexes, `DateTimeOffset` ordering, and JSON query patterns per provider.
- Move background job claiming to a provider-safe transactional claim pattern before high concurrency workers.

## Backup, Restore, And Retention

- Back up the relational database, file storage, vector store, and any secret references as one recovery unit.
- Test tenant-level restore separately from full environment restore.
- Retain audit logs and AI interaction metadata long enough for compliance review, then purge or anonymize per tenant policy.
- Keep source documents and generated indexes aligned: a restored DB must point to matching file/vector snapshots.

## Future Research

- On-premise deployment: package API, worker, database, object storage, and vector store with explicit upgrade steps.
- Data residency: route tenants to region-specific storage, AI providers, and vector indexes.
- Local models: support tenant-level model routing for air-gapped or regulated customers.
- Per-tenant provider isolation: prevent shared provider keys, model configs, and rate limits from crossing tenant boundaries.

## Enterprise Architecture Direction (PO-approved, 2026-10-29)

- [DEC-0004](../decisions/DEC-0004.md): Strict Security Boundary, default fail-closed and initial read-only integration.
- [DEC-0005](../decisions/DEC-0005.md): Platform Admin / Tenant Admin separation.
- [DEC-0006](../decisions/DEC-0006.md): Shared authorization and source revalidation.
- [DEC-0007](../decisions/DEC-0007.md): Incremental hardening and benchmark-led runtime changes.
- [AR-B-01–AR-B-04 report](../architecture/AR-B-01_TO_AR-B-04_ARCHITECTURE_REVIEW.md) lists SEC-01..07 and TD-01..06, still OPEN. These notes are requirements/directions, **not verified production guarantees**.
