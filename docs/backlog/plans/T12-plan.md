# T12 — Data Governance & Knowledge Management (RAG-ready) — Implementation Plan

## Authoritative alignment note (required)

**ADR path mismatch:** The `/start-task` template lists `docs/adr/0001`–`0004`, `0006`, `0007` as mandatory. This repository’s `docs/adr/` currently contains **`0005`, `0008`, `0009`, `0010` only**. `docs/governance.md` and `docs/security.md` still **cite** ADR-0004 / 0006 / 0007 by name.

**Resolution for T12 implementation:** Treat **`docs/governance.md`**, **`docs/security.md`**, **`docs/compliance.md`**, **`docs/architecture.md`**, and **`docs/cost-model.md`** / **`docs/finops.md`** as the binding policy sources. Do **not** invent ADR text. If a decision needs an ADR anchor, add a **small** new ADR or a doc note in a **later** chore—**out of scope** for T12 sub-tasks unless explicitly requested.

**Security invariant (non-negotiable):** No raw document text, secrets, or PII in logs/traces; metadata and hashes only (consistent with existing ingestion logging).

---

## Task overview

Close the gap between **written governance policy** (`docs/governance.md`, largely already drafted) and the **running system**: ingestion must capture **classification, owner, lineage (source + version), timestamps, and confidential approval/audit fields**; chunks/index payloads must carry **minimum lineage**; the **Governance** portal page must summarize rules and show **example/live metadata** for a sample document; **architecture.md** must describe ingestion with **lineage + staleness** explicitly.

**Baseline (current code):**

- `DocumentMetadata` tracks id, names, chunk count, indexed time, version/deprecation, content hash — **no** classification, owner, retention, or approval.
- `DocumentChunk` has `SourceId`, `SourceName`, `IndexedAt` — **no** document version or classification-at-ingest.
- `DocumentEndpoints` upload path generates `sourceId` from filename + timestamp; **no** governance form fields.
- `Governance.razor` is a **placeholder**.
- `docs/governance.md` already defines lifecycle, DLP mention, confidential approval, RBAC roles, staleness — **implementation must converge** without contradicting it.

---

## High-level plan (5 steps)

1. **Model & API** — Add governance fields to application models and document upload API (multipart + validation); define `IngestionGovernanceContext` (or equivalent) passed into ingestion.
2. **Ingestion enforcement** — Wire classification rules: reject **Restricted**; **Confidential** requires approval flag + approver metadata; integrate **DLP/pattern guard** on content (reject + metadata-only security signal); ensure **operators vs approvers** is enforceable (lab may use config/claims stub).
3. **Lineage in chunks & vector stores** — Extend `DocumentChunk` and both `IVectorStore` implementations so each indexed vector carries **sourceId, sourceName, version, indexedAt, classification** (and any filter fields needed for deprecated/stale behavior later).
4. **Metadata persistence** — Extend `IDocumentMetadataStore`, `InMemoryDocumentMetadataStore`, and `PostgresDocumentMetadataStore` (table columns + `EnsureInitialized` migration-style DDL) for new fields and **approval audit** (who/when).
5. **Portal + architecture docs** — Replace Governance placeholder with read-only summary + example metadata; update `docs/architecture.md` **Document Ingestion Flow** for lineage, re-index, retire/delete, staleness pointer.

---

## Sub-task decomposition

### T12.A — Governance models + upload API contract

**Scope:**

- Introduce or finalize `DataClassification` and related enums/constants aligned with `docs/governance.md` (Public / Internal / Confidential / Restricted).
- Extend `DocumentMetadata` with: `Owner`, `SourceType`, `Classification`, `RetentionClass` or `ExpiresAt` / review fields, confidential **approval** fields (`ApprovedForIngest`, `ApprovedBy`, `ApprovedAt` — names flexible), optional `LastReviewedAt` / `CreatedAt` as needed for acceptance criteria.
- Add multipart form fields on `POST /api/documents` (and update path if `PUT` versioning already exists): e.g. `classification`, `owner`, optional `sourceType`, `retention` / `expiresAt`, `confidentialApproved`, `approvedBy` — **exact names** chosen to match Blazor upload and tests.
- Parse and validate; return **400** with clear, non-leaking messages on invalid combinations (e.g. Confidential without approval).

