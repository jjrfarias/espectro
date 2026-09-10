# Pré-jogo

A entrada online agora apresenta o universo antes de pedir credenciais. A tela de autenticação usa a mesma `NetworkUI` consumida por `NetworkSession`, portanto login, cadastro e criação de personagem continuam ligados às rotas reais do servidor.

O painel apresenta a chamada “O Véu está se abrindo”, o título **Espectro — O Berço**, uma sinopse curta sobre a Mata dos Sussurros e duas ações claras: **Entrar no Berço** e **Criar conta**. Depois da autenticação, a criação de personagem usa a mesma linguagem e conduz o jogador para **Entrar no mundo**.

A composição foi feita inteiramente em runtime, com tipografia legível, contraste azul-petróleo/âmbar e escala baseada em 1920×1080 para manter a leitura em WebGL e telas menores. A lógica de sessão permanece em `NetworkSession.cs`; a camada visual fica isolada em `NetworkUI.cs`.
