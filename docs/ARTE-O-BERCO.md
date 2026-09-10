# O Berço — composição ambiental

Implementação de 09/09/2026, orientada por `ESPECTRO-VISAO.md` e `DIRECAO-ARTISTICA.md`.

## Intenção

O Berço deve parecer um lugar onde as pessoas vivem e trabalham. O jogador chega a uma comunidade pequena, encontra ofícios e caminhos e percebe que existe um mundo além dela. A composição mantém a escala do protótipo: detalhar essa região precede aumentar o mapa.

A praça é o espaço de encontro; o Marco dos Mil Caminhos representa possibilidades. A arquitetura permanece de madeira, pedra e reboco, com tecidos e sinais de uso. Os acentos espectrais ficam concentrados no marco e nos elementos já existentes de exploração. As quatro culturas não são transformadas em quatro classes ou quatro bairros obrigatórios.

## Percurso e lugares

| Lugar | Composição implementada | Leitura pretendida |
|---|---|---|
| Praça | Anel incompleto de pedras e quatro ramificações junto ao Marco; bancos nas bordas | Encontro, orientação e memória, com centro livre para circulação |
| Conselho | Identificação da fachada e mural com avisos em papel | A comunidade registra e compartilha acontecimentos |
| Estalagem | Toldo azul e cru, lanternas, mesa, banco, pote e barril | Acolhimento, permanência e conversa |
| Casa Oeste | Floreiras junto à fachada | Cuidado cotidiano e identidade doméstica |
| Forja | Toldo vinho, bancada, bigorna, martelo, lingotes e braseiro com grelha | Trabalho material e ligação entre coleta e produção |
| Comércio | Pequena banca com toldo, produtos, cerâmica e barril | Troca e circulação de recursos; decoração, sem inventar funções de comércio |
| Rio | Margens curvas, areia, água rasa e canal profundo, reflexos discretos, pedras e juncos | Paisagem natural e uma pausa visual entre vila e floresta |
| Ponte | Tábuas alternadas, postes, corrimãos e piso caminhável | Travessia legível e construção mantida pela comunidade |
| Floresta | Oito grupos de vegetação com alturas variadas e sub-bosque | Enquadramento por massas vegetais, mantendo caminhos reconhecíveis |
| Montanha/mineiro | Rochas agrupadas, portal existente, lanternas, trilhos, dormentes, caixote e minério | Transição da vegetação para pedra e sinais de extração |
| Interior da mina | Escoramentos, rochas laterais e detalhes de ferro/cobre | Trabalho subterrâneo; corredor e destinos de interação preservados |

## Caminhos

Três percursos curvos organizam o mapa: praça → floresta, praça → mina e praça → ponte → trilha do rio. O caminho norte contorna a Casa do Conselho. A estrada da mina leva à posição real da interação da entrada. Pedras espaçadas e pequenas manchas de vegetação marcam as bordas.

As superfícies antigas de estrada deixam de ser desenhadas; seus colisores existentes são preservados. A altura das faixas novas acompanha esses degraus do mapa. A ponte recebe um colisor simples de piso. Os demais detalhes são decorativos e não bloqueiam os personagens.

## Paleta e luz

- Luz principal dourada, menos alaranjada que a versão anterior.
- Ambiente frio e névoa verde-azulada para separar distâncias.
- Pedra quente na praça; pedra cinza-esverdeada no horizonte e na mina.
- Madeira escura, tábuas com duas tonalidades, tecidos azuis, crus e vinho.
- Lanternas e brasas usam materiais luminosos simples; apenas dois pontos de luz adicionais, sem sombras dinâmicas.
- Contraste e saturação do pós-processamento reduzidos para preservar a leitura dos materiais.

## Implementação e orçamento

`BercoWorldArt.Build` é chamado explicitamente após a integração dos modelos em `StylizedVisualBootstrap`. Não depende da ordem entre novos bootstraps. Ornamentos de fachadas usam os mesmos limites reais que a reconstrução modular; paredes laterais foram corrigidas para ter espessura no eixo X e profundidade no eixo Z.

Detalhes procedurais são combinados por material e célula de 12 m. Materiais e meshes próprios são descartados junto com a camada. Modelos importados sem Read/Write mantêm seus renderers: não é necessário duplicar os dados de malha na memória nem mudar os importadores para que apareçam no player. Instâncias compartilham os materiais convertidos existentes. A distribuição de vegetação é determinística e evita edifícios, praça, margens e rotas.

Este orçamento é uma decisão de implementação, não uma medição de FPS. A meta de 30 FPS no Android ainda precisa ser medida no aparelho-alvo. A iluminação e os detalhes finais dependem da conferência no jogo.

## Validação desta rodada

- 28 scripts runtime compilaram com os defines Editor e WebGL.
- Build WebGL concluído pelo Unity 6000.5.10f1, saída `client/Builds/WebGL/`; log `client/Logs/build-world-art.log`.
- O acesso normal à licença local permitiu o build; a falha da rodada anterior não se repetiu nessa execução.
- A inspeção automática no navegador não foi concluída: não havia navegador conectado ao plugin Browser; a alternativa de controle Windows foi encerrada porque não conseguiu determinar a URL com segurança. Nenhuma imagem desta rodada foi aprovada visualmente.

Próxima conferência no jogo: vista inicial da praça, anexos das fachadas, travessia da ponte, chegada à mina, interior da mina e retorno. Verificar chão/caminhos, recorte da folhagem, textos, distância de leitura, câmera, sobreposições, recursos ausentes no console e desempenho no Android. Ajustar proporção, densidade e iluminação a partir dessas vistas antes de declarar a arte final.

## Refino após capturas — 10/09/2026

As capturas reais revelaram problemas que a compilação não mostrava. Folhas com textura nativa agora recebem recorte alpha, são visíveis pelos dois lados e não usam o casco preto de contorno. O brilho de borda foi reduzido para preservar as cores da copa. O mural foi separado da identificação do Conselho, com título curto. Caminhos usam uma superfície visual contínua, evitando degraus triangulares nos limites dos antigos blocos. O interior da mina só é desenhado quando o jogador está na área subterrânea, e recebeu teto rochoso. As rochas do horizonte ficaram menores e mais afastadas.

## Arquitetura fechada e habitada — 10/09/2026

As casas modulares receberam um casco interno opaco nas quatro faces e no teto. As aberturas do kit ainda dão profundidade visual, mas não revelam mais gramado, céu ou objetos através do volume. Fundações contínuas de pedra fecham frestas junto ao solo. Nas fachadas, duas linhas de vigas, soleiras, pedras de aproximação, lenha empilhada e ferramentas pequenas reforçam peso, manutenção e uso cotidiano sem criar novos obstáculos de jogabilidade. A identificação grande do mural foi substituída por uma plaqueta curta presa ao quadro, mantendo a placa do Conselho legível.

Os quatro lados do casco interno também definem os colisores reais das casas. Assim, a área física acompanha exatamente o volume visual reconstruído, inclusive nas fachadas maiores que os blocos originais do protótipo. O mural recebeu um colisor simples envolvendo quadro e pés; toldos continuam transitáveis por baixo.
