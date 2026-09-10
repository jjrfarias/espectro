# Game Design Document — MVP

**Projeto:** Espectro  
**Versão:** 0.1  
**Status:** proposta para validação  
**Plataforma inicial:** Android  
**Tecnologia prevista:** Unity/C# + Node.js + PostgreSQL

## 1. Objetivo do MVP

Provar, com poucos sistemas bem conectados, que jogadores podem compartilhar um mundo persistente, combater, minerar, transformar minério, negociar com NPCs e deixar acontecimentos registrados pelo servidor.

O MVP não tenta provar escala de MMORPG, política entre cidades ou construção livre. Ele prova o primeiro ciclo completo:

**explorar → combater/coletar → transformar → vender → evoluir → deixar registro**

### Hipóteses a validar

1. O ciclo de mineração e metalurgia é compreensível e satisfatório.
2. A progressão sem classes permite ao jogador perceber evolução.
3. A presença de outros jogadores torna o mapa mais vivo.
4. A persistência dá valor às ações realizadas em sessões anteriores.
5. Eventos históricos simples despertam curiosidade sobre o mundo.
6. O servidor consegue validar as ações essenciais sem confiar no cliente.

### Indicadores do teste

- 80% dos testadores concluem o tutorial sem ajuda externa.
- 70% completam ao menos um ciclo de minério até venda.
- 60% retornam para uma segunda sessão no teste fechado.
- Menos de 1% das transações válidas geram inconsistência de inventário.
- Nenhuma moeda, item ou experiência é criada exclusivamente pelo cliente.

Esses números são metas de produto para o teste, não compromissos comerciais.

## 2. Escopo fechado

### Incluído

- criação de conta, autenticação e um personagem por conta;
- uma aparência básica com opções limitadas;
- movimentação em terceira pessoa;
- uma cidade, uma floresta, uma montanha e uma mina;
- outros jogadores visíveis na mesma instância;
- chat local;
- inventário e equipamento;
- combate com espada contra dois tipos de monstro;
- morte, renascimento e recuperação;
- mineração de ferro e cobre;
- fundição de lingotes de ferro e cobre;
- compra e venda com um comerciante NPC;
- progressão de personagem, espada, mineração e metalurgia;
- missões de tutorial;
- registro de acontecimentos selecionados;
- salvamento persistente no servidor.

### Fora do MVP

- PvP, guildas, grupos e comércio direto entre jogadores;
- construção, propriedades e cidades de jogadores;
- agricultura, alquimia e outras profissões;
- magia, arco e múltiplos estilos de combate;
- política, impostos e guerras;
- leilão ou mercado global;
- IA generativa em tempo real;
- recompensas em dinheiro real, anúncios e loja paga;
- mundo sem instâncias, múltiplos continentes e clima dinâmico;
- versões para PC e iOS.

## 3. Público e experiência pretendida

O primeiro teste atende jogadores de RPG e sandbox em celulares intermediários, com sessões de 10 a 30 minutos. O tom é de fantasia medieval acolhedora na cidade e progressivamente perigosa fora dela.

O jogador deve sentir:

- liberdade para alternar entre luta e produção;
- clareza sobre como melhorar;
- segurança na cidade e risco durante a exploração;
- utilidade econômica nos recursos coletados;
- pertencimento a um mundo compartilhado.

## 4. Jornada da primeira sessão

1. Entrar ou criar conta.
2. Criar nome e aparência do personagem.
3. Surgir na praça de O Berço.
4. Aprender movimentação e interação.
5. Falar com a instrutora e receber uma espada simples.
6. Derrotar uma criatura na floresta.
7. Receber uma picareta e viajar à mina.
8. Extrair três minérios de ferro.
9. Fundir um lingote na forja da cidade.
10. Vender o lingote ao comerciante.
11. Consultar o registro público do primeiro feito.

Tempo-alvo: 15 a 20 minutos.

## 5. Mundo do MVP

### O Berço — cidade inicial

Zona segura, sem combate. Contém ponto de renascimento, instrutora, comerciante, ferreiro/forja, mural de crônicas e acessos para floresta e montanha.

### Floresta

Zona de combate introdutória. Contém lobos, javalis e caminhos claros de retorno. Serve para ensinar ataque, dano, cura passiva e saque.

### Montanha

Zona de transição, com monstros mais resistentes. Conecta a cidade à mina e comunica aumento de risco.

### Mina

Contém veios de ferro e cobre. Os veios são compartilhados, reaparecem e têm posições definidas pelo servidor. Não há recursos raros no MVP.

