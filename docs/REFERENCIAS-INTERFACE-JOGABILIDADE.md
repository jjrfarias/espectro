# Espectro — Referências de interface e jogabilidade

Este documento registra as referências de jogos escolhidas com o usuário pra guiar decisões de
**interface (UI/UX) e sensação de jogabilidade** do Espectro daqui pra frente. Vale tanto pra
Claude (servidor/protocolo/UI de código) quanto pro Codex (arte/cliente Unity) — releia antes de
desenhar ou reorganizar qualquer tela.

**Importante**: isto é sobre *interação e organização de interface*, não sobre estilo visual. A
direção de arte (paleta, materiais, iluminação, motivo dos "Mil Caminhos") continua sendo a de
[DIRECAO-ARTISTICA.md](DIRECAO-ARTISTICA.md) — não vira um clone visual de nenhum dos jogos abaixo.

## Referências escolhidas

### The Legend of Zelda: Breath of the Wild / Tears of the Kingdom
**Por quê**: o padrão-ouro de HUD minimalista. Só o essencial fica permanentemente na tela
(corações de vida, um mapa pequeno no canto); tudo o mais só aparece quando é relevante naquele
instante, através de **um único prompt de ação contextual** (ex.: "A: Falar", "A: Abrir",
"A: Pegar") que muda de acordo com o que está na frente do jogador.

**O que copiamos daqui**: a filosofia de "não mostrar o que não é preciso agora". Painéis inteiros
(loja, forja, atributos) não devem ficar plantados na tela o tempo todo — devem abrir sob demanda
a partir de um prompt contextual, e fechar depois.

### Palia
**Por quê**: é o mais próximo do Espectro em escopo e tom — um MMO cozy 3D estilizado, com
coleta/mineração, fundição/crafting e comércio com NPCs, pensado pra rodar em hardware modesto.

**O que copiamos daqui**: a organização de painéis de coleta/crafting (grades simples, poucos
cliques até completar uma ação) e a ideia de que interagir com um NPC específico é o que abre a
respectiva tela (loja abre perto do comerciante, forja perto do ferreiro) — em vez de tudo
disponível em um HUD permanente, não importa onde o jogador esteja.

### RuneScape (o clássico)
**Por quê**: referência histórica de MMORPG leve rodando em navegador — exatamente a restrição
técnica do Espectro (cliente WebGL). Convenções: minimapa fixo num canto, inventário em grade
simples, interação por proximidade/clique num NPC ou objeto do mundo, sem HUD 3D flutuante
poluindo a cena.

**O que copiamos daqui**: minimapa compacto num canto (já implementado), preferir listas/grades
simples a hierarquias de menu profundas, e manter o custo de interface leve o bastante pra rodar
bem em qualquer navegador/aparelho.

## Princípios práticos daqui pra frente

1. **HUD permanente = só o crítico.** Vida, nível/XP, minimapa. Tudo o mais (atributos, loja,
   forja, inventário detalhado) começa fechado.
2. **Um prompt contextual por interação**, no mesmo padrão que `InteractionController.cs` já usa
   pra diálogo (E pra falar/interagir) — a tela cheia daquele sistema só abre depois do prompt.
   Loja do Comerciante e forja do Ferreiro devem migrar pra esse padrão em vez de ficarem sempre
   visíveis no painel de economia (ver nota já deixada em `NetworkEconomyController.cs` sobre a
   checagem de proximidade — o próximo passo é trocar "seção sempre visível quando perto" por
   "painel fechado que abre com um prompt/tecla de interação").
3. **Texto sobre a cena 3D sempre com fundo legível** (backdrop semi-transparente) — já aplicado
   no HUD de combate nesta sessão.
4. **Poucos cliques até a ação.** Vender, fundir, comprar poção: uma tela, uma lista, um botão —
   sem sub-telas aninhadas.
5. **Toggle explícito para painéis de informação** (ex.: abrir/fechar Atributos clicando na barra
   de vida) em vez de deixá-los sempre montados ocupando espaço.

## Não copiar

- Estilo visual/artístico de nenhum dos três (isso é definido por `DIRECAO-ARTISTICA.md`).
- Sistemas de progressão, itens ou economia desses jogos — o Espectro segue o GDD-MVP.
- Controles específicos de console (botão A, D-pad) — o Espectro é mouse/teclado (e touch, no
  Android). O paralelo é conceitual ("um prompt contextual"), não literal ("apertar A").

