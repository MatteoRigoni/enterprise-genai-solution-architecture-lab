using './main.bicep'

param baseName = 'aisa'
param environment = 'prod'
param appServicePlanSkuName = 'P1v2'
param appServicePlanCapacity = 1
param searchServiceSku = 'standard'
param deployPostgres = false
