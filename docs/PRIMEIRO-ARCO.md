# Primeiro arco: O Sussurro sob a Mata

O primeiro arco jogável conduz o visitante do Berço até a entrada da Mina Silenciosa. A Anciã Mira apresenta o chamado, a Lumina funciona como objeto de descoberta e o retorno à praça fecha o capítulo com uma consequência no mural.

O servidor já registra conversas com NPCs e os passos do tutorial. No cliente, o HUD agora apresenta capítulo e objetivo em duas linhas, atualizando a cada avanço da interação. O método `InteractionController.ApplyRemoteTutorialStage` permite que a sessão online substitua o estágio local assim que receber o snapshot, sem duplicar a apresentação.

Próximas extensões do arco: enviar `npc.talk.request` a partir da interação online, refletir `tutorial.snapshot` no HUD e substituir a conclusão local por recompensa persistente do servidor.
