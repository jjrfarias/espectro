namespace Espectro.Network
{
    // O cliente publicado aponta para o Railway. Para trabalhar localmente, troque
    // UseProduction para false (ou altere Host/Port) antes de executar o Editor.
    // Em um Android físico, use `adb reverse tcp:3000 tcp:3000` para que "127.0.0.1" no aparelho
    // aponte para o servidor rodando na máquina de desenvolvimento; no emulador Android, troque
    // por "10.0.2.2".
    public static class NetworkSettings
    {
        public const bool UseProduction = true;
        public const string ProductionHttpBaseUrl = "https://server-production-266d.up.railway.app";
        public const string ProductionWebSocketBaseUrl = "wss://server-production-266d.up.railway.app";
        public const string Host = "127.0.0.1";
        public const int Port = 3000;

        public static string HttpBaseUrl => UseProduction ? ProductionHttpBaseUrl : $"http://{Host}:{Port}";
        public static string WebSocketBaseUrl => UseProduction ? ProductionWebSocketBaseUrl : $"ws://{Host}:{Port}";
    }
}
