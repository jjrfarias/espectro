# Cliente — exploração e combate online

## Arte ambiental de O Berço — 09/09/2026

Nova composição em `Assets/Scripts/World/BercoWorldArt.cs`, chamada por `StylizedVisualBootstrap`: caminhos curvos, rio com margens/juncos, ponte caminhável, toldos e ofícios nas fachadas, praça com bancos e mural, bosque em grupos e detalhes da mina. [Direção, implementação e validação](../docs/ARTE-O-BERCO.md).

**Build WebGL desta rodada concluído**, em `Builds/WebGL`, pelo Unity 6000.5.10f1 com acesso normal à licença local (`Logs/build-world-art.log`). A compilação dos 28 scripts runtime passou para Editor e WebGL. A conferência visual automática ficou pendente porque o controle Windows não conseguiu identificar a URL do navegador com segurança.

Versão registrada em `ProjectSettings/ProjectVersion.txt`: **Unity 6000.5.10f1**.

## Primeira abertura

1. Instale pelo Unity Hub o Editor 6000.5.10f1.
2. Inclua Android Build Support, Android SDK & NDK Tools e OpenJDK.
3. No Hub, adicione esta pasta `client` como projeto.
4. Aguarde a importação. A cena `Assets/Scenes/Corte0.unity` será criada automaticamente.
5. Pressione Play ou selecione Android no perfil de build.

Também é possível recriar a cena pelo menu **Espectro → Recriar Cena do Corte 0**.

## Controles

- Editor: WASD ou setas.
- Android: joystick virtual no canto inferior esquerdo.

## Critérios deste corte

- personagem move no mapa cinza;
- câmera acompanha em terceira pessoa;
- interface adapta a 1920×1080 e proporções próximas;
- HUD informa FPS;
- build ARM64, paisagem e Android 10+;
- meta de 30 FPS em aparelho intermediário.

## Build automatizado (Android)

Com Unity e a licença ativos:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Unity.exe' `
  -batchmode -quit `
  -projectPath 'C:\Projetos\game\client' `
  -buildTarget Android `
  -executeMethod Espectro.Editor.Corte0ProjectSetup.BuildAndroid `
  -logFile 'C:\Projetos\game\client\Logs\build-android.log'
```

O APK de desenvolvimento é criado em `Builds/Android/Espectro-Corte0.apk`.

## Build WebGL (teste rápido, em paralelo ao Android)

Android continua sendo a plataforma prioritária do projeto (`docs/ESPECTRO-VISAO.md` §45, princípio 8). WebGL existe só como atalho de iteração: testar no navegador sem instalar APK a cada mudança, mesmo projeto, nada do trabalho existente se perde.

**Pré-requisito atendido nesta máquina:** WebGL Build Support está instalado. Os relatos abaixo de validação no navegador são de builds anteriores; a integração de combate de 09/09/2026 ainda exige novo build e teste visual.

Depois de instalado:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Unity.exe' `
  -batchmode -quit `
  -projectPath 'C:\Projetos\game\client' `
  -buildTarget WebGL `
  -executeMethod Espectro.Editor.Corte0ProjectSetup.BuildWebGL `
  -logFile 'C:\Projetos\game\client\Logs\build-webgl.log'
