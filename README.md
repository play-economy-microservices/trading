# play.trading
Play Economy Trading microservice

## Build the docker image
```powershell
$version="1.0.1"
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
