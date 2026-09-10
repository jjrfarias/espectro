# Coordenação entre agentes

Mais de uma IA trabalha neste repositório ao mesmo tempo. O registro compartilhado é
[`docs/AGENT-WORK-LOG.md`](docs/AGENT-WORK-LOG.md).

## Antes de cada tarefa

1. Leia o log inteiro, novamente, antes de começar.
2. Confira se a área, sistema, pasta ou arquivos necessários estão marcados como
   `EM ANDAMENTO` por outra IA. Se houver sobreposição, pare, avise o usuário e espere
   a liberação. Não altere os arquivos reservados nem encerre a entrada de outra IA.
3. Se a área estiver livre, registre sua entrada no topo da lista com data/hora,
   identificação da IA, área, arquivos/pastas, descrição e status `EM ANDAMENTO`.
   Salve essa entrada **antes de editar qualquer arquivo da tarefa**.
4. Releia o log após registrar a entrada para conferir se outra IA reservou a mesma
   área nesse intervalo. Se houver conflito, pare e avise o usuário.

## Durante e depois

- Antes de ampliar a tarefa para outros arquivos ou sistemas, releia o log, confira
  conflitos e atualize sua reserva antes das novas edições. Isso inclui arquivos
  compartilhados e arquivos que builds ou ferramentas possam modificar.
- Preserve as entradas das outras IAs. Ao atualizar o log, releia a versão atual e
  faça alterações pontuais para não sobrescrever registros novos.
- Ao terminar, marque sua entrada como `CONCLUÍDO` e registre o resultado e as
  verificações realizadas. Se parar no meio, use `INTERROMPIDO` e descreva o que
  ficou feito e o que falta.
- A mesma regra vale para subagentes: registre a responsabilidade e os arquivos
  de cada um antes de delegar edições, sem sobreposição de arquivos entre eles.
