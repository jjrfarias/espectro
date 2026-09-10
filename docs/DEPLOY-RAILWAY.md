# Cliente e Railway

O servidor publicado no Railway é a origem de produção do pré-jogo online. `NetworkSettings.cs` usa HTTPS para as rotas de conta e WSS para o mundo WebSocket, evitando conteúdo misto quando o cliente WebGL é servido por HTTPS.

Para desenvolvimento local, altere `UseProduction` para `false`; nesse modo o cliente volta a usar `http://127.0.0.1:3000` e `ws://127.0.0.1:3000`. Nenhuma credencial de produção é armazenada no cliente ou neste documento.

O repositório GitHub está conectado ao serviço do Railway, portanto um push na branch publicada dispara o deploy automático. Depois de cada mudança de servidor, verificar o health check, login, cadastro e conexão `/world` no endereço público.
