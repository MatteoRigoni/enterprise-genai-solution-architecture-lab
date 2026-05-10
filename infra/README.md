# Azure infrastructure (Bicep)

Templates under `bicep/` provision the lab footprint from [T09](../docs/backlog/T09-iac-bicep.md): Linux App Service (single host per ADR-0001), Log Analytics + Application Insights, Key Vault, Azure AI Search, and optionally PostgreSQL Flexible Server.

**Secrets:** do not put API keys, connection strings with passwords, or admin passwords in parameter files committed to git. Pass secure values at deploy time (CLI `--parameters` or CI secrets). See `docs/security.md` and T10 for Key Vault + managed identity.

## Prerequisites

- Azure subscription and resource group
- [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli) with Bicep (`az bicep install` if prompted)
- `baseName` must be short (2–16 chars, letters/digits) so derived names stay within Azure limits

## Validate (no Azure deployment)

```bash
az bicep build --file infra/bicep/main.bicep
```

## Deploy (dev)

Replace `rg-aisa-dev` and `eastus` as needed.

```bash
az group create --name rg-aisa-dev --location eastus

az deployment group create \
  --resource-group rg-aisa-dev \
  --template-file infra/bicep/main.bicep \
  --parameters infra/bicep/parameters.dev.bicepparam
```

## Deploy (prod)

```bash
az group create --name rg-aisa-prod --location eastus

az deployment group create \
  --resource-group rg-aisa-prod \
  --template-file infra/bicep/main.bicep \
  --parameters infra/bicep/parameters.prod.bicepparam
```

## Optional: PostgreSQL

1. Set `deployPostgres = true` in a **local** copy of the parameter file, or override on the CLI (do not commit passwords).
2. Provide a strong `postgresAdminPassword` (and optionally override `postgresAdminLogin`).

Example override:

```bash
az deployment group create \
  --resource-group rg-aisa-dev \
  --template-file infra/bicep/main.bicep \
  --parameters infra/bicep/parameters.dev.bicepparam \
  --parameters postgresAdminPassword="$(openssl rand -base64 24)"
```

(On Windows, generate a password with your preferred tool and pass it safely.)

## Outputs

Deployment outputs include the web app hostname, resource IDs, Application Insights connection string, Key Vault URI, and Search endpoint/name. Use `az deployment group show` to retrieve output values after deploy.

## App runtime stack

The web app uses `DOTNETCORE|10.0` by default (`webAppLinuxFxVersion`). If a region does not offer that stack yet, override `webAppLinuxFxVersion` (for example `DOTNETCORE|9.0`) via a custom parameter file.

## Idempotency

Re-running `az deployment group create` with the same parameters updates the resource group deployment in place; ARM applies incremental changes.
