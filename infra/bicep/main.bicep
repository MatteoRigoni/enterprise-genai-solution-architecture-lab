targetScope = 'resourceGroup'

@minLength(2)
@maxLength(16)
@description('Short name prefix (letters and digits). Used in resource names.')
param baseName string

@description('Azure region for all resources.')
param location string = resourceGroup().location

@allowed(['dev', 'prod'])
@description('Environment label for naming and tagging.')
param environment string

@allowed(['B1', 'B2', 'B3', 'S1', 'P1v2', 'P2v2'])
@description('App Service Plan SKU name (lab-friendly default: B1).')
param appServicePlanSkuName string = 'B1'

@allowed([1, 2, 3])
@description('App Service Plan instance count.')
param appServicePlanCapacity int = 1

@allowed(['basic', 'standard', 'standard2'])
@description('Azure AI Search SKU (dev: basic; prod: standard or standard2).')
param searchServiceSku string = 'basic'

@description('Optional: deploy Azure Database for PostgreSQL Flexible Server (extra cost).')
param deployPostgres bool = false

@description('PostgreSQL admin login when deployPostgres is true.')
param postgresAdminLogin string = 'aisaadmin'

@secure()
@description('PostgreSQL admin password when deployPostgres is true (provide via CLI or CI secret, never commit).')
param postgresAdminPassword string = ''

@description('App Service Linux stack (DOTNETCORE|10.0 when available in the region).')
param webAppLinuxFxVersion string = 'DOTNETCORE|10.0'

var tags = {
  Environment: environment
  Application: 'AiSa'
}

var logAnalyticsName = '${baseName}-${environment}-law'
var appInsightsName = '${baseName}-${environment}-ai'
var appServicePlanName = '${baseName}-${environment}-plan'
var webAppName = '${baseName}-${environment}-web'
// Key Vault name: alphanumeric only, 3–24 chars, globally unique
var keyVaultName = take('kv${uniqueString(subscription().subscriptionId, resourceGroup().id, baseName, environment)}', 24)
// Search service name: lowercase, globally unique
var searchServiceName = '${baseName}${environment}search${take(uniqueString(resourceGroup().id, baseName, 'search'), 10)}'
var postgresServerName = '${baseName}-${environment}-pg-${take(uniqueString(resourceGroup().id, baseName, 'pg'), 8)}'

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: logAnalyticsName
  location: location
  tags: tags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: environment == 'prod' ? 90 : 30
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  tags: tags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    Flow_Type: 'Bluefield'
    Request_Source: 'rest'
    WorkspaceResourceId: logAnalytics.id
  }
}

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  tags: tags
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true
    enabledForDeployment: false
    enabledForDiskEncryption: false
    enabledForTemplateDeployment: true
  }
}

resource searchService 'Microsoft.Search/searchServices@2023-11-01' = {
  name: searchServiceName
  location: location
  tags: tags
  sku: {
    name: searchServiceSku
  }
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    replicaCount: environment == 'prod' ? 2 : 1
    partitionCount: 1
    hostingMode: 'default'
    publicNetworkAccess: 'enabled'
  }
}

resource appServicePlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: appServicePlanName
  location: location
  tags: tags
  sku: {
    name: appServicePlanSkuName
    capacity: appServicePlanCapacity
  }
  kind: 'linux'
  properties: {
    reserved: true
  }
}

resource webApp 'Microsoft.Web/sites@2023-12-01' = {
  name: webAppName
  location: location
  tags: tags
  kind: 'app,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    clientAffinityEnabled: false
    siteConfig: {
      linuxFxVersion: webAppLinuxFxVersion
      alwaysOn: environment == 'prod'
      http20Enabled: true
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
      appSettings: [
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsights.properties.ConnectionString
        }
        {
          name: 'ApplicationInsightsAgent_EXTENSION_VERSION'
          value: '~3'
        }
        {
          name: 'XDT_MicrosoftApplicationInsights_Mode'
          value: 'Recommended'
        }
      ]
    }
  }
}

resource postgresServer 'Microsoft.DBforPostgreSQL/flexibleServers@2023-12-01-preview' = if (deployPostgres) {
  name: postgresServerName
  location: location
  tags: tags
  sku: {
    name: environment == 'prod' ? 'Standard_D2s_v3' : 'Standard_B1ms'
    tier: environment == 'prod' ? 'GeneralPurpose' : 'Burstable'
  }
  properties: {
    version: '16'
    administratorLogin: postgresAdminLogin
    administratorLoginPassword: postgresAdminPassword
    storage: {
      storageSizeGB: environment == 'prod' ? 128 : 32
    }
    backup: {
      backupRetentionDays: environment == 'prod' ? 14 : 7
      geoRedundantBackup: environment == 'prod' ? 'Enabled' : 'Disabled'
    }
    highAvailability: {
      mode: 'Disabled'
    }
    authConfig: {
      activeDirectoryAuth: 'Disabled'
      passwordAuth: 'Enabled'
    }
  }
}

@description('Default hostname of the Linux Web App (HTTPS).')
output webAppDefaultHostName string = webApp.properties.defaultHostName

@description('Web App resource name.')
output webAppNameOut string = webApp.name

@description('Web App Azure resource ID.')
output webAppResourceId string = webApp.id

@description('App Service principal ID (managed identity) for Key Vault RBAC in T10.')
output webAppPrincipalId string = webApp.identity.principalId

@description('Application Insights connection string (store in Key Vault for production; see security.md).')
output applicationInsightsConnectionString string = appInsights.properties.ConnectionString

@description('Application Insights resource ID.')
output applicationInsightsResourceId string = appInsights.id

@description('Log Analytics workspace resource ID.')
output logAnalyticsWorkspaceId string = logAnalytics.id

@description('Key Vault URI.')
output keyVaultUri string = keyVault.properties.vaultUri

@description('Key Vault resource ID.')
output keyVaultResourceId string = keyVault.id

@description('Azure AI Search HTTPS endpoint.')
output searchServiceEndpoint string = 'https://${searchService.name}.search.windows.net'

@description('Azure AI Search resource name (also used as part of the endpoint).')
output searchServiceNameOut string = searchService.name

@description('Azure AI Search resource ID.')
output searchServiceResourceId string = searchService.id

@description('PostgreSQL FQDN when deployPostgres is true; empty otherwise.')
output postgresFqdn string = deployPostgres ? postgresServer.properties.fullyQualifiedDomainName : ''
