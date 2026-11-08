namespace IoBuild.Api.Publishing.Domain.Model.Aggregates;

/// <summary>
/// Agregado Project (Condominio) en el contexto de Catastro (Cadastre).
/// Representa un edificio o condominio residencial cuyas áreas comunes son monitoreadas
/// y cuyos gastos comunes se prorratean entre los departamentos.
/// </summary>
public sealed class Project
{
    // Identificador único del condominio
    public int Id { get; set; }

    // Nombre comercial del condominio (ej. "Residencial Los Robles")
    public string Name { get; set; } = string.Empty;

    // Reseña o descripción del condominio
    public string Description { get; set; } = string.Empty;

    // Dirección fiscal y física del inmueble
    public string Address { get; set; } = string.Empty;

    // Puente de compatibilidad con columna física Location en la base de datos
    public string Location
    {
        get => string.IsNullOrWhiteSpace(Address) ? _location : Address;
        set { _location = value; if (string.IsNullOrWhiteSpace(Address)) Address = value; }
    }
    private string _location = string.Empty;

    // Distrito de ubicación (ej. "Jesús María", "Miraflores", "San Isidro")
    public string District { get; set; } = "Jesús María";

    // Cantidad total de pisos del edificio
    public int TotalFloors { get; set; } = 1;

    // Cantidad total de departamentos o unidades del condominio
    public int TotalUnits { get; set; }

    // Código de suministro matriz de agua emitido por Sedapal
    public string SedapalSupplyCode { get; set; } = string.Empty;

    // Código de suministro matriz de energía eléctrica emitido por Luz del Sur / Enel
    public string LuzDelSurSupplyCode { get; set; } = string.Empty;

    // Capacidad volumétrica nominal de la cisterna principal en metros cúbicos (m3)
    public decimal CisternCapacityM3 { get; set; } = 45.0m;

    // Estado administrativo del condominio (ej. "Activo", "En Auditoría")
    public string Status { get; set; } = "Activo";

    // Identificador del usuario administrador del condominio (PropertyManager)
    public int AdministratorId { get; set; }

    // Puente de compatibilidad con la columna física BuilderId en la base de datos
    public int BuilderId
    {
        get => AdministratorId;
        set => AdministratorId = value;
    }

    // URL de la fotografía o fachada del condominio
    public string? ImageUrl { get; set; }

    // Indicador de si el padrón y estructura de alícuotas del condominio ya fueron definidos
    public bool StructureDefined { get; set; }

    // Fecha de registro del condominio en la plataforma
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
