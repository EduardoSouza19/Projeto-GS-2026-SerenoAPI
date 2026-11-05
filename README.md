# Sereno — SerenoAPI (Elevador Funciona?)

> API RESTful em ASP.NET Core 8 que mostra, **antes de a pessoa sair de casa**, quais elevadores, escadas rolantes, rampas e plataformas das estações de São Paulo estão funcionando. Usa Oracle, MongoDB, JWT, idempotência, Rate Limiting, Health Checks, Docker e deploy na Azure com Azure DevOps.

**Global Solution 2026/2 — CitizenTech · FIAP · Advanced Business Development with .NET · 2TDSPI**

---

## Equipe

| Nome | RM |
|---|---|
| Pedro Henrique dos Santos Costa | RM562156 |
| Eduardo Augusto de Oliveira Souza | RM565269 |
| Fellipe Costa de Oliveira | RM564673 |
| Felype Ferreira Maschio | RM563009 |
| Gustavo Vieira de Matos | RM563304 |

---

## Descrição

Cidadãos reportam se os equipamentos de acessibilidade das estações estão **FUNCIONANDO**, **PARADO** ou **EM_MANUTENCAO**. A API mantém o status atual de cada equipamento e avisa quantos estão parados em cada estação.

- **Público:** cadeirantes, idosos, pessoas com mobilidade reduzida e quem usa carrinho de bebê.
- **Pensado para quem não sabe coordenadas:**
  - a viagem é consultada **pelo nome das estações** (`/api/rotas/acessibilidade?origem=Sé&destino=Paulista`);
  - o endereço da estação pode ser preenchido **só com o CEP** (ViaCEP);
  - latitude e longitude são opcionais.
- **ODS:** 10, 11 e 3.
- **Base legal:** Lei Brasileira de Inclusão (Lei 13.146/2015).

## Requisitos da disciplina

| # | Requisito | Pts | Onde está |
|---|---|---|---|
| 1 | API RESTful com validações, códigos HTTP e Swagger/OpenAPI | 20 | `Controllers/`, `Models/Requests.cs` (DataAnnotations), erro padrão no `Program.cs`, Swagger e Scalar |
| 2 | JWT protegendo endpoints | 20 | `Program.cs` (AddJwtBearer), `[Authorize]` e política `Gestor`; token em `POST /api/auth/token` |
| 3 | Idempotência | 15 | `Services/IdempotencyService.cs` + `POST /api/reportes` com header `Idempotency-Key` |
| 4 | Rate Limiting com 429 | 15 | `Program.cs`: 100 req/min no geral e 10 reportes/min por usuário, com `Retry-After` |
| 5 | Testes unitários e de integração (xUnit) | 20 | `SerenoAPI.UnitTests/` e `SerenoAPI.IntegrationTests/` |
| 6 | Health Checks | 10 | `/health`: **Oracle** e **MongoDB** |

---

## Arquitetura

```text
        App Mobile / Swagger / Scalar / Postman
                        |  Authorization: Bearer <token>
                        v
            +--------------------------+
            |   SerenoAPI (.NET 8)     |
            | JWT · Rate Limit · CORS  |
            +--------------------------+
              |            |           |
         Controllers    Services    Serilog / OpenTelemetry
              |            |
              |            +--> ViaCEP (endereço pelo CEP)
              v
     EF Core --> Oracle  (ESTACOES, EQUIPAMENTOS, REPORTES, IDEMPOTENCY_RECORDS)
     MongoDB.Driver --> MongoDB "sereno" (historico_reportes)

  Azure: Boards -> Repos -> Pipeline (testes + imagem Docker no ACR) -> Release -> Web App for Containers
         Oracle e MongoDB rodando em containers (ACI)
```

## Regras de negócio

```text
Estacao (1) ──> (N) Equipamento (1) ──> (N) Reporte
```

- **`statusAtual`** do equipamento = status do **reporte mais recente**. É recalculado ao criar, editar ou excluir um reporte.
- **`equipamentosParados`** = equipamentos PARADO ou EM_MANUTENCAO. Aparece na lista de estações.
- **Classificação da estação:**

  | Classificação | Quando |
  |---|---|
  | ACESSIVEL | Tudo funcionando |
  | PARCIAL | Algum parado |
  | INACESSIVEL | Todos parados |
  | SEM_INFORMACAO | Sem equipamentos |

