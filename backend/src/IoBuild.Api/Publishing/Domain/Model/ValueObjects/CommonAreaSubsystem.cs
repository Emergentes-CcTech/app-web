namespace IoBuild.Api.Publishing.Domain.Model.ValueObjects;

/// <summary>
/// Subsistemas de áreas comunes monitoreados en condominios residenciales.
/// Restringido exclusivamente al monitoreo comunal de la infraestructura central del edificio.
/// </summary>
public enum CommonAreaSubsystem
{
    // Cisterna Principal de almacenamiento de agua potable
    MainCistern,

    // Cuarto de Bombas Hidroneumáticas y sistema de impulsión
    PressurePumpsRoom,

    // Banco de Ascensores del edificio
    ElevatorsBank,

    // Tablero General de distribución e iluminación común
    GeneralElectricalPanel
}
