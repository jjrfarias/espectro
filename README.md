# Espectro

MMORPG 3D de mundo aberto, persistente e orientado por jogadores.

> O jogo fornece o mundo. Os jogadores escrevem a história.

## Documentos

- [Visão do projeto — ESPECTRO](docs/ESPECTRO-VISAO.md) — lore, pilares, as quatro culturas, economia, roadmap e princípios para IAs que trabalham no projeto. **Leia primeiro.**
- [GDD do MVP](docs/GDD-MVP.md) — regras, escopo e critérios de aceite da primeira versão jogável.
- [Arquitetura técnica do MVP](docs/ARQUITETURA-MVP.md) — componentes, dados, rede, segurança e plano de implementação.

## Implementação

- [Cliente Unity — Corte 0](client/README.md) — movimentação, câmera, joystick e interação com o mundo em Android.
- [Servidor — Cortes 1 e 2](server/README.md) — conta, personagem, presença compartilhada e combate autoritativo via WebSocket sobre Node.js + PostgreSQL.
- [Contratos do protocolo](contracts/src/index.ts) — envelope e mensagens versionadas compartilhadas pelo servidor.

## Estado

- **Cliente:** Unity 6000.5.10f1, exploração local sem login, personagem KayKit e cenário Quaternius.
- **Servidor:** conta, personagem, presença compartilhada, movimento e combate autoritativos contra lobos/javalis.
- **Combate online integrado (09/09/2026):** botão **ENTRAR ONLINE**, seleção de inimigos, ataque por **F** ou botão, vida, XP, morte e retorno ao Berço guiados pelo servidor. **TAB** ou **ALVO** troca a seleção. O protótipo de combate local antigo permanece desativado.
- **Movimento online:** envia velocidade efetiva no mapa, respeita o limite informado pelo servidor e corrige divergências horizontais pelos snapshots.
- **Progressão:** recompensa, habilidade de espada e pontos de atributo são salvos em uma transação; falha permite repetir o ataque sem duplicar recompensa.
- **WebGL:** módulo instalado e builds anteriores existentes. Esta etapa foi compilada em C# com defines Editor e WebGL; o build executável e o teste visual desta integração ainda precisam ser validados.
- **Validação desta etapa:** contratos e servidor compilados; **50 testes** passaram. Os testes usam persistência simulada e WebSocket real, sem PostgreSQL real nesta rodada.
- **Bloqueio do novo build:** Unity encerrou com código 198 por ausência de licença válida (`client/Logs/build-online-combat.log`). É necessário ativar a licença no Unity Hub para concluir o build e a validação visual.
- **Próximo:** validar o combate jogando; depois inventário/equipamentos, mineração, metalurgia e economia (Corte 3).

Antes de iniciar trabalho paralelo, leia [o protocolo de coordenação](AGENTS.md) e [o log compartilhado](docs/AGENT-WORK-LOG.md).
