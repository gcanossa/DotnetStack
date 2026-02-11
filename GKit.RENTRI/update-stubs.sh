#!/bin/bash

declare -A APIS
APIS[Anagrafiche]="https://api.rentri.gov.it/docs/anagrafiche/v1.0"
APIS[CaRentri]="https://api.rentri.gov.it/docs/ca-rentri/v1.0"
APIS[Codifiche]="https://api.rentri.gov.it/docs/codifiche/v1.0"
APIS[DatiRegistri]="https://api.rentri.gov.it/docs/dati-registri/v1.0"
APIS[Formulari]="https://api.rentri.gov.it/docs/formulari/v1.0"
APIS[VidimazioneFormulari]="https://api.rentri.gov.it/docs/vidimazione-formulari/v1.0"

for API_NAME in "${!APIS[@]}"
do
  API_URL="${APIS[$API_NAME]}"

  curl -X GET "$API_URL" | \
    yq "del(.paths.[].[] | select(.deprecated == true))" | \
    yq "del(.paths.[] | select(objects and length == 0))" \
    > "${API_NAME}.json"

  nswag openapi2csclient \
    /input:"${API_NAME}.json" \
    /classname:"${API_NAME}Stub" \
    /namespace:"GKit.RENTRI.Stubs.${API_NAME}" \
    /output:"Stubs/${API_NAME}Stub.cs" \
    /jsonlibrary:SystemTextJson \
    /jsonpolymorphicserializationstyle:SystemTextJson \
    /clientbaseclass:BaseClient

  rm "${API_NAME}.json"
done