---

## Plano de evolução — interface, mecânicas e fluidez

**Registrado em:** 10/09/2026, por Codex.

**Estado:** proposta para revisão do usuário, antes de implementar. Nenhuma etapa abaixo está concluída por estar descrita aqui.

**Base:** referências acima, [visão do Espectro](ESPECTRO-VISAO.md), [GDD do MVP](GDD-MVP.md), [direção artística](DIRECAO-ARTISTICA.md), código atual e problemas relatados pelo usuário.

### Resultado que precisamos alcançar

Transformar o ciclo já existente em uma experiência coesa: entrar, reconhecer o próximo objetivo, mover-se com precisão, lutar com resposta visível, trabalhar na mina/forja, negociar, evoluir e reencontrar tudo salvo ao voltar. O acabamento deve aparecer tanto na reação de um botão quanto na continuidade entre essas atividades.

A primeira entrega recomendada é **uma sessão de 15–20 minutos bem acabada em O Berço**, com a ida à floresta e à mina e o retorno à cidade. Expandir o mapa ou adicionar novos sistemas fica depois de provar a qualidade desse percurso. Preservar a liberdade de alternar entre combate e profissão, a progressão sem classes e a memória persistente do mundo.

O WebGL publicado é a superfície atual de teste. Android continua sendo a plataforma inicial dos documentos de visão/GDD; fluidez no navegador não comprova desempenho no aparelho Android. Os dois precisam de validação própria.

### Como aplicar as referências ao Espectro

| Referência já escolhida | Aplicação proposta | Evidência esperada no Espectro |
|---|---|---|
| Zelda BOTW/TOTK | HUD com hierarquia; uma ação contextual; orientação pelo cenário | Ao chegar a um NPC ou veio, aparece uma ação clara; o jogador enxerga o caminho e o personagem sem painéis competindo |
| Palia | Profissões apresentadas no lugar onde acontecem; gestos de trabalho; painéis simples | Picareta acompanha extração; forja mostra receita/ingredientes/tempo; loja abre pela interação com comerciante |
| RuneScape clássico | Inventário e habilidades fáceis de consultar; navegação compacta; progressão legível | Grade de itens, equipamento e XP compreensíveis, poucos passos para executar uma ação e custo de UI controlado |
| Identidade própria do Espectro | Mil Caminhos, madeira/pedra quente, petróleo/âmbar e acentos espectrais | Marca, menus, mapa, interações e crônicas parecem pertencer ao mesmo universo |

As capturas de Tibia, WoW e a referência da árvore enviadas na conversa servem como contexto complementar de composição do pré-jogo: cenário com presença, marca reconhecível e formulário legível. Não substituem as três referências do documento, nem autorizam copiar arte, interface ou regras desses jogos.

### Diagnóstico atual: o que precisa de atenção

Esta revisão é documental e de código. Não houve teste visual de movimento nesta etapa. Causas do tremor ainda precisam ser reproduzidas e medidas; as correções anteriores de câmera não comprovam resolução do problema.