### Estrutura de instância

Uma instância comporta inicialmente até **30 jogadores simultâneos**. Ao atingir o limite, o servidor cria outra instância. Personagem, inventário, economia e história são persistentes entre instâncias; posição momentânea e monstros pertencem à instância.

## 6. Personagem e atributos

### Atributos usados

Para evitar sistemas sem função no MVP, apenas quatro atributos entram na primeira versão:

| Atributo | Efeito inicial |
|---|---|
| Força | aumenta dano físico e capacidade de carga |
| Agilidade | reduz intervalo entre ataques |
| Vitalidade | aumenta HP máximo |
| Resistência | reduz dano físico recebido |

Inteligência e Vontade ficam armazenadas no modelo futuro, mas não são exibidas nem evoluídas no MVP.

### Valores iniciais e fórmulas

- todos os atributos começam em 5;
- HP máximo = `100 + Vitalidade × 10`;
- capacidade de carga = `20 + Força × 2` unidades de peso;
- dano bruto = `dano da arma + Força × 1,5`;
- dano recebido = `máximo(1, dano bruto - Resistência × 0,5)`;
- intervalo de ataque = `máximo(0,7 s, 1,5 s - Agilidade × 0,03 s)`.

Os resultados são arredondados pelo servidor. Não há acerto crítico, esquiva, mana ou efeitos elementais.

### Progressão geral

O personagem recebe XP geral por combate, coleta, fabricação e conclusão de missões. Cada nível concede um ponto de atributo.

XP para o próximo nível = `100 × nível atual^1,5`, arredondado para cima.

O MVP possui nível máximo 10 e permite redistribuição gratuita durante o teste, falando com a instrutora. Isso facilita balanceamento e evita punir experimentação.

## 7. Habilidades sem classe

As habilidades evoluem pelo uso e possuem níveis separados de 1 a 10.

| Habilidade | Ação que concede XP | Benefício por nível |
|---|---|---|
| Espada | causar dano válido | +2% de dano com espada |
| Mineração | concluir extração | -2% no tempo de extração |
| Metalurgia | concluir receita | -2% no tempo de fabricação |

XP para o próximo nível da habilidade = `50 × nível atual^1,6`.

Uma mesma ação não pode conceder XP mais de uma vez. Tentativas canceladas, alvos inválidos ou receitas revertidas não concedem XP.

## 8. Combate

### Controles

- joystick virtual para movimento;
- botão de ataque;
- seleção automática do inimigo válido mais próximo dentro do cone frontal;
- botão para usar poção;
- sem bloqueio manual no MVP.

### Regras

O cliente solicita o ataque. O servidor confirma distância, direção aproximada, intervalo, estado do atacante e existência do alvo. O servidor calcula dano, HP, morte, saque e XP.

### Inimigos

| Inimigo | HP | Dano base | Recompensa | Papel |
|---|---:|---:|---|---|
| Lobo | 60 | 8 | 20 XP, chance de couro | tutorial |
| Javali | 110 | 13 | 35 XP, chance de couro | desafio básico |

Os valores são parâmetros de balanceamento no servidor.

### Morte

Ao chegar a 0 HP, o personagem fica incapacitado por cinco segundos e renasce em O Berço com HP completo. No MVP não perde itens, XP ou moedas. O objetivo é testar o ciclo, não penalidade.

## 9. Inventário e equipamentos

- inventário inicial com 20 espaços;
- itens iguais ocupam pilhas até um limite definido por item;
- peso total não pode exceder a capacidade do personagem;
- equipamento possui dois espaços ativos: mão principal e ferramenta;
- itens do MVP: espada simples, picareta simples, poção, minério de ferro, minério de cobre, lingote de ferro, lingote de cobre e couro;
- não há durabilidade, raridade aleatória, aprimoramento nem descarte no chão.

Toda alteração de inventário é uma transação atômica no servidor. Se uma operação falhar, nenhum item ou moeda parcial é mantido.

## 10. Mineração

### Fluxo

1. Equipar a picareta.
2. Aproximar-se de um veio disponível.
3. Manter o comando de interação durante a extração.
4. O servidor valida alcance e estado do veio.
5. Ao concluir, o servidor entrega o recurso, XP e inicia o reaparecimento.

### Parâmetros iniciais

| Recurso | Tempo base | Quantidade | Reaparecimento |
|---|---:|---:|---:|
| Ferro | 4 s | 1–2 | 45 s |
| Cobre | 6 s | 1 | 90 s |

