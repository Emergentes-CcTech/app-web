namespace IoBuild.Api.Publishing.Domain.Model.Aggregates;

/// <summary>
/// Publishing (CoreBusiness) aggregate: Project.
/// Mapped to CoreBusiness per course reuse (Publishing BC from learning-center).
/// Physical location: Publishing/Domain/Model/Aggregates/Project.cs
/// Namespace kept as IoBuild.Api.Persistence for test compatibility.
/// Configuration: Publishing/Infrastructure/Persistence/EFC/Configuration/ProjectConfiguration.cs
/// </summary>
public sealed class Project
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public int TotalUnits { get; set; }
    public int BuilderId { get; set; }
    public string? ImageUrl { get; set; }
    public bool StructureDefined { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