| Situação | Evidência local | Consequência para a experiência |
|---|---|---|
| Movimento tem duas aplicações de deslocamento | `PrototypePlayerController.Update` move o controlador; `NetworkSession.LateUpdate` aplica correção em direção ao último snapshot; câmera também acompanha em `LateUpdate` | Possível disputa entre previsão, confirmação e apresentação. É hipótese a medir para o tremor, não diagnóstico fechado |
| Root motion já estava desligado no prefab | `Aventureiro.prefab` contém `m_ApplyRootMotion: 0`; `AdventurerCharacterSetup` também o desativa | Desligá-lo novamente não demonstra a causa. Examinar raiz, visual, ossos, cadência de quadros e correções de rede separadamente |
| Combate ainda não tem apresentação completa do jogador | Controller preparado em `AdventurerCharacterSetup` usa Idle/Walking/Running; `NetworkSession.RequestAttack` envia a solicitação | Falta conectar preparação, golpe, recuperação e reações às ações online |
| Disponibilidade de ataque não acompanha a regra real | Cliente permite nova tentativa após 0,2 s e seleciona até 25 m; servidor limita ataque a 2,25 m e usa intervalo por agilidade | O botão pode convidar a ações que serão recusadas; alinhar alcance, direção, recarga e feedback |
| Tutorial do servidor e objetivo mostrado são diferentes | `HandleTutorialSnapshot` reduz a quantidade de passos a 0–4; `InteractionController.RefreshObjective` exibe o arco Anciã/Lumina | É possível apresentar etapa/conclusão sem corresponder ao combate, mineração, fundição e venda confirmados |
| Loja/forja mantêm um fallback de protótipo | `NetworkEconomyController.UpdateNpcContext` mostra seções quando não encontra os NPCs | Serviços podem aparecer fora do contexto; é preciso posicionar e identificar os NPCs antes de remover esse fallback |
| Interação está distribuída | `InteractionController` e `NetworkEconomyController` leem E separadamente | Uma tecla pode disparar mais de um sistema; falta um responsável pela escolha e execução do contexto |
| NPCs necessários não foram encontrados na configuração revisada | Busca na cena e scripts de mundo/Editor não encontrou os papéis Instrutora/Minerador/Ferreiro/Comerciante conectados ao fluxo; confirmar também em runtime | Conta nova pode ficar sem como liberar a forja ou concluir o tutorial pelo cliente |
| Recuperação de senha ainda não funciona | `NetworkUI` exibe apenas uma mensagem no botão “Esqueci a senha” | Recuperação exige fluxo real de servidor/e-mail e telas de confirmação, erro e nova senha |
| Lembrar sessão e saída precisam ser concluídos | `NetworkSession` grava refresh token em `PlayerPrefs`, possui `ReturnToLocal` e limpa a sessão salva em um catch genérico de restauração | Falta escolha de permanência, logout com revogação e distinção entre indisponibilidade da rede e sessão inválida |
| Desempenho não tem uma linha de base suficiente | `PerformanceHud` força alvo de 30 FPS; câmera usa `SphereCastAll`; build WebGL usa `BuildOptions.Development` | Medir tempo por quadro e alocações, definir perfis e separar build de diagnóstico do candidato publicado |
| Verificação de entrega precisa abranger o cliente | Workflow CI atual testa contratos/servidor; upload do WebGL é separado | Compilar ou receber ID de upload não basta para afirmar que a versão está ativa e visualmente correta |
| Chat/crônicas ainda não têm ligação na camada de cliente auditada | Não foi encontrado dispatch de chat em `WorldConnection`/`Protocol`; `ApiClient` não consulta eventos do mural | Apresentar as capacidades persistentes que o servidor já oferece faz parte da entrega, além do polimento visual |

As notas antigas do log incluem pendências que já ganharam UI e reservas duplicadas. Antes de cada execução, conferir o código e o estado atual com o responsável pela área; não usar uma nota histórica como prova de ausência ou conclusão.

### Ordem de execução proposta

| Etapa | Prioridade | Entrega | Condição para avançar |
|---|---|---|---|
| 0 — Diagnóstico reproduzível | P0 | Captura e medição do tremor, controles, quadros e sincronização | Problema reproduzido; componente responsável identificado ou hipóteses isoladas com evidência |
| 1 — Movimento e interação consistentes | P0 | Deslocamento estável, câmera previsível, foco de interface e ação contextual única | Andar, virar, colidir e abrir painéis sem oscilação recorrente nem comando duplicado |
| 2 — Entrada e linguagem de interface | P1 | Login/cadastro/recuperação, sessão, navegação e componentes compartilhados | Entrada e saída reais; telas legíveis; recuperação testada de ponta a ponta |
| 3 — Combate expressivo | P1 | Golpe, alvo, resposta de dano, inimigos, morte/retorno e réplica visual | Combate compreensível para quem joga e para outro jogador observando |
| 4 — Profissões e primeira jornada | P1 | NPCs, inventário, equipamentos, produção, loja, tutorial e crônicas conectados | Ciclo completo com progresso/recompensas do servidor e retomada correta |
| 5 — Acabamento e teste fechado | P1/P2 | Áudio, gestos, social, opções, acessibilidade e desempenho medido | Percurso validado em WebGL e aparelho-alvo; versão publicada identificável |

Diagnóstico e estabilidade bloqueiam o polimento do movimento. **Mapeamento incorreto do tutorial e NPCs que bloqueiam conta nova são P0:** resolver sua ligação funcional na etapa 1; a apresentação completa fica na etapa 4. Trabalho independente de arte pode acontecer em paralelo após reservar arquivos; `NetworkSession`, protocolo, cena, log e build precisam de um integrador por vez.

