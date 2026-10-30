using BlazorFrontend.Models;
using System.Diagnostics;
using System.Text.Json;

namespace BlazorFrontend.Services;

public class GatewayApiService(HttpClient httpClient, ILogger<GatewayApiService> logger)
{
    private readonly JsonSerializerOptions _jsonOpts = new() { PropertyNameCaseInsensitive = true };

    public async Task<OrderResult> PostOrderAsync()
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var response = await httpClient.PostAsync("/order", null);
            sw.Stop();

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var data = JsonSerializer.Deserialize<Dictionary<string, object>>(json, _jsonOpts);

                return new OrderResult
                {
                    Sucesso = true,
                    OrderId = data?.GetValueOrDefault("orderId")?.ToString(),
                    Message = data?.GetValueOrDefault("message")?.ToString(),
                    StatusCode = (int)response.StatusCode,
                    ElapsedMs = sw.ElapsedMilliseconds
                };
            }

            return new OrderResult
            {
                Sucesso = false,
                StatusCode = (int)response.StatusCode,
                ElapsedMs = sw.ElapsedMilliseconds,
                Erro = $"HTTP {(int)response.StatusCode} - {response.ReasonPhrase}"
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.LogError(ex, "Erro ao chamar POST /order");
            return new OrderResult
            {
                Sucesso = false,
                StatusCode = 0,
                ElapsedMs = sw.ElapsedMilliseconds,
                Erro = ex.Message.Contains("circuit") || ex.Message.Contains("Circuit")
                    ? "⚡ Circuit Breaker ABERTO! Chamada bloqueada instantaneamente."
                    : $"Erro de conexão: {ex.Message}"
            };
        }
    }

    public async Task<ServiceHealth> CheckHealthAsync(string nome, string path, string icone)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var response = await httpClient.GetAsync(path);
            sw.Stop();
            return new ServiceHealth
            {
                Nome = nome,
                Url = path,
                Online = response.IsSuccessStatusCode,
                LatenciaMs = sw.ElapsedMilliseconds,
                Status = response.IsSuccessStatusCode ? "Saudável" : $"Erro {(int)response.StatusCode}",
                Icone = icone
            };
        }
        catch
        {
            sw.Stop();
            return new ServiceHealth
            {
                Nome = nome,
                Url = path,
                Online = false,
                LatenciaMs = sw.ElapsedMilliseconds,
                Status = "Indisponível",
                Icone = icone
            };
        }
    }
}

