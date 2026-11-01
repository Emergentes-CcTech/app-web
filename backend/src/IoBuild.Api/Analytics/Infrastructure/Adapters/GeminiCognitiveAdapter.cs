using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using IoBuild.Api.Analytics.Domain.Services;
using Microsoft.Extensions.Caching.Memory;

namespace IoBuild.Api.Analytics.Infrastructure.Adapters;

/// <summary>
/// Explicación del consumo con Gemini. La detección sigue siendo determinística: el modelo solo redacta, a partir de
/// los hechos que calcula el adaptador de reglas, una respuesta clara a la pregunta del usuario.
/// Si falta la clave, se agota la cuota o la respuesta no es válida, devuelve la explicación de reglas.
/// </summary>
public sealed class GeminiCognitiveAdapter(
    HttpClient http,
    IConfiguration configuration,
    MockAiCognitiveAdapter facts,
    IMemoryCache cache,
    ILogger<GeminiCognitiveAdapter> logger) : IAiCognitiveService
{
    public const string DefaultModel = "gemini-3.5-flash-lite";
    private const string DefaultBaseUrl = "https://generativelanguage.googleapis.com";
    // Las respuestas se reutilizan para no gastar la cuota gratuita con preguntas repetidas
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(6);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<AiExplanationDto> ExplainConsumptionVariationAsync(int projectId, int? unitId, string billingPeriod, string question, string language = "es")
    {
        var periodFacts = await facts.BuildFactsAsync(projectId, unitId, billingPeriod);
        var baseline = MockAiCognitiveAdapter.Render(periodFacts);
        var apiKey = configuration["Gemini:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey)) return baseline;

        var model = string.IsNullOrWhiteSpace(configuration["Gemini:Model"]) ? DefaultModel : configuration["Gemini:Model"]!;
        var factsJson = JsonSerializer.Serialize(periodFacts, Json);
        // Si cambian los datos del periodo (nuevas lecturas o alertas), la respuesta en caché deja de servir
        var cacheKey = $"gemini:{model}:{projectId}:{unitId}:{periodFacts.Period}:{language}:{Hash(question)}:{Hash(factsJson)}";
        if (cache.TryGetValue(cacheKey, out AiExplanationDto? cached) && cached is not null) return cached;

        try
        {
            var generated = await GenerateAsync(apiKey, model, baseline, factsJson, question, language);
            if (generated is null) return baseline;
            cache.Set(cacheKey, generated, CacheDuration);
            return generated;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
        {
            logger.LogWarning("Gemini no respondió ({Reason}); se usa la explicación de reglas.", ex.GetType().Name);
            return baseline;
        }
    }

    private async Task<AiExplanationDto?> GenerateAsync(string apiKey, string model, AiExplanationDto baseline, string factsJson, string question, string language)
    {
        var baseUrl = configuration["Gemini:BaseUrl"] ?? DefaultBaseUrl;
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/v1beta/models/{Uri.EscapeDataString(model)}:generateContent");
        // La clave va en cabecera, nunca en la URL (las URLs quedan en logs)
        request.Headers.Add("x-goog-api-key", apiKey);
        request.Content = JsonContent.Create(BuildRequest(factsJson, baseline, question, language));

        using var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Gemini devolvió {Status}; se usa la explicación de reglas.", (int)response.StatusCode);
            return null;
        }

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var text = document.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString();
        if (string.IsNullOrWhiteSpace(text)) return null;

        var answer = JsonSerializer.Deserialize<GeminiAnswer>(text, Json);
        if (answer is null || string.IsNullOrWhiteSpace(answer.Summary) || string.IsNullOrWhiteSpace(answer.WaterAnalysis)
            || string.IsNullOrWhiteSpace(answer.EnergyAnalysis) || string.IsNullOrWhiteSpace(answer.AliquotImpact))
            return null;

        // Si hay anomalías lo decide el motor de reglas, no el modelo
        return new AiExplanationDto(
            answer.Summary.Trim(),
            answer.WaterAnalysis.Trim(),
            answer.EnergyAnalysis.Trim(),
            answer.AliquotImpact.Trim(),
            (answer.Recommendations ?? []).Where(r => !string.IsNullOrWhiteSpace(r)).Select(r => r.Trim()).Take(5).ToList(),
            baseline.HasAnomalies,
            Source: "gemini",
            Period: baseline.Period);
    }

    private static object BuildRequest(string factsJson, AiExplanationDto baseline, string question, string language)
    {
        var lang = language == "en" ? "English" : "español de Perú";
        var system =
            "Eres el asistente de IoBuild, una plataforma que mide el consumo de agua y luz de las áreas comunes de condominios en Lima " +
            "y prorratea el gasto entre los departamentos según su alícuota (Ley 27157). " +
            $"Responde en {lang}, con frases cortas y claras para un propietario o un administrador. " +
            "Usa solo los HECHOS que se te entregan: no inventes cifras, fechas, causas ni equipos. " +
            "Si un dato no está en los hechos, dilo. No des consejos legales ni financieros. " +
            "Responde primero a la pregunta en el resumen. Compara con el periodo anterior cuando haya datos; si el periodo está en curso, aclara que es parcial. " +
            "Los montos están en soles (S/), el agua en m³ y la energía en kWh. " +
            "Significado de los datos: 'consumption' es lo que midieron los sensores de IoBuild con las lecturas disponibles " +
            "('daysWithData' indica cuántos días tienen lecturas; si son pocos, la medición es parcial y no debe compararse con el recibo); " +
            "'bill' es el recibo matriz de Sedapal y Luz del Sur registrado por la administración y su conciliación; " +
            "'statement' es la cuota del departamento con su desglose; los campos 'previous...' son del mes anterior. " +
            "No mezcles cifras de 'consumption' con las de 'bill'. " +
            "Si hay 'unitNumber', quien pregunta es un residente: las recomendaciones deben ser acciones a su alcance " +
            "(revisar su estado de cuenta, consultar a la administración, reportar fugas) y no tareas técnicas de mantenimiento. " +
            "Las recomendaciones deben ser acciones concretas de mantenimiento o revisión, como máximo cinco.";
        var user = $"PREGUNTA: {question}\n\nHECHOS DEL PERIODO (JSON calculado por IoBuild):\n{factsJson}\n\nEXPLICACIÓN DE REFERENCIA DE LAS REGLAS:\n{baseline.Summary}";

        var text = new { type = "STRING" };
        return new
        {
            systemInstruction = new { parts = new[] { new { text = system } } },
            contents = new[] { new { role = "user", parts = new[] { new { text = user } } } },
            generationConfig = new
            {
                temperature = 0.2,
                maxOutputTokens = 900,
                responseMimeType = "application/json",
                responseSchema = new
                {
                    type = "OBJECT",
                    properties = new
                    {
                        summary = text,
                        waterAnalysis = text,
                        energyAnalysis = text,
                        aliquotImpact = text,
                        recommendations = new { type = "ARRAY", items = text }
                    },
                    required = new[] { "summary", "waterAnalysis", "energyAnalysis", "aliquotImpact", "recommendations" }
                }
            }
        };
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value.Trim().ToLowerInvariant())))[..16];

    private sealed record GeminiAnswer(string? Summary, string? WaterAnalysis, string? EnergyAnalysis, string? AliquotImpact, List<string>? Recommendations);
}