Mover-se, sofrer dano ou perder conexão cancela a extração sem recompensa. O resultado aleatório é decidido pelo servidor. Os veios são globais dentro da instância para criar disputa leve, sem PvP.

## 11. Metalurgia

A fabricação ocorre somente na forja de O Berço.

| Receita | Entrada | Saída | Tempo base |
|---|---|---|---:|
| Lingote de ferro | 3 minérios de ferro | 1 lingote de ferro | 5 s |
| Lingote de cobre | 3 minérios de cobre | 1 lingote de cobre | 7 s |

O servidor reserva os ingredientes ao iniciar a receita. Cancelamento voluntário antes da metade do tempo devolve os ingredientes; depois da metade, devolve dois dos três minérios. Queda de conexão pausa a tarefa por 60 segundos e, depois, aplica a mesma regra de cancelamento.

Não haverá fila de fabricação nem criação de armas no MVP.

## 12. Economia

Existe uma única moeda virtual provisoriamente chamada **coroa**.

### Fontes de moeda

- recompensa única de missões do tutorial;
- venda de recursos e produtos ao comerciante.

### Sumidouros de moeda

- compra de poções;
- reposição de picareta ou espada perdida por suporte/teste;
- redistribuição de atributos será gratuita no MVP e não é sumidouro.

### Preços iniciais

| Item | NPC compra do jogador | NPC vende ao jogador |
|---|---:|---:|
| Minério de ferro | 3 | — |
| Minério de cobre | 5 | — |
| Lingote de ferro | 12 | — |
| Lingote de cobre | 20 | — |
| Couro | 4 | — |
| Poção | — | 15 |

O valor do lingote supera seus ingredientes para remunerar tempo e progressão em metalurgia. Os preços são fixos no MVP. Toda criação e destruição de moeda gera registro auditável com motivo e referência da transação.

## 13. NPCs e missões

NPCs utilizam diálogo roteirizado, sem IA generativa.

- **Instrutora:** tutorial, espada e redistribuição de atributos.
- **Minerador:** entrega a primeira picareta e ensina mineração.
- **Ferreiro:** libera a forja e explica metalurgia.
- **Comerciante:** compra materiais e vende poções.
- **Cronista:** apresenta feitos registrados.

As missões são lineares somente durante o tutorial. Depois dele, o jogador pode repetir livremente o ciclo principal.

## 14. História viva

O MVP registra eventos estruturados, não narrativas geradas por IA.

### Eventos registrados

- primeiro jogador do servidor a entrar na mina;
- primeira extração de cada tipo de minério;
- primeiro lingote de cada tipo;
- primeira derrota de cada tipo de monstro;
- primeiro personagem a alcançar nível 10 em uma habilidade.

Cada evento contém ID, tipo, personagem, instante UTC, local, dados específicos e versão do esquema. Um evento único usa restrição no banco para impedir vencedores duplicados por concorrência.

O mural de crônicas exibe frases criadas por modelos de texto fixos, por exemplo: “{personagem} fundiu o primeiro lingote de ferro em {data}.” Nomes inadequados passam por moderação e podem ser substituídos por “um aventureiro” no registro público.

## 15. Multiplayer e comunicação

- estado de movimento replicado em frequência ajustável, com interpolação no cliente;
- combate, recursos, inventário e economia confirmados pelo servidor;
- chat local alcança jogadores da mesma instância;
- limite inicial de 200 caracteres por mensagem;
- bloqueio, denúncia, filtro de termos e limitação de frequência são obrigatórios antes do teste externo;
- mensagens de chat ficam retidas pelo período definido na política de privacidade e moderação.

O jogo deve comunicar perda de conexão, tentar reconectar e restaurar o último estado persistido confirmado.

## 16. Persistência e autoridade

### Dados persistentes

- conta e sessão;
- personagem, aparência, posição segura e atributos;
- níveis e XP;
- inventário e equipamento;
- saldo e livro-razão econômico;
- progresso de missões;
- eventos históricos;
- punições, bloqueios e denúncias.

### Regras de autoridade

O cliente envia intenção; o servidor determina resultado. O servidor valida velocidade, alcance, tempo entre ações, receita, saldo, propriedade de itens, capacidade de inventário e recompensa.

Operações econômicas usam transações de banco e chaves de idempotência. Repetir uma requisição após timeout deve retornar o mesmo resultado, não duplicar a operação.

## 17. Interface mínima

Telas obrigatórias:

