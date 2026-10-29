namespace IoBuild.Api.Analytics.Domain.Services;

/// <summary>
/// DTO que encapsula la explicación cognitiva del consumo mensual y su impacto en alícuota.
/// </summary>
public sealed record AiExplanationDto(
    string Summary,
    string WaterAnalysis,
    string EnergyAnalysis,
    string AliquotImpact,
    List<string> Recommendations,
    bool HasAnomalies);

/// <summary>
/// Contrato del dominio para servicios cognitivos de análisis de consumo.
/// Desacopla la lógica de negocio de los proveedores de LLM o adaptadores de prueba.
/// </summary>
public interface IAiCognitiveService
{
    // Construye un informe explicativo del comportamiento de consumo mensual y su distribución
    Task<AiExplanationDto> ExplainConsumptionVariationAsync(int projectId, int? unitId, string billingPeriod, string question);
}
