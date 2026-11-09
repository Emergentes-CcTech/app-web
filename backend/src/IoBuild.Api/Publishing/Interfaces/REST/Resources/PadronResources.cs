namespace IoBuild.Api.Publishing.Interfaces.REST.Resources;

/// <summary>Una fila del padrón de unidades tal como la edita el administrador.</summary>
public sealed record PadronUnitResource(int? Id, string? Number, string? Type, decimal? AreaM2, decimal? ParticipationPct, int? Floor = null);

public sealed record SavePadronResource(List<PadronUnitResource>? Units);

/// <summary>
/// Reglas del padrón (Ley N.° 27157): cada unidad con participación mayor a 0 % y el total del edificio en 100 %.
/// </summary>
public static class UnitPadron
{
    private static readonly string[] Types = ["apartment", "storage", "parking"];

    public static string NormalizeType(string? type) =>
        Types.Contains(type?.Trim().ToLowerInvariant()) ? type!.Trim().ToLowerInvariant() : "apartment";

    // "401" → piso 4; "1205" → piso 12; un código no numérico queda en el piso 0
    public static int FloorFromNumber(string number) =>
        int.TryParse(number, out var n) && n >= 100 ? n / 100 : 0;

    public static List<string> Validate(IReadOnlyList<PadronUnitResource> rows)
    {
        var errors = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            var number = row.Number?.Trim() ?? "";
            if (number.Length is 0 or > 10) { errors.Add("Cada unidad necesita un número de hasta 10 caracteres."); continue; }
            if (!seen.Add(number)) errors.Add($"La unidad {number} está repetida.");
            if (row.AreaM2 is not > 0 || row.AreaM2 > 100000) errors.Add($"La unidad {number} necesita un área mayor a 0 m².");
            if (row.ParticipationPct is not > 0 || row.ParticipationPct > 100) errors.Add($"La unidad {number} necesita una participación mayor a 0 % y hasta 100 %.");
        }
        if (errors.Count == 0 && rows.Count > 0)
        {
            var total = rows.Sum(r => Math.Round(r.ParticipationPct!.Value, 4));
            if (Math.Abs(total - 100m) > 0.0001m) errors.Add($"Las participaciones suman {total:F4} %; deben sumar exactamente 100 %.");
        }
        return errors;
    }
}