- Só o **dono** do reporte, ou um **GESTOR**, edita ou exclui (senão, 403).
- Estação ou equipamento inexistente no corpo da requisição → **422**.
- **Seed:** 5 estações (Sé, Luz, Paulista, Tatuapé e Palmeiras-Barra Funda) e 12 equipamentos, alguns já parados.

## Estrutura

```text
SerenoAPI/
├── Controllers/     Auth, Estacoes, Equipamentos, Reportes, Rotas, Cep, Historico, MongoTest
├── Data/            AppDbContext e DataSeeder
├── Models/          Entidades, enums, requests e responses
├── Repositories/    MongoHistoricoRepository
├── Services/        StatusService, IdempotencyService, TokenService, ViaCepService, MongoHealthCheck
├── Settings/        MongoDbSettings
├── SettingsJWT/     JwtSettings
├── Telemetry/       OpenTelemetry
├── Properties/      launchSettings.json
├── dockerfiles/     Dockerfile, docker-compose.yml, .env.example
├── scripts/         infra.sh (Azure CLI), delete.sh, script-bd.sql
├── SerenoAPI.UnitTests/
├── SerenoAPI.IntegrationTests/
├── azure-pipeline.yml
├── SerenoAPI.http
└── Program.cs
```

---

## Endpoints

🔒 = precisa de token · 👮 = somente GESTOR

| Método | Rota | Acesso | Corpo / observação |
|---|---|---|---|
| POST | `/api/auth/token` | público | `{email}`: `gestor@sereno.app` ou `cidadao@sereno.app` |
| GET | `/api/estacoes?page=0&size=10&linha=&busca=` | 🔒 | Paginado, com `equipamentosParados` |
| GET | `/api/estacoes/{id}` | 🔒 | Com os equipamentos e o `statusAtual` |
| POST · PUT · DELETE | `/api/estacoes`, `/api/estacoes/{id}` | 👮 | `{nome, linha, endereco ou cep, latitude?, longitude?}` |
| GET | `/api/equipamentos/{id}` | 🔒 | |
| POST · PUT · DELETE | `/api/equipamentos`, `/api/equipamentos/{id}` | 👮 | `{estacaoId, tipo, localizacao}` |
| POST | `/api/reportes` | 🔒 + `Idempotency-Key` | `{equipamentoId, status, observacao}` |
| GET | `/api/reportes/meus?page=0&size=10` | 🔒 | Paginado |
| GET | `/api/reportes/{id}` | 🔒 | |
| PUT · DELETE | `/api/reportes/{id}` | dono ou GESTOR | `{status, observacao}` |
| GET | `/api/rotas/acessibilidade?origem=&destino=` | 🔒 | Pelo **nome** das estações |
| GET | `/api/cep/{cep}` | público | ViaCEP |
| GET | `/api/historico?equipamentoId=` | 👮 | Histórico no MongoDB |
| GET | `/api/MongoTest` | público | Testa a conexão com o MongoDB |
| GET | `/health` | público | Oracle e MongoDB |

**Valores aceitos:**

| Campo | Valores |
|---|---|
| `tipo` | `ELEVADOR`, `ESCADA_ROLANTE`, `RAMPA`, `PLATAFORMA` |
| `status` | `FUNCIONANDO`, `PARADO`, `EM_MANUTENCAO` |

**Paginação:**

```json
{ "items": [ ... ], "page": 0, "size": 10, "totalItems": 42, "totalPages": 5 }
```

**Erro** (400, 401, 403, 404, 409, 422, 429, 500):

```json
{ "status": 400, "erro": "VALIDACAO", "mensagem": "Dados inválidos",
  "campos": [ { "campo": "status", "mensagem": "valor inválido" } ],
  "timestamp": "2026-11-10T14:30:00Z", "path": "/api/reportes" }
```

---

## JWT

| Item | Valor |
|---|---|
| Algoritmo | HS256 |
| Segredo | variável `JWT_SECRET` (32 ou mais caracteres; nunca no código) |
| Emissor / audiência | `sereno-auth` / `sereno-app` |
| Claims | `sub` (id), `email`, `role` (`CIDADAO` ou `GESTOR`) |
| Validade | 2 horas |

- O token é gerado em `POST /api/auth/token`. O endpoint funciona em Development ou com `AuthDev__Habilitado=true`, que o script da Azure já liga.
- Como o formato é o do Contrato do grupo, tokens emitidos por outros serviços com o mesmo segredo também são aceitos.
- Sem token → **401** `NAO_AUTENTICADO`. CIDADAO em rota de gestor → **403** `ACESSO_NEGADO`.

