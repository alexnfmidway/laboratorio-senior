namespace BlazorFrontend.Models;

public class OrderResult
{
    public bool Sucesso { get; set; }
    public string? OrderId { get; set; }
    public string? Message { get; set; }
    public int StatusCode { get; set; }
    public long ElapsedMs { get; set; }
    public string? Erro { get; set; }
}

public class ServiceHealth
{
    public string Nome { get; set; } = "";
    public string Url { get; set; } = "";
    public bool Online { get; set; }
    public long LatenciaMs { get; set; }
    public string Status { get; set; } = "Verificando...";
    public string Icone { get; set; } = "🔄";
}

public class CircuitBreakerState
{
    public string Estado { get; set; } = "Fechado"; // Fechado, Aberto, MeioAberto
    public int FalhasConsecutivas { get; set; }
    public int TotalBloqueadas { get; set; }
    public int TotalSucesso { get; set; }
    public DateTime? AberturaEm { get; set; }
    public int MaxFalhas { get; set; } = 3;
    public int TempoEsperaSegundos { get; set; } = 30;

    public string Emoji => Estado switch
    {
        "Aberto" => "🔴",
        "MeioAberto" => "🟡",
        _ => "🟢"
    };

    public string Cor => Estado switch
    {
        "Aberto" => "#dc2626",
        "MeioAberto" => "#d97706",
        _ => "#16a34a"
    };
}

public class ModuloProgresso
{
    public string Id { get; set; } = "";
    public string Nome { get; set; } = "";
    public bool Concluido { get; set; }
}

