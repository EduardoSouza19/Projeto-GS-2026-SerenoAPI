# Sereno — SerenoAPI (Elevador Funciona?)

> API RESTful em ASP.NET Core 8 que mostra, **antes de a pessoa sair de casa**, quais elevadores, escadas rolantes, rampas e plataformas das estações de São Paulo estão funcionando. Usa Oracle, MongoDB, JWT, idempotência, Rate Limiting, Health Checks e Docker, com deploy no Render (e scripts prontos para a Azure).

**Global Solution 2026/2 — CitizenTech · FIAP · Advanced Business Development with .NET · 2TDSPI**

---

## Sumário

1. [Equipe](#equipe)
2. [O que a API faz](#o-que-a-api-faz)
3. [Requisitos da disciplina](#requisitos-da-disciplina)
4. [Arquitetura](#arquitetura)
5. [Regras de negócio](#regras-de-negócio)
6. [Coordenadas das estações](#coordenadas-das-estações)
7. [Estrutura do projeto](#estrutura-do-projeto)
8. [Endpoints](#endpoints)
9. [Formatos do Contrato](#formatos-do-contrato)
10. [Segurança: JWT](#segurança-jwt)
11. [Idempotência](#idempotência)
12. [Rate Limiting](#rate-limiting)
13. [Health Check](#health-check)
14. [Como executar na sua máquina](#como-executar-na-sua-máquina)
15. [Como publicar no Render](#como-publicar-no-render)
16. [Como integrar com o app mobile](#como-integrar-com-o-app-mobile)
17. [Docker](#docker)
18. [DevOps na Azure](#devops-na-azure)
19. [Testes](#testes)
20. [Problemas comuns](#problemas-comuns)
21. [Fontes de dados e atribuição](#fontes-de-dados-e-atribuição)
22. [Uso de IA Generativa](#uso-de-ia-generativa)

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

## O que a API faz

O cidadão reporta se um equipamento de acessibilidade da estação está **FUNCIONANDO**, **PARADO** ou **EM_MANUTENCAO**. A API guarda o status atual de cada equipamento, conta quantos estão parados em cada estação e diz se uma viagem entre duas estações é acessível.

- **Público:** cadeirantes, idosos, pessoas com mobilidade reduzida e quem usa carrinho de bebê.
- **Pensada para quem não sabe coordenadas:**
  - a viagem é consultada **pelo nome das estações** (`/api/rotas/acessibilidade?origem=Sé&destino=Paulista`);
  - o endereço da estação pode ser preenchido **só com o CEP** (ViaCEP);
  - **latitude e longitude são preenchidas pela própria API** para todas as estações de Metrô e trem da Grande São Paulo.
- **ODS:** 3, 10 e 11.
- **Base legal:** Lei Brasileira de Inclusão (Lei 13.146/2015).

---

## Requisitos da disciplina

| # | Requisito | Pts | Onde está |
|---|---|---|---|
| 1 | API RESTful com validações, códigos HTTP e Swagger/OpenAPI | 20 | `Controllers/` (cada endpoint documenta suas respostas com `[ProducesResponseType]`), `Models/Requests.cs` (DataAnnotations), erro padrão no `Program.cs`, Swagger e Scalar |
| 2 | JWT protegendo endpoints | 20 | `Program.cs` (AddJwtBearer), `[Authorize]` e política `Gestor` |
| 3 | Idempotência | 15 | `Services/IdempotencyService.cs` + `POST /api/reportes` com header `Idempotency-Key` |
| 4 | Rate Limiting com 429 | 15 | `Program.cs`: 100 req/min no geral e 10 reportes/min por usuário, com `Retry-After` |
| 5 | Testes unitários e de integração (xUnit) | 20 | `SerenoAPI.UnitTests/` (25 testes) e `SerenoAPI.IntegrationTests/` (15 testes) |
| 6 | Health Checks | 10 | `/health`: **API**, bancos (**Oracle** e **MongoDB**) e serviço externo (**ViaCEP**) |

---

## Arquitetura

```text
          App Mobile (React Native) / Swagger / Scalar / Postman
                          |  Authorization: Bearer <token do login Java>
                          v
              +------------------------------+
              |     SerenoAPI (.NET 8)       |
              | JWT · Rate Limit · CORS ·    |
              | Idempotência · Health Checks |
              +------------------------------+
                |             |            |
           Controllers     Services     Serilog / OpenTelemetry
                |             |
                |             +--> ViaCEP (endereço pelo CEP)
                |             +--> CoordenadasEstacoes (186 estações com lat/long)
                v
   EF Core --------> Oracle   (ESTACOES, EQUIPAMENTOS, REPORTES, IDEMPOTENCY_RECORDS)
   MongoDB.Driver -> MongoDB  (historico_reportes)

   Produção: GitHub (main) -> Render (build do Dockerfile, HTTPS automático)
             Oracle da FIAP + MongoDB Atlas
   Alternativa: scripts/infra.sh + azure-pipeline.yml (Azure)
```

---

## Regras de negócio

```text
Estacao (1) ──> (N) Equipamento (1) ──> (N) Reporte
```

- **`statusAtual`** do equipamento = status do **reporte mais recente** (sem regra de consenso). É recalculado ao criar, editar ou excluir um reporte. Sem reportes = `FUNCIONANDO`.
- **`atualizadoEm`** do equipamento = data do reporte mais recente (o app mostra "confirmado há 12 min").
- **`equipamentosParados`** = equipamentos `PARADO` ou `EM_MANUTENCAO`. Aparece na lista de estações.
- **Classificação da estação** (usada na rota acessível):

  | Classificação | Quando |
  |---|---|
  | `ACESSIVEL` | Tudo funcionando |
  | `PARCIAL` | Algum parado |
  | `INACESSIVEL` | Todos parados |
  | `SEM_INFORMACAO` | Sem equipamentos |

- **Viagem:** se uma das duas estações for `INACESSIVEL`, a viagem é inacessível; se as duas forem `ACESSIVEL`, é acessível; o resto é `PARCIAL`.
- Só o **dono** do reporte, ou um **GESTOR**, edita ou exclui (senão, 403).
- O id do usuário do reporte vem do claim `sub` do token, nunca do corpo.
- Estação ou equipamento inexistente no corpo da requisição → **422**.
- **Seed:** na primeira execução, 5 estações (Sé, Luz, Paulista, Tatuapé e Palmeiras-Barra Funda) e 12 equipamentos, alguns já parados.

---

## Coordenadas das estações

O app usa latitude e longitude para achar a estação mais próxima do destino. Para ninguém precisar digitar coordenadas, a API conhece **as 186 estações em operação** do Metrô, CPTM, ViaQuatro, ViaMobilidade, TIC Trens e Trivia Trens (linhas 1 a 15 e trechos abertos das linhas 6 e 17).

| Arquivo | Conteúdo |
|---|---|
| `Data/CoordenadasEstacoes.cs` | A lógica: busca pelo nome, validação e decisão |
| `Data/CoordenadasEstacoes.Lista.cs` | A lista com as 186 estações e os apelidos |

**Como funciona:**

1. **Ao cadastrar ou editar uma estação** (`POST`/`PUT /api/estacoes`):
   - coordenada informada **e** dentro da Grande São Paulo → é usada;
   - coordenada vazia ou fora de São Paulo (ex.: 90/180) → a API usa a coordenada conhecida pelo nome;
   - estação que não está na lista e veio sem coordenada → fica vazia.
2. **Ao subir a API:** estações já cadastradas sem coordenada, ou com coordenada fora de São Paulo, são corrigidas sozinhas.
3. **Nome flexível:** acentos, maiúsculas, hífens, nome de patrocínio e prefixos não atrapalham. Todos estes encontram a estação: `Sé`, `se`, `Palmeiras-Barra Funda`, `Barra Funda`, `Estação Tatuapé`, `Faria Lima`, `Faria Lima-PagBank`.

**Estação nova que ainda não existe na lista** (ex.: uma estação inaugurada depois): o gestor manda `latitude` e `longitude` no cadastro, ou alguém acrescenta uma linha em `Data/CoordenadasEstacoes.Lista.cs`:

```csharp
["NOME SEM ACENTO EM MAIUSCULO"] = (-23.000000, -46.000000), // Nome (linhas)
```

---

## Estrutura do projeto

```text
SerenoAPI/
├── Controllers/     Auth, Estacoes, Equipamentos, Reportes, Rotas, Cep, Historico, MongoTest
├── Data/            AppDbContext, DataSeeder e CoordenadasEstacoes (+ .Lista)
├── Migrations/      Migration inicial do EF Core (Oracle)
├── Models/          Entidades, enums, requests e responses
├── Repositories/    MongoHistoricoRepository
├── Services/        StatusService, IdempotencyService, TokenService, ViaCepService, MongoHealthCheck, ViaCepHealthCheck
├── Settings/        MongoDbSettings
├── SettingsJWT/     JwtSettings
├── Telemetry/       Métricas do OpenTelemetry
├── Properties/      launchSettings.json
├── dockerfiles/     Dockerfile, docker-compose.yml, .env.example
├── scripts/         infra.sh (Azure CLI), delete.sh, script-bd.sql
├── SerenoAPI.UnitTests/
├── SerenoAPI.IntegrationTests/
├── azure-pipeline.yml
├── SerenoAPI.http   Requisições prontas para testar no VS/VS Code
└── Program.cs
```

---

## Endpoints

Todas as rotas começam com `/api`, menos `/health`, `/swagger` e `/scalar`.
🔒 = precisa de token · 👮 = somente GESTOR

| Método | Rota | Acesso | Corpo / observação |
|---|---|---|---|
| GET | `/api/estacoes?page=0&size=10&linha=&busca=` | 🔒 | Paginado, com `latitude`, `longitude` e `equipamentosParados` |
| GET | `/api/estacoes/{id}` | 🔒 | Com os equipamentos (`statusAtual` e `atualizadoEm`) |
| POST · PUT · DELETE | `/api/estacoes`, `/api/estacoes/{id}` | 👮 | `{nome, linha, endereco ou cep, latitude?, longitude?}` |
| GET | `/api/equipamentos/{id}` | 🔒 | |
| POST · PUT · DELETE | `/api/equipamentos`, `/api/equipamentos/{id}` | 👮 | `{estacaoId, tipo, localizacao}` |
| POST | `/api/reportes` | 🔒 + `Idempotency-Key` | `{equipamentoId, status, observacao}` |
| GET | `/api/reportes/meus?page=0&size=10` | 🔒 | Paginado, cada reporte com `tipo`, `localizacao`, `estacaoId`, `estacaoNome` |
| GET | `/api/reportes/{id}` | 🔒 | Idem |
| PUT · DELETE | `/api/reportes/{id}` | dono ou GESTOR | `{status, observacao}` |
| GET | `/api/rotas/acessibilidade?origem=&destino=` | 🔒 | Pelo **nome** das estações (ou parte dele) |
| GET | `/api/cep/{cep}` | público | Consulta no ViaCEP |
| GET | `/api/historico?equipamentoId=` | 👮 | Histórico de reportes no MongoDB |
| GET | `/api/MongoTest` | público | Testa a conexão com o MongoDB |
| POST | `/api/auth/token` | só em desenvolvimento | `{email}`: token de teste para `cidadao@sereno.app` ou `gestor@sereno.app` |
| GET | `/health` | público | API, Oracle, MongoDB e ViaCEP |

**Valores aceitos:**

| Campo | Valores |
|---|---|
| `tipo` | `ELEVADOR`, `ESCADA_ROLANTE`, `RAMPA`, `PLATAFORMA` |
| `status` | `FUNCIONANDO`, `PARADO`, `EM_MANUTENCAO` |

**Swagger:** cada endpoint mostra os parâmetros, o modelo de entrada, o modelo de saída e **todas as respostas HTTP possíveis** (200/201/204, 400, 401, 403, 404, 409, 422, 429, 500, 503), com o formato de erro do Contrato. Os exemplos de corpo aparecem só nas requisições que o cidadão envia (reporte, edição de reporte e token de teste).

---

## Formatos do Contrato

Combinados entre a API Java, a API .NET, o serviço de IA e o app:

- campos em **camelCase em português**;
- enums em **MAIÚSCULO**;
- datas em **ISO 8601 UTC** (`2026-11-15T14:30:00Z`).

**Paginação** (a página começa em 0):

```json
{ "items": [ ... ], "page": 0, "size": 10, "totalItems": 42, "totalPages": 5 }
```

**Erro** (400, 401, 403, 404, 409, 422, 429, 500):

```json
{ "status": 400, "erro": "VALIDACAO", "mensagem": "Dados inválidos",
  "campos": [ { "campo": "status", "mensagem": "valor inválido" } ],
  "timestamp": "2026-11-15T14:30:00Z", "path": "/api/reportes" }
```

**Exemplos do cidadão:**

```json
POST /api/reportes        (header Idempotency-Key: 3f1c2a9e-7b6d-4c1a-9f0e-2d5b8a7c6e10)
{ "equipamentoId": 1, "status": "PARADO", "observacao": "Elevador parado desde cedo" }

201 Created
{ "id": 10, "equipamentoId": 1, "usuarioId": 1, "status": "PARADO",
  "observacao": "Elevador parado desde cedo", "criadoEm": "2026-11-15T14:30:00Z",
  "tipo": "ELEVADOR", "localizacao": "Acesso pela Praça da Sé",
  "estacaoId": 1, "estacaoNome": "Sé" }

PUT /api/reportes/10
{ "status": "FUNCIONANDO", "observacao": "Voltou a funcionar" }

GET /api/estacoes/1
{ "id": 1, "nome": "Sé", "linha": "1-Azul, 3-Vermelha", "endereco": "Praça da Sé - Sé, São Paulo - SP",
  "latitude": -23.550921, "longitude": -46.633135, "equipamentosParados": 1,
  "equipamentos": [
    { "id": 1, "estacaoId": 1, "tipo": "ELEVADOR", "localizacao": "Acesso pela Praça da Sé",
      "statusAtual": "PARADO", "atualizadoEm": "2026-11-15T14:30:00Z" } ] }
```

Mais requisições prontas no arquivo `SerenoAPI.http`.

---

## Segurança: JWT

| Item | Valor |
|---|---|
| Algoritmo | HS256 |
| Segredo | variável `JWT_SECRET` (32 ou mais caracteres; nunca no código) |
| Emissor / audiência | `sereno-auth` / `sereno-app` |
| Claims | `sub` (id numérico do usuário), `email`, `role` (`CIDADAO` ou `GESTOR`) |
| Validade | 2 horas |

- **Em produção, o token vem do login da API Java** (`POST /api/auth/login`). A API .NET só valida, com o mesmo `JWT_SECRET`.
- No Java, a chave precisa ser o texto do segredo em UTF-8 (`Keys.hmacShaKeyFor(secret.getBytes(StandardCharsets.UTF_8))`), sem decodificar Base64. Senão, a assinatura não bate e o .NET responde 401.
- `POST /api/auth/token` gera um token de teste **sem senha**. Por isso ele só funciona em Development ou com `AuthDev__Habilitado=true`. **Na nuvem fica desligado** (`AuthDev__Habilitado=false`), para ninguém gerar um token de GESTOR só com o e-mail.
- Sem token, token inválido ou expirado → **401** `NAO_AUTENTICADO`. CIDADAO em rota de gestor → **403** `ACESSO_NEGADO`.

---

## Idempotência

O header `Idempotency-Key` é obrigatório em `POST /api/reportes`. O app gera um UUID ao abrir o formulário e reenvia **a mesma chave** nas retentativas.

| Situação | Resposta |
|---|---|
| Sem a chave | **400** |
| Mesma chave + mesmo corpo | Devolve a resposta original (mesmo id), com `Idempotency-Replayed: true`, **sem duplicar** |
| Mesma chave + corpo diferente | **409** |

O registro fica em `IDEMPOTENCY_RECORDS`, vale 24 horas e é separado por usuário.

---

## Rate Limiting

| Política | Limite |
|---|---|
| Geral | 100 requisições/min por usuário (ou IP, sem token) |
| `reportes` | 10 reportes/min por usuário |

Ao passar do limite, a resposta é **429** com o header `Retry-After` (em segundos). O app lê esse header para mostrar "aguarde X segundos". Os limites ficam em `appsettings.json` → `RateLimit`.

---

## Health Check

`GET /health` (público) monitora a API, os bancos e o serviço externo usado:

| Check | Tag | O que verifica | Se falhar |
|---|---|---|---|
| `api` | `api` | A própria API respondendo | — |
| `oracle` | `db` | Conexão com o Oracle (`AspNetCore.HealthChecks.Oracle`) | `Unhealthy` |
| `mongodb` | `db` | Ping no MongoDB | `Unhealthy` |
| `viacep` | `externo` | Consulta de um CEP no ViaCEP | `Degraded` (a API continua funcionando; só o cadastro por CEP fica sem endereço automático) |

| Situação | Resposta |
|---|---|
| Tudo funcionando | **200** `Healthy` |
| Só o ViaCEP com problema | **200** `Degraded` |
| Algum banco fora do ar | **503** `Unhealthy`, com o nome do check que falhou |

```json
{ "status": "Healthy", "duracaoMs": 120,
  "checks": [
    { "name": "api", "status": "Healthy", "description": "API no ar.", "duracaoMs": 1, "tags": ["api"] },
    { "name": "oracle", "status": "Healthy", "description": null, "duracaoMs": 45, "tags": ["db"] },
    { "name": "mongodb", "status": "Healthy", "description": "MongoDB respondendo.", "duracaoMs": 30, "tags": ["db"] },
    { "name": "viacep", "status": "Healthy", "description": "ViaCEP respondendo.", "duracaoMs": 90, "tags": ["externo"] } ] }
```

---

## Como executar na sua máquina

**Pré-requisitos:** .NET SDK 8, acesso a um Oracle (o da FIAP serve) e a um MongoDB (o Atlas gratuito serve).

### 1. Clonar e compilar

```powershell
git clone https://github.com/EduardoSouza19/Projeto-GS-2026-SerenoAPI.git
cd Projeto-GS-2026-SerenoAPI
dotnet restore
dotnet build
```

### 2. Configurar os segredos (User Secrets)

Os segredos ficam fora do código e fora do GitHub:

```powershell
dotnet user-secrets set "JWT_SECRET" "O_MESMO_SEGREDO_DO_JAVA_COM_32_CARACTERES_OU_MAIS"
dotnet user-secrets set "ConnectionStrings:Default" "User Id=RM000000;Password=SUA_SENHA;Data Source=oracle.fiap.com.br:1521/ORCL"
dotnet user-secrets set "MongoDbSettings:ConnectionString" "mongodb+srv://USUARIO:SENHA@SEU-CLUSTER.mongodb.net/"
dotnet user-secrets set "MongoDbSettings:DatabaseName" "SerenoDB"
```

### 3. Tabelas no Oracle

Não precisa fazer nada: no primeiro start, a API aplica a migration da pasta `Migrations/`, cria as tabelas e cadastra o seed.
Se preferir criar à mão, rode `scripts/script-bd.sql` no SQL Developer.

### 4. Executar

```powershell
dotnet run
```

| O quê | Endereço |
|---|---|
| Swagger | http://localhost:5241/swagger |
| Scalar | http://localhost:5241/scalar/v1 |
| Health | http://localhost:5241/health |

### 5. Teste rápido no Swagger

1. `POST /api/auth/token` com `{"email":"cidadao@sereno.app"}` (funciona porque local é Development). Copie o token e clique em **Authorize**.
2. `GET /api/estacoes`: aparecem as 5 estações do seed, com latitude e longitude.
3. `POST /api/reportes` com o header `Idempotency-Key: teste-001` e `{"equipamentoId":1,"status":"FUNCIONANDO"}` → **201**, com `estacaoNome: "Sé"`.
4. Repita o mesmo POST → volta **o mesmo id**, sem duplicar.
5. Mesma chave com `"status":"PARADO"` → **409**.
6. Envie 11 reportes em menos de 1 minuto → o 11º responde **429** com `Retry-After`.
7. Ainda como cidadão, tente `POST /api/estacoes` → **403**.
8. Gere o token de `gestor@sereno.app` e cadastre `{"nome":"Brás","linha":"3-Vermelha","endereco":"Brás, São Paulo - SP"}` sem coordenadas → a resposta já vem com latitude e longitude.
9. `GET /api/rotas/acessibilidade?origem=Sé&destino=Tatuapé` → `PARCIAL`, com o aviso do elevador parado da Sé.

### 6. Para o app testar na sua máquina

O celular não acessa `localhost`. No `Properties/launchSettings.json`, troque `applicationUrl` para `http://0.0.0.0:5241`. Rode a API e use no app o IP do computador na rede (`http://192.168.x.x:5241/api`), com o celular no mesmo Wi-Fi.

---

## Como publicar no Render

O Render builda a imagem pelo Dockerfile a cada push na `main` e já entrega HTTPS (exigido pelo build de release do Android).

### 1. Bancos

- **Oracle:** o da FIAP (`oracle.fiap.com.br:1521/ORCL`), com o RM e a senha de um integrante.
- **MongoDB Atlas:** crie um cluster **M0 (Free)**, um usuário de banco e, em **Network Access**, libere `0.0.0.0/0` (o Render gratuito não tem IP fixo).

### 2. Criar o serviço

1. render.com → **New → Web Service** → escolha o repositório.
2. Preencha:

   | Campo | Valor |
   |---|---|
   | Language | Docker |
   | Branch | main |
   | Root Directory | vazio |
   | Dockerfile Path | `dockerfiles/Dockerfile` |
   | Instance Type | Free |
   | Health Check Path | `/health` (deixe vazio no primeiro deploy) |
   | Auto-Deploy | On Commit |

3. **Environment Variables** (botão **Add from .env**):

   ```
   PORT=8080
   ConnectionStrings__Default=User Id=RM000000;Password=SUA_SENHA;Data Source=oracle.fiap.com.br:1521/ORCL
   MongoDbSettings__ConnectionString=mongodb+srv://USUARIO:SENHA@SEU-CLUSTER.mongodb.net/
   MongoDbSettings__DatabaseName=SerenoDB
   JWT_SECRET=O_MESMO_SEGREDO_DO_JAVA
   AuthDev__Habilitado=false
   ```

4. **Deploy Web Service.** O primeiro build leva de 5 a 10 minutos. Espere aparecer **"Your service is live"**.

### 3. Conferir

- `https://SEU-SERVICO.onrender.com/health` → `Healthy`
- `https://SEU-SERVICO.onrender.com/swagger`

### 4. Atualizar

É só dar `git push` na `main`. O Render faz o deploy sozinho. Ao subir, a API aplica migrations e corrige as coordenadas das estações.

> **Plano gratuito:** depois de 15 minutos sem uso, o serviço dorme. A primeira chamada seguinte pode levar até 50 segundos.
> **Nunca** coloque senhas, strings de conexão ou o `JWT_SECRET` no GitHub. Elas ficam só nas variáveis do Render e nos User Secrets.

---

## Como integrar com o app mobile

| Item | Valor |
|---|---|
| Base URL | `https://SEU-SERVICO.onrender.com/api` |
| Autenticação | `Authorization: Bearer <token do login Java>` |
| Timeout recomendado | 60 segundos (por causa do plano gratuito) |

**Telas e chamadas:**

| Tela do app | Chamada |
|---|---|
| Lista de estações / estação mais próxima | `GET /estacoes?page=0&size=50` (usa `latitude` e `longitude`) |
| Detalhe da estação | `GET /estacoes/{id}` (`statusAtual` e `atualizadoEm` de cada equipamento) |
| Reportar status | `POST /reportes` com `Idempotency-Key` (UUID gerado ao abrir o formulário) |
| Minhas contribuições | `GET /reportes/meus` (já traz `tipo`, `localizacao` e `estacaoNome`) |
| Editar / excluir reporte | `PUT` / `DELETE /reportes/{id}` |
| Viagem acessível | `GET /rotas/acessibilidade?origem=Sé&destino=Tatuapé` |

**Respostas que o app precisa tratar:**

| Código | O que mostrar |
|---|---|
| 401 | Sessão expirada: voltar para o login |
| 403 | "Você não tem permissão para isso" |
| 409 | "Este reporte já foi enviado com outros dados" |
| 422 | "Equipamento não encontrado" |
| 429 | "Muitas tentativas, aguarde X segundos" (X = header `Retry-After`) |

---

## Docker

Na raiz do projeto:

```bash
cp dockerfiles/.env.example dockerfiles/.env        # troque as senhas
docker compose -f dockerfiles/docker-compose.yml --env-file dockerfiles/.env up -d --build
```

- Sobe **Oracle XE + MongoDB + API** (http://localhost:8080/swagger). A API cria as tabelas e o seed sozinha.
- O Oracle leva alguns minutos para ligar na primeira vez.
- Para parar: `docker compose -f dockerfiles/docker-compose.yml down`.

---

## DevOps na Azure

Alternativa ao Render, com o fluxo pedido na disciplina de DevOps.

### 1. Infraestrutura: `scripts/infra.sh` (Azure CLI)

No Azure Cloud Shell, na raiz do repositório:

```bash
sed -i 's/\r$//' scripts/*.sh          # corrige quebras de linha do Windows
export RM=563304
export ORACLE_PASSWORD='SenhaForte123'
export MONGO_PASSWORD='OutraSenhaForte123'
export JWT_SECRET='O_MESMO_SEGREDO_DO_JAVA'
bash scripts/infra.sh
```

| Recurso | Nome |
|---|---|
| Resource Group | `rg-sereno` |
| Azure Container Registry | `acrsereno<RM>` |
| Oracle XE (ACI) | `aci-oracle-sereno-rm<RM>` |
| MongoDB (ACI) | `aci-mongo-sereno-rm<RM>` |
| App Service Plan Linux | `plan-sereno` |
| Web App for Containers | `sereno-dotnet-rm<RM>` (somente HTTPS) |

O script faz o primeiro build da imagem no ACR e configura as variáveis de ambiente (com `AuthDev__Habilitado=false`). Nenhuma senha fica escrita nele. Para apagar tudo (só depois de 30/11): `bash scripts/delete.sh`.

### 2. Build (CI): `azure-pipeline.yml`

1. No Azure DevOps, crie a service connection **Docker Registry → Azure Container Registry** com o nome **`sereno-acr`**.
2. O gatilho é a `main`.
3. Etapas: restore → build → testes xUnit (resultados publicados) → build e push da imagem `sereno-api:<BuildId>` e `latest` → artefato `drop`.

### 3. Release (CD)

1. Pipelines → Releases → New → **Azure App Service deployment**, com o gatilho de implantação contínua ligado.
2. Tarefa **Azure Web App for Containers**: imagem `acrsereno<RM>.azurecr.io/sereno-api:$(Build.BuildId)`.

### Variáveis de ambiente da API

| Variável | Uso |
|---|---|
| `ConnectionStrings__Default` | Oracle |
| `MongoDbSettings__ConnectionString` | MongoDB |
| `MongoDbSettings__DatabaseName` | Nome do banco no MongoDB |
| `JWT_SECRET` | Segredo do JWT (o mesmo do Java) |
| `AuthDev__Habilitado` | `false` na nuvem; `true` liga o token de teste |
| `PORT=8080` | Porta do container no Render |
| `WEBSITES_PORT=8080` | Porta do container na Azure |

---

## Testes

```bash
dotnet test
```

Resultado esperado: **40 testes passando** (25 unitários e 15 de integração).

### Unitários

| Arquivo | O que testa |
|---|---|
| `StatusServiceTests` | Status atual = último reporte; sem reportes = FUNCIONANDO; contagem de parados; classificação da estação |
| `IdempotencyServiceTests` | Mesma chave devolve a resposta salva; corpo diferente gera outro hash; chave de outro usuário não vale; registro com mais de 24 h é descartado |
| `EstacoesControllerTests` | Detalhe com parados; 404 no formato do Contrato; coordenadas preenchidas sozinhas; sem endereço e sem CEP → 400 |
| `CoordenadasEstacoesTests` | Lista com todas as estações; nome com e sem acento, hífen e prefixo; ignora 90/180; estação desconhecida fica sem coordenada |

### Integração (WebApplicationFactory + banco em memória; o teste gera o próprio JWT)

| Arquivo | O que testa |
|---|---|
| `ReportesControllerIntegrationTests` | 401; 400 sem chave; 201 com `tipo` e `estacaoNome`; reenvio não duplica; 409; status inválido 400; **429** no 11º; 403 ao excluir reporte alheio |
| `EstacoesControllerIntegrationTests` | 401; página no formato do Contrato; 403 para CIDADAO; CRUD completo como GESTOR; rota pelo nome; `/health` com API, Oracle, MongoDB e ViaCEP |
| `SwaggerIntegrationTests` | O Swagger documenta as respostas 201, 400, 401, 409, 422 e 429 do `POST /api/reportes` |

---

## Problemas comuns

| Erro | Solução |
|---|---|
| `Configure a variável JWT_SECRET` | Configure o segredo (passo 2 ou variável no Render) |
| `ORA-01017` | Usuário ou senha do Oracle errados |
| `ORA-00942` | Tabelas não existem: rode a API de novo ou o `scripts/script-bd.sql` |
| 401 com o token do app | O `JWT_SECRET` do Java e do .NET são diferentes, ou o Java decodifica o segredo em Base64 |
| 404 em `/api/auth/token` na nuvem | Esperado: o token de teste fica desligado em produção. Use o login do Java |
| `/health` com mongodb Unhealthy | Confira a string do Atlas e o `0.0.0.0/0` no Network Access |
| `/health` com viacep Degraded | O ViaCEP está fora do ar ou lento; a API continua funcionando |
| Primeira chamada no Render demora | Plano gratuito acordando; espere até 50 segundos |
| App não acessa a API local | Use o IP do computador e `applicationUrl` em `0.0.0.0` (passo 6) |

---

## Fontes de dados e atribuição

- **Coordenadas das estações:** conjunto aberto [transporte-sp](https://github.com/roquerodrigo/transporte-sp), atualizado em 09/11/2026, que reúne dados do **GeoSampa** (Prefeitura de São Paulo, CC-BY-SA 4.0), do **OpenStreetMap** (© colaboradores do OpenStreetMap, ODbL 1.0) e do **Wikidata** (CC0). A lista derivada em `Data/CoordenadasEstacoes.Lista.cs` mantém essa atribuição e a licença ODbL.
- **Endereços:** [ViaCEP](https://viacep.com.br).

---

## Uso de IA Generativa

O grupo usou o **Claude (Anthropic)** como apoio para adaptar a estrutura do projeto do Challenge, revisar as regras de idempotência, rate limiting e health checks, montar a lista de coordenadas das estações e escrever a documentação. O código foi revisado, executado e testado pelo grupo, que sabe explicar cada parte na apresentação.
