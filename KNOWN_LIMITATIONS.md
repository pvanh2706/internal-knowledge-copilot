# Known Limitations

Last updated: 2026-05-10

This file records intentional MVP limits so future AI coding sessions do not mistake them for accidental omissions.

## Product Scope

- No SSO. Authentication is local username/password with JWT.
- No email notifications.
- No mobile app.
- No report export.
- No automatic Confluence import.
- No file preview experience inside the app.

## Document And Wiki

- Supported upload formats are PDF, DOCX, Markdown, and TXT.
- Excel, PowerPoint, HTML, image OCR, and scanned PDFs are out of scope.
- Wiki draft generation is from one approved document at a time.
- Wiki editing inside the app is not implemented.
- Wiki versioning is not implemented.
- Multi-document or folder-level wiki generation is not implemented.

## Search And RAG

- ChromaDB is the implemented development/test vector database.
- Qdrant is a future replacement option, not the current implemented runtime.
- No dedicated reranker is implemented.
- No Elasticsearch integration.
- Keyword/full-text search is intentionally lightweight.
- Vector data can be rebuilt from approved document versions and published wiki pages.

## Operations And Security

- SQLite is the MVP metadata database; SQL Server/PostgreSQL migration is deferred.
- Background processing is implemented inside the .NET API, not as a separate worker service.
- No Redis, RabbitMQ, or Hangfire.
- No antivirus/malware scanning.
- No secret scanning, masking, or redaction pipeline.
- Audit logging covers major business actions, not every document view or every low-level read.

## AI Provider

- Local/test behavior can use deterministic mock services.
- Production use still requires a privacy-appropriate AI provider configuration and real secrets outside the repository.

