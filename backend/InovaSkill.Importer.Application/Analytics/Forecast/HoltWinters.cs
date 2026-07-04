namespace InovaSkill.Importer.Application.Analytics.Forecast;

public sealed class HoltWinters
{
    private readonly List<decimal> _serie;
    private const double Alpha = 0.3;
    private const double Beta = 0.1;
    private const double Gamma = 0.1;
    private const int PeriodoSazonal = 12;

    public List<decimal> Suavizados { get; } = [];

    public HoltWinters(List<decimal> serie)
    {
        _serie = serie.Count > 0 ? serie : throw new ArgumentException("Serie precisa ter ao menos 1 valor");
    }

    public void Ajustar()
    {
        Suavizados.Clear();

        var n = _serie.Count;

        var nivel0 = (double)_serie[0];
        double tendencia0 = n > 1 ? (double)(_serie[^1] - _serie[0]) / n : 0;

        var sazonalInicial = new double[PeriodoSazonal];
        if (n >= PeriodoSazonal)
        {
            for (var i = 0; i < PeriodoSazonal; i++)
            {
                var soma = 0.0;
                var count = 0;
                for (var j = i; j < n; j += PeriodoSazonal)
                {
                    soma += (double)_serie[j];
                    count++;
                }
                sazonalInicial[i] = count > 0 ? soma / count : 1;
            }
        }
        else
        {
            for (var i = 0; i < PeriodoSazonal; i++)
                sazonalInicial[i] = 1;
        }

        var nivel = nivel0;
        var tendencia = tendencia0;
        var sazonal = (double[])sazonalInicial.Clone();

        for (var i = 0; i < n; i++)
        {
            var valor = (double)_serie[i];
            var idx = i % PeriodoSazonal;

            var nivelAnterior = nivel;
            var nivelEstimado = nivel + tendencia;

            if (i < PeriodoSazonal && sazonal[idx] == 0)
                sazonal[idx] = 1;

            var sazFator = sazonal[idx] > 0 ? sazonal[idx] : 1;

            nivel = Alpha * (valor / sazFator) + (1 - Alpha) * nivelEstimado;
            tendencia = Beta * (nivel - nivelAnterior) + (1 - Beta) * tendencia;
            sazonal[idx] = Gamma * (valor / nivel) + (1 - Gamma) * sazFator;

            Suavizados.Add((decimal)(nivelEstimado * sazFator));
        }
    }

    public decimal Prever(int passos)
    {
        if (Suavizados.Count == 0)
            Ajustar();

        var n = Suavizados.Count;
        var ultimoNivel = (double)_serie[^1];

        double tendenciaF = 0;
        if (n >= 2)
        {
            var diff = (double)(_serie[^1] - _serie[^2]);
            tendenciaF = diff;
        }
        else
        {
            var ultimos = Suavizados.TakeLast(Math.Min(3, Suavizados.Count)).ToList();
            if (ultimos.Count >= 2)
                tendenciaF = (double)(ultimos[^1] - ultimos[^2]);
        }

        var previsao = 0.0;
        for (var i = 0; i < passos; i++)
        {
            var idx = (n + i) % PeriodoSazonal;
            var sazFator = 1.0;

            var nivelAtual = ultimoNivel + tendenciaF * (i + 1);
            previsao = nivelAtual * sazFator;
        }

        return (decimal)previsao;
    }
}