- entrada, criação de conta e personagem;
- HUD com HP, nível, alvo e atalhos;
- inventário/equipamento;
- atributos e habilidades;
- diálogo e loja;
- fabricação;
- missões;
- chat;
- mural de crônicas;
- configurações, denúncia e bloqueio.

Requisitos de usabilidade: áreas de toque adequadas, escala de interface, opção de reduzir qualidade gráfica, controle de volume separado e feedback visível para ações aguardando confirmação do servidor.

## 18. Requisitos não funcionais

- alvo de 30 FPS em aparelho Android intermediário definido antes da produção;
- reconexão sem perda de transação confirmada;
- senhas nunca armazenadas em texto puro;
- comunicação autenticada e criptografada;
- logs sem senha, token ou conteúdo pessoal desnecessário;
- métricas de latência, erros, jogadores simultâneos e falhas transacionais;
- backup automático do banco e teste documentado de restauração;
- ambiente de desenvolvimento separado do teste;
- compatibilidade arquitetural com futuros clientes, sem lógica confiável exclusiva no Unity.

## 19. Telemetria de produto

Com consentimento e minimização de dados, registrar:

- início e conclusão de cada etapa do tutorial;
- duração de sessão;
- mortes e desconexões;
- recursos extraídos e receitas concluídas;
- compras, vendas e saldo agregado;
- níveis alcançados;
- uso do mural de crônicas.

Eventos de telemetria não substituem o livro-razão autoritativo. Privacidade, retenção e exclusão de conta devem ser definidas antes do teste com público externo.

## 20. Critérios de aceite do MVP

O MVP está funcionalmente pronto quando:

1. Dois ou mais jogadores entram, se veem e se movimentam na mesma instância.
2. Um jogador conclui toda a jornada da primeira sessão.
3. Combate e mineração rejeitam ações fora de alcance ou frequência permitida.
4. Inventário e saldo sobrevivem a logout, reconexão e reinício planejado do servidor.
5. Repetição de requisições não duplica itens, moeda ou XP.
6. Dois jogadores concorrendo pelo mesmo primeiro feito geram um único evento histórico.
7. O ciclo ferro → lingote → venda altera corretamente itens, XP e saldo.
8. Chat possui limitação, bloqueio e denúncia.
9. O jogo mantém desempenho aceitável no aparelho-alvo e com 30 jogadores de teste.
10. Backup e restauração do banco são demonstrados em ambiente de teste.

## 21. Ordem recomendada de prototipação

1. Movimento local e mapa cinza, sem arte final.
2. Login e personagem persistente.
3. Replicação de dois jogadores.
4. Inventário transacional.
5. Combate autoritativo.
6. Mineração e reaparecimento de veios.
7. Metalurgia e economia.
8. Tutorial e interface completa.
9. História viva estruturada.
10. Telemetria, moderação, testes de carga e estabilidade.

Cada etapa deve possuir teste de ponta a ponta antes de ampliar o conteúdo.

## 22. Riscos principais

| Risco | Resposta no MVP |
|---|---|
| Escopo grande demais | lista explícita de itens adiados e apenas um ciclo produtivo |
| Trapaça e duplicação | servidor autoritativo, transações e idempotência |
| Latência no celular | previsão no movimento visual; resultado de ações no servidor |
| Economia inflacionária | poucas fontes, preços fixos e livro-razão auditável |
| Custo de infraestrutura | instâncias pequenas e teste fechado antes de escalar |
| Conteúdo social inadequado | filtro, bloqueio, denúncia, logs e ferramentas de moderação |
| IA imprevisível | nenhuma IA generativa necessária para validar o MVP |
| Desempenho Android | aparelho-alvo e orçamento gráfico definidos cedo |

## 23. Decisões abertas antes da implementação

As seguintes decisões precisam de validação do responsável pelo produto:

1. Nome oficial do jogo e nomenclatura final da moeda.
2. Direção artística e referência visual.
3. Câmera: terceira pessoa livre ou isométrica.
4. Combate: seleção automática proposta ou alvo manual.
5. Modelo de infraestrutura e região inicial dos servidores.
6. Aparelho Android mínimo e tamanho máximo do aplicativo.
7. Faixa etária, países do teste e requisitos legais aplicáveis.
8. Número e perfil dos participantes do teste fechado.

## 24. Definição de sucesso

O MVP terá cumprido sua função quando demonstrar, de forma estável e mensurável, que um jogador consegue entrar em um mundo compartilhado, escolher entre combate e produção, progredir sem classe fixa, realizar uma transação econômica persistente e perceber que ações reais passam a fazer parte da memória do servidor.