### 0. Diagnóstico antes de outra correção do tremor

- Capturar o mesmo percurso antes/depois: 30 segundos parado, caminhada reta, curvas, inversões de 180°, corrida, parada, contato lateral com parede, passagem estreita e rampa. Repetir na praça, floresta e mina.
- Registrar por quadro, apenas em diagnóstico: posição da raiz física, deslocamento aplicado, correção do servidor, posição local do modelo, estado/velocidade do Animator, posição da câmera e tempo do quadro. Registrar idade do snapshot e latência sem tokens nem dados de conta.
- Comparar execução local isolada para diagnóstico, execução online e dois clientes. O modo local de diagnóstico fica em ambiente de desenvolvimento, sem reintroduzir entrada offline na produção.
- Repetir em 30/60 FPS e com RTT controlado de 0/80/160 ms e variação de 20 ms em teste, além da rede real; anotar o método e os limites da simulação.
- Isolar uma variável por vez: câmera fixa, animação temporariamente suspensa e correção de rede instrumentada. Executar comparações em teste; alterações temporárias não entram no build publicado.
- Conferir curvas dos clipes, transições, root motion, colisores que surgem após o bootstrap, ordem de atualização e compatibilidade de posição/colisão entre cliente e servidor.
- Investigar os tempos de movimento: cliente agrega movimento pelo tempo local, enquanto o servidor calcula deslocamento pelo intervalo de chegada de pacotes. Definir estratégia de confirmação/reconciliação adequada sem tornar o tempo ou a posição enviados pelo cliente autoridade irrestrita.

**Aceite:** evidência que separa tremor da câmera, do corpo/rig, da raiz de movimento e de baixa cadência de quadros. A correção escolhida deve eliminar o padrão reproduzido. Se restar oscilação, registrar o teste que falha e continuar o diagnóstico.

### 1. Movimento, câmera e comandos

**Movimento:** consolidar a aplicação de movimento físico em um responsável por quadro. A correção de rede deve integrar-se à previsão em uma ordem definida; a câmera deve observar o estado final. Se necessário, evoluir protocolo para confirmar entradas e reconciliar deslocamento; escolher isso após a medição, com limites e colisões validados no servidor.

**Locomoção:** ajustar aceleração, desaceleração, giro e transições Idle/Walk/Run à velocidade realmente aplicada. Empurrar uma parede não deve manter corrida plena nem produzir vibração do modelo. Teleporte, reconexão e renascimento devem resetar históricos de interpolação/animação/câmera. Evitar suavizações acumuladas que só acrescentem atraso aos controles.

**Câmera:** órbita e zoom previsíveis, tratamento de obstáculos sem atravessar paredes, recuperação suave ao sair de um obstáculo, acompanhamento de rampas/escadas sem atraso vertical excessivo. Tremor de impacto fica opcional e desligável; movimento e colisão não devem disparar sacudida de tela.

**Entrada:** criar estados explícitos para login, exploração, diálogo, painel e chat. Campo de texto com foco consome teclas; clicar em UI não ataca; rolar uma lista não dá zoom; arrastar botão/joystick não gira a câmera. Em multiplayer, abrir menu bloqueia os comandos pertinentes do próprio jogador, sem pausar o mundo inteiro.

**Proposta de comandos desktop:** manter WASD, Shift, E, F para atacar, Tab para alvo e N para mapa durante a transição; Escape fecha o painel superior; adicionar atalhos visíveis para inventário, personagem e poção. Minerar migra para o contexto E, com equivalência touch. Só anunciar atalhos depois de unificados e testados; prever remapeamento sem colisões. No touch, garantir movimento e ação simultâneos com dois dedos.

**Aceite:** uma interação produz uma ação; nenhum personagem anda/ataca enquanto se digita; as manobras da etapa 0 passam online e com dois clientes. Medir reação visual ao comando sem esperar a confirmação de rede, preservando resultado autoritativo.

### 2. Pré-jogo e sistema de interface

**Fluxo:** carregamento identificável → login ou cartão de sessão lembrada → criação de personagem quando necessária → conexão → mundo. Na sessão lembrada, oferecer continuar/trocar conta. Logout retorna à entrada, encerra a conexão e revoga/limpa a sessão correspondente; falha de rede oferece tentar novamente. Eliminar transições de produção para exploração local.

