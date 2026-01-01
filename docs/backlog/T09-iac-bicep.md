# T09 - Infrastructure as Code (Bicep) for Azure

## Goal
Provision required Azure resources with Bicep for dev/prod.

## Scope
- main.bicep to deploy:
  - App Service plan + Web App (Linux)
  - Application Insights / Monitor
  - Azure AI Search
  - Key Vault
  - Optional: Azure Database for PostgreSQL
- params: dev/prod
- infra/README.md: how to deploy

## Acceptance Criteria
- Deploy works via az CLI.
- Outputs include endpoints and resource ids.
- Deploy is idempotent.
- No secrets in params.

## Files / Areas
- infra/bicep/*
- infra/README.md

## DoD
- Infra deploy documented
- Minimal resources only

## Demo
Deploy dev infra -> see resources created

## Sources (passive)
- Microsoft Learn: Bicep fundamentals + modules
- YouTube: “Bicep end-to-end deployment”
- Docs: App Service + Key Vault deployment patterns
