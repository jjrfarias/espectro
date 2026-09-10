# Arquitetura Técnica — MVP

**Versão:** 0.1  
**Status:** base para implementação  
**Referência funcional:** [GDD do MVP](GDD-MVP.md)

## 1. Decisões adotadas

Para permitir o início do protótipo, esta arquitetura assume:

- câmera livre em terceira pessoa;
- combate com alvo automático assistido;
- direção visual low-poly estilizada;
- Android 10 ou superior;
- primeiro teste fechado com 10 a 20 participantes;
- uma região de servidor para o teste;
- até 30 jogadores por instância;
- nome do projeto e moeda ainda provisórios.

Essas premissas podem ser revistas sem alterar a regra central: o servidor é a autoridade.

## 2. Objetivos arquiteturais

1. Impedir que o cliente crie itens, moeda, XP ou resultados de combate.
2. Entregar um corte vertical completo antes de buscar escala de MMORPG.
3. Separar regras do jogo, transporte e persistência.
4. Permitir testes automatizados das regras sem executar o Unity.
5. Suportar reconexão e repetição segura de requisições.
6. Manter uma trilha auditável das alterações econômicas.
7. Evitar serviços distribuídos prematuros.

## 3. Visão geral

O MVP usa um **monólito modular** no servidor. É uma única aplicação implantável, internamente dividida por domínio. Isso reduz operação e mantém limites que poderão ser extraídos no futuro.

```text
Unity Android
  ├─ HTTPS: conta, personagem e dados iniciais
  └─ WebSocket seguro: sessão de mundo e chat
            │
            ▼
Servidor Node.js + TypeScript
  ├─ API e autenticação
  ├─ gateway de tempo real
  ├─ instâncias do mundo
  ├─ combate e progressão
  ├─ inventário e economia
  ├─ coleta e fabricação
  ├─ missões e história
  └─ moderação e telemetria
            │
            ▼
PostgreSQL
  ├─ estado persistente
  ├─ livro-razão
  └─ eventos históricos
```

Não entram no primeiro corte: microsserviços, Kubernetes, fila distribuída, cache externo e banco por domínio. Só devem ser adicionados quando uma medição indicar necessidade.

## 4. Tecnologias propostas

### Cliente

- Unity em uma versão LTS definida na preparação do ambiente;
- C#;
- Universal Render Pipeline;
- Input System;
- Addressables apenas quando houver conteúdo remoto real;
- serialização JSON durante o protótipo, com possibilidade de formato binário após medição.

### Servidor

- Node.js em versão LTS;
- TypeScript com modo estrito;
- framework HTTP e WebSocket escolhido por prova técnica curta;
- validação explícita de todos os payloads na entrada;
- ORM ou query builder com migrações versionadas;
- testes unitários, integração com PostgreSQL e testes de protocolo.

### Dados e operação

- PostgreSQL como fonte de verdade;
- contêineres para servidor e banco no desenvolvimento;
- logs estruturados;
- métricas e rastreamento de erros;
- segredos somente por variáveis de ambiente ou cofre do provedor.

Escolhas de bibliotecas devem ser confirmadas com documentação atual no momento da instalação.

## 5. Estrutura prevista do repositório

```text
game/
  client/                 projeto Unity
  server/                 aplicação Node.js/TypeScript
    src/
      modules/
        auth/
        characters/
        world/
        combat/
        inventory/
        economy/
        gathering/
        crafting/
        quests/
        history/
        chat/
      transport/
      persistence/
      observability/
      config/
    migrations/
    tests/
  contracts/              esquemas compartilhados e exemplos de mensagens
  infrastructure/         execução local e futura implantação
  docs/
```

O cliente não importa código do servidor. Ambos dependem apenas dos contratos versionados.

## 6. Responsabilidades dos componentes

### Unity

- captura entrada;
- apresenta o mundo e a interface;
- prevê movimento local para resposta visual;
- interpola outros personagens;
- envia intenções com número sequencial;
- exibe apenas resultados confirmados para inventário, economia, XP e combate;
- mantém fila temporária somente de comandos que podem ser repetidos com segurança.

### API HTTP

- registro, login e renovação de sessão;
- criação e seleção de personagem;
- configuração inicial e manifesto de conteúdo;
- operações administrativas fora do mundo.

### Gateway WebSocket

- autentica a conexão;
- associa conexão, personagem e instância;
- valida envelope e versão do protocolo;
- aplica limites de frequência;
- encaminha comandos ao módulo correto;
- publica eventos relevantes ao jogador ou à área visível.

### Instância do mundo