**Files (indicative):** `AiSa.Application/Models/*`, `AiSa.Host/Endpoints/DocumentEndpoints.cs`, `AiSa.Host/Components/Pages/Documents.razor` (+ code-behind) if upload UI must send new fields.

**Minimal tests:**

- Unit tests: classification + approval validation matrix (Restricted rejected; Confidential without approval rejected; Public/Internal happy path).
- Optional: minimal API test that **multipart** with missing required governance field returns 400.

**Acceptance:** API accepts governance metadata consistent with policy; no secret/PII in error responses or logs.

---

### T12.B — Ingestion pipeline: guards + `DocumentIngestionService` wiring

**Scope:**

- Integrate **content guard** (pattern-based secrets/PII — extend or add `IngestionContentGuard` per backlog): **reject** ingestion on match; emit **metadata-only** log / optional metric hook (align with security docs — no raw matched text).
- Pass `IngestionGovernanceContext` from endpoint into `IDocumentIngestionService.IngestAsync` overload (keep existing overload for backward compatibility if needed).
- Enforce **Restricted** and **Confidential** rules **server-side** (do not trust client only).
- Telemetry: span tags **metadata only** (classification label, approval present boolean — avoid owner PII in tags if deemed sensitive; prefer hash or omit).

**Files (indicative):** `DocumentIngestionService.cs`, `IDocumentIngestionService.cs`, `IngestionContentGuard.cs`, `DocumentEndpoints.cs`, tests `IngestionContentGuardTests.cs`.

**Minimal tests:**

- Guard unit tests: known secret pattern triggers reject; benign text passes.
- Service-level test (mocked dependencies): ingestion aborted before embedding when guard fires or policy fails.

**Acceptance:** Ingestion cannot bypass classification/DLP checks; logging remains metadata-only.

---

### T12.C — Chunk lineage + vector store payloads

**Scope:**

- Add to `DocumentChunk`: at minimum **`DocumentVersion`** (int or string per existing versioning), **`Classification`** (or string from enum), and any field required for staleness filters later.
- Update chunking/embedding path in `DocumentIngestionService` to populate these fields.
- Update **`AzureSearchVectorStore`** and **`PgVectorVectorStore`** mappings (index/schema or JSON metadata) so stored vectors round-trip lineage fields; ensure search/filter for **non-deprecated** remains correct (may be metadata-store–driven first; document limitation if vector filter lags).

**Files (indicative):** `DocumentChunk.cs`, `DocumentIngestionService.cs`, `AzureSearchVectorStore.cs`, `PgVectorVectorStore.cs`, tests touching serialization if present.

**Minimal tests:**

- Unit test: chunk builder sets lineage consistent with ingestion input.
- If integration tests exist for vector store, extend one case; else document manual verification for sub-task commit.

**Acceptance:** Every persisted chunk/vector associates with **source + version + classification + indexed time** per acceptance criteria.

---

### T12.D — Metadata store: extended columns + approval audit

**Scope:**

- Extend `IngestionResult` if needed to carry governance fields into `IDocumentMetadataStore.StoreAsync`.
- Update `InMemoryDocumentMetadataStore` and `PostgresDocumentMetadataStore` (`EnsureInitialized` ALTER/CREATE) with new columns; **migrate** existing rows with sensible defaults (e.g. Internal, system owner).
- Ensure **list/get** APIs used by UI expose non-sensitive metadata for demo.

