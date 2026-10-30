using Blazored.LocalStorage;
using BlazorFrontend.Models;

namespace BlazorFrontend.Services;

public class ProgressService(ILocalStorageService localStorage)
{
    private const string StorageKey = "modulos_progresso";

    public static readonly List<ModuloProgresso> TodosModulos =
    [
        new() { Id = "intro",               Nome = "Visão Geral da Arquitetura" },
        new() { Id = "tracing",             Nome = "Distributed Tracing & Jaeger" },
        new() { Id = "comunicacao",         Nome = "Síncrono vs Assíncrono" },
        new() { Id = "circuit-breaker",     Nome = "Circuit Breaker com Polly" },
        new() { Id = "gateway",             Nome = "API Gateway & Service Discovery" },
        new() { Id = "saga",               Nome = "Saga Pattern" },
        new() { Id = "outbox",             Nome = "Outbox Pattern" },
        new() { Id = "simulador",          Nome = "Simulador ao Vivo" },
    ];

    public async Task<List<ModuloProgresso>> GetProgressoAsync()
    {
        try
        {
            var saved = await localStorage.GetItemAsync<List<string>>(StorageKey) ?? [];
            return TodosModulos.Select(m => new ModuloProgresso
            {
                Id = m.Id,
                Nome = m.Nome,
                Concluido = saved.Contains(m.Id)
            }).ToList();
        }
        catch
        {
            return TodosModulos.ToList();
        }
    }

    public async Task MarcarConcluidoAsync(string moduloId)
    {
        try
        {
            var saved = await localStorage.GetItemAsync<List<string>>(StorageKey) ?? [];
            if (!saved.Contains(moduloId))
            {
                saved.Add(moduloId);
                await localStorage.SetItemAsync(StorageKey, saved);
            }
        }
        catch { }
    }

    public async Task<int> GetPercentualAsync()
    {
        var lista = await GetProgressoAsync();
        if (lista.Count == 0) return 0;
        return (int)((double)lista.Count(m => m.Concluido) / lista.Count * 100);
    }

    public async Task ResetarAsync()
    {
        await localStorage.RemoveItemAsync(StorageKey);
    }
}