**Login/cadastro:** formulários próprios, mostrar/ocultar senha, validação junto ao campo, Enter para enviar, Tab para navegar, indicadores de espera, prevenção de envio repetido e mensagens recuperáveis. Separar “lembrar e-mail” de “manter conectado neste dispositivo”, com escolha explícita. Não guardar senha; revisar armazenamento de token por plataforma, rotação, expiração e revogação. `PlayerPrefs` não deve ser descrito como armazenamento protegido.

**Esqueci a senha:** formulário de e-mail → resposta neutra → mensagem real com link/token temporário de uso único → nova senha/confirmar → retorno ao login. Exigir limitação de tentativas, expiração e invalidação de sessões conforme política definida. Dependência concreta: provedor/remetente de e-mail e configuração do servidor, com teste de entrega; um botão com texto de indisponibilidade não atende o requisito. Validar token válido, expirado e reutilizado sem expor se uma conta existe.

**Criação de personagem:** nome validado, prévia visível, pequena apresentação do Berço e opções limitadas de aparência que possam ser persistidas e mostradas aos outros jogadores. Aproveitar o que os assets existentes suportam; não prometer classes ou editor corporal amplo.

**Direção da tela:** composição própria do Berço/Mil Caminhos, marca reconhecível, sinopse de duas ou três linhas e formulário legível sobre fundo controlado. Um cenário de apresentação pode ser usado depois de medir seu custo; o jogador não controla o mundo atrás do formulário.

**Componentes compartilhados:** tipografia, escalas de espaço, paleta, ícones, botões, campos, abas, janelas, notificações, tooltips e estados de foco/pressionado/desabilitado/carregando/erro/sucesso. Separar componentes de apresentação da sessão de rede. Usar layout responsivo e área segura; substituir posicionamento repetido que depende de uma única resolução.

**Organização do HUD:** vida/nível e acesso ao personagem; minimapa compacto; objetivo atual recolhível; atalhos de ação; um prompt contextual. Alvo e feedback de combate aparecem quando necessários. Inventário, atributos, loja, forja, mapa completo, missões e crônicas abrem sob demanda. Notificações não cobrem botões nem disputam a mesma área.

**Aceite:** testar 1280×720, 1920×1080, janela estreita e touch em paisagem; nenhum texto cortado, painel sobreposto ou ação inacessível. Sessão válida, expirada, falha temporária, conta sem personagem e troca de conta têm caminhos corretos. Recuperação exige teste real de envio e redefinição em conta de teste.

### 3. Combate com ação e leitura

- Manter o combate de espada do MVP e suas regras de alcance, direção, intervalo, vida, XP e recompensas no servidor. A evolução principal é conectar apresentação e resposta às ações que já existem.
- Preparar animações de antecipação, golpe e recuperação; transições de caminhada para ataque; reação a dano; queda/incapacitação e retorno. Se o rig permitir, animar tronco/arma sem um segundo script deslocar a raiz física.
- Dar resposta visual imediata à intenção válida de atacar; impacto, dano e recompensa acompanham a confirmação. Resolver rejeição/atraso sem repetir animação, dano ou pedido. Comparar temporizações com o intervalo real da arma/agilidade.
- Tornar alvo selecionado, direção, distância e tempo para próximo golpe legíveis. Seleção automática prioriza inimigo válido no cone frontal; troca manual previsível. Ataque fora de alcance deve explicar o motivo sem spam de mensagens.
- Dar aos inimigos preparação visível, perseguição, golpe, reação, morte e reaparecimento coerentes com seus estados do servidor. Revisar a solução atual de encolhimento da morte; avaliar rig/clipes disponíveis antes de investir em nova arte ou animação procedural extensa.
- Conferir perseguição e colisão: a rotina atual avança em linha reta até o jogador, sem navegação por obstáculos nessa função. Garantir limites de perseguição/retorno à origem e segurança de O Berço; testar parede, árvore, ponte e entrada da cidade. Animação melhor não substitui um caminho válido.
- Adicionar arco de arma, som de golpe/impacto, números de dano contidos e feedback de poção/vida baixa. Evitar congelamento global do tempo e tremor obrigatório para dar sensação de impacto.
- Replicar ações para os observadores: outro jogador deve ver ataque, dano e morte, além de movimento. Verificar os eventos atuais e ampliar o contrato só onde falta informação, com ordem e deduplicação.