**Files (indicative):** `IngestionResult.cs`, `IDocumentMetadataStore.cs`, `InMemoryDocumentMetadataStore.cs`, `PostgresDocumentMetadataStore.cs`, any DTOs for document list endpoints.

**Minimal tests:**

- Store round-trip: after ingest, metadata retrieved includes classification, owner, version, approval fields.
- Postgres: test uses existing test DB pattern if available; else scoped integration or manual checklist in commit message.

**Acceptance:** End-to-end **metadata** survives restart for Postgres mode; in-memory still works for tests/local.

---

### T12.E — Governance UI + architecture documentation

**Scope:**

- Replace `Governance.razor` placeholder with:
  - Short **classification rules** summary (aligned with `docs/governance.md`, not a full duplicate).
  - **Example metadata** JSON block (static) **plus** optional live table fed from existing documents API (metadata only).
- Update `docs/architecture.md` **Document Ingestion Flow** (and related L3 component blurb) to include: validation → DLP → approval → chunk/index → lineage; pointers to **retire/re-index** and **staleness** behavior per `docs/governance.md`.
- Adjust `docs/governance.md` **only if** implementation intentionally narrows scope (e.g. “lab uses config approver list”) — keep a single source of truth.

**Files (indicative):** `Governance.razor` (+ `.cs` if needed), `docs/architecture.md`, possibly `docs/governance.md` (small delta).

**Minimal tests:**

- `dotnet build` / `dotnet test` clean; optional Host integration test GET `/governance` if project already uses WebApplicationFactory for pages (otherwise manual demo per DoD).

**Acceptance:** Demo path: upload **Internal** doc → Governance shows rules + sample metadata visible (Admin/Governance).

---

## Minimal tests per sub-task (summary)

| Sub-task | Tests |
|----------|--------|
| T12.A | Classification/approval validation unit tests; optional API 400 multipart test |
| T12.B | Content guard tests; ingestion aborts when policy/guard fails |
| T12.C | Chunk lineage field tests; vector mapping compile/runtime smoke |
| T12.D | Metadata store round-trip (in-memory + Postgres if harness exists) |
| T12.E | Build + test suite; manual Governance page check acceptable for UI |

---

## Risks / open questions

1. **Identity/RBAC:** Enterprise backlog expects **operators vs admins/approvers**. If the lab lacks real roles, use **configuration allowlist** (approver principals) or **development-only bypass** clearly flagged in `appsettings.Development.json` — document in `docs/governance.md` “Lab implementation” subsection to avoid compliance confusion.
2. **PII in metadata:** `owner` may be personal email — confirm whether to store **team name only** in logs/tags; never log full multipart field dumps.
3. **Vector schema migration:** Azure AI Search index may need **field additions**; pgvector JSON metadata column may be easier — plan one store first if sub-task LOC budget bites.
4. **Staleness in retrieval:** Full “answer must include outdated warning” may require **chat/RAG** changes; if out of LOC scope for T12, document as **follow-up** and still persist `expiresAt` / `lastReviewedAt` for future use.
5. **ADR drift:** Missing numbered ADRs in repo vs docs — track as repo hygiene; not blocking if policy docs remain consistent.

---

## Branch and commit pattern

- **Branch:** `feature/T12-data-governance-knowledge`
- **Commits:** `feat(T12): ...`, `chore(T12): ...`, `fix(T12): ...` (optional suffix in message body for sub-task, e.g. `feat(T12): add classification to upload API` for T12.A)

---

## Git commands (do not execute in start-task; user may run)

```bash
git checkout main
git pull
git checkout -b feature/T12-data-governance-knowledge
# after committing this plan:
git add docs/backlog/plans/T12-plan.md
git commit -m "chore(T12): add T12 data governance implementation plan"
```

---

## Execution protocol

- **Stop after this plan** until the user sends: `Implement T12.A` (then `T12.B`, …).
- **Max ~300–400 LOC** per sub-task; keep the repo buildable.
- Touch **only** files required for the requested sub-task.
