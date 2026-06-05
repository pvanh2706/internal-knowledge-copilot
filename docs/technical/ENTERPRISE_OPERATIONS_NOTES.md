# Enterprise Operations Notes

This note keeps enterprise-only planning separate from the first implementation pass.

## Secret Handling

- Store integration credentials and AI provider keys outside the application database for production.
- Use a cloud secret manager or vault-backed provider keyed by tenant and application.
- Keep only non-secret references in `integration_connections.secret_reference` and AI provider settings.
- Rotate secrets by creating a new version, testing it, then retiring the old version after active jobs drain.
- Audit who changed a secret reference, never the secret value.

## SQL Server Or PostgreSQL Migration

- Keep SQLite for local development and demos only.
- Prefer PostgreSQL for JSON-heavy metadata, partial indexes, and operational reporting.
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
