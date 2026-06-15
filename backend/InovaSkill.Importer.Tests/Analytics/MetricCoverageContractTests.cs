using System.Reflection;

namespace InovaSkill.Importer.Tests.Analytics;

public sealed class MetricCoverageContractTests
{
    [Fact]
    public void CriticalMetricCalculatorsAndProcessors_MustHaveDedicatedTestFiles()
    {
        var testsRoot = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
            ?? throw new InvalidOperationException("Nao foi possivel localizar o assembly de testes.");

        var projectRoot = Path.GetFullPath(Path.Combine(testsRoot, "..", "..", "..", "..", ".."));

        var expectedTestFiles = new[]
        {
            "backend/InovaSkill.Importer.Tests/Analytics/CustomerCalculatorsTests.cs",
            "backend/InovaSkill.Importer.Tests/Analytics/CustomerCommercialHealthAnalyzerTests.cs",
            "backend/InovaSkill.Importer.Tests/Analytics/CustomerInsightsCalculatorTests.cs",
            "backend/InovaSkill.Importer.Tests/Analytics/MovingAverageCalculatorTests.cs",
            "backend/InovaSkill.Importer.Tests/Analytics/PeriodComparisonCalculatorTests.cs",
            "backend/InovaSkill.Importer.Tests/Analytics/RiskClassifierTests.cs",
            "backend/InovaSkill.Importer.Tests/Processing/CustomerSummaryProcessorTests.cs",
            "backend/InovaSkill.Importer.Tests/Processing/SalesCompanySummaryCalculatorTests.cs",
            "backend/InovaSkill.Importer.Tests/Processing/SalesSummaryProcessorTests.cs"
        };

        foreach (var relativePath in expectedTestFiles)
        {
            var fullPath = Path.Combine(projectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(fullPath), $"Arquivo de teste obrigatório ausente para métrica crítica: {relativePath}");
        }
    }
}
