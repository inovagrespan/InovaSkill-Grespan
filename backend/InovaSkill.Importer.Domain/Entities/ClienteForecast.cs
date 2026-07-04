namespace InovaSkill.Importer.Domain.Entities;

public sealed class ClienteForecast
{
    public long Id { get; set; }
    public string ClienteId { get; set; } = string.Empty;
    public decimal Previsao30Dias { get; set; }
    public decimal Previsao60Dias { get; set; }
    public decimal Previsao90Dias { get; set; }
    public string TendenciaPrevista { get; set; } = "Estavel";
    public decimal? ErroMedioHistorico { get; set; }
    public decimal? ConfiancaModelo { get; set; }
    public DateTime UltimaObservacao { get; set; }
    public DateTime AtualizadoEm { get; set; } = DateTime.UtcNow;
}
