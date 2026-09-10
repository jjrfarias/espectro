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