- mantém jogadores, monstros e veios ativos em memória;
- executa simulação em passo fixo;
- valida movimento, distância e tempo;
- decide visibilidade e mensagens a transmitir;
- grava checkpoints e mudanças persistentes.

### PostgreSQL

- guarda estado durável;
- executa transações de inventário e economia;
- garante unicidade de primeiros feitos;
- permite auditoria e restauração.

## 7. Modelo de execução do mundo

Cada instância possui um identificador e executa em um único processo lógico. No MVP, várias instâncias podem residir no mesmo processo Node.js.

- passo de simulação inicial: 10 Hz;
- envio de snapshots: 10 Hz, ajustável por medição;
- entrada de movimento: até 15 mensagens/s por jogador;
- persistência de posição segura: a cada 10 s e na saída;
- combate e economia persistidos no momento da confirmação;
- relógio do servidor como única referência para cooldowns.

Se uma instância falhar, o jogador retorna à última posição segura persistida. Nenhuma recompensa ainda não confirmada deve sobreviver.

## 8. Protocolo de comunicação

### Envelope comum

```json
{
  "v": 1,
  "type": "combat.attack.request",
  "requestId": "uuid",
  "sequence": 184,
  "sentAt": "2026-08-27T18:30:00.000Z",
  "payload": {}
}
```

- `v`: versão principal do protocolo;
- `type`: nome estável da mensagem;
- `requestId`: chave de correlação e, quando necessário, idempotência;
- `sequence`: ordem dos comandos daquela conexão;
- `sentAt`: diagnóstico; nunca substitui o relógio do servidor;
- `payload`: conteúdo validado conforme o tipo.

### Resposta de erro

```json
{
  "v": 1,
  "type": "error",
  "requestId": "uuid",
  "payload": {
    "code": "OUT_OF_RANGE",
    "message": "O alvo está fora de alcance.",
    "retryable": false
  }
}
```

O cliente reage ao `code`; a mensagem serve para apresentação e diagnóstico.

### Mensagens mínimas

| Direção | Tipo | Finalidade |
|---|---|---|
| cliente → servidor | `world.join` | entrar em uma instância |
| cliente → servidor | `movement.input` | enviar direção e ações de movimento |
| servidor → cliente | `world.snapshot` | estado visível confirmado |
| cliente → servidor | `combat.attack.request` | solicitar ataque ao alvo |
| servidor → cliente | `combat.resolved` | informar dano, HP, morte e XP |
| cliente → servidor | `gathering.start` | iniciar extração |
| cliente → servidor | `gathering.cancel` | cancelar extração |
| servidor → cliente | `gathering.resolved` | informar recurso e XP |
| cliente → servidor | `crafting.start` | iniciar receita |
| servidor → cliente | `crafting.resolved` | informar consumo e produto |
| cliente → servidor | `shop.buy` / `shop.sell` | transação com NPC |
| servidor → cliente | `inventory.changed` | versão e conteúdo alterado |
| cliente → servidor | `chat.send` | enviar mensagem local |
| servidor → cliente | `chat.message` | entregar mensagem moderada |
| servidor → cliente | `history.created` | anunciar feito histórico |

## 9. Fluxos autoritativos

### Ataque

1. Cliente envia alvo e sequência.
2. Servidor verifica sessão, estado, alcance, cooldown e existência do alvo.
3. Servidor calcula dano e atualiza o estado da instância.
4. Em caso de morte, determina saque e XP.
5. Alterações persistentes são gravadas.
6. Resultado é enviado ao atacante e jogadores interessados.

O cliente pode tocar animação imediatamente, mas corrige a apresentação conforme a resposta.

### Mineração

1. Servidor reserva o veio para o jogador.
2. Registra instante de conclusão esperado.
3. A cada passo, valida alcance, movimento, dano e conexão.
4. Na conclusão, abre transação.
5. Verifica espaço e peso, sorteia quantidade, adiciona item e XP.
6. Marca o veio indisponível e agenda reaparecimento.
7. Confirma o resultado.

### Venda

1. Cliente envia item, quantidade e `requestId` único.
2. Servidor obtém ou cria o registro de idempotência.
3. Em uma transação, bloqueia inventário e saldo do personagem.
4. Valida quantidade e preço vigente.
5. Remove itens, credita saldo e cria lançamentos do livro-razão.
6. Armazena a resposta associada ao `requestId`.
7. Confirma inventário e saldo com novas versões.

## 10. Modelo inicial de dados

Todas as chaves principais usam UUID. Datas são armazenadas em UTC. Tabelas mutáveis incluem `created_at`, `updated_at` e, quando necessário, `version` para concorrência otimista.

