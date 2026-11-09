using System.Text.RegularExpressions;

namespace IoBuild.Api.Publishing.Interfaces.REST.Resources;

/// <summary>Datos del propietario que la administración registra antes de que él cree su cuenta.</summary>
public sealed record OwnerInvitationResource(string? FirstName, string? LastName, string? Email, string? PhoneNumber);

public static class OwnerInvitation
{
    private static readonly Regex EmailPattern = new(@"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}\z", RegexOptions.CultureInvariant);
    private static readonly Regex PhonePattern = new(@"^\+?[0-9\s\-()]{7,20}\z", RegexOptions.CultureInvariant);

    public static string FullName(OwnerInvitationResource r) => $"{r.FirstName!.Trim()} {r.LastName!.Trim()}";

    public static List<string> Validate(OwnerInvitationResource r)
    {
        var errors = new List<string>();
        if (r.FirstName?.Trim() is not { Length: >= 2 and <= 60 }) errors.Add("Ingresa el nombre del propietario.");
        if (r.LastName?.Trim() is not { Length: >= 2 and <= 60 }) errors.Add("Ingresa el apellido del propietario.");
        var email = r.Email?.Trim() ?? "";
        if (email.Length > 150 || !EmailPattern.IsMatch(email)) errors.Add("Ingresa un correo válido.");
        if (!PhonePattern.IsMatch(r.PhoneNumber?.Trim() ?? "")) errors.Add("Ingresa un teléfono válido.");
        return errors;
    }
}
