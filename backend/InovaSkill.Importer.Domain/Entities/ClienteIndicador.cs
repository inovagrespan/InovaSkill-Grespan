namespace InovaSkill.Importer.Domain.Entities;

public sealed class ClienteIndicador
{
    public long Id { get; set; }
    public string ClienteId { get; set; } = string.Empty;
    public decimal Faturamento3M { get; set; }
    public decimal Faturamento6M { get; set; }
    public decimal Faturamento12M { get; set; }
    public decimal? Crescimento3M { get; set; }
    public decimal? Crescimento6M { get; set; }
    public decimal? Crescimento12M { get; set; }
    public decimal MediaMovel3M { get; set; }
    public decimal MediaMovel6M { get; set; }
    public decimal MediaMovel12M { get; set; }
    public decimal FrequenciaCompra { get; set; }
    public decimal TicketMedioGeral { get; set; }
    public int ScoreCrescimento { get; set; }
    public int ScoreFrequencia { get; set; }
    public int ScoreTicket { get; set; }
    public int ScoreRecencia { get; set; }
    public int ScorePotencial { get; set; }
    public string Tendencia { get; set; } = string.Empty;
    public string Classificacao { get; set; } = string.Empty;
    public DateTime AtualizadoEm { get; set; }
}
