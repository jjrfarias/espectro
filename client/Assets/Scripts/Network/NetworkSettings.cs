namespace Espectro.Network
{
    // Aponta para o servidor do Corte 1 (server/README.md). No Editor, "127.0.0.1" já funciona.
    // Em um Android físico, use `adb reverse tcp:3000 tcp:3000` para que "127.0.0.1" no aparelho
    // aponte para o servidor rodando na máquina de desenvolvimento; no emulador Android, troque
    // por "10.0.2.2".
    public static class NetworkSettings
    {
        public const string Host = "127.0.0.1";
        public const int Port = 3000;

        public static string HttpBaseUrl => $"http://{Host}:{Port}";
        public static string WebSocketBaseUrl => $"ws://{Host}:{Port}";
    }
}
