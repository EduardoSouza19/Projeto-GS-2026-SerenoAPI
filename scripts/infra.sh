#!/bin/bash
# =====================================================================
# Sereno - API .NET - Provisionamento na Azure (Azure CLI)
#
# Cria:
#   1. Resource Group
#   2. Azure Container Registry (ACR) - guarda a imagem Docker da API
#   3. Oracle XE em container (ACI)
#   4. MongoDB em container (ACI)
#   5. Build da imagem da API direto no ACR (não precisa de Docker na máquina)
#   6. App Service Plan Linux + Web App for Containers rodando a imagem
#
# Uso (Azure Cloud Shell, na raiz do projeto, depois de "az login"):
#   export ORACLE_PASSWORD='SenhaForte123'
#   export MONGO_PASSWORD='OutraSenhaForte123'
#   export JWT_SECRET='segredo-com-32-caracteres-ou-mais'
#   bash scripts/infra.sh
#
# Nenhuma senha fica escrita neste arquivo: tudo vem de variáveis de ambiente.
# =====================================================================
set -e

RM="${RM:-562156}"
LOCATION="${LOCATION:-brazilsouth}"
RESOURCE_GROUP="rg-sereno"
ACR_NAME="acrsereno${RM}"
ORACLE_CONTAINER="aci-oracle-sereno-rm${RM}"
MONGO_CONTAINER="aci-mongo-sereno-rm${RM}"
PLAN="plan-sereno"
WEBAPP="sereno-dotnet-rm${RM}"
IMAGE="sereno-api:latest"

: "${ORACLE_PASSWORD:?Defina ORACLE_PASSWORD (export ORACLE_PASSWORD='...')}"
: "${MONGO_PASSWORD:?Defina MONGO_PASSWORD (export MONGO_PASSWORD='...')}"
: "${JWT_SECRET:?Defina JWT_SECRET (mínimo 32 caracteres)}"

echo "== Sereno API .NET - Provisionamento Azure (RM ${RM}) =="

echo "[1/6] Resource Group..."
az group create --name "$RESOURCE_GROUP" --location "$LOCATION" --output table

echo "[2/6] Azure Container Registry..."
az acr create --resource-group "$RESOURCE_GROUP" --name "$ACR_NAME" --sku Basic --admin-enabled true --output table
ACR_SERVER=$(az acr show --name "$ACR_NAME" --query loginServer --output tsv)
ACR_USER=$(az acr credential show --name "$ACR_NAME" --query username --output tsv)
ACR_PASS=$(az acr credential show --name "$ACR_NAME" --query "passwords[0].value" --output tsv)

echo "[3/6] Oracle XE em container (ACI)..."
az container create \
  --resource-group "$RESOURCE_GROUP" \
  --name "$ORACLE_CONTAINER" \
  --image gvenzl/oracle-xe:21-slim \
  --os-type Linux --cpu 2 --memory 4 \
  --ports 1521 --ip-address Public --dns-name-label "$ORACLE_CONTAINER" \
  --environment-variables APP_USER=sereno \
  --secure-environment-variables ORACLE_PASSWORD="$ORACLE_PASSWORD" APP_USER_PASSWORD="$ORACLE_PASSWORD" \
  --output table
ORACLE_HOST=$(az container show --resource-group "$RESOURCE_GROUP" --name "$ORACLE_CONTAINER" --query ipAddress.fqdn --output tsv)

echo "[4/6] MongoDB em container (ACI)..."
az container create \
  --resource-group "$RESOURCE_GROUP" \
  --name "$MONGO_CONTAINER" \
  --image mongo:7 \
  --os-type Linux --cpu 1 --memory 1.5 \
  --ports 27017 --ip-address Public --dns-name-label "$MONGO_CONTAINER" \
  --environment-variables MONGO_INITDB_ROOT_USERNAME=sereno \
  --secure-environment-variables MONGO_INITDB_ROOT_PASSWORD="$MONGO_PASSWORD" \
  --output table
MONGO_HOST=$(az container show --resource-group "$RESOURCE_GROUP" --name "$MONGO_CONTAINER" --query ipAddress.fqdn --output tsv)

echo "[5/6] Build da imagem da API no ACR..."
az acr build --registry "$ACR_NAME" --image "$IMAGE" --file dockerfiles/Dockerfile .

echo "[6/6] App Service Plan + Web App for Containers..."
az appservice plan create --name "$PLAN" --resource-group "$RESOURCE_GROUP" --is-linux --sku B1 --output table

az webapp create \
  --name "$WEBAPP" \
  --resource-group "$RESOURCE_GROUP" \
  --plan "$PLAN" \
  --deployment-container-image-name "${ACR_SERVER}/${IMAGE}" \
  --output table

az webapp config container set \
  --name "$WEBAPP" \
  --resource-group "$RESOURCE_GROUP" \
  --docker-custom-image-name "${ACR_SERVER}/${IMAGE}" \
  --docker-registry-server-url "https://${ACR_SERVER}" \
  --docker-registry-server-user "$ACR_USER" \
  --docker-registry-server-password "$ACR_PASS" \
  --output none

az webapp config appsettings set \
  --name "$WEBAPP" \
  --resource-group "$RESOURCE_GROUP" \
  --settings \
    WEBSITES_PORT=8080 \
    ASPNETCORE_ENVIRONMENT="Production" \
    AuthDev__Habilitado="true" \
    ConnectionStrings__Default="User Id=sereno;Password=${ORACLE_PASSWORD};Data Source=${ORACLE_HOST}:1521/XEPDB1" \
    MongoDbSettings__ConnectionString="mongodb://sereno:${MONGO_PASSWORD}@${MONGO_HOST}:27017" \
    MongoDbSettings__DatabaseName="sereno" \
    JWT_SECRET="$JWT_SECRET" \
  --output none

echo ""
echo "== PRONTO! =="
echo "Imagem:   ${ACR_SERVER}/${IMAGE}"
echo "Oracle:   ${ORACLE_HOST}:1521/XEPDB1 (usuário: sereno)"
echo "MongoDB:  ${MONGO_HOST}:27017 (usuário: sereno)"
echo "API:      https://${WEBAPP}.azurewebsites.net/swagger"
echo "Health:   https://${WEBAPP}.azurewebsites.net/health"
echo ""
echo "Obs.: o Oracle leva de 2 a 5 minutos para ficar pronto; a API cria as tabelas e o seed sozinha."
echo "Pipeline: crie a service connection do ACR com o nome 'sereno-acr' e use o azure-pipeline.yml."
