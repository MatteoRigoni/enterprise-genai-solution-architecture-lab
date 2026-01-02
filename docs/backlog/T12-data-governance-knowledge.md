# T12 - Data Governance & Knowledge Management (RAG-ready)

## Goal
Ensure the knowledge base is controlled, auditable, and safe to use in GenAI.

## Scope
- Data classification policy:
  - Public / Internal / Confidential / Restricted
- Ingestion rules:
  - what can be indexed
  - required metadata (owner, source, last updated, retention class)
  - Approval workflow for Confidential classification:
    - require approval flag + approver metadata
    - audit trail of who approved what
  - Role-based access control:
    - who can ingest (operators)
    - who can approve Confidential (admins/owners)
  - DLP mention: pattern matching for secrets/PII during ingestion (reject if detected)
- Retention & deletion policy:
  - how to delete vectors/chunks when source is removed
- Lineage:
  - each chunk must reference original source id + version
- Staleness:
  - define “expiry” for documents and re-ingestion strategy
- Portal Governance page:
  - show classification rules summary
  - show example metadata for ingested docs

## Deliverables
- docs/governance.md (policy + lifecycle)
- Update docs/architecture.md flow with lineage/staleness
- Minimal metadata fields implemented in ingestion pipeline

## Acceptance Criteria
- Every ingested chunk has metadata: source, owner, classification, version/timestamp.
- There is a documented process for:
  - retiring docs
  - re-indexing updated docs
  - proving what the model saw

## Files / Areas
- docs/governance.md
- src/AiSa.Application/Infrastructure: metadata model
- src/AiSa.Host: Governance page (read-only summary)

## DoD
- Governance doc exists
- Metadata exists end-to-end
- Demo shows metadata for a sample doc

## Demo
Upload doc with classification=Internal -> show metadata visible in Admin/Governance.

## Sources (passive)
- DAMA-DMBOK (concetti: data governance, data quality, metadata)
- YouTube: “Data governance for GenAI / RAG”
- Articles: “Knowledge lifecycle management” (KM principles)

### Related context
- docs/governance.md
- docs/compliance.md
- docs/security.md
- docs/architecture.md
- docs/adr/0004-telemetry-policy.md
