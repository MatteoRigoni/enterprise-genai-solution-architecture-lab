# T10 - Hardening: Identity, Key Vault, RBAC, Secure Config

## Goal
Make the deployment secure-by-default.

## Scope
- Managed identity for App Service.
- Store secrets in Key Vault:
  - LLM key (Azure OpenAI or other)
  - AI Search key (if needed)
  - Postgres connection (if applicable)
- RBAC: least privilege from App Service to Key Vault.
- Admin page: Config health check (no secret values).

## Acceptance Criteria
- App reads secrets from Key Vault at runtime.
- Missing secrets show clear health check error (without leaking).
- docs/security.md updated.

## Files / Areas
- src/AiSa.Host: configuration + health checks
- infra/bicep: identity + KV permissions
- docs/security.md

## DoD
- Secrets removed from repo
- Health check present
- Deploy workflow uses GitHub secrets only for non-secret IDs

## Demo
Remove secret -> Admin shows missing -> add -> green

## Sources (passive)
- Microsoft docs: Managed Identity patterns
- YouTube: “Key Vault + App Service managed identity”
- Docs: least privilege / RBAC basics

### Related context
- docs/security.md
- docs/compliance.md
- docs/governance.md
- docs/architecture.md
- docs/adr/0001-hosting-model.md
- docs/adr/0004-telemetry-policy.md