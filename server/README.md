# Servidor — Cortes 1, 2 e 3

Implementa **presença compartilhada**, **combate** e **economia produtiva** de [docs/ARQUITETURA-MVP.md](../docs/ARQUITETURA-MVP.md): conta e personagem persistem em PostgreSQL, clientes conectados por WebSocket se veem no mesmo mundo, lutam contra inimigos autoritativos no servidor, e mineram/fundem/vendem recursos com economia auditável.

## Escopo implementado

### Corte 1 — presença compartilhada

- `POST /auth/register`, `POST /auth/login`, `POST /auth/refresh`, `POST /auth/logout` — conta e sessão, com token de acesso curto (JWT) e token de renovação rotativo (armazenado apenas como hash).
- `POST /characters`, `GET /characters/me` — um personagem por conta.
- `GET /world` (WebSocket) — envelope versionado (`docs/ARQUITETURA-MVP.md`, seção 8); mensagens `world.join`, `movement.input` (cliente → servidor) e `world.snapshot`, `error` (servidor → cliente).
- Movimento validado e aplicado inteiramente pelo servidor: velocidade máxima, `dt` medido pelo relógio do servidor (nunca pelo cliente) e limite de mensagens por segundo.
- Posição persistida a cada 10 s e ao desconectar; ao reconectar, o servidor envia um snapshot completo a partir da última posição salva.

### Corte 2 — combate (novo)

As fórmulas de dano, HP e progressão seguem [GDD-MVP.md §6-§8](../docs/GDD-MVP.md).

- Atributos (`character_attributes`: força, agilidade, vitalidade, resistência, todos começando em 5) e a habilidade de espada (`character_skills`), criados junto com o personagem.
- Dois inimigos fixos no MVP (`src/modules/combat/enemy-definitions.ts`): Lobo (60 HP, tutorial) e Javali (110 HP, desafio básico), com pontos de spawn fixos, perseguição por agressividade e ataque corpo a corpo — tudo em memória, sem tabela no banco (eles não são persistidos, só respawnam).
- `combat.attack.request` (cliente → servidor) → `combat.resolved` (servidor → cliente): valida incapacitação, cooldown (fórmula de intervalo de ataque por agilidade), alcance e existência do alvo antes de aplicar dano.
- `combat.player_damaged` (servidor → cliente): mensagem iniciada pelo servidor quando um inimigo acerta o jogador — não é resposta a um request do cliente, por isso é um tipo à parte na tabela de mensagens.
- `world.snapshot` agora inclui `enemies[]` (posição, HP, vivo/morto) e `hp`/`maxHp` em `self`/`others`.
- Morte do jogador: incapacita por 5 s (GDD §8) e renasce em O Berço com HP completo; morte do inimigo: concede XP (personagem e habilidade de espada) e ele respawna depois de um tempo fixo.
- **Cliente integrado:** entrada online opcional, inimigos, seleção/ataque e HUD de vida/XP em Unity. Ver [cliente](../client/README.md). Compilação C# concluída; validação jogando ainda pendente.
- Recompensas de combate, habilidade e pontos de atributo são salvos na mesma transação. Estado em memória só muda após commit; falhas retornam `PERSISTENCE_FAILED` e permitem tentar novamente.
- Personagem carregado com HP zero volta incapacitado e aguarda respawn do servidor, evitando ficar morto indefinidamente após reconectar.
- O snapshot informa `movementSpeed` para a previsão do cliente respeitar o limite configurado.

### Corte 3 — economia produtiva (novo)

As regras de mineração, metalurgia e preços seguem [GDD-MVP.md §9-§12](../docs/GDD-MVP.md).

- Inventário (`inventory_items`) e moeda (`characters.coin_balance`) por personagem; toda mudança é uma transação atômica (mesmo padrão de combate: nada muda em memória antes do commit).
- Veios de recurso (`resource_nodes`, 3 de ferro + 2 de cobre perto da mina) são **conteúdo orientado a dados** — ficam no banco, não hardcoded como os inimigos ainda estão — carregados uma vez na inicialização (`defaultInstance.loadResourceNodes()`) e geridos em memória durante a execução (disponível/esgotado/reaparecendo).
- `resource.mine.request` → `resource.mine.started` (com `durationMs`) → depois de `durationMs`, `resource.mine.result`. Ver "Canais de mineração e fundição" abaixo — este fluxo mudou de instantâneo para um canal com duração real em 10/09/2026.
- `craft.request` → `craft.started` → `craft.result`; `craft.cancel` cancela voluntariamente. Ver a mesma seção abaixo.
- `trade.sell.request` → `trade.sell.result`: vende ao comerciante por preços fixos (GDD §12), com registro append-only em `ledger_entries` (motivo + referência) para toda criação de moeda.
- `economy.snapshot` (servidor → cliente): saldo e inventário do próprio personagem, privado — não faz parte do `world.snapshot` (que também ganhou `resourceNodes[]`, visível a todos).
- **Simplificações conscientes desta primeira versão** (ver `docs/GDD-MVP.md` §9 para o alvo completo): sem sistema de equipamento (picareta/espada não precisam estar "equipadas" pra minerar/lutar) e sem capacidade/peso de inventário (só quantidade por item). *(Nota: equipamento e o limite de 20 espaços foram implementados depois — ver seções mais abaixo; este parágrafo descreve só o estado original do Corte 3.)*
- **Cliente Unity consumia estas mensagens no formato instantâneo original** (`NetworkEconomyController.cs`, ver [client/README.md](../client/README.md)): marcas simples nos veios, prompt de mineração por proximidade, painel de moedas/inventário com venda e fundição. **Isto ficou desatualizado em 10/09/2026** — ver "Canais de mineração e fundição" abaixo; o cliente ainda espera um `resource.mine.result`/`craft.result` imediato e precisa de ajuste (barra de progresso ou similar) pra UX fazer sentido com a duração real.
- Testado (`server/tests/unit/economy.service.test.ts`): ciclo minério → lingote → venda, todas as rejeições (veio inexistente/esgotado/fora de alcance, minério insuficiente, item não vendável) e idempotência sob concorrência (duas vendas simultâneas do mesmo item não duplicam o crédito).
- **Validado ponta a ponta em 10/09/2026** contra PostgreSQL real (ver "Sem Docker" abaixo): conta → personagem → conectar ao mundo → andar até dois veios de ferro → minerar 4 minérios → fundir 1 lingote (consumindo 3) → vender por 12 moedas (preço exato do GDD §12) → crônica `"<nome> fundiu o primeiro lingote de ferro em <data>."` aparece em `GET /world/chronicles`. Script de teste descartado depois de validar (não faz parte da suíte automatizada, que já cobre os mesmos casos com mocks).

### Canais de mineração e fundição (GDD §10/§11, novo — substitui a resolução instantânea)

Minerar e fundir levavam 0 segundos de verdade (só um cooldown pós-ação); agora seguem o GDD: uma ação com duração real, interrompível.

- `resource.mine.request` (`nodeId`) valida equipamento/alcance/estado do veio e, se ok, **inicia um canal** — não entrega nada ainda. Responde `resource.mine.started` (`nodeId`, `resourceCode`, `durationMs`). O veio fica indisponível para todos assim que o canal começa (mesmo padrão de antes).
- Quando `durationMs` passa sem o canal ser cancelado, o servidor entrega o recurso e o XP sozinho, via `resource.mine.result` — não é mais resposta direta a um request do cliente.
- `durationMs` = tempo base do recurso (GDD §10) reduzido 2% por nível de mineração (GDD §7), calculado em `skillAdjustedDurationMs`.
- **Cancelamento (GDD §10: "mover-se, sofrer dano ou perder conexão cancela a extração sem recompensa")**: um `movement.input` com deslocamento real, receber dano de um inimigo, ou a conexão cair cancelam o canal — sem recompensa, veio reaberto na hora. Movimento e dano geram `resource.mine.cancelled` (`nodeId`, `reason`: `"moved"`|`"damaged"`); desconexão não gera mensagem (o socket já caiu).
- `craft.request` (`recipeCode`) reserva o insumo **de imediato** (removido do inventário na hora, GDD §11: "o servidor reserva os ingredientes ao iniciar") e inicia o canal. Responde `craft.started` (`recipeCode`, `durationMs`). Ao terminar sem cancelamento, `craft.result` entrega o produto e o XP.
- `craft.cancel` cancela voluntariamente (GDD §11). Responde `craft.cancelled` (`recipeCode`, `refundedQuantity`): antes de metade do tempo devolve o insumo por completo; depois da metade, devolve tudo menos 1 unidade (GDD: "dois dos três minérios"). Fundição não é cancelada por movimento/dano — a forja é parada, só o cancelamento voluntário e a queda de conexão se aplicam.
- Só um canal por personagem por vez — tentar minerar ou fundir com um canal já ativo retorna `ON_COOLDOWN` (o cooldown pós-ação de antes deixou de existir: a própria duração do canal já ocupa o personagem).
- **Simplificação deliberada**: o GDD pede uma pausa de 60s na queda de conexão antes de aplicar a regra de cancelamento da fundição; em vez disso, a regra é aplicada na hora, sem a pausa — documentado aqui em vez de implementado, mesmo espírito das outras simplificações deste módulo.
- **Decisão não coberta pelo GDD**: se o produto de uma fundição não couber no inventário (20 espaços) quando o canal termina, o insumo já reservado é devolvido em vez de perdido — mesmo raciocínio de não inventar uma penalidade que o GDD não pede.
- **Requer atualização do cliente Unity**: o fluxo antigo (`NetworkEconomyController.cs`) esperava `resource.mine.result`/`craft.result` como resposta imediata ao request; agora a resposta chega depois de `durationMs` (ou nunca, se cancelado). Fica para o Codex ajustar a UI (barra de progresso, feedback de cancelamento) — sem isso, minerar/fundir no cliente atual provavelmente trava esperando uma resposta que só chega mais tarde.
- Testado (`server/tests/unit/economy.service.test.ts`, `describe("mining channel")`/`describe("crafting channel")`): início, todas as rejeições, conclusão via `tickChannels`, cancelamento por movimento/dano, os dois níveis de reembolso da fundição, inventário cheio na conclusão (mineração reabre o veio; fundição devolve o insumo) e recuperação de falha de persistência a meio da conclusão.
- Validado ponta a ponta em 10/09/2026 contra servidor real + Postgres: ciclo completo de mineração (`started` → `result`), cancelamento por movimento (`started` → `cancelled`, veio reaberto), cancelamento voluntário de fundição antes da metade (reembolso total) e ciclo completo de fundição (`started` → `result`).

### Histórico do mundo (base do "mural de crônicas", adiantado do Corte 4)

- `world_events` (docs/ARQUITETURA-MVP.md §10): registro append-only de fatos do mundo — nunca editado nem apagado por código de aplicação.
- `GET /world/chronicles` (público, sem conta): últimas 50 frases fixas geradas a partir de eventos conhecidos (GDD §13). Um `event_type` sem modelo de frase em `events.routes.ts` fica gravado no banco mas não aparece no mural.
- Primeiro hook real: fundir o primeiro lingote de ferro/cobre do mundo inteiro grava `primeiro_lingote` na mesma transação da fundição (`economy.service.ts`), usando `unique_key` pra garantir que só o primeiro (mesmo sob corrida entre dois jogadores) vira crônica.
- Moderação de nome de verdade (GDD §13) é escopo do Corte 4; o único fallback hoje é "um aventureiro" quando o personagem já não existe mais.

### Chat local (GDD §2/§9, novo)

Diferente do mural de crônicas, chat é item explicitamente incluído no escopo do MVP (GDD §2), não uma antecipação do Corte 4.

- `chat.send` (cliente → servidor) → `chat.message` (servidor → cliente): alcança todo mundo na mesma instância, inclusive quem enviou (eco confirma que foi aceito). Mensagem vazia/só espaço ou maior que 240 caracteres é rejeitada pelo schema (`chatSendPayloadSchema`).
- Toda mensagem é retida em `chat_messages` (`instance_id`, `character_id`, `content`, `moderation_status`) — inclusive as moderadas, pro registro existir mesmo sem ser exibido.
- Limite de 5 mensagens por 10s por conexão (`CHAT_MAX_MESSAGES_PER_10S`), mesmo padrão de `FixedWindowRateLimiter` do movimento.
- **Moderação é um placeholder mínimo** (`chat.service.ts`, `BLOCKED_SUBSTRINGS`): lista fixa de palavras que marca a mensagem como `hidden` e nunca a entrega a ninguém. Isto não é moderação de verdade (sem contexto, sem revisão humana) — substituir antes de expor a jogadores reais.
- Tabela `reports` (GDD §9, denúncia de jogador) já existia no schema; o endpoint `POST /reports` (HTTP, autenticado) agora grava nela: exige `targetCharacterId` (uuid), `reason` (1-500 caracteres) e opcionalmente `chatMessageId`. Rejeita alvo inexistente (`TARGET_NOT_FOUND`, 404) e auto-denúncia (`CANNOT_REPORT_SELF`, 400). **Só registra a denúncia** (status `"open"`) — revisão humana, punição e qualquer UI no cliente pra abrir uma denúncia continuam fora de escopo (Corte 4).
- Testado (`server/tests/unit/chat.service.test.ts`, `server/tests/unit/reports.routes.test.ts`) e validado ponta a ponta em 10/09/2026 contra PostgreSQL real: dois clientes conectados, mensagem normal chega a ambos (com eco), mensagem com palavra bloqueada não chega a ninguém, mensagem vazia é rejeitada; denúncia contra outro jogador grava e retorna 201, auto-denúncia e alvo inexistente são rejeitados.

### Equipamento (GDD §9, novo)

Simplificação deixada de propósito de fora do Corte 3, completada agora.

- `character_equipment` (uma linha por personagem, colunas `main_hand_item_code`/`tool_item_code`) — só existem esses dois espaços fixos no MVP, sem inventário de equipamento nem múltiplos sets.
- `equip.request` (`slot`: `main_hand`|`tool`, `itemCode`: item ou `null` pra desequipar) → `equip.result`: exige possuir o item (`INSUFFICIENT_ITEMS` se não tiver) e que ele sirva pra aquele espaço (`ITEM_NOT_EQUIPPABLE`; espada só vai em `main_hand`, picareta só em `tool`).
- **Minerar exige picareta equipada** (GDD §10, fluxo passo 1 "Equipar a picareta"): sem ela, `resource.mine.request` é rejeitado com `TOOL_NOT_EQUIPPED`, antes de qualquer outra checagem.
- **Atacar exige espada equipada** (GDD §9, mesmo padrão da picareta): sem espada em `main_hand`, `combat.attack.request` é rejeitado com `WEAPON_NOT_EQUIPPED`, antes de checar alcance/cooldown/alvo.
- GDD §9 flui a picareta vindo de um NPC (Minerador); sem sistema de entrega de item por NPC ainda, todo personagem novo já nasce com espada e picareta simples no inventário, equipadas — simplificação documentada, não o fluxo final.
- **Inventário com limite de 20 espaços** (GDD §9): um espaço = um tipo de item distinto (a mesma pilha cresce sem limite numérico próprio). Ganhar um item que já existe no inventário nunca falha; ganhar um tipo *novo* quando já há 20 tipos diferentes falha com `INVENTORY_FULL` (minerar deixa o veio disponível de novo; fundir não consome o material). O GDD também menciona limite de pilha por item e peso total, mas não dá números concretos para nenhum dos dois — ficam de fora, documentados como tal, em vez de inventar valores de balanceamento.
- Testado (`server/tests/unit/economy.service.test.ts`, `server/tests/unit/combat.service.test.ts`) e validado ponta a ponta em 10/09/2026: personagem nasce equipado, desequipar a picareta bloqueia minerar com `TOOL_NOT_EQUIPPED`, desequipar a espada bloqueia atacar com `WEAPON_NOT_EQUIPPED`, reequipar libera de novo.

Fora de escopo ainda (fica para o Corte 4, exceto onde já adiantado acima): limite de pilha por item e peso de inventário, tutorial guiado, UI de denúncia no cliente e moderação de conteúdo de verdade (revisão humana, punição).

### Alocação de pontos de atributo (GDD §6, novo)

`character_attributes.unspent_points` já existia e já era incrementado a cada nível desde o Corte 2, mas nunca era lido de volta nem exposto ao cliente — os pontos ficavam acumulados sem forma de gastá-los.

- `attributes.snapshot` (servidor → cliente): atributos e pontos não gastos do próprio personagem, privado como o `economy.snapshot`. Enviado ao entrar no mundo e após cada alocação.
- `attribute.allocate` (`attribute`: `strength`|`agility`|`vitality`|`resistance`) → `attributes.snapshot`: gasta 1 ponto atomicamente (checagem `unspent_points > 0` na própria query, sem transação explícita — sob concorrência, só uma alocação simultânea afeta a linha). Sem pontos disponíveis, retorna `NO_UNSPENT_POINTS`.
- Gastar um ponto em vitalidade recalcula `maxHp` (`100 + vitalidade × 10`) imediatamente.
- Testado (`server/tests/unit/attributes.service.test.ts`, mais uma asserção em `combat.service.test.ts` confirmando que subir de nível credita `unspentAttributePoints` em memória, não só no banco) e validado ponta a ponta em 10/09/2026: `attributes.snapshot` chega ao entrar com 0 pontos pra um personagem novo, alocar sem pontos retorna `NO_UNSPENT_POINTS`, e alocar em vitalidade depois de ganhar 1 ponto sobe `vitality` e `maxHp` corretamente.
- Fora de escopo: "redistribuição gratuita" completa via NPC Instrutora (GDD §6/§13, resetar todos os pontos já alocados) é um fluxo de diálogo/UI, não só a alocação em si — fica pra quando a interação com NPCs existir.

### Comprar do comerciante e usar poção (GDD §8/§12, novo)

O comerciante só vendia (`trade.sell.request`); a metade "compra do jogador" da tabela de preços (§12) e o "botão para usar poção" do combate (§8) não existiam.

- `trade.buy.request` (`itemCode`, `quantity`) → `trade.buy.result`: hoje só a poção tem preço de venda ao jogador (15 moedas, GDD §12); outro item retorna `ITEM_NOT_BUYABLE`. Sem moedas suficientes, `INSUFFICIENT_COINS`; inventário cheio, `INVENTORY_FULL`. Débito e entrega do item na mesma transação (mesmo padrão de `trade.sell.request`, com o sinal do lançamento no livro-razão invertido).
- `item.use.request` (`itemCode`) → `item.use.result`: hoje só a poção é usável, curando **75 HP** (sem passar de `maxHp`) e consumindo 1 unidade. Outro item retorna `ITEM_NOT_USABLE`; sem a poção, `INSUFFICIENT_ITEMS`. **O GDD não especifica quantidade de cura** — valor autoral, documentado como tal (`POTION_HEAL_AMOUNT` em `economy.constants.ts`), mesmo raciocínio dos outros números de balanceamento não especificados neste módulo (XP de mineração/metalurgia).
- Testado (`server/tests/unit/economy.service.test.ts`, `describe("buyItem")`/`describe("useItem")`) e validado ponta a ponta em 10/09/2026: comprar 2 poções debita 30 moedas e entrega os itens; comprar item não vendido e comprar além do saldo são rejeitados sem alterar nada; usar 1 poção cura de 60 para 135 HP e deixa 1 poção no inventário.

### NPCs e progresso do tutorial (GDD §13/§4, novo)

O maior pilar do GDD-MVP que ainda não existia: cinco NPCs com diálogo roteirizado e a jornada linear da primeira sessão (§4). Este módulo cobre só **estado de progresso no servidor** — o conteúdo das falas (o diálogo em si) é responsabilidade do cliente; nenhuma IA generativa está envolvida (GDD §13 é explícito sobre isso).

- `npc.talk.request` (`npcCode`: `instrutora`|`minerador`|`ferreiro`|`comerciante`|`cronista`) → `npc.talk.result`: registra a conversa (idempotente — falar de novo não faz nada além de reconfirmar) e devolve o progresso do tutorial atualizado. Conversar com a Instrutora ou o Minerador completa o passo de tutorial correspondente (ver abaixo); Comerciante e Cronista não têm passo mapeado no momento — só ficam registrados como "já conversou", pro cliente decidir o que fazer com isso.
- **Fundir agora exige ter conversado com o Ferreiro primeiro** (GDD §13: "libera a forja") — sem isso, `craft.request` é rejeitado com `FORGE_LOCKED`, antes de qualquer outra checagem. Personagens criados antes desta migração (007) já têm essa conversa retroativa (backfill), pra não travar quem já tinha acesso à forja.
- `tutorial.snapshot` (servidor → cliente, privado como `economy.snapshot`): `completedSteps` (array dos passos já concluídos), `completed` (todos os 6 passos rastreáveis concluídos) e `rewardClaimed`. Enviado ao entrar no mundo e de novo sempre que um passo novo é concluído.
- **Passos rastreados automaticamente** (GDD §4, passos 5-10 da jornada — os únicos com uma ação de servidor pra confirmar; passos 1-4 e 11 não têm equivalente no servidor): `falou_instrutora`, `derrotou_criatura` (primeira morte em combate), `falou_minerador`, `extraiu_minerio` (primeira mineração concluída), `fundiu_lingote` (primeira fundição concluída), `vendeu_lingote` (primeira venda de um lingote, não de minério/couro). Cada passo só conta uma vez, mesmo que a ação se repita.
- **Recompensa única do tutorial** (GDD §12: "recompensa única de missões do tutorial", fonte de moeda) — paga automaticamente, uma única vez por personagem, quando o 6º e último passo rastreado é concluído; registrada no livro-razão como qualquer outra criação de moeda. **O GDD não dá um valor** — 50 moedas, autoral, documentado como tal (`TUTORIAL_REWARD_COINS` em `missions.constants.ts`), perto do preço do lingote de cobre (a entrada mais cara da tabela §12).
- **Fora do que o servidor rastreia**: os passos 1-4 (entrar/criar conta, criar personagem, nascer na praça, aprender movimento) e o passo 11 (consultar o mural de crônicas, que é um endpoint público sem conta associada — não dá pra atribuir a um personagem). Espada e picareta continuam nascendo já equipadas (simplificação do Corte 3/006_equipment.ts) — conversar com a Instrutora/Minerador não entrega nada de novo, só marca o passo.
- Testado (`server/tests/unit/missions.service.test.ts`, `server/tests/unit/economy.service.test.ts` para o gate da forja): conversa idempotente, passo concluído uma única vez, recompensa paga exatamente uma vez mesmo sob nova tentativa após já concluído, e o bloqueio de fundição sem o Ferreiro. Validado ponta a ponta em 10/09/2026 contra servidor real + Postgres: `tutorial.snapshot` vazio ao entrar, fundir bloqueado antes de falar com o Ferreiro, liberado depois, progresso registrado ao falar com Instrutora/Minerador e ao minerar.

## Requisitos

- Node.js 24 LTS.
- PostgreSQL 16+ — via `docker compose up -d db` na raiz do repositório (preferido), **ou** uma instalação local própria se Docker não estiver disponível (ver abaixo).

## Como rodar

```bash
# na raiz do repositório
npm install
npm run build --workspace contracts   # necessário antes do primeiro dev/test: contracts é consumido como pacote compilado

docker compose up -d db                # ou aponte DATABASE_URL para um PostgreSQL já existente

cp server/.env.example server/.env     # ajuste os valores, especialmente JWT_ACCESS_SECRET
npm run server:migrate

npm run server:dev
```

### Sem Docker (Windows)

Esta máquina de desenvolvimento não tem Docker instalado (tentativa de instalar via `winget` em 10/09/2026 travou pedindo elevação UAC numa sessão sem admin). **Estado atual desta máquina:** já existe um serviço Windows nativo `postgresql-x64-16` rodando na porta 5432 (instalado antes, fora do controle deste projeto), com autenticação `trust` para conexões locais (`pg_hba.conf`) — ou seja, sem senha pra conectar localmente. O papel/banco `espectro` (mesmas credenciais do `docker-compose.yml`) foi criado **dentro desse Postgres já existente**, sem apagar nem alterar mais nada nele:

```powershell
node -e "import('pg').then(async ({default:pg}) => { const c = new pg.Client({host:'127.0.0.1',port:5432,user:'postgres',database:'postgres'}); await c.connect(); await c.query(\"DO $$ BEGIN IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname='espectro') THEN CREATE ROLE espectro LOGIN PASSWORD 'espectro'; END IF; END $$;\"); await c.query(\"SELECT 1 FROM pg_database WHERE datname='espectro'\").then(async r => { if (r.rowCount===0) await c.query('CREATE DATABASE espectro OWNER espectro'); }); await c.end(); })"
```

(rode a partir de `server/`, onde o pacote `pg` já está instalado). Depois disso, `npm run server:dev` funciona normalmente com o `DATABASE_URL` padrão do `.env.example`.

**Atenção ao rodar migração nesta máquina:** `npm run migrate up` (que usa `--envPath .env`) ignorou o `.env` e tentou conectar com o usuário do Windows em vez de `espectro` (erro `role "jorge.farias" does not exist`). Contornado passando `DATABASE_URL` explícito na própria chamada: `DATABASE_URL="postgres://espectro:espectro@localhost:5432/espectro" npx node-pg-migrate --migrations-dir migrations --tsconfig tsconfig.json --tsx up` (rodar de dentro de `server/`).

