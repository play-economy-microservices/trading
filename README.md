# Trading


## Build the docker image

```powershell
$version="1.0.2"
$env:GH_OWNER="play-economy-microservices"
$env:GH_PAT="[PAT HERE]"
$appname="playeconomycontainerregistry"
docker build --secret id=GH_OWNER --secret id=GH_PAT -t "$appname.azurecr.io/play.trading:$version" .
```

## Run the docker image

```powershell
$cosmosDbConnString="[CONN STRING HERE]"
$serviceBusConnString="[CONN STRING HERE]"
docker run -it --rm -p 5006:5006 --name trading -e
MongoDBSettings__ConnectionString=$cosmosDbConnString -e
ServiceBusSettings__ConnectionString=$serviceBusConnString -e
ServiceSettings__MessageBroker="SERVICEBUS" play.trading:$version
```

## Publishing the Docker image

```powershell
$appname="playeconomycontainerregistry"
$version="1.0.1"
az acr login --name $appname
docker push "$appname.azurecr.io/play.trading:$version"
```

## Creating the Azure Managed Identity and granting it access to the Key Vault

```powershell
$namespace="trading"
$appname="playeconomy"

az identity create --resource-group $appname --name $namespace

$IDENTITY_CLIENT_ID=az identity show -g $appname -n $namespace --query clientId -otsv
az keyvault set-policy -n $appname --secret-permissions get list --spn $IDENTITY_CLIENT_ID
```

## Establish the federated identity credential

```powershell PowerShell
$appname="playeconomy"
$namespace="trading"

$AKS_OIDC_ISSUER=az aks show -n $appname -g $appname --query "oidcIssuerProfile.issuerUrl" -otsv

az identity federated-credential create --name $namespace --identity-name $namespace
--resource-group $appname --issuer $AKS_OIDC_ISSUER --subject "system:serviceaccount:${namespace}:${namespace}-serviceaccount"
```

## Install the Helm Chart

```powershell
$appname="playeconomyacr"
$namespace="trading"

$helmUser=[guid]::Empty.Guid
$helmPassword=az acr login --name $appname --expose-token --output tsv --query accessToken

# This is no longer needed after Helm v3.8.0

$env:HELM_EXPERIMENTAL_OCI=1

# authenticate

helm registry login "$appname.azurecr.io" --username $helmUser --password $helmPassword

# Install the Helm Chart from ACR with trading Values

$chartVersion="0.1.0"
helm upgrade trading-service oci://$appname.azurecr.io/helm/microservice --version $chartVersion -f ./helm/values.yaml -n $namespace --install
```

## Required repository secrets for GitHub Workflow

- `GH_PAT`
- `AZURE_CLIENT_ID`
- `AZURE_TENANT_ID`
- `AZURE_SUBSCRIPTION_ID`