**Aceite:** dez ataques válidos, rejeições por alcance/intervalo, troca de alvo, duas pessoas no mesmo combate e morte/renascimento. Sem T-pose, arma imóvel durante golpe, corrida parada contra obstáculo ou recompensa duplicada. Os dois clientes percebem a mesma ação e resultado. Ajustes cosméticos não alteram os tempos e fórmulas de combate sem revisão explícita.

**Limite de escopo:** esquiva com invulnerabilidade, combos, bloqueio, stamina, salto com efeito de gameplay e novas armas não fazem parte desta passagem. O GDD exclui esquiva/bloqueio no MVP; qualquer mudança deve primeiro alterar o design e a validação do servidor. Gestos sociais podem ser cosméticos.

### 4. Profissões, inventário, NPCs e jornada conectada

| Sistema | Experiência proposta | Critério de aceite |
|---|---|---|
| NPCs e interação | Instrutora, Minerador, Ferreiro, Comerciante e função de Cronista acessíveis; identificação por código estável, sem depender do texto do nome; E/toque escolhe um contexto | Diálogo/loja/forja não abrem juntos; distâncias coerentes e ações de serviço validadas também no servidor |
| Inventário/equipamento | Grade simples, pilhas, ocupação, descrição/uso, slots de mão/ferramenta; poção acessível no combate; equipamento refletido no modelo | Equipar, guardar e usar sobrevivem à reconexão; limites mostrados correspondem aos realmente aplicados no servidor |
| Atributos/habilidades | Pontos disponíveis evidentes, benefício de cada atributo e XP por prática; comparação antes/depois; redistribuição ligada à instrutora | Um comando gasta um ponto; interface não inventa valores nem concede bônus; o jogador entende o que mudou |
| Mineração | Veio disponível/ocupado/esgotado reconhecível; picareta em movimento, som/partícula, progresso, interrupção e retorno do recurso | Movimento/dano cancelam com feedback correto; dois jogadores disputam o mesmo veio sem recompensa duplicada |
| Forja | Lista de receitas, quantidade de insumos, resultado, duração, disponibilidade e regra de cancelamento visíveis; gesto de trabalho | Só opera no contexto permitido; interrupção/reconexão preservam as regras de reserva e devolução do servidor |
| Loja | Comprar/vender separados na mesma janela, quantidade, preço unitário/total, saldo e resultado claro | Poucos cliques, sem envio duplo; erro de saldo/inventário cheio é recuperável; transação atômica |
| Tutorial/missões | Uma jornada online por IDs de etapa, objetivo atual e destino; progresso salvo; conclusão pela condição do servidor | Falar, combater, minerar, fundir e vender atualizam o objetivo certo; reconectar em qualquer etapa retoma corretamente |
| Mural e crônicas | Feitos reais com autor, data e ação; acesso contextual; estado vazio e falha de carregamento tratados | O registro exibido vem do servidor e corresponde ao feito; ninguém recebe uma vitória fictícia |
| Mapa/exploração | Marcadores de serviços, objetivo e caminho de retorno; legenda simples; entradas da mina e limites legíveis | A posição física, o mapa e a área válida online correspondem; nenhum serviço necessário fica inacessível |

A missão da Lumina pode permanecer como conteúdo narrativo separado. Ela não deve reutilizar a contagem do tutorial produtivo como se fossem as mesmas etapas. Materiais e ferramentas que já nascem no inventário devem ser reconhecidos no diálogo; não criar uma segunda entrega por acidente.

As regras documentadas e implementadas têm divergências (ex.: manter interação para minerar versus comando de início; pausa de fundição na desconexão; peso/pilhas). Registrar a decisão vigente, conferir implementação e atualizar documentação antes de desenhar mensagens e controles que prometam outro comportamento.

**Aceite da etapa:** percorrer login → praça → instrução → floresta → mina → forja → comerciante → crônicas, em 15–20 minutos, sem console/comandos de banco nem orientação externa. Repetir com conta nova e personagem em progresso. Preparar o teste de compreensão com as metas do GDD: 80% concluem tutorial sem ajuda e 70% concluem o ciclo produtivo; metas ainda não medidas nesta revisão.

### 5. Acabamento, social e opções