**Alternativa anterior** (não usada nesta máquina porque já havia um Postgres rodando na 5432, mas funciona se a máquina não tiver nenhum Postgres): baixar o **zip portátil** do PostgreSQL 17 da EDB (não exige admin/serviço do Windows, ao contrário do instalador `.exe`):

```powershell
Invoke-WebRequest "https://get.enterprisedb.com/postgresql/postgresql-17.11-1-windows-x64-binaries.zip" -OutFile postgresql.zip
Expand-Archive postgresql.zip "$env:LOCALAPPDATA\PostgreSQL17"

$bin = "$env:LOCALAPPDATA\PostgreSQL17\pgsql\bin"
$data = "$env:LOCALAPPDATA\PostgreSQL17\data"
& "$bin\initdb.exe" -D $data -U postgres -A trust
& "$bin\pg_ctl.exe" -D $data -l "$env:LOCALAPPDATA\PostgreSQL17\server.log" start
& "$bin\psql.exe" -U postgres -h localhost -c "CREATE ROLE espectro LOGIN PASSWORD 'espectro';"
& "$bin\psql.exe" -U postgres -h localhost -c "CREATE DATABASE espectro OWNER espectro;"
```

Isso cria as mesmas credenciais do `docker-compose.yml` (`espectro`/`espectro`), então `DATABASE_URL` não muda entre os dois caminhos. Para parar: `& "$bin\pg_ctl.exe" -D $data stop`.

## Testes

```bash
npm run server:test
```

- **Rodada de 09/09/2026:** 50 testes passaram; contratos e servidor compilaram. Inclui 9 regressões de recompensa transacional (nível/pontos, rollback, repetição e concorrência) e testes WebSocket para falha de persistência e reconexão morto. Persistência simulada nesta rodada; o teste com PostgreSQL real descrito abaixo pertence à validação anterior.
- **Unitários** (`tests/unit`): regras puras — movimento, limite de taxa, hashing de senha, tokens, validação do envelope, fórmulas de combate (`combat-formulas.test.ts`, uma asserção por fórmula do GDD). Não exigem banco.
- **Integração** (`tests/integration`): sobe o Fastify real e conecta um cliente WebSocket de verdade, com o módulo de personagens mockado (sem banco). Cobre especificamente a corrida entre a promoção do socket e o registro do listener de mensagens — ver nota abaixo.
- **Validado manualmente ponta a ponta** contra PostgreSQL real: Corte 1 (registro → login → personagem → dois clientes no mesmo mundo → um vê o outro se mover → desconexão persiste posição → reconexão restaura posição) e Corte 2 (personagem anda até um lobo, ataque fora de alcance é rejeitado, golpes batem com a fórmula de dano exata, morte do inimigo concede o XP do GDD e persiste, e o caminho inverso — inimigo atacando o jogador — também confere com a fórmula). Ainda não automatizado como teste de CI porque exige orquestrar um Postgres efêmero.

### Bugs reais encontrados e corrigidos durante a validação

- **Corte 1** — `ws` começa a decodificar frames assim que o socket é promovido a WebSocket, antes do handler de conexão do Fastify rodar. A implementação original fazia `await getCharacterByAccountId(...)` *antes* de registrar o listener `message`, então uma mensagem que chegasse durante essa janela era perdida para sempre (`EventEmitter` não enfileira eventos sem listener) — o segundo cliente a conectar quase sempre perdia seu próprio `world.join`. Corrigido em [`src/transport/ws.ts`](src/transport/ws.ts): listeners síncronos, mensagens bufferizadas até o personagem estar pronto.
- **Corte 2** — personagens novos nasciam com `hp = 100` (default antigo da coluna, de antes da fórmula de HP existir) em vez do HP cheio calculado pela vitalidade inicial (150). Corrigido em [`characters.service.ts`](src/modules/characters/characters.service.ts) e com backfill para os personagens já existentes.
- **Corte 2** — personagens criados antes desta migração não tinham linha em `character_attributes`; como a consulta usada ao entrar no mundo faz `inner join` com essa tabela, eles não conseguiriam mais entrar no mundo depois do deploy. A migração [`002_combat.ts`](migrations/002_combat.ts) faz o backfill dessas linhas para personagens existentes.

## Variáveis de ambiente

Ver [.env.example](.env.example). Nenhum valor secreto real está commitado; `JWT_ACCESS_SECRET` deve ser gerado localmente.
