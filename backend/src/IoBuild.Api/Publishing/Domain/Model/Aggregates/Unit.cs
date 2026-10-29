using System.ComponentModel.DataAnnotations.Schema;

namespace IoBuild.Api.Publishing.Domain.Model.Aggregates;

/// <summary>
/// Agregado Unit (Departamento / Unidad Privativa) en el contexto de Catastro (Cadastre).
/// Modela cada departamento con su porcentaje legal de participación (Alícuota %) bajo la Ley N.° 27157.
/// </summary>
public sealed class Unit
{
    // Identificador único de la unidad privativa
    public int Id { get; set; }

    // Identificador del condominio al que pertenece el departamento
    public int ProjectId { get; set; }

    // Número de piso en el que se ubica el departamento
    public int FloorNumber { get; set; }

    // Propiedad puente para compatibilidad con el esquema existente de base de datos
    public int Floor
    {
        get => FloorNumber;
        set => FloorNumber = value;
    }

    // Número identificador del departamento (ej. "101", "402")
    public string UnitNumber { get; set; } = string.Empty;

    // Propiedad puente para compatibilidad con columna RoomNumber
    public string RoomNumber
    {
        get => string.IsNullOrEmpty(_roomNumber) ? UnitNumber : _roomNumber;
        set { _roomNumber = value; if (string.IsNullOrEmpty(UnitNumber)) UnitNumber = value; }
    }
    private string _roomNumber = string.Empty;

    // Área techada ocupada por el departamento en metros cuadrados
    public decimal AreaM2 { get; set; } = 75.0m;

    // Porcentaje de participación legal en áreas comunes (Ley N.° 27157). Debe sumar 100.0000% en el edificio.
    public decimal AliquotPercentage { get; set; } = 0.0000m;

    // Correo electrónico del residente registrado (utilizado para la auto-vinculación en IAM)
    public string? ResidentEmail { get; set; }

    // Propiedad puente para compatibilidad con la columna OwnerEmail
    public string? OwnerEmail
    {
        get => ResidentEmail;
        set => ResidentEmail = value;
    }

    // Identificador del usuario residente en IAM una vez vinculado
    public int? OwnerId { get; set; }

    // Nombre completo del residente o propietario del departamento
    public string? ResidentFullName { get; set; }

    // Estado administrativo de cuenta de cobranza del departamento (ej. "Al día", "Moroso")
    public string AccountStatus { get; set; } = "Al día";

    // Estado de ocupación de la unidad ("available", "occupied") para compatibilidad con el catálogo
    public string Status
    {
        get => _status;
        set => _status = value;
    }
    private string _status = "available";

    public Unit() { }

    // Constructor con parámetros completos para el contexto de Catastro y alícuotas
    public Unit(int projectId, string unitNumber, int floorNumber, decimal areaM2, decimal aliquotPercentage)
    {
        ProjectId = projectId;
        UnitNumber = unitNumber;
        FloorNumber = floorNumber;
        AreaM2 = areaM2;
        AliquotPercentage = aliquotPercentage;
        _status = "available";
        AccountStatus = "Al día";
    }

    // Constructor sobrecargado para compatibilidad con suites de pruebas existentes
    public Unit(int projectId, string unitNumber, int? ownerId = null, int floor = 0, string roomNumber = "")
    {
        ProjectId = projectId;
        UnitNumber = unitNumber;
        OwnerId = ownerId;
        FloorNumber = floor;
        RoomNumber = string.IsNullOrEmpty(roomNumber) ? unitNumber : roomNumber;
        AreaM2 = 75.0m;
        AliquotPercentage = 0.0m;
        _status = ownerId.HasValue ? "occupied" : "available";
        AccountStatus = "Al día";
    }

    // Asigna el residente responsable de la unidad y actualiza su estado de ocupación y cuenta
    public void AssignResident(string? email, string? fullName = null, int? iamUserId = null)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            ResidentEmail = null;
            ResidentFullName = null;
            OwnerId = null;
            _status = "available";
            AccountStatus = "Sin asignar";
        }
        else
        {
            ResidentEmail = email.Trim().ToLowerInvariant();
            ResidentFullName = fullName;
            if (iamUserId.HasValue)
            {
                OwnerId = iamUserId.Value;
            }
            _status = "occupied";
            AccountStatus = "Al día";
        }
    }

    // Método de asignación retrocompatible con llamadas existentes
    public void AssignOwner(string? email, int? ownerId = null)
    {
        AssignResident(email, fullName: null, iamUserId: ownerId);
    }
}