## Idempotência

O header `Idempotency-Key` é obrigatório em `POST /api/reportes`. O app gera um UUID ao abrir o formulário.

| Situação | Resposta |
|---|---|
| Sem a chave | **400** |
| Mesma chave + mesmo corpo | Devolve a resposta original (mesmo id), com `Idempotency-Replayed: true`, **sem duplicar** |
| Mesma chave + corpo diferente | **409** |

O registro fica em `IDEMPOTENCY_RECORDS` e vale 24 horas.

## Rate Limiting

| Política | Limite |
|---|---|
| Geral | 100 requisições/min por usuário ou IP |
| `reportes` | 10/min por usuário |

Ao passar do limite, a resposta é **429** com o header `Retry-After`. Os limites ficam em `appsettings.json` → `RateLimit`.

## Health Check

`GET /health` verifica **Oracle** (`AspNetCore.HealthChecks.Oracle`) e **MongoDB** (ping). Healthy responde 200; Unhealthy responde 503.

---

## Como executar (passo a passo)

### 1. Restaurar e compilar

```powershell
cd SerenoAPI
dotnet restore
dotnet build
```

### 2. Segredos (User Secrets)

```powershell
dotnet user-secrets set "JWT_SECRET" "um-segredo-com-32-caracteres-ou-mais-aqui"
dotnet user-secrets set "ConnectionStrings:Default" "User Id=RM000000;Password=SUA_SENHA;Data Source=oracle.fiap.com.br:1521/ORCL"
dotnet user-secrets set "MongoDbSettings:ConnectionString" "mongodb+srv://USUARIO:SENHA@SEU-CLUSTER.mongodb.net/"
```

### 3. Tabelas no Oracle

Escolha **uma** opção:

- **Migrations:**
  ```powershell
  dotnet tool install --global dotnet-ef --version 8.0.11
  dotnet ef migrations add InitialCreate
  dotnet ef database update
  ```
- **Script:** `scripts/script-bd.sql` no SQL Developer.
- **Banco vazio** (Docker ou Azure): a API cria as tabelas sozinha no primeiro start.

### 4. Executar

```powershell
dotnet run
```

Acesse:

- Swagger: http://localhost:5241/swagger
- Scalar: http://localhost:5241/scalar/v1
- Health: http://localhost:5241/health

### 5. Teste rápido no Swagger

1. `POST /api/auth/token` com `{"email":"gestor@sereno.app"}`. Copie o token e clique em **Authorize**.
2. `GET /api/estacoes`: aparecem as 5 estações do seed.
3. `POST /api/reportes` com `Idempotency-Key: teste-001` e `{"equipamentoId":1,"status":"FUNCIONANDO"}`. Resposta **201**.
4. Repita o mesmo POST. Volta **o mesmo id**, sem duplicar.
5. Mesma chave com `"status":"PARADO"`. Resposta **409**.
6. Envie 11 reportes com chaves diferentes. O 11º responde **429**.
7. Gere o token de `cidadao@sereno.app` e tente `POST /api/estacoes`. Resposta **403**.
8. `GET /api/rotas/acessibilidade?origem=Sé&destino=Paulista`.

### Problemas comuns

| Erro | Solução |
|---|---|
| `Configure a variável JWT_SECRET` | Passo 2 |
| `ORA-00942` | Passo 3 |
| `ORA-01017` | Usuário ou senha do Oracle errados |
| 401 em tudo | Gere o token e clique em **Authorize** |
| `/health` com mongodb Unhealthy | Configure o MongoDB e libere o seu IP no Atlas |

---

## Docker

Na raiz do projeto:

```bash
cp dockerfiles/.env.example dockerfiles/.env        # troque as senhas
docker compose -f dockerfiles/docker-compose.yml --env-file dockerfiles/.env up -d --build
```

