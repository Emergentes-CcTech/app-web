using IoBuild.Api.Publishing.Domain.Model.ValueObjects;

namespace IoBuild.Api.Publishing.Domain.Model.Aggregates;

/// <summary>
/// Entidad de Área Común dentro del contexto de Catastro (Cadastre).
/// Representa una infraestructura compartida del condominio sujeta a telemetría IoT y prorrateo.
/// </summary>
public sealed class CommonArea
{
    // Identificador único del área común
    public int Id { get; set; }

    // Identificador del condominio al que pertenece el área común
    public int ProjectId { get; set; }

    // Nombre descriptivo (ej. "Cisterna Principal Sótano 2", "Tablero de Fuerza Bombas")
    public string Name { get; set; } = string.Empty;

    // Subsistema técnico al que corresponde
    public CommonAreaSubsystem Subsystem { get; set; }

    // Ubicación física dentro del condominio (ej. "Sótano 1", "Azotea", "Hall Principal")
    public string Location { get; set; } = string.Empty;

    // Descripción o notas operativas del área común
    public string Description { get; set; } = string.Empty;

    public CommonArea() { }

    public CommonArea(int projectId, string name, CommonAreaSubsystem subsystem, string location, string description = "")
    {
        ProjectId = projectId;
        Name = name;
        Subsystem = subsystem;
        Location = location;
        Description = description;
    }
}