**Presença e gestos:** nomes legíveis na escala do personagem; animação remota a partir de estados/velocidade estabilizados; equipamento visível; cumprimentar e celebrar como primeiros gestos cosméticos, com frequência limitada e replicação leve. Gestos não bloqueiam comandos essenciais nem modificam posição/HP.

**Social:** integrar chat local, foco para digitar, mensagens contidas e identificação consistente de jogador; acesso a bloquear/denunciar. Conferir quais rotas/eventos já existem e completar o cliente, sem assumir que servidor pronto significa interface pronta.

**Som e cenário:** passos, roupa/arma, golpe, picareta, forja, poção, interface e ambiente com volumes separados. Retorno visual, sonoro e animação devem pertencer à mesma ação. Revisar obstáculos decorativos, caminhos de passagem, silhuetas dos serviços e visibilidade na mata; adiar nova expansão territorial.

**Configurações/acessibilidade:** sensibilidade e inversão de câmera, escala de UI/texto, contraste, controle de sacudida, volumes e qualidade gráfica. Estados não dependem só de cor; foco de teclado visível; alvos touch confortáveis e sem sobreposição. Testar menus e teclado virtual sem mover/atacar por trás.

**Desempenho:** medir CPU/GPU, alocação/GC, tempo de quadro, memória e carregamento na mesma rota. Reutilizar efeitos/números de dano onde o perfil indicar custo; reduzir reconstruções de UI e buscas de cena por quadro; orçamento para vegetação, sombras e quantidade de personagens. Não apenas aumentar suavização ou retirar detalhe sem medição.

**Metas propostas para validar:** 30 FPS sustentados no Android intermediário a definir, conforme GDD; perfil de 60 FPS para WebGL em desktop de referência, mantendo opção 30 FPS. Capturar p50/p95/p99 do tempo de quadro e picos acima de 50 ms durante cinco minutos, excluindo carregamento inicial e documentando hardware, resolução e qualidade. Metas e orçamento final dependem dessa linha de base; não são desempenho já entregue.

### Entrega e prova de qualidade

1. Reservar no log a tarefa e cada arquivo, incluindo saídas de ferramentas. Reservas antigas com conclusão duplicada devem ser esclarecidas pelo próprio responsável; não encerrar entrada de outra IA por suposição.
2. Entregar por etapa ou fatia jogável, com revisão de diferenças e testes adequados. Alterações em protocolo exigem contratos/servidor/cliente compatíveis e verificação conjunta de dois jogadores.
3. Usar dados e contas de teste em ambiente separado para reproduções, simulação de latência e testes destrutivos. Não presumir que uma branch de desenvolvimento já tem serviço Railway e banco isolados: conferir a configuração e documentar o endereço de cada ambiente antes de depender disso.
4. Validar comportamento e apresentação no cliente executável: percurso, captura/vídeo antes/depois e medidas. Build sem erro comprova compilação; não comprova fluidez nem usabilidade. Testes de protocolo não substituem jogar a jornada.
5. Para mudanças no cliente, concluir o build WebGL e publicar a saída conforme `AGENTS.md`. Esperar término real do Unity, resultado do build e status final `SUCCESS` do deploy; receber uma URL de logs após upload não comprova ativação.
6. Identificar o build por versão/commit/data e manifesto de arquivos. Conferir arquivos servidos em produção por hash/manifesto e abrir a URL para validar a versão em uso. Versionar os recursos/cache para reduzir a dependência de Ctrl+F5. O artefato testado deve ser o publicado, com possibilidade de retornar à versão anterior.
7. Registrar no log: o que mudou, testes, problemas restantes, commit, build, deploy e URL. Diferenciar implementado, compilado, testado visualmente e ativo em produção. Evitar encerrar a entrega como resolvida enquanto ainda há teste obrigatório pendente.

**Matriz mínima de regressão:** primeira entrada e retorno; login inválido e recuperação; logout/reconexão; movimento em oito direções e contato com cenário; combate observado por segundo cliente; coleta interrompida e disputada; fabricação cancelada; compra/venda/equipamento; tutorial retomado; crônica persistente; foco de chat/menu; resoluções desktop e controles touch. Revalidar 30 jogadores no servidor e no cliente-alvo quando as mudanças afetarem custo de replicação/apresentação.

### Decisões e dependências para a execução