| Tabela | Campos essenciais |
|---|---|
| `accounts` | id, email_normalized, password_hash, status, created_at |
| `sessions` | id, account_id, refresh_token_hash, expires_at, revoked_at |
| `characters` | id, account_id, name, appearance_json, level, xp, hp, position_json, version |
| `character_attributes` | character_id, strength, agility, vitality, resistance, unspent_points |
| `character_skills` | character_id, skill_code, level, xp, version |
| `item_definitions` | code, name_key, weight, stack_limit, properties_json, enabled |
| `inventories` | id, character_id, slot_limit, version |
| `inventory_stacks` | id, inventory_id, slot, item_code, quantity, metadata_json |
| `equipment` | character_id, slot, inventory_stack_id |
| `wallets` | character_id, balance, version |
| `ledger_entries` | id, character_id, transaction_id, delta, reason, reference_type, reference_id, balance_after |
| `idempotency_keys` | account_id, request_id, operation, response_json, expires_at |
| `quest_progress` | character_id, quest_code, state, progress_json, completed_at |
| `world_events` | id, event_type, unique_key, character_id, occurred_at, location_code, data_json, schema_version |
| `chat_messages` | id, instance_id, character_id, content, created_at, moderation_status |
| `player_blocks` | blocker_account_id, blocked_account_id, created_at |
| `reports` | id, reporter_account_id, target_account_id, chat_message_id, reason, status |

### Restrições importantes

- email normalizado único;
- nome público normalizado único, sujeito à política definida;
- um personagem por conta no MVP;
- um slot por inventário;
- uma habilidade por personagem e código;
- `world_events.unique_key` único para primeiros feitos;
- `idempotency_keys(account_id, request_id, operation)` único;
- saldo nunca negativo;
- quantidade de pilha sempre positiva e dentro do limite do item.

### Livro-razão

O saldo em `wallets` permite leitura rápida. `ledger_entries` é a trilha imutável. Cada transação econômica deve terminar com a soma das alterações esperadas e registrar o saldo resultante. Correções são novos lançamentos, nunca edição silenciosa do histórico.

## 11. Consistência e concorrência

- compra, venda, coleta concluída e fabricação usam transação de banco;
- linhas de inventário e carteira relevantes são bloqueadas durante a alteração;
- versão de inventário permite ao cliente detectar estado antigo;
- chaves de idempotência protegem repetição após timeout;
- primeiros feitos dependem de restrição única, não apenas de verificação em memória;
- mensagens fora de ordem são rejeitadas ou reconciliadas pelo número sequencial;
- nenhuma operação mantém transação de banco aberta durante animações ou temporizadores.

## 12. Autenticação e segurança

- email e senha no MVP, com possibilidade futura de login de plataforma;
- hash de senha com algoritmo resistente e parâmetros atuais;
- token de acesso curto e token de renovação rotativo;
- token de renovação armazenado no servidor somente como hash;
- HTTPS e WebSocket seguro em ambientes remotos;
- limite de tentativas por IP, conta e dispositivo quando aplicável;
- validação de tamanho, tipo, faixa e enumeração de todo payload;
- autorização por personagem em toda operação;
- segredos nunca incluídos no aplicativo ou repositório;
- IDs não conferem permissão por si mesmos;
- trilha administrativa separada e auditada.

### Validações antitrapaça do MVP

- velocidade e aceleração máximas;
- posição navegável e distância plausível;
- cooldown do servidor;
- alcance e linha de ação aproximada;
- propriedade e equipamento do item;
- disponibilidade de veio ou receita;
- limite de inventário e peso;
- frequência de chat e comandos;
- padrões anormais registrados para análise.

## 13. Reconexão

1. Cliente detecta interrupção e bloqueia novas operações econômicas.
2. Tenta renovar autenticação se necessário.
3. Abre nova conexão e envia o último snapshot confirmado.
4. Servidor reassocia o personagem ou seleciona nova instância.
5. Servidor envia snapshot completo autoritativo.
6. Cliente descarta previsões e reconstrói interface.

Comandos econômicos sem resposta podem ser reenviados com o mesmo `requestId`. Movimento antigo nunca é reenviado.

## 14. Conteúdo orientado a dados

Itens, inimigos, receitas, XP, preços e parâmetros de combate devem ficar em definições versionadas no servidor. O Unity recebe apenas os dados necessários para apresentação.

Alterações de balanceamento:

- passam por validação de esquema;
- recebem versão;
- são promovidas entre ambientes;
- não são editadas diretamente no banco de produção;
- geram registro de quem alterou e por quê quando houver painel administrativo.

