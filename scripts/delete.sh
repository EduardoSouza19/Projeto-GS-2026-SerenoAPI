#!/bin/bash
# Remove TODOS os recursos da API .NET do Sereno na Azure. ATENÇÃO: só rode depois de 30/11 (fim da correção).
RESOURCE_GROUP="rg-sereno"

echo "DELETANDO todos os recursos do grupo ${RESOURCE_GROUP}..."
read -p "Tem certeza? (s/N): " CONFIRMA
if [[ "$CONFIRMA" != "s" && "$CONFIRMA" != "S" ]]; then
  echo "Cancelado."
  exit 0
fi

az group delete --name "$RESOURCE_GROUP" --yes --no-wait
echo "Remoção iniciada! Tire um print do portal Azure como evidência."