- Escolher aparelho Android e desktop de referência para tornar as metas verificáveis; isso não bloqueia a auditoria inicial no WebGL existente.
- Confirmar provedor/remetente e endereço do fluxo de recuperação de senha; construir e testar a integração real antes de anunciá-la como disponível.
- Inventariar clipes/rigs/equipamentos/sons existentes e licenças; priorizar reaproveitamento. Registrar lacunas de assets antes de propor compra ou produção adicional.
- Confirmar ambientes de desenvolvimento/produção no GitHub e Railway e o responsável pela integração de cada etapa. Não há migração de hospedagem proposta.
- Preservar o escopo do MVP: sem nova classe, profissão, PvP, guilda, construção, economia nova ou redesign de regras para compensar problemas de apresentação.

**Próximo passo após esta revisão:** executar a etapa 0 e fechar a estabilidade da etapa 1. Em seguida, usar a mesma rota do primeiro ciclo para reconstruir e validar entrada, combate, profissões e interface, com uma entrega verificável por etapa.

---

## Comentários de revisão — Claude (Sonnet 5, Claude Code)

**Registrado em:** 10/09/2026, a pedido do usuário ("você concorda com o plano do Codex e tem algo a acrescentar?").

**Veredito geral:** concordo com a essência do plano — está fundamentado em evidência de código (não em achismo), e a ordem de prioridade (diagnóstico → movimento/interação → entrada → combate → profissões → polimento) faz sentido: resolve estabilidade antes de empilhar mais coisa em cima. Os critérios de aceite por etapa são testáveis, não vagos. Não altero nenhuma linha do que o Codex escreveu acima; isto é só acréscimo.

### Diagnósticos que já posso confirmar ou atualizar, com o que fiz nesta sessão

- **"Interação está distribuída" (seção "Diagnóstico atual")**: confirmo de forma independente — cheguei à mesma conclusão ao implementar o prompt contextual de Comerciante/Ferreiro em `NetworkEconomyController.cs` e já deixei um aviso equivalente para o Codex no histórico deste log. Concordo que resolver isso é P0, antes de qualquer NPC ganhar diálogo de verdade.
- **Fallback de `NetworkEconomyController.UpdateNpcContext`**: não é uma pendência de código a corrigir, é proposital — a seção some/aparece contextualmente perto do NPC, mas volta a "sempre visível" só enquanto nenhum `WorldInteractable` com aquele nome existir na cena (evita esconder a função de vez). Vira automático assim que o Codex posicionar Ferreiro/Comerciante — nenhuma mudança de código adicional necessária desse lado.
- **Criação de personagem, "opções limitadas de aparência... persistidas"**: a metade de dados já foi entregue depois que este plano foi escrito — raça (Humano/Elfo/Anão/Orc) e gênero (Masculino/Feminino) são escolhidos na tela de criação, salvos em `characters.appearance_json` (formato `{ race, gender }`) e devolvidos por `POST /characters`/`GET /characters/me`, validado contra produção. Não toca atributos/progressão (mantém "raça não é classe" e "sem classe fixa"). Falta só a metade visual: mostrar a raça/gênero escolhidos no modelo 3D e replicar isso pros outros jogadores verem — isso segue sendo trabalho de arte do Codex, e o dado já está pronto pra ele consumir.
- **Encolhimento da morte do monstro** (nota já no plano pra revisar): concordo integralmente que é um remendo, não solução final. O giro que eu tinha feito antes colava as pernas no torso (partes fixas, não um mesh articulado); troquei por encolher+afundar só pra parar de ficar visualmente quebrado enquanto não há rig/clipe de queda de verdade.

### Uma ferramenta a reaproveitar

Já existe um script de teste de carga com ~30 clientes reais (usado pra validar o critério de aceite §20.9 do GDD-MVP contra produção). Vale reaproveitar esse padrão quando a "matriz mínima de regressão" pedir revalidar 30 jogadores, em vez de reconstruir do zero.

### Uma ressalva sobre a etapa 0 (diagnóstico do tremor)

A etapa está completa, mas pesada — captura quadro a quadro em três cenários × várias condições de rede simuladas antes de isolar variáveis. Eu inverteria a ordem: começar pelos testes baratos que o próprio plano já cita (câmera fixa, animação temporariamente suspensa) e só montar a instrumentação pesada (matriz de RTT, comparação local/online/dois-clientes) se esses não revelarem a causa. Numa entrega que mira "uma sessão de 15–20 minutos bem acabada", vale não deixar o diagnóstico virar um projeto à parte.
