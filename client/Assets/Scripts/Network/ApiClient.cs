using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Espectro.Network
{
    public sealed class ApiException : Exception
    {
        public long StatusCode { get; }
        public string ErrorCode { get; }

        public ApiException(long statusCode, string errorCode, string message) : base(message)
        {
            StatusCode = statusCode;
            ErrorCode = errorCode;
        }
    }

    // Cliente HTTP para as rotas REST do servidor do Corte 1 (server/README.md).
    public static class ApiClient
    {
        public static Task<SessionResponse> RegisterAsync(string email, string password) =>
            SendAsync<SessionResponse>("POST", "/auth/register", new CredentialsRequest { email = email, password = password }, null);

        public static Task<SessionResponse> LoginAsync(string email, string password) =>
            SendAsync<SessionResponse>("POST", "/auth/login", new CredentialsRequest { email = email, password = password }, null);

        public static Task<SessionResponse> RefreshAsync(string refreshToken) =>
            SendAsync<SessionResponse>("POST", "/auth/refresh", new RefreshRequest { refreshToken = refreshToken }, null);

        // docs/REFERENCIAS-INTERFACE-JOGABILIDADE.md, plano de evolução §"Lembrar sessão e saída":
        // sem isto, "SAIR DO ONLINE" só limpava o estado local — o refresh token continuava válido
        // no servidor até expirar sozinho. Resposta é 204 sem corpo, daí o T generico sem uso real.
        public static Task LogoutAsync(string refreshToken) =>
            SendAsync<object>("POST", "/auth/logout", new RefreshRequest { refreshToken = refreshToken }, null);

        // docs/REFERENCIAS-INTERFACE-JOGABILIDADE.md, plano de evolução §2 "Esqueci a senha":
        // servidor pronto, sem tela ainda consumindo — ver nota no documento. `RequestPasswordReset`
        // sempre "sucede" (resposta neutra); o chamador não deve tratar isso como confirmação de
        // que o e-mail existe.
        public static Task<PasswordResetRequestResponse> RequestPasswordResetAsync(string email) =>
            SendAsync<PasswordResetRequestResponse>("POST", "/auth/password-reset/request", new PasswordResetRequestPayload { email = email }, null);

        public static Task ConfirmPasswordResetAsync(string token, string newPassword) =>
            SendAsync<object>("POST", "/auth/password-reset/confirm", new PasswordResetConfirmPayload { token = token, password = newPassword }, null);

        public static Task<CharacterResponse> CreateCharacterAsync(string accessToken, string name, string race, string gender) =>
            SendAsync<CharacterResponse>("POST", "/characters", new CreateCharacterRequest { name = name, race = race, gender = gender }, accessToken);

        public static Task<CharacterResponse> GetMyCharacterAsync(string accessToken) =>
            SendAsync<CharacterResponse>("GET", "/characters/me", null, accessToken);

        private static async Task<T> SendAsync<T>(string method, string path, object body, string accessToken)
        {
            var url = NetworkSettings.HttpBaseUrl + path;
            using var request = new UnityWebRequest(url, method);
            if (body != null)
            {
                var json = JsonUtility.ToJson(body);
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                request.SetRequestHeader("Content-Type", "application/json");
            }

            request.downloadHandler = new DownloadHandlerBuffer();
            if (!string.IsNullOrEmpty(accessToken))
            {
                request.SetRequestHeader("Authorization", "Bearer " + accessToken);
            }

            await SendWebRequestAsync(request);

            var responseText = request.downloadHandler.text;
            if (request.result != UnityWebRequest.Result.Success)
            {
                var errorCode = "NETWORK_ERROR";
                var message = string.IsNullOrEmpty(request.error) ? "Falha de rede." : request.error;
                if (!string.IsNullOrEmpty(responseText))
                {
                    try
                    {
                        var parsed = JsonUtility.FromJson<ApiErrorResponse>(responseText);
                        if (parsed != null && !string.IsNullOrEmpty(parsed.error))
                        {
                            errorCode = parsed.error;
                            message = string.IsNullOrEmpty(parsed.message) ? DefaultMessageFor(errorCode) : parsed.message;
                        }
                    }
                    catch (ArgumentException)
                    {
                        // Corpo de resposta não era o JSON de erro esperado; mantém a mensagem de rede.
                    }
                }

                throw new ApiException(request.responseCode, errorCode, message);
            }

            return string.IsNullOrEmpty(responseText) ? default : JsonUtility.FromJson<T>(responseText);
        }

        // Nunca deixa uma mensagem crua de HTTP chegar à interface, mesmo se o servidor
        // esquecer de mandar "message" (ex.: um 400 de validação sem mensagem amigável).
        private static string DefaultMessageFor(string errorCode) => errorCode switch
        {
            "VALIDATION_ERROR" => "Dados inválidos. Verifique o e-mail e a senha (mínimo 8 caracteres).",
            "EMAIL_TAKEN" => "Este e-mail já está cadastrado.",
            "INVALID_CREDENTIALS" => "E-mail ou senha inválidos.",
            "INVALID_REFRESH_TOKEN" => "Sessão expirada. Entre novamente.",
            "UNAUTHORIZED" => "Sessão inválida. Entre novamente.",
            "CHARACTER_EXISTS" => "Esta conta já possui um personagem.",
            "NAME_TAKEN" => "Este nome já está em uso.",
            "CHARACTER_NOT_FOUND" => "Nenhum personagem encontrado para esta conta.",
            _ => "Não foi possível completar a operação.",
        };

        private static Task SendWebRequestAsync(UnityWebRequest request)
        {
            var tcs = new TaskCompletionSource<bool>();
            request.SendWebRequest().completed += _ => tcs.SetResult(true);
            return tcs.Task;
        }
    }
}
