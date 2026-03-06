# Enterprise GenAI Architecture Playground

<div align="center">

<p><strong>Focus:</strong> experiments and best practices for enterprise GenAI • <strong>Stack:</strong> .NET 10, ASP.NET Core, Blazor, Azure-first (provider-agnostic)</p>


<img src="https://img.shields.io/badge/Scope-Architecture%20Playground-4C1" alt="Architecture Playground" />
<img src="https://img.shields.io/badge/Topics-Governance%20%7C%20Cost%20%7C%20Security-0ea5e9" alt="Topics badge" />

</div>


## 📌 Overview

This repository is a **playground** used to test patterns, architectural choices, and operational guardrails for enterprise GenAI.
It is intentionally practical: each module explores constraints like governance, compliance, cost, observability, and quality.

The objective is to collect reusable best practices and document trade-offs, not to present a finished product.

<hr>

## ✅ What This Project Is

- A modular **architecture sandbox** (not only a chatbot)
- A place to validate ideas with **Architectural Decision Records (ADR)**
- A reference for governance, observability, cost control, and compliance patterns
- Documentation-oriented: explains *why* a choice was made, not only *how*

## 🚫 What This Project Is Not

- A polished enterprise product
- A pure ML research project
- A benchmark of frameworks/providers
- A UI showcase

<hr>

## 🏗️ High-Level Architecture

<details open>
<summary><strong>Core components</strong></summary>

- **Blazor Portal** (UI + navigation)
- **ASP.NET Core API** (Minimal APIs)
- **RAG Pipeline** with pluggable vector stores
- **Agent Orchestration Layer**
- **Evaluation & Regression Harness**
- **Observability & Cost Control**
- **Governance, Security & Compliance Layer**

</details>

<details open>
<summary><strong>Design principles</strong></summary>

- Provider-agnostic LLM interface
- Azure-first, but portable
- Small, reviewable changes
- Explicit trade-offs documented via ADRs

</details>

<hr>

## 🧭 Portal Navigation

The Blazor portal exposes each architectural concern as a first-class area:

- **Chat** – RAG-based conversational interface
- **Agent** – Multi-step agent execution with guardrails
- **Documents** – Knowledge ingestion and lifecycle
- **Evaluations** – Quality & regression testing
- **Cost & FinOps** – Token usage, budgets, degradation
- **Governance** – Data classification & policies
- **Security** – Threats, mitigations, compliance
- **Admin** – Provider configuration & health checks

<hr>

## 🧩 Key Architectural Capabilities

### 📚 Retrieval-Augmented Generation (RAG)
- Dual vector store support:
  - Azure AI Search
  - pgvector (PostgreSQL)
- Metadata lifecycle store:
  - PostgreSQL-persisted metadata in all modes (including Azure AI Search vector mode)
- Chunking, embedding, citation enforcement
- “I don’t know” behavior when ungrounded
- Idempotent upload semantics via SHA-256 content hash (`new`/`updated`/`unchanged`)

### 🤖 Agentic AI (Controlled)
- Planner + execution loop
- Tool calling with strict allow-lists
- Step/time/token limits
- Full traceability per agent run

### 🎯 Evaluation & Quality
- Versioned evaluation datasets
- Batch evaluation runner
- Regression gates (CI-ready)
- Metrics: answer rate, citation rate, latency

### 🔍 Observability
- OpenTelemetry traces and metrics
- Request → Retrieval → LLM → Tool spans
- SLO-driven thinking
- Operational runbooks

### 💰 Cost Control & FinOps
- Token accounting per request
- Budget thresholds and graceful degradation
- Cost-to-value metrics
- FinOps-style decision making

### 🛡️ Governance & Knowledge Management
- Data classification and ingestion rules
- Knowledge lifecycle (create → update → retire)
- Explicit decisions on what **must not** be indexed

### 🔒 Security & Compliance
- Threat modeling (prompt injection, tool abuse, data leakage)
- GDPR analysis (lawful basis, minimization)
- EU AI Act risk classification
- Incident response procedures

<hr>

## 💼 Business Perspective

This playground also keeps business impact visible:

- Use cases and KPIs to evaluate usefulness
- ROI modeling (baseline vs AI-enabled)
- Stop/kill criteria when AI does not justify cost or risk
- Alignment with finance, legal, and security stakeholders

<hr>

## 📑 Architectural Documentation

- `docs/architecture.md` – System overview (C4 style)
- `docs/adr/` – Architectural Decision Records
- `docs/governance.md` – Data & knowledge governance
- `docs/security.md` – Threat model and compliance
- `docs/cost-model.md` – Cost & FinOps strategy
- `docs/runbooks/` – Operational procedures

<hr>

## 🐘 Running pgvector locally

To run the app with **pgvector** as the vector store (no Azure AI Search required), use the **.NET Aspire AppHost**. It starts the Host and a Postgres container with the pgvector extension; the connection string is injected automatically and `VectorStore:Provider` is set to `PgVector`.

**Prerequisites:** Docker (for the Postgres+pgvector container).

```bash
dotnet run --project src/AiSa.AppHost
