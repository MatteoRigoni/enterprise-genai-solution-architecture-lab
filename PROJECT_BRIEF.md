# Enterprise GenAI Solution Architecture Lab (Azure-first, portable)

## Goal
Build an enterprise-grade AI assistant with:
- RAG with citations
- tool/function calling with guardrails
- agentic orchestration (bounded + observable)
- evaluation + regression gates
- observability + SLOs
- cost controls + FinOps artifacts
- CI/CD + IaC
- governance + compliance documentation

## Constraints
- Azure-first deployment (App Service)
- Portable architecture: provider-agnostic LLM interface + pluggable vector store
- No sensitive data in logs; secrets via Key Vault/managed identity
- Two-language allowed: .NET for runtime, Python optionally for eval tooling

## UI (Portal)
Blazor portal with left navigation:
- Chat
- Agent
- Documents
- Evaluations
- Observability
- Cost
- Governance
- Security/Compliance
- Admin

## Deliverables
- Source code + docs (ADR, runbooks, threat model)
- GitHub Actions pipelines
- Bicep infra modules
- Business case, governance, compliance and FinOps docs
