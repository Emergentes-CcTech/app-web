using System.Net;
using System.Text;
using System.Text.Json;
using IoBuild.Api.Analytics.Infrastructure.Adapters;
using IoBuild.Api.Persistence;
using IoBuild.Api.Publishing.Domain.Model.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace IoBuild.Modules.Tests.Analytics;

/// <summary>
/// El adaptador de Gemini solo redacta: sin clave, con error o con respuesta inválida devuelve la explicación de reglas.
/// </summary>
public sealed class GeminiCognitiveAdapterTests
{
    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }

    private static async Task<IoBuildDbContext> SeededDbAsync()
    {
        var db = new IoBuildDbContext(new DbContextOptionsBuilder<IoBuildDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Projects.Add(new Project { Id = 10, Name = "Residencial Los Robles" });
        await db.SaveChangesAsync();
        return db;
    }

    private static GeminiCognitiveAdapter Adapter(IoBuildDbContext db, StubHandler handler, string? apiKey) =>
        new(new HttpClient(handler),
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Gemini:ApiKey"] = apiKey }).Build(),
            new MockAiCognitiveAdapter(db),
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<GeminiCognitiveAdapter>.Instance);

    private static string GeminiReply(object answer) => JsonSerializer.Serialize(new
    {
        candidates = new[] { new { content = new { parts = new[] { new { text = JsonSerializer.Serialize(answer) } } } } }
    });

    [Fact]
    public async Task Without_api_key_the_rules_explanation_is_returned_and_gemini_is_not_called()
    {
        await using var db = await SeededDbAsync();
        var handler = new StubHandler(HttpStatusCode.OK, "{}");

        var result = await Adapter(db, handler, apiKey: null).ExplainConsumptionVariationAsync(10, null, "2026-10", "¿Por qué varió?");

        Assert.Equal("rules", result.Source);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task With_a_valid_reply_gemini_wording_is_used_and_repeated_questions_hit_the_cache()
    {
        await using var db = await SeededDbAsync();
        var handler = new StubHandler(HttpStatusCode.OK, GeminiReply(new
        {
            summary = "Este mes el consumo común se mantuvo estable.",
            waterAnalysis = "No hubo flujo nocturno anómalo.",
            energyAnalysis = "Las bombas trabajaron dentro de lo normal.",
            aliquotImpact = "Tu parte se calcula con tu alícuota.",
            recommendations = new[] { "Revisar la boya de la cisterna cada mes." }
        }));
        var adapter = Adapter(db, handler, apiKey: "test-key");

        var first = await adapter.ExplainConsumptionVariationAsync(10, null, "2026-10", "¿Por qué varió?");
        var second = await adapter.ExplainConsumptionVariationAsync(10, null, "2026-10", "¿POR QUÉ VARIÓ? ");

        Assert.Equal("gemini", first.Source);
        Assert.Equal("Este mes el consumo común se mantuvo estable.", first.Summary);
        Assert.Single(first.Recommendations);
        Assert.False(first.HasAnomalies); // lo decide el motor de reglas, no el modelo
        Assert.Equal(first, second);
        Assert.Equal(1, handler.Calls);
        // La clave viaja en cabecera, no en la URL
        Assert.True(handler.LastRequest!.Headers.Contains("x-goog-api-key"));
        Assert.DoesNotContain("test-key", handler.LastRequest.RequestUri!.ToString());
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, "{\"error\":{\"code\":429}}")]
    [InlineData(HttpStatusCode.OK, "{\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"{\\\"summary\\\":\\\"\\\"}\"}]}}]}")]
    [InlineData(HttpStatusCode.OK, "no es json")]
    public async Task When_gemini_fails_or_answers_incompletely_the_rules_explanation_is_returned(HttpStatusCode status, string body)
    {
        await using var db = await SeededDbAsync();

        var result = await Adapter(db, new StubHandler(status, body), apiKey: "test-key")
            .ExplainConsumptionVariationAsync(10, null, "2026-10", "¿Por qué varió?");

        Assert.Equal("rules", result.Source);
        Assert.Contains("Residencial Los Robles", result.Summary);
    }
}