```

Gera uma pasta em `Builds/WebGL`. Sirva com um servidor HTTP local (`npx serve Builds/WebGL`, por exemplo) — abrir o `index.html` direto do disco não funciona por causa de CORS/COOP do navegador.

**Validado rodando de verdade no navegador.** Na primeira tentativa, o build compilava mas dava erro em runtime (`ReferenceError: espectroWsDispatch is not defined`) — uma função do plugin `.jslib` estava definida fora do sistema de dependências do Emscripten e sumia do bundle final mesmo sem erro de compilação. Corrigido em `Assets/Plugins/WebGL/EspectroWebSocket.jslib` (função registrada como dependência `$nome`, não solta) e reconfirmado com login/mundo funcionando no navegador.

## Rede (Cortes 1 e 2)

`Assets/Scripts/Network/` conecta este cliente ao [servidor](../server/README.md): login/criação de conta, criação de personagem, WebSocket para ver outros jogadores e lutar contra inimigos em tempo real. **Validado funcionando de verdade no Editor** (login, criação de personagem e conexão ao mundo confirmados via logs do servidor).

- `Protocol.cs` — DTOs que espelham `contracts/src/index.ts` (Cortes 1 e 2). Mantenha os dois em sincronia manualmente ao mudar o protocolo.
- `ApiClient.cs` — HTTP (`/auth/*`, `/characters*`) via `UnityWebRequest`.
- `WorldConnection.cs` — WebSocket `/world`. Em Android/Editor/PC usa `System.Net.WebSockets.ClientWebSocket`; em build WebGL real usa a WebSocket nativa do navegador via `WebGlWebSocketBridge.cs` + `Plugins/WebGL/EspectroWebSocket.jslib` (`ClientWebSocket` não funciona em WebGL — o navegador não dá acesso a sockets nativos). Os dois caminhos convergem no mesmo `PumpMainThread()`, então nenhum outro script precisa saber qual dos dois está em uso.
- `RemotePlayerView.cs` — avatar simples para outros jogadores, interpolado entre snapshots (~10 Hz).
- `NetworkSession.cs` — orquestra o fluxo login → personagem → mundo; único ponto que fala com o servidor.
- `NetworkUI.cs` — tela mínima de login/criação de personagem, **funcional mas não definitiva**: pensada para ser restilizada sem tocar em `NetworkSession.cs`.

`NetworkSettings.cs` aponta para `127.0.0.1:3000` por padrão (funciona direto no Editor e no build WebGL local). Em Android físico use `adb reverse tcp:3000 tcp:3000`; no emulador, troque o host para `10.0.2.2`.

O jogo inicia na exploração local, preservando o teste sem conta. Use **ENTRAR ONLINE** no canto superior direito para abrir o login/criação de conta existente; **VOLTAR AO TESTE LOCAL** cancela o fluxo. Após o primeiro snapshot, aparecem inimigos, vida e progresso. Use **F** ou **ATACAR** para pedir um golpe ao servidor, **TAB** ou **ALVO** para trocar de inimigo; clicar no inimigo também seleciona. **SAIR DO ONLINE** encerra a conexão e retorna à exploração local.

`NetworkCombatController.cs` apresenta vida, alvo e XP; `NetworkEnemyView.cs` apresenta lobos/javalis procedurais conforme snapshots. O bootstrap antigo em `Assets/Scripts/Combat/` continua desligado. Morte bloqueia movimento/ataque; só o snapshot de respawn do servidor libera o personagem. A reconexão limpa representações antigas e tentativas canceladas não podem reabrir uma sessão.

O movimento online usa a velocidade efetiva em coordenadas do mundo de `PrototypePlayerController.LastWorldVelocity`, já considerando a câmera e colisões locais. A velocidade fica limitada pelo `movementSpeed` do snapshot; correções horizontais reduzem divergências. Isto ainda não constitui um sistema completo de previsão com confirmação de cada input; colisões/navegação do cenário ainda não são simuladas no servidor.

**Verificação de 09/09/2026:** 27 scripts runtime compilados sem erros para Editor e WebGL usando o compilador instalado do Unity e saídas isoladas em `.codex-validation/csharp/`. Não foi executado build IL2CPP/JSLib nem teste visual desta integração. Para aceite, verificar entrada local → login → inimigos → ataque/dano/XP → morte/respawn → saída/reconexão com servidor e banco em execução.

## Economia (Corte 3, novo)

`NetworkEconomyController.cs` liga o cliente ao [Corte 3 do servidor](../server/README.md) (inventário, mineração, metalurgia, venda): mesmo padrão do combate — presentation only, todo resultado vem confirmado do servidor.

- Veios de recurso (`resourceNodes` do `world.snapshot`) viram marcas simples no mundo (cápsula marrom = ferro, laranja = cobre) só pra terem algo clicável/perto-de-si — **placeholder funcional, não visual definitivo** (fica pro Codex refinar como o resto da arte).
- Perto de um veio disponível (2,75 m, mesmo alcance validado pelo servidor), aparece "Pressione M para minerar (Ferro/Cobre)"; `M` envia `resource.mine.request`.
- Painel no canto superior direito mostra moedas e a quantidade de cada item vendável, com um botão VENDER por linha (`trade.sell.request`, vende a pilha inteira) e dois botões de fundição (`craft.request` para `lingote_ferro`/`lingote_cobre`).
- `Protocol.cs` ganhou os DTOs de `economy.snapshot`, `resource.mine.result`, `craft.result`, `trade.sell.result`, `equip.request`/`equip.result` e `resourceNodes[]` em `WorldSnapshotPayload` — mantenha em sincronia com `contracts/src/index.ts` como o resto do protocolo.
- **Equipamento (novo):** o painel de economia mostra "Arma: ... · Ferramenta: ..." e um botão que alterna EQUIPAR/GUARDAR PICARETA. Minerar agora exige a picareta equipada (rejeitado pelo servidor com `TOOL_NOT_EQUIPPED` senão) — o personagem já nasce equipado, então isso só importa se o jogador guardar a picareta de propósito.

**Validado em 10/09/2026:** compila limpo no Editor (`Unity_RunCommand` via MCP). Ciclo completo (minerar → fundir → vender → crônica) validado ponta a ponta contra o Postgres real — ver [server/README.md](../server/README.md) — com um script de teste que fala o protocolo diretamente (não pelo Editor). Testar minerar/fundir/vender de dentro do próprio jogo (Editor, ao vivo) ainda não foi feito.

## Mapa (novo)

`NetworkMapController.cs`: minimapa (canto inferior direito) sempre visível online, e uma tela de mapa completo (tecla **N**) com legenda. Mesmo padrão presentation-only dos outros controllers de rede — desenha só o que já vem no `world.snapshot` (posição própria, outros jogadores, inimigos vivos, veios de recurso disponíveis), nada é inventado no cliente.

- Minimapa: você fica sempre no centro (seta branca, gira com `facingY`), o mundo se move ao redor num raio de ~26 unidades.
- Mapa completo: posição fixa no mundo inteiro (±40 unidades, mesmo limite do `Terreno do Berco`), você se move dentro dele.
- **Tecla N**, não M — M já é usada por `NetworkEconomyController` pra minerar; as duas juntas na mesma tecla faria minerar e abrir/fechar o mapa ao mesmo tempo.
- Cores: você (branco), outros jogadores (azul), inimigos (vermelho), ferro (marrom), cobre (laranja) — mesmas cores dos veios em `NetworkEconomyController`.
- **Validado em 10/09/2026** via `Unity_RunCommand` (MCP): aplicando um `world.snapshot` de teste, as posições calculadas nas marcas do mapa batem exatas com a conta manual (ex.: veio em `(18,-3)` no mundo vira `(144,-24)` no mapa completo, escala 8x = 320px / 40 unidades). Não visto pixel a pixel (a ferramenta de captura do MCP só pega a Scene View, que não renderiza Canvas de tela).

## Personagem (KayKit)

**Bloqueio de validação da integração online (09/09/2026):** a tentativa de build em Unity 6000.5.10f1 encerrou com código 198 e `No valid Unity Editor license found. Please activate your license.` O log está em `Logs/build-online-combat.log`. Ativar a licença pelo Unity Hub e repetir o build antes de considerar esta integração validada no navegador. A compilação C# isolada passou, mas não substitui essa validação.

`Assets/Editor/AdventurerCharacterSetup.cs` monta, uma vez só, um personagem de verdade a partir do pacote gratuito **KayKit Adventurers** (`Assets/ThirdParty/KayKit`, já importado): modelo Knight, animações reais de Idle/Andar/Correr (extraídas dos FBX de animação do pacote com Loop Time habilitado), espada e escudo encaixados nos ossos `handslot.r`/`handslot.l` do próprio rig. O resultado é salvo como prefab em `Assets/Resources/EspectroModels/Adventurers/Aventureiro.prefab`.

`Assets/Scripts/World/AdventurerCharacterBootstrap.cs` troca o corpo em primitivas do jogador por esse prefab em runtime, e liga um `Animator` que recebe a velocidade atual (`PrototypePlayerController.CurrentSpeed`) para animar Idle/Andar/Correr. Roda em `MonoBehaviour.Start()` (não direto em `RuntimeInitializeOnLoadMethod`) de propósito: a ordem entre esse bootstrap e `StylizedVisualBootstrap` (que cria acessórios em primitiva) não é garantida no mesmo evento `AfterSceneLoad`, e `Start()` só roda depois que ambos já terminaram — evita sobra de primitiva grudada no personagem novo.

**Validado visualmente** (o usuário rodou e conferiu no Editor): capacete, escudo, túnica visíveis com a cor certa. Um bug real apareceu e foi corrigido nesse processo — materiais criados em script (`new Material(...)`) só existem na memória; se forem referenciados por um GameObject salvo com `PrefabUtility.SaveAsPrefabAsset` sem antes virarem asset persistido (`AssetDatabase.CreateAsset`), a referência serializa como `fileID: 0` e o Unity renderiza o material de erro (rosa). Corrigido salvando cada material como `.mat` de verdade antes de atribuir. Altura-alvo ajustada para 1.65 m (proporcional às casas).

## Vegetação e vila (Quaternius) — texturas reais

`UpgradeWithImportedModels` (em `StylizedVisualBootstrap.cs`) estava **desligada** — o jogo rodava só com primitivas geométricas mesmo com os modelos do Quaternius (`Assets/ThirdParty/Quaternius`) já importados. Religada.

O FBX do Quaternius não resolve a textura embutida ao importar (referência externa quebrada — confirmado extraindo os materiais em batchmode: `source.mainTexture` vem `null` para o pacote inteiro). `ConvertImportedMaterials` carrega a textura real explicitamente por nome, com duas tabelas de mapeamento:

- **Por recurso** (`ResourceTextureOverrides`): peças de material único — paredes, telhado, porta, piso, props da vila. O nome do arquivo já identifica a textura certa.
- **Por material** (`MaterialTextureOverrides`): peças com mais de um material, como árvore = casca + folha, onde o nome do recurso sozinho não bastaria. Os nomes (`NormalTree_Bark`, `NormalTree_Leaves`, `PineTree_Bark`, `PineTree_Leaves`, `Bush_Leaves`, `Grass`, `Rock`) foram confirmados extraindo os FBX em batchmode, não adivinhados.

A folhagem (folhas/grama/vinha) usa cartões planos recortados pelo canal alpha da textura (fundo branco = transparente). Sem ativar o recorte, o cartão inteiro aparecia como painel sólido — era o efeito de "teia"/galhos esparramados no lugar da copa da árvore. `ConfigureAlphaClip` liga `_AlphaClip`/`_Cutoff`/`_ALPHATEST_ON` no material pra essas texturas.

Texturas usadas ficam copiadas em `Assets/Resources/EspectroModels/Textures/` (precisa estar em `Resources` pra `Resources.Load` achar em build).

Árvores importadas eram escaladas pra bater com a altura da primitiva antiga que substituíram (~5,7 m, valor arbitrário do protótipo original) — isso deixava galho/tronco grossos demais, porque o modelo real do Quaternius não tem essa proporção. Reduzido pra metade (`sizeMultiplier 0.5`) em `StylizedVisualBootstrap.cs`.

## Vila (Village) — bug de textura por peça inteira

Existiam dois pacotes da mesma vila medieval: `Assets/Resources/EspectroModels/Village/` (FBX, em uso) e `Assets/Resources/EspectroModels/VillageOBJ/` (OBJ, importado mas nunca ativado — pertencia a um pipeline paralelo em `Assets/Editor/EspectroVisualLabBuilder.cs`, também nunca ativado). O OBJ tinha os `.mtl` apontando pra caminhos absolutos de outra máquina (`C:/Users/Usuario/Desktop/...`), quebrados — corrigidos pra apontar pro nome do arquivo na mesma pasta.

Mas o pacote quebrado não era o que rodava. Extraindo os materiais do FBX que está de fato ativo (`Village/`) em batchmode, apareceu um bug real: parede e telhado têm mais de um material (viga de madeira + reboco, por exemplo), e o próprio FBX já resolve a textura certa por submesh sozinho — só falha onde a textura nem existe no pacote (acabamento de pedra "RockTrim", vidro de janela). `ConvertImportedMaterials` estava ignorando essa resolução por submesh e forçando **a peça inteira** pra uma textura única por nome de arquivo (`ResourceTextureOverrides`), apagando a textura de madeira correta da viga e pintando por cima com reboco/telha. Prioridade corrigida: 1) textura que o FBX já resolveu, 2) override por nome de material só quando o FBX não achou nada (segue valendo pro pacote Nature, que não resolve nada sozinho), 3) cor fixa pros dois materiais sem textura no pacote (pedra e vidro).

De quebra, duas peças extras que só existem no OBJ (`Corner_ExteriorWide_Brick`) foram ligadas como acabamento de canto nas quatro esquinas de cada casa, pra quebrar a repetição das paredes lisas.

## Nature/Village (FBX) — eixo errado e escala minúscula

Medindo os bounds locais crus dos modelos (sem nenhuma rotação/escala nossa em cima), ficou claro que os pacotes `Nature/` (árvore, rocha, arbusto, grama) e `Village/` (parede, telhado, porta, props) foram exportados com o "comprimento" do modelo no eixo Z em vez do Y, numa escala ~100-1000x menor que o resto da cena. O pacote `VillageOBJ/` (OBJ) não tem esse problema — importa com eixo e escala corretos. Isso explicava três coisas ao mesmo tempo:

- **Árvore deitada no chão**: o "crescimento" da árvore ficava no eixo Z (horizontal), não no Y (vertical).
- **Arbusto/grama/carroça/caixote/cerca/vinha praticamente invisíveis**: escala fixa (`Vector3.one * algumaCoisa`) aplicada direto sobre um mesh cru de ~0,01-0,08 unidade.
- **Vigas de parede tortas**: o encaixe por bounds (`SpawnFitted`) tentava esticar o eixo errado pra bater com a altura-alvo.

Corrigido em `StylizedVisualBootstrap.cs`:

1. `SpawnImported` aplica uma rotação de -90° em X antes de qualquer outra, só pra recursos `Nature/`/`Village/` — isso põe o eixo real do modelo (Z) apontando pra cima (Y do mundo), pra qualquer yaw aplicado depois (rotação em Y não mexe em quem já ficou alinhado ao Y).
2. Um novo `SpawnNormalized` substitui escala fixa por escala calculada pela altura real em metros (mesma lógica que já existia pras árvores) — usado agora em arbusto, grama, carroça, caixote, cerca e vinha.
3. `SpawnFitted` (encaixe não-uniforme por eixo, usado pra parede/telhado/porta) precisou saber que a altura agora vem do canal Z local, e que os dois eixos horizontais trocam de canal local dependendo do yaw ser 0°/180° ou 90°/270° — sem isso, a parede lateral (90°/270°) ficava esmagada de um lado e esticada ~20 m do outro. Validado comparando os bounds finais com o alvo pra cada parede das 4 casas: batem exatos agora.

As "montanhas" do horizonte (`CreateOrganicHorizon`) também pareciam flutuar — estavam em z=43~46, além da borda de `Terreno do Berco` (um Plane de escala 8, vai só até ±40). Movidas pra z=33~36, dentro do limite.