- Sobe **Oracle XE + MongoDB + API** (http://localhost:8080/swagger). A API cria as tabelas e o seed sozinha.
- Para parar: `docker compose -f dockerfiles/docker-compose.yml down`.

---

## DevOps (Azure)

### 1. Infraestrutura: `scripts/infra.sh` (Azure CLI)

No Azure Cloud Shell, na raiz do repositório:

```bash
export ORACLE_PASSWORD='SenhaForte123'
export MONGO_PASSWORD='OutraSenhaForte123'
export JWT_SECRET='segredo-com-32-caracteres-ou-mais'
bash scripts/infra.sh
```

O script cria:

| Recurso | Nome |
|---|---|
| Resource Group | `rg-sereno` |
| Azure Container Registry | `acrsereno562156` |
| Oracle XE (ACI) | `aci-oracle-sereno-rm562156` |
| MongoDB (ACI) | `aci-mongo-sereno-rm562156` |
| App Service Plan Linux | `plan-sereno` |
| Web App for Containers | `sereno-dotnet-rm562156` |

O script também faz o primeiro build da imagem no ACR e configura todas as variáveis de ambiente. Nenhuma senha fica escrita nele. Para outro RM: `export RM=563304` antes de rodar.

Para apagar tudo (só depois de 30/11): `bash scripts/delete.sh`.

### 2. Build (CI): `azure-pipeline.yml`

1. No Azure DevOps, crie a service connection **Docker Registry → Azure Container Registry** com o nome **`sereno-acr`**.
2. O gatilho é só na `main` (após o merge do PR).
3. Etapas:
   - restore;
   - build;
   - testes xUnit, com resultados publicados;
   - **build e push da imagem** `sereno-api:<BuildId>` e `latest` no ACR;
   - artefato `drop` (scripts + `imagem.txt`).

### 3. Release (CD)

1. Pipelines → Releases → New → **Azure App Service deployment**.
2. Artifact: o build acima, com o **gatilho de implantação contínua** ligado.
3. Tarefa **Azure Web App for Containers**:
   - App: `sereno-dotnet-rm562156`;
   - Image: `acrsereno562156.azurecr.io/sereno-api:$(Build.BuildId)`.

### Variáveis de ambiente da API

| Variável | Uso |
|---|---|
| `ConnectionStrings__Default` | Oracle |
| `MongoDbSettings__ConnectionString` | MongoDB |
| `JWT_SECRET` | Segredo do JWT |
| `AuthDev__Habilitado` | Liga o `POST /api/auth/token` fora do Development |
| `WEBSITES_PORT=8080` | Porta do container no Web App |

---

## Testes

```bash
dotnet test
```

### Unitários

| Arquivo | O que testa |
|---|---|
| `StatusServiceTests` | Status atual = último reporte; sem reportes = FUNCIONANDO; contagem de parados; classificação |
| `IdempotencyServiceTests` | Mesma chave devolve a resposta salva; corpo diferente gera outro hash; chave de outro usuário não vale; registro com mais de 24 h é descartado |
| `EstacoesControllerTests` | Detalhe com parados; 404 no formato do Contrato; estação **sem latitude e longitude**; sem endereço e sem CEP → 400 |

### Integração (WebApplicationFactory + banco em memória; o teste gera o próprio JWT)

| Arquivo | O que testa |
|---|---|
| `ReportesControllerIntegrationTests` | 401; 400 sem chave; 201; reenvio não duplica; 409; status inválido 400; **429** no 11º; 403 ao excluir reporte alheio |
| `EstacoesControllerIntegrationTests` | 401; página no formato do Contrato; 403 para CIDADAO; CRUD completo como GESTOR; rota pelo nome; `/health` com Oracle e MongoDB |

---

## Exemplos JSON (CRUD)

```json
POST /api/estacoes
{ "nome": "Brás", "linha": "3-Vermelha, 10-Turquesa", "endereco": "Brás, São Paulo - SP" }

POST /api/equipamentos
{ "estacaoId": 1, "tipo": "ELEVADOR", "localizacao": "Acesso pela Praça da Sé" }

POST /api/reportes        (header Idempotency-Key: <uuid>)
{ "equipamentoId": 1, "status": "PARADO", "observacao": "Elevador parado desde cedo" }

PUT /api/reportes/1
{ "status": "FUNCIONANDO", "observacao": "Voltou a funcionar" }
```

Mais exemplos no arquivo `SerenoAPI.http`.

---

## Uso de IA Generativa

O grupo usou o **Claude (Anthropic)** como apoio para adaptar a estrutura do projeto do Challenge, revisar as regras de idempotência, rate limiting e health checks e escrever a documentação. O código foi revisado, executado e testado pelo grupo, que sabe explicar cada parte na apresentação.
