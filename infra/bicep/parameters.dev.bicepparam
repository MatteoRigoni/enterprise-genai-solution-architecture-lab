using './main.bicep'

param baseName = 'aisa'
param environment = 'dev'
param appServicePlanSkuName = 'B1'
param appServicePlanCapacity = 1
param searchServiceSku = 'basic'
param deployPostgres = false