## 15. Observabilidade

### Logs

Cada registro inclui instante, nível, serviço, ambiente, `requestId`, conta/personagem quando permitido, instância e código do evento. Senhas, tokens e conteúdo pessoal desnecessário são excluídos.

### Métricas mínimas

- conexões e jogadores por instância;
- latência de comandos por tipo;
- passos de simulação atrasados;
- taxa de desconexão e reconexão;
- erros por código;
- duração e falha de transações;
- itens e moeda criados/destruídos por razão;
- uso de CPU, memória, conexões e armazenamento.

### Alertas iniciais

- servidor indisponível;
- taxa de erro elevada;
- banco sem conexão;
- saldo negativo ou divergência de livro-razão;
- fila de simulação atrasada;
- backup falhou.

## 16. Ambientes

| Ambiente | Finalidade | Dados |
|---|---|---|
| local | desenvolvimento individual | descartáveis e sintéticos |
| teste | integração e QA | sintéticos, reiniciáveis |
| fechado | teste com jogadores convidados | persistentes durante o ciclo de teste |

Não haverá produção pública no MVP. Cada ambiente usa banco, segredos e chaves separados.

## 17. Estratégia de testes

### Unitários

Fórmulas, progressão, dano, capacidade, receitas, preços, permissões e validação de comandos.

### Integração

Migrações, inventário, carteira, livro-razão, idempotência, primeiros feitos e concorrência entre transações.

### Protocolo

Compatibilidade de mensagens, payload inválido, versão não suportada, repetição, ordem e limites de frequência.

### Ponta a ponta

Login → personagem → mundo → lobo → mineração → lingote → venda → logout → retorno.

### Carga

Robôs de teste simulam 30 jogadores por instância, movimento, ataques e mineração. O teste mede estabilidade do passo, latência e uso de recursos; não tenta simular ainda milhares de usuários.

## 18. Backup e recuperação

- backup automatizado do PostgreSQL no ambiente fechado;
- retenção definida antes de receber jogadores;
- criptografia em trânsito e repouso conforme provedor;
- restauração ensaiada em banco separado;
- objetivo inicial de perda máxima: até 15 minutos;
- objetivo inicial de recuperação: até 4 horas.

Os objetivos são provisórios e devem ser revisados conforme custo e criticidade.

## 19. Cortes verticais

### Corte 0 — movimento local

- projeto Unity abre no Android;
- mapa cinza e personagem em terceira pessoa;
- 30 FPS no aparelho escolhido;
- sem servidor.

### Corte 1 — presença compartilhada

- servidor e banco executam localmente;
- conta e personagem persistem;
- dois clientes entram e se veem;
- reconexão restaura estado.

### Corte 2 — combate

- lobo autoritativo;
- ataque, dano, morte, renascimento e XP;
- rejeição de distância e cooldown inválidos.

### Corte 3 — economia produtiva

- inventário e equipamento;
- mineração de ferro;
- fabricação de lingote;
- venda e livro-razão;
- idempotência comprovada por teste.

### Corte 4 — MVP fechado

- conteúdo restante do GDD;
- tutorial, chat, moderação e crônicas;
- telemetria, backup, carga e distribuição aos convidados.

## 20. Critérios para iniciar código

Antes do Corte 0:

- confirmar versão LTS do Unity e módulo Android;
- escolher um aparelho Android mínimo real;
- confirmar orientação de tela e esquema de controles;
- criar repositório Git e regras para arquivos grandes do Unity;
- definir licenças e responsáveis pelas contas de distribuição.

Antes do Corte 1:

- confirmar versão LTS do Node.js;
- decidir framework, biblioteca WebSocket e acesso ao PostgreSQL por uma prova técnica;
- definir contratos v1 como esquemas validáveis;
- configurar migrações e testes no fluxo de integração;
- documentar variáveis de ambiente sem incluir valores secretos.

## 21. Decisões futuras, não bloqueantes

- provedor e região de hospedagem;
- formato binário para snapshots;
- cache ou presença distribuída;
- separação do simulador em serviço próprio;
- login Google/Apple;
- entrega remota de conteúdo;
- painel administrativo completo;
- IA para crônicas e NPCs.

Essas decisões só serão antecipadas se bloquearem uma métrica real do MVP.

## 22. Próxima ação executável

Preparar o **Corte 0**: escolher a versão LTS do Unity e o aparelho mínimo, criar o projeto Android com URP, montar um mapa cinza e validar câmera, joystick, movimentação e 30 FPS diretamente no dispositivo.

