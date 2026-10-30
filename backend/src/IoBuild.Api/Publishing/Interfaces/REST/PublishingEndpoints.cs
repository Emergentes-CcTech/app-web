using System.Security.Claims;

using IoBuild.Api.CoreBusiness;

using IoBuild.Api.Persistence;

using IoBuild.Api.Publishing.Application.Internal.CommandServices;

using IoBuild.Api.Publishing.Domain.Services;

using IoBuild.Api.Publishing.Domain.Services.Commands;

using IoBuild.Api.Publishing.Domain.Services.Queries;

using IoBuild.Api.Publishing.Interfaces.REST.Resources;

using IoBuild.Api.Publishing.Interfaces.REST.Transform;

using Microsoft.AspNetCore.Mvc;

using Microsoft.EntityFrameworkCore;



namespace IoBuild.Api.Publishing.Interfaces.REST;



public static class PublishingEndpoints

{

    // Ownership convention: the JWT user id (Sid claim) must own the acted-upon

    // project, directly or through the unit/client parent. Foreign ids read as

    // not found; explicit mismatches are forbidden.

    private static int SelfId(ClaimsPrincipal user) =>

        int.TryParse(user.FindFirst(ClaimTypes.Sid)?.Value ?? user.FindFirst("sid")?.Value ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("sub")?.Value, out var id) ? id : 0;



    private static bool HasRole(ClaimsPrincipal user, string expectedRole)
    {
        var roles = user.FindAll(ClaimTypes.Role)
            .Concat(user.FindAll("role"))
            .Select(c => c.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (roles.Contains(expectedRole)) return true;
        if (expectedRole.Equals("Builder", StringComparison.OrdinalIgnoreCase) && (roles.Contains("PropertyManager") || roles.Contains("Admin"))) return true;
        if (expectedRole.Equals("PropertyManager", StringComparison.OrdinalIgnoreCase) && (roles.Contains("Builder") || roles.Contains("Admin"))) return true;
        if (expectedRole.Equals("Admin", StringComparison.OrdinalIgnoreCase) && (roles.Contains("PropertyManager") || roles.Contains("Builder"))) return true;
        if (expectedRole.Equals("Resident", StringComparison.OrdinalIgnoreCase) && roles.Contains("Owner")) return true;
        if (expectedRole.Equals("Owner", StringComparison.OrdinalIgnoreCase) && roles.Contains("Resident")) return true;
        return false;
    }

    private static bool IsAdministrator(ClaimsPrincipal user)
    {
        var roles = user.FindAll(ClaimTypes.Role)
            .Concat(user.FindAll("role"))
            .Select(c => c.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return roles.Contains("Builder") || roles.Contains("PropertyManager") || roles.Contains("Admin");
    }

    private static async Task<List<int>> GetResidentUnitIdsAsync(ClaimsPrincipal user, IoBuildDbContext db, CancellationToken ct)
    {
        var self = SelfId(user);
        var email = user.FindFirst(ClaimTypes.Email)?.Value?.ToLowerInvariant()
            ?? user.FindFirst("email")?.Value?.ToLowerInvariant();

        var unitIds = await db.Units
            .Where(u => u.OwnerId == self || (email != null && u.OwnerEmail != null && u.OwnerEmail.ToLower() == email))
            .Select(u => u.Id)
            .ToListAsync(ct);

        var projectedIds = await db.UnitOwnerProjections
            .Where(p => p.OwnerUserId == self)
            .Select(p => p.UnitId)
            .ToListAsync(ct);

        return unitIds.Union(projectedIds).Distinct().ToList();
    }

    private static bool OwnsProject(ClaimsPrincipal user, IoBuild.Api.Publishing.Domain.Model.Aggregates.Project project)
    {
        var self = SelfId(user);
        return self == project.BuilderId || self == project.AdministratorId;
    }

    private static async Task<bool> CanReadProjectAsync(ClaimsPrincipal user, IoBuildDbContext db, IoBuild.Api.Publishing.Domain.Model.Aggregates.Project project, CancellationToken ct)
    {
        if (OwnsProject(user, project)) return true;
        var self = SelfId(user);
        if (HasRole(user, "Resident") || HasRole(user, "Owner"))
        {
            var email = user.FindFirst(ClaimTypes.Email)?.Value?.ToLower();
            return await db.Units.AnyAsync(u => u.ProjectId == project.Id && (u.OwnerId == self || (!string.IsNullOrEmpty(email) && u.OwnerEmail != null && u.OwnerEmail.ToLower() == email)), ct);
        }
        return false;
    }

    private static async Task<bool> OwnsProjectIdAsync(ClaimsPrincipal user, IoBuildDbContext db, int projectId, CancellationToken ct)
    {
        var project = await db.Projects.FindAsync([projectId], ct);
        return project is not null && OwnsProject(user, project);
    }



    private static async Task<bool> OwnsUnitIdAsync(ClaimsPrincipal user, IoBuildDbContext db, int unitId, CancellationToken ct)

    {

        var unit = await db.Units.FindAsync([unitId], ct);

        return unit is not null && await OwnsProjectIdAsync(user, db, unit.ProjectId, ct);

    }



    private static async Task<bool> OwnsUnitInProjectAsync(ClaimsPrincipal user, IoBuildDbContext db, int unitId, int projectId, CancellationToken ct)

    {

        var unit = await db.Units.FindAsync([unitId], ct);

        return unit is not null && unit.ProjectId == projectId && await OwnsProjectIdAsync(user, db, projectId, ct);

    }



    private static (bool isValid, string error) CheckTextLegibility(string text, string fieldName)

    {

        var trimmed = text.Trim();

        var words = trimmed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);



        // 1. Any word > 20 chars or words >= 3 chars with no vowels (skip words containing digits such as identifiers/codes/stamps)

        foreach (var word in words)

        {

            if (System.Text.RegularExpressions.Regex.IsMatch(word, @"\d"))

                continue;



            var clean = System.Text.RegularExpressions.Regex.Replace(word, @"[^a-zA-ZáéíóúÁÉÍÓÚñÑüÜ]", "");

            if (clean.Length > 20)

                return (false, "El texto contiene palabras excesivamente largas o no válidas.");

            if (clean.Length >= 3 && !System.Text.RegularExpressions.Regex.IsMatch(clean, @"[aeiouáéíóúAEIOUÁÉÍÓÚyY]", System.Text.RegularExpressions.RegexOptions.IgnoreCase))

                return (false, "Cada palabra debe ser comprensible y contener vocales.");

        }



        // 2. Minimum words required for Description

        if (fieldName == "description")

        {

            if (words.Length < 2 || !trimmed.Contains(' '))

                return (false, "La descripción debe ser una frase u oración compuesta por varias palabras separadas por espacios.");

        }



        // 3. Location structure

        if (fieldName == "location")

        {

            if (trimmed.Length >= 8 && !trimmed.Contains(' '))

                return (false, "La ubicación debe describir una dirección o zona válida con palabras separadas por espacios.");

        }



        // 4. Consecutive consonants (5 or more consonants in a row is impossible in Spanish/English)

        if (System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"[bcdfghjklmnñpqrstvwxyzBCDFGHJKLMNÑPQRSTVWXYZ]{5,}", System.Text.RegularExpressions.RegexOptions.IgnoreCase))

            return (false, "El texto contiene combinaciones de consonantes no legibles o de escritura aleatoria.");



        // Pure words for vowel ratio, keyboard mash and repetitive pattern detection

        var pureWords = words.Where(w => !System.Text.RegularExpressions.Regex.IsMatch(w, @"\d")).ToList();

        var pureText = pureWords.Count > 0 ? string.Join(" ", pureWords) : trimmed;

        var pureLetters = System.Text.RegularExpressions.Regex.Matches(pureText, @"[a-zA-ZáéíóúÁÉÍÓÚñÑüÜ]");

        var pureVowels = System.Text.RegularExpressions.Regex.Matches(pureText, @"[aeiouáéíóúAEIOUÁÉÍÓÚyY]", System.Text.RegularExpressions.RegexOptions.IgnoreCase);



        // 5. Vowel ratio check (between 15% and 85% for text with 6+ letters)

        if (pureLetters.Count >= 6)

        {

            var ratio = (double)pureVowels.Count / pureLetters.Count;

            if (ratio < 0.15 || ratio > 0.85)

                return (false, "El texto debe contener una proporción legible de vocales y consonantes.");

        }



        // 6. Keyboard mash / Home row spam check:

        if (pureLetters.Count >= 8)

        {

            var uniqueLetters = new HashSet<char>(pureText.ToLowerInvariant().Where(c => char.IsLetter(c)));

            if (pureLetters.Count >= 10 && uniqueLetters.Count <= 4)

                return (false, "El texto parece una combinación aleatoria o repetitiva del teclado.");



            var homeRowKeys = new HashSet<char>("asdfghjkl".ToCharArray());

            var homeCount = pureText.ToLowerInvariant().Count(c => homeRowKeys.Contains(c));

            if (pureLetters.Count >= 10 && ((double)homeCount / pureLetters.Count) >= 0.88)

                return (false, "El texto contiene patrones repetitivos de teclas del teclado.");

        }



        // 7. Repetitive sub-patterns (e.g. asdasd, dfsdfs, etc.)

        if (pureWords.Count > 0 && System.Text.RegularExpressions.Regex.IsMatch(pureText, @"([a-zA-Z]{2,5})\1{2,}", System.Text.RegularExpressions.RegexOptions.IgnoreCase))

            return (false, "El texto contiene patrones o secuencias repetitivas de caracteres.");



        // 8. Keyboard sequences and pure repetition (e.g. asdf, qwer, zxcv, asdasd)

        var cleanAlpha = System.Text.RegularExpressions.Regex.Replace(pureText.ToLowerInvariant(), @"[^a-záéíóúñü]", "");

        string[] keyboardSequences = ["asdf", "qwer", "zxcv", "hjkl", "yuio", "ghjk", "fdsa", "rewq", "vcxz"];

        if (keyboardSequences.Any(seq => cleanAlpha.Contains(seq)))

            return (false, "El texto contiene secuencias de teclas del teclado (ej. asdf).");

        if (cleanAlpha.Length >= 4 && System.Text.RegularExpressions.Regex.IsMatch(cleanAlpha, @"^([a-z]{2,4})\1+$"))

            return (false, "El texto contiene secuencias repetitivas del teclado.");



        return (true, string.Empty);

    }



    private static (bool isValid, string error) ValidateProjectData(string? name, string? location, string? description)

    {

        var trimmedName = name?.Trim();

        if (string.IsNullOrWhiteSpace(trimmedName))

            return (false, "El nombre del proyecto es obligatorio.");

        if (trimmedName.Length > 100)

            return (false, "El nombre del proyecto debe tener entre 3 y 100 caracteres.");

        if (trimmedName.Contains('<') || trimmedName.Contains('>'))

            return (false, "El nombre del proyecto no puede contener etiquetas ni caracteres HTML (<, >).");

        if (System.Text.RegularExpressions.Regex.IsMatch(trimmedName, @"(.)\1{3,}", System.Text.RegularExpressions.RegexOptions.IgnoreCase))

            return (false, "El nombre del proyecto no puede contener caracteres repetitivos continuos (ej. aaaa).");

        if (trimmedName.Length >= 4)

        {

            var nameLetters = System.Text.RegularExpressions.Regex.Matches(trimmedName, @"[a-zA-ZáéíóúÁÉÍÓÚñÑüÜ]").Count;

            if (nameLetters < 3)

                return (false, "El nombre del proyecto debe contener al menos 3 letras y no consistir únicamente en números o símbolos.");

            if (!System.Text.RegularExpressions.Regex.IsMatch(trimmedName, @"[aeiouáéíóúAEIOUÁÉÍÓÚ]", System.Text.RegularExpressions.RegexOptions.IgnoreCase))

                return (false, "El nombre del proyecto debe contener al menos una vocal.");

            var nameLegibility = CheckTextLegibility(trimmedName, "name");

            if (!nameLegibility.isValid) return nameLegibility;

        }



        var trimmedLoc = location?.Trim();

        if (string.IsNullOrWhiteSpace(trimmedLoc))

            return (false, "La ubicación del proyecto es obligatoria.");

        if (trimmedLoc.Length > 150)

            return (false, "La ubicación del proyecto debe tener entre 4 y 150 caracteres.");

        if (trimmedLoc.Contains('<') || trimmedLoc.Contains('>'))

            return (false, "La ubicación del proyecto no puede contener caracteres HTML (<, >).");

        if (System.Text.RegularExpressions.Regex.IsMatch(trimmedLoc, @"(.)\1{3,}", System.Text.RegularExpressions.RegexOptions.IgnoreCase))

            return (false, "La ubicación del proyecto no puede contener caracteres repetitivos continuos.");

        if (trimmedLoc.Length >= 4)

        {

            var locLetters = System.Text.RegularExpressions.Regex.Matches(trimmedLoc, @"[a-zA-ZáéíóúÁÉÍÓÚñÑüÜ]").Count;

            if (locLetters < 3)

                return (false, "La ubicación del proyecto debe contener al menos 3 letras que describan una dirección o zona válida.");

            if (!System.Text.RegularExpressions.Regex.IsMatch(trimmedLoc, @"[aeiouáéíóúAEIOUÁÉÍÓÚ]", System.Text.RegularExpressions.RegexOptions.IgnoreCase))

                return (false, "La ubicación del proyecto debe ser una dirección o zona legible y contener al menos una vocal.");

            var locLegibility = CheckTextLegibility(trimmedLoc, "location");

            if (!locLegibility.isValid) return locLegibility;

        }



        var trimmedDesc = description?.Trim();

        if (string.IsNullOrWhiteSpace(trimmedDesc))

            return (false, "La descripción del proyecto es obligatoria.");

        if (trimmedDesc.Length > 500)

            return (false, "La descripción del proyecto debe tener entre 10 y 500 caracteres.");

        if (trimmedDesc.Contains('<') || trimmedDesc.Contains('>'))

            return (false, "La descripción del proyecto no puede contener caracteres HTML (<, >).");

        if (System.Text.RegularExpressions.Regex.IsMatch(trimmedDesc, @"(.)\1{3,}", System.Text.RegularExpressions.RegexOptions.IgnoreCase))

            return (false, "La descripción del proyecto no puede contener caracteres repetitivos continuos.");

        if (trimmedDesc.Length >= 10)

        {

            var descLetters = System.Text.RegularExpressions.Regex.Matches(trimmedDesc, @"[a-zA-ZáéíóúÁÉÍÓÚñÑüÜ]").Count;

            if (descLetters < 5)

                return (false, "La descripción del proyecto debe contener al menos 5 letras y ser un texto descriptivo comprensible.");

            if (!System.Text.RegularExpressions.Regex.IsMatch(trimmedDesc, @"[aeiouáéíóúAEIOUÁÉÍÓÚ]", System.Text.RegularExpressions.RegexOptions.IgnoreCase))

                return (false, "La descripción del proyecto debe ser un texto legible y contener vocales.");

            var descLegibility = CheckTextLegibility(trimmedDesc, "description");

            if (!descLegibility.isValid) return descLegibility;

        }



        return (true, string.Empty);

    }



    private static (bool isValid, string error) ValidateClientData(string? fullName, string? email, string? phoneNumber, string? address)

    {

        var trimmedName = fullName?.Trim();

        if (string.IsNullOrWhiteSpace(trimmedName))

            return (false, "El nombre completo del cliente es obligatorio.");

        if (trimmedName.Length < 3 || trimmedName.Length > 100)

            return (false, "El nombre completo del cliente debe tener entre 3 y 100 caracteres.");

        if (trimmedName.Contains('<') || trimmedName.Contains('>'))

            return (false, "El nombre completo no puede contener caracteres HTML (<, >).");

        if (!System.Text.RegularExpressions.Regex.IsMatch(trimmedName, @"^[a-zA-Z0-9áéíóúÁÉÍÓÚñÑüÜ\s.\-']+$"))

            return (false, "El nombre completo solo puede contener letras y caracteres válidos.");

        var nameLetters = System.Text.RegularExpressions.Regex.Matches(trimmedName, @"[a-zA-ZáéíóúÁÉÍÓÚñÑüÜ]").Count;

        if (nameLetters < 3)

            return (false, "El nombre completo debe contener al menos 3 letras.");



        var nameLegibility = CheckTextLegibility(trimmedName, "name");

        if (!nameLegibility.isValid) return nameLegibility;



        var trimmedEmail = email?.Trim();

        if (string.IsNullOrWhiteSpace(trimmedEmail))

            return (false, "El correo electrónico del cliente es obligatorio.");

        if (trimmedEmail.Length < 6 || trimmedEmail.Length > 100)

            return (false, "El correo electrónico debe tener entre 6 y 100 caracteres.");

        if (trimmedEmail.Contains('<') || trimmedEmail.Contains('>'))

            return (false, "El correo electrónico no puede contener etiquetas HTML (<, >).");

        if (!System.Text.RegularExpressions.Regex.IsMatch(trimmedEmail, @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$"))

            return (false, "Ingrese un correo electrónico válido (ej. usuario@empresa.com).");



        var emailParts = trimmedEmail.Split('@');

        if (emailParts.Length != 2)

            return (false, "Ingrese un correo electrónico válido con formato usuario@dominio.com.");



        var username = emailParts[0];

        var domain = emailParts[1];



        if (username.Length < 2)

            return (false, "El usuario del correo electrónico debe tener al menos 2 caracteres.");

        if (System.Text.RegularExpressions.Regex.IsMatch(username, @"(.)\1{3,}", System.Text.RegularExpressions.RegexOptions.IgnoreCase))

            return (false, "El correo electrónico no puede contener caracteres repetitivos continuos.");



        var cleanUserAlpha = System.Text.RegularExpressions.Regex.Replace(username.ToLowerInvariant(), @"[^a-z]", "");

        string[] keyboardSequences = ["asdf", "qwer", "zxcv", "hjkl", "yuio", "uiop", "ghjk", "fdsa", "rewq", "vcxz"];

        if (keyboardSequences.Any(seq => cleanUserAlpha.Contains(seq)))

            return (false, "El correo electrónico contiene secuencias de teclas del teclado (ej. asdf).");

        string[] repetitiveKeyboardPatterns = ["asdasd", "adadad", "dfdfdf", "jkjkjk", "ababab", "testtest"];

        if (repetitiveKeyboardPatterns.Any(pat => cleanUserAlpha.Contains(pat)) ||

            (cleanUserAlpha.Length >= 6 && System.Text.RegularExpressions.Regex.IsMatch(cleanUserAlpha, @"^(.{2,3})\1{2,}$")))

            return (false, "El correo electrónico contiene secuencias repetitivas.");

        if (!System.Text.RegularExpressions.Regex.IsMatch(username, @"[aeiou0-9]", System.Text.RegularExpressions.RegexOptions.IgnoreCase))

            return (false, "El usuario del correo debe ser un texto legible y contener al menos una vocal o número.");



        var domainParts = domain.Split('.');

        if (domainParts.Length < 2 || domainParts.Any(p => p.Length < 2))

            return (false, "El dominio del correo debe tener una estructura válida (ej. empresa.com).");



        var domainName = domainParts[0].ToLowerInvariant();

        if (keyboardSequences.Any(seq => domainName.Contains(seq)))

            return (false, "El dominio del correo contiene secuencias del teclado no válidas.");

        if (System.Text.RegularExpressions.Regex.IsMatch(domainName, @"(.)\1{3,}", System.Text.RegularExpressions.RegexOptions.IgnoreCase))

            return (false, "El dominio del correo contiene caracteres repetitivos no válidos.");



        var trimmedPhone = phoneNumber?.Trim();

        if (!string.IsNullOrWhiteSpace(trimmedPhone))

        {

            var digitsOnly = System.Text.RegularExpressions.Regex.Replace(trimmedPhone, @"\D", "");

            if (digitsOnly.Length < 7 || digitsOnly.Length > 15)

                return (false, "El número telefónico debe tener entre 7 y 15 dígitos.");

        }



        var trimmedAddr = address?.Trim();

        if (!string.IsNullOrWhiteSpace(trimmedAddr))

        {

            if (trimmedAddr.Length < 4 || trimmedAddr.Length > 150)

                return (false, "La dirección debe tener entre 4 y 150 caracteres.");

            if (trimmedAddr.Contains('<') || trimmedAddr.Contains('>'))

                return (false, "La dirección no puede contener caracteres HTML (<, >).");

            var addrLetters = System.Text.RegularExpressions.Regex.Matches(trimmedAddr, @"[a-zA-ZáéíóúÁÉÍÓÚñÑüÜ]").Count;

            if (addrLetters < 3)

                return (false, "La dirección debe contener al menos 3 letras.");



            var addrLegibility = CheckTextLegibility(trimmedAddr, "location");

            if (!addrLegibility.isValid) return addrLegibility;

        }



        return (true, string.Empty);

    }



    public static void MapPublishingEndpoints(this WebApplication app)

    {

        // ── Projects Endpoints ──

        var projects = app.MapGroup("/api/v1/projects").WithTags("Projects");



        projects.MapGet("", async (ClaimsPrincipal user, IoBuildDbContext db, CancellationToken ct) =>
        {
            var sid = user.FindFirst(ClaimTypes.Sid)?.Value ?? user.FindFirst("sid")?.Value ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("sub")?.Value;
            var builderId = int.TryParse(sid, out var id) ? id : 0;
            List<IoBuild.Api.Publishing.Domain.Model.Aggregates.Project> projectList;
            if (HasRole(user, "Resident") || HasRole(user, "Owner"))
            {
                var email = user.FindFirst(ClaimTypes.Email)?.Value?.ToLower();
                var residentProjectIds = await db.Units
                    .Where(u => u.OwnerId == builderId || (!string.IsNullOrEmpty(email) && u.OwnerEmail != null && u.OwnerEmail.ToLower() == email))
                    .Select(u => u.ProjectId)
                    .Distinct()
                    .ToListAsync(ct);
                projectList = await db.Projects.Where(p => residentProjectIds.Contains(p.Id)).OrderBy(p => p.Id).ToListAsync(ct);
            }
            else
            {
                projectList = await db.Projects.Where(project => project.BuilderId == builderId).OrderBy(project => project.Id).ToListAsync(ct);
            }

            var projectIds = projectList.Select(p => p.Id).ToList();
            var occupiedCounts = await db.Units
                .Where(u => projectIds.Contains(u.ProjectId) && (!string.IsNullOrEmpty(u.OwnerEmail) || u.OwnerId.HasValue))
                .GroupBy(u => u.ProjectId)
                .Select(g => new { ProjectId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.ProjectId, g => g.Count, ct);

            var result = projectList.Select(p => new
            {
                p.Id,
                p.Name,
                p.Description,
                p.Location,
                p.TotalUnits,
                OccupiedUnits = occupiedCounts.GetValueOrDefault(p.Id, 0),
                p.BuilderId,
                AdministratorId = p.AdministratorId,
                p.ImageUrl,
                p.StructureDefined,
                p.CreatedAt
            });

            return Results.Ok(result);
        }).RequireAuthorization();



        projects.MapPost("", async (CreateProjectRequest request, ClaimsPrincipal user, CoreBusinessService service, CancellationToken ct) =>

        {

            var tokenBuilderId = SelfId(user);

            if (request.BuilderId.HasValue && request.BuilderId.Value > 0 && request.BuilderId.Value != tokenBuilderId) return Results.Forbid();

            var builderId = request.BuilderId.HasValue && request.BuilderId.Value > 0 ? request.BuilderId.Value : tokenBuilderId;



            var validation = ValidateProjectData(request.Name, request.Location, request.Description);

            if (!validation.isValid) return Results.Json(new { error = validation.error }, statusCode: 422);



            var project = await service.CreateProjectAsync(request.Name, request.Description, request.Location, request.TotalUnits, builderId, request.ImageUrl, ct);

            return Results.Created($"/api/v1/projects/{project.Id}", project);

        }).RequireAuthorization();



        projects.MapGet("/{id:int}", async (int id, ClaimsPrincipal user, IoBuildDbContext db, CancellationToken ct) =>
        {
            var item = await db.Projects.FindAsync([id], ct);
            if (item is null || !await CanReadProjectAsync(user, db, item, ct)) return Results.NotFound();

            var occupiedUnits = await db.Units
                .CountAsync(u => u.ProjectId == id && (!string.IsNullOrEmpty(u.OwnerEmail) || u.OwnerId.HasValue), ct);

            return Results.Ok(new
            {
                item.Id,
                item.Name,
                item.Description,
                item.Location,
                item.TotalUnits,
                OccupiedUnits = occupiedUnits,
                item.BuilderId,
                AdministratorId = item.AdministratorId,
                item.ImageUrl,
                item.StructureDefined,
                item.CreatedAt
            });
        }).RequireAuthorization();

        // ── GET /projects/{projectId}/units (filtrado por residentUserId u ownerId con aislamiento de privacidad) ──
        projects.MapGet("/{projectId:int}/units", async (
            int projectId,
            [FromQuery] int? residentUserId,
            [FromQuery] int? ownerId,
            ClaimsPrincipal user,
            IUnitQueryService queryService,
            IoBuildDbContext db,
            CancellationToken ct) =>
        {
            var project = await db.Projects.FindAsync([projectId], ct);
            if (project is null) return Results.NotFound();

            var targetOwnerId = ownerId ?? residentUserId;
            IEnumerable<Unit> unitList;

            if (!IsAdministrator(user))
            {
                if (!await CanReadProjectAsync(user, db, project, ct))
                {
                    return Results.NotFound();
                }

                var self = SelfId(user);
                if (targetOwnerId.HasValue && targetOwnerId.Value != self)
                {
                    return Results.Forbid();
                }

                var myUnitIds = await GetResidentUnitIdsAsync(user, db, ct);
                unitList = await db.Units
                    .Where(u => u.ProjectId == projectId && myUnitIds.Contains(u.Id))
                    .ToListAsync(ct);
            }
            else
            {
                if (targetOwnerId.HasValue)
                {
                    unitList = await db.Units.Where(u => u.ProjectId == projectId && u.OwnerId == targetOwnerId.Value).ToListAsync(ct);
                }
                else
                {
                    unitList = await queryService.Handle(new GetUnitsByProjectIdQuery(projectId), ct);
                }
            }

            return Results.Ok(unitList.Select(UnitResourceFromEntityAssembler.ToResourceFromEntity));
        }).RequireAuthorization();



        projects.MapPut("/{id:int}", async (int id, CreateProjectRequest request, ClaimsPrincipal user, IoBuildDbContext db, CancellationToken ct) =>

        {

            var item = await db.Projects.FindAsync([id], ct);

            if (item is null || !OwnsProject(user, item)) return Results.NotFound();



            var validation = ValidateProjectData(request.Name, request.Location, request.Description);

            if (!validation.isValid) return Results.Json(new { error = validation.error }, statusCode: 422);



            item.Name = request.Name;

            item.Description = request.Description;

            item.Location = request.Location;

            item.TotalUnits = request.TotalUnits;

            item.ImageUrl = request.ImageUrl;

            await db.SaveChangesAsync(ct);

            return Results.NoContent();

        }).RequireAuthorization();



        projects.MapDelete("/{id:int}", async (int id, ClaimsPrincipal user, IoBuildDbContext db, CancellationToken ct) =>

        {

            var item = await db.Projects.FindAsync([id], ct);

            if (item is null || !OwnsProject(user, item)) return Results.NotFound();



            // 1. Remove clients associated with this project

            var clients = await db.Clients.Where(c => c.ProjectId == id).ToListAsync(ct);

            if (clients.Count > 0) db.Clients.RemoveRange(clients);



            // 2. Remove devices and their projections

            var devices = await db.Devices.Where(d => d.ProjectId == id).ToListAsync(ct);

            if (devices.Count > 0) db.Devices.RemoveRange(devices);



            var devProjections = await db.DeviceProjections.Where(d => d.ProjectId == id).ToListAsync(ct);

            if (devProjections.Count > 0) db.DeviceProjections.RemoveRange(devProjections);



            // 3. Remove units and their projections

            var units = await db.Units.Where(u => u.ProjectId == id).ToListAsync(ct);

            if (units.Count > 0) db.Units.RemoveRange(units);



            var unitProjections = await db.UnitProjections.Where(u => u.ProjectId == id).ToListAsync(ct);

            if (unitProjections.Count > 0) db.UnitProjections.RemoveRange(unitProjections);



            // 4. Remove project projections

            var projectProjections = await db.ProjectProjections.Where(p => p.ProjectId == id).ToListAsync(ct);

            if (projectProjections.Count > 0) db.ProjectProjections.RemoveRange(projectProjections);



            // 5. Remove project record

            db.Projects.Remove(item);

            await db.SaveChangesAsync(ct);

            return Results.NoContent();

        }).RequireAuthorization();



        projects.MapPost("/{id:int}/structure", async (int id, ProjectStructureRequest request, ClaimsPrincipal user, IProjectCommandService commandService, IoBuildDbContext db, CancellationToken ct) =>

        {

            var role = user.FindFirst(ClaimTypes.Role)?.Value ?? user.FindFirst("role")?.Value;

            if (!string.Equals(role, "PropertyManager", StringComparison.OrdinalIgnoreCase) && !string.Equals(role, "Builder", StringComparison.OrdinalIgnoreCase))

                return Results.Json(new { error = "Solo usuarios con rol PropertyManager o Administrador pueden definir la estructura del condominio." }, statusCode: 403);

            if (request.Floors < 1 || request.Floors > 50 || request.UnitsPerFloor < 1 || request.UnitsPerFloor > 20)

                return Results.Json(new { error = "Los pisos deben situarse entre 1 y 50, y las unidades por piso entre 1 y 20." }, statusCode: 422);

            if ((long)request.Floors * request.UnitsPerFloor > 500)

                return Results.Json(new { error = "El total de departamentos no puede exceder 500 unidades." }, statusCode: 422);

            if (request.FloorNumbers?.Any(floor => floor < 1 || floor > request.Floors) == true)

                return Results.BadRequest(new { error = "La referencia de pisos está fuera del rango permitido." });



            var project = await db.Projects.FindAsync([id], ct);

            if (project is null || !OwnsProject(user, project)) return Results.NotFound();

            if (project.StructureDefined) return Results.Conflict(new { error = "La estructura del condominio ya fue definida previamente." });



            // Validación frente a los límites del plan HaaS contratado

            var builderId = SelfId(user);

            var sub = await db.Subscriptions

                .Where(s => (s.ProjectId == id || s.BuilderId == builderId) && s.Status == "active")

                .OrderByDescending(s => s.Id)

                .FirstOrDefaultAsync(ct);



            int totalRequestedUnits = request.Floors * request.UnitsPerFloor;

            if (sub != null)

            {

                var plan = await db.Plans.FindAsync([sub.PlanId], ct);

                if (plan != null && totalRequestedUnits > plan.MaxUnits)

                {

                    return Results.BadRequest(new {

                        error = $"El condominio requiere {totalRequestedUnits} departamentos, superando el límite de {plan.MaxUnits} del Plan {plan.Name} contratado."

                    });

                }

            }



            try

            {

                await commandService.DefineProjectStructureAsync(id, request.Floors, request.UnitsPerFloor, request.FloorNumbers, ct);

            }

            catch (ArgumentException ex)

            {

                return Results.BadRequest(new { error = ex.Message });

            }

            catch (InvalidOperationException ex)

            {

                return Results.BadRequest(new { error = ex.Message });

            }

            catch (DbUpdateException ex) when (ex.InnerException is MySqlConnector.MySqlException mysql && mysql.Number == 1062)

            {

                return Results.Conflict(new { error = "La estructura del condominio ya fue definida previamente." });

            }



            return Results.Created($"/api/v1/projects/{id}/structure", new {

                message = $"Estructura del condominio definida: {request.Floors} piso(s), {request.UnitsPerFloor} unidad(es) por piso. Suma de alícuotas: 100.0000% (Ley N.° 27157)."

            });

        }).RequireAuthorization();



        // ── Invariante Sagrado: Actualización y validación de alícuotas al 100.0000% (Ley N.° 27157) ──

        projects.MapPut("/{id:int}/aliquots", async (int id, List<UpdateUnitAliquotItem> aliquotItems, ClaimsPrincipal user, IoBuildDbContext db, CancellationToken ct) =>

        {

            if (!await OwnsProjectIdAsync(user, db, id, ct)) return Results.NotFound();

            if (aliquotItems == null || aliquotItems.Count == 0)

                return Results.BadRequest(new { error = "Debe enviar la lista de alícuotas para todas las unidades del condominio." });



            // Invariante Sagrado de la Ley N.° 27157: La suma total de alícuotas debe ser exactamente 100.0000%

            decimal totalAliquot = aliquotItems.Sum(a => a.AliquotPercentage);

            if (Math.Abs(totalAliquot - 100.0000m) > 0.0001m)

            {

                return Results.BadRequest(new {

                    error = $"La suma de alícuotas del condominio debe ser exactamente 100.0000% (Ley N.° 27157). La suma enviada es {totalAliquot:F4}%."

                });

            }



            var units = await db.Units.Where(u => u.ProjectId == id).ToListAsync(ct);

            var map = aliquotItems.ToDictionary(a => a.UnitId, a => a.AliquotPercentage);

            foreach (var unit in units)

            {

                if (map.TryGetValue(unit.Id, out var newAliquot))

                {

                    unit.AliquotPercentage = newAliquot;

                }

            }

            await db.SaveChangesAsync(ct);

            return Results.Ok(new { message = "Alícuotas actualizadas y validadas al 100.0000% conforme a Ley N.° 27157." });

        }).RequireAuthorization();



        // ── Gestión de Áreas Comunes del Condominio ──

        projects.MapGet("/{id:int}/common-areas", async (int id, ClaimsPrincipal user, IoBuildDbContext db, CancellationToken ct) =>

        {

            if (!await OwnsProjectIdAsync(user, db, id, ct)) return Results.NotFound();

            var areas = await db.CommonAreas.Where(c => c.ProjectId == id).ToListAsync(ct);

            return Results.Ok(areas);

        }).RequireAuthorization();



        projects.MapPost("/{id:int}/common-areas", async (int id, CreateCommonAreaRequest req, ClaimsPrincipal user, IoBuildDbContext db, CancellationToken ct) =>

        {

            if (!await OwnsProjectIdAsync(user, db, id, ct)) return Results.NotFound();

            var area = new CommonArea(id, req.Name, req.Subsystem, req.Location, req.Description);

            db.CommonAreas.Add(area);

            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/v1/projects/{id}/common-areas/{area.Id}", area);

        }).RequireAuthorization();



        // ── Units Endpoints ──

        var units = app.MapGroup("/api/v1/units").WithTags("Units");



        units.MapGet("", async (
            [FromQuery] int? projectId,
            [FromQuery] int? condominiumId,
            [FromQuery] int? ownerId,
            [FromQuery] int? residentUserId,
            ClaimsPrincipal user,
            IUnitQueryService queryService,
            IoBuildDbContext db,
            CancellationToken ct) =>
        {
            var targetProjectId = projectId ?? condominiumId;
            var targetOwnerId = ownerId ?? residentUserId;
            IEnumerable<Unit> unitList;

            if (!IsAdministrator(user))
            {
                var self = SelfId(user);
                if (targetOwnerId.HasValue && targetOwnerId.Value != self)
                {
                    return Results.Forbid();
                }

                var myUnitIds = await GetResidentUnitIdsAsync(user, db, ct);
                var query = db.Units.Where(u => myUnitIds.Contains(u.Id));
                if (targetProjectId.HasValue)
                {
                    query = query.Where(u => u.ProjectId == targetProjectId.Value);
                }
                unitList = await query.ToListAsync(ct);
            }
            else
            {
                if (targetProjectId.HasValue && targetOwnerId.HasValue)
                {
                    unitList = await db.Units.Where(u => u.ProjectId == targetProjectId.Value && u.OwnerId == targetOwnerId.Value).ToListAsync(ct);
                }
                else if (targetOwnerId.HasValue)
                {
                    unitList = await queryService.Handle(new GetUnitsByOwnerIdQuery(targetOwnerId.Value), ct);
                }
                else if (targetProjectId.HasValue)
                {
                    unitList = await queryService.Handle(new GetUnitsByProjectIdQuery(targetProjectId.Value), ct);
                }
                else
                {
                    unitList = await queryService.Handle(new GetAllUnitsQuery(), ct);
                }
            }

            return Results.Ok(unitList.Select(UnitResourceFromEntityAssembler.ToResourceFromEntity));
        }).RequireAuthorization();

        units.MapGet("/{id:int}", async (
            int id,
            ClaimsPrincipal user,
            IUnitQueryService queryService,
            IoBuildDbContext db,
            CancellationToken ct) =>
        {
            if (!IsAdministrator(user))
            {
                var myUnitIds = await GetResidentUnitIdsAsync(user, db, ct);
                if (!myUnitIds.Contains(id))
                {
                    return Results.Forbid();
                }
            }

            var unit = await queryService.Handle(new GetUnitByIdQuery(id), ct);
            return unit is null ? Results.NotFound() : Results.Ok(UnitResourceFromEntityAssembler.ToResourceFromEntity(unit));
        }).RequireAuthorization();

        units.MapPost("", async (CreateUnitResource resource, ClaimsPrincipal user, IUnitCommandService commandService, IUnitQueryService queryService, IoBuildDbContext db, CancellationToken ct) =>
        {
            if (!await OwnsProjectIdAsync(user, db, resource.ProjectId, ct)) return Results.NotFound();
            var command = new CreateUnitCommand(resource.ProjectId, resource.UnitNumber, resource.OwnerId, resource.Floor, resource.RoomNumber);
            int unitId;
            try
            {
                unitId = await commandService.Handle(command, ct);
            }
            catch (DbUpdateException ex) when (ex.InnerException is MySqlConnector.MySqlException mysql && mysql.Number == 1062)
            {
                // Duplicate (project, floor, room): update it instead of duplicating.
                return Results.Conflict(new { error = "A unit with this floor and room already exists in the project." });
            }
            var created = await queryService.Handle(new GetUnitByIdQuery(unitId), ct);
            return created is null ? Results.Problem(statusCode: 500) : Results.Created($"/api/v1/units/{unitId}", UnitResourceFromEntityAssembler.ToResourceFromEntity(created));
        }).RequireAuthorization();

        units.MapPatch("/{id:int}/assign-owner", async (int id, AssignUnitOwnerResource resource, ClaimsPrincipal user, IUnitCommandService commandService, IUnitQueryService queryService, IoBuildDbContext db, CancellationToken ct) =>
        {
            if (!await OwnsUnitIdAsync(user, db, id, ct)) return Results.NotFound();
            try
            {
                var effectiveOwnerId = resource.OwnerId ?? resource.ResidentUserId;
                await commandService.Handle(new AssignUnitOwnerEmailCommand(id, resource.OwnerEmail, effectiveOwnerId), ct);
                var updated = await queryService.Handle(new GetUnitByIdQuery(id), ct);
                return updated is null ? Results.NotFound() : Results.Ok(UnitResourceFromEntityAssembler.ToResourceFromEntity(updated));
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
        }).RequireAuthorization();

        units.MapPatch("/{id:int}", async (int id, AssignUnitOwnerResource resource, ClaimsPrincipal user, IUnitCommandService commandService, IUnitQueryService queryService, IoBuildDbContext db, CancellationToken ct) =>
        {
            if (!await OwnsUnitIdAsync(user, db, id, ct)) return Results.NotFound();
            try
            {
                var effectiveOwnerId = resource.OwnerId ?? resource.ResidentUserId;
                await commandService.Handle(new AssignUnitOwnerEmailCommand(id, resource.OwnerEmail, effectiveOwnerId), ct);
                var updated = await queryService.Handle(new GetUnitByIdQuery(id), ct);
                return updated is null ? Results.NotFound() : Results.Ok(UnitResourceFromEntityAssembler.ToResourceFromEntity(updated));
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
        }).RequireAuthorization();




        // ── Clients Endpoints ──

        var clients = app.MapGroup("/api/v1/clients").WithTags("Clients");



        clients.MapGet("", async ([FromQuery] int? builderId, [FromQuery] int? projectId, ClaimsPrincipal user, IClientQueryService queryService, IoBuildDbContext db, CancellationToken ct) =>

        {

            if (!HasRole(user, "Builder")) return Results.Forbid();

            var builderIdFromToken = SelfId(user);

            if (builderIdFromToken <= 0) return Results.Unauthorized();

            if (builderId.HasValue && builderId.Value != builderIdFromToken) return Results.Forbid();

            if (projectId.HasValue && !await OwnsProjectIdAsync(user, db, projectId.Value, ct)) return Results.NotFound();



            var clientList = await queryService.Handle(new GetClientsByBuilderIdQuery(builderIdFromToken, projectId), ct);

            // En áreas comunes los departamentos privados no tienen dispositivos IoT propios
            var result = clientList.Select(c => ClientResourceFromEntityAssembler.ToResourceFromEntity(c, 0));

            return Results.Ok(result);
        }).RequireAuthorization();

        clients.MapGet("/{id:int}", async (int id, ClaimsPrincipal user, IClientQueryService queryService, IoBuildDbContext db, CancellationToken ct) =>
        {
            if (!HasRole(user, "Builder")) return Results.Forbid();
            var client = await queryService.Handle(new GetClientByIdQuery(id), ct);
            if (client is null || client.BuilderId != SelfId(user)) return Results.NotFound();
            // Dispositivos privados eliminados; siempre retorna 0 dispositivos de departamento
            return Results.Ok(ClientResourceFromEntityAssembler.ToResourceFromEntity(client, 0));

        }).RequireAuthorization();



        clients.MapPost("", async (CreateClientResource resource, ClaimsPrincipal user, IClientCommandService commandService, IClientQueryService queryService, IoBuildDbContext db, CancellationToken ct) =>

        {

            if (!HasRole(user, "Builder")) return Results.Forbid();

            var tokenBuilderId = SelfId(user);

            if (tokenBuilderId <= 0) return Results.Unauthorized();

            if (resource.BuilderId > 0 && resource.BuilderId != tokenBuilderId) return Results.Forbid();

            if (!await OwnsProjectIdAsync(user, db, resource.ProjectId, ct)) return Results.NotFound();

            if (resource.UnitId.HasValue && !await OwnsUnitInProjectAsync(user, db, resource.UnitId.Value, resource.ProjectId, ct)) return Results.NotFound();



            var validation = ValidateClientData(resource.FullName, resource.Email, resource.PhoneNumber, resource.Address);

            if (!validation.isValid) return Results.Json(new { error = validation.error }, statusCode: 422);



            var command = new CreateClientCommand(

                resource.FullName,

                resource.ProjectName,

                resource.AccountStatement,

                tokenBuilderId,

                resource.ProjectId,

                resource.Email,

                resource.PhoneNumber,

                resource.Address,

                resource.UnitId,

                resource.UnitNumber);

            var clientId = await commandService.Handle(command, ct);

            var created = await queryService.Handle(new GetClientByIdQuery(clientId), ct);

            return created is null ? Results.Problem(statusCode: 500) : Results.Created($"/api/v1/clients/{clientId}", ClientResourceFromEntityAssembler.ToResourceFromEntity(created));

        }).RequireAuthorization();



        clients.MapPut("/{id:int}", async (int id, UpdateClientResource resource, ClaimsPrincipal user, IClientCommandService commandService, IClientQueryService queryService, IoBuildDbContext db, CancellationToken ct) =>

        {

            if (!HasRole(user, "Builder")) return Results.Forbid();

            var tokenBuilderId = SelfId(user);

            if (tokenBuilderId <= 0) return Results.Unauthorized();

            var existingPut = await queryService.Handle(new GetClientByIdQuery(id), ct);

            if (existingPut is null || existingPut.BuilderId != tokenBuilderId) return Results.NotFound();

            if (resource.BuilderId > 0 && resource.BuilderId != tokenBuilderId) return Results.Forbid();

            if (!await OwnsProjectIdAsync(user, db, resource.ProjectId, ct)) return Results.NotFound();

            if (resource.UnitId.HasValue && !await OwnsUnitInProjectAsync(user, db, resource.UnitId.Value, resource.ProjectId, ct)) return Results.NotFound();



            var validation = ValidateClientData(resource.FullName, resource.Email, resource.PhoneNumber, resource.Address);

            if (!validation.isValid) return Results.Json(new { error = validation.error }, statusCode: 422);



            try

            {

                var command = new UpdateClientCommand(

                    id,

                    resource.FullName,

                    resource.ProjectName,

                    resource.AccountStatement,

                    tokenBuilderId,

                    resource.ProjectId,

                    resource.Email,

                    resource.PhoneNumber,

                    resource.Address,

                    resource.UnitId,

                    resource.UnitNumber);

                await commandService.Handle(command, ct);

                return Results.NoContent();

            }

            catch (KeyNotFoundException)

            {

                return Results.NotFound();

            }

        }).RequireAuthorization();



        clients.MapDelete("/{id:int}", async (int id, ClaimsPrincipal user, IClientCommandService commandService, IClientQueryService queryService, CancellationToken ct) =>

        {

            if (!HasRole(user, "Builder")) return Results.Forbid();

            var existingDel = await queryService.Handle(new GetClientByIdQuery(id), ct);

            if (existingDel is null || existingDel.BuilderId != SelfId(user)) return Results.NotFound();

            try

            {

                await commandService.Handle(new DeleteClientCommand(id), ct);

                return Results.NoContent();

            }

            catch (KeyNotFoundException)

            {

                return Results.NotFound();

            }

        }).RequireAuthorization();

        // ── Condominiums Compatibility Endpoints (/condominiums -> /projects) ──
        var condos = app.MapGroup("/api/v1/condominiums").WithTags("Condominiums");

        condos.MapGet("", async ([FromQuery] int? administratorId, ClaimsPrincipal user, IoBuildDbContext db, CancellationToken ct) =>
        {
            var self = SelfId(user);
            var query = db.Projects.AsQueryable();
            if (administratorId.HasValue)
            {
                query = query.Where(p => p.BuilderId == administratorId.Value);
            }
            else if (HasRole(user, "Resident") || HasRole(user, "Owner"))
            {
                var email = user.FindFirst(ClaimTypes.Email)?.Value?.ToLower();
                var residentProjectIds = await db.Units
                    .Where(u => u.OwnerId == self || (!string.IsNullOrEmpty(email) && u.OwnerEmail != null && u.OwnerEmail.ToLower() == email))
                    .Select(u => u.ProjectId)
                    .Distinct()
                    .ToListAsync(ct);
                query = query.Where(p => residentProjectIds.Contains(p.Id));
            }
            else if (self > 0)
            {
                query = query.Where(p => p.BuilderId == self);
            }
            var projectList = await query.OrderBy(p => p.Id).ToListAsync(ct);
            var projectIds = projectList.Select(p => p.Id).ToList();
            var commonAreas = await db.CommonAreas.Where(c => projectIds.Contains(c.ProjectId)).ToListAsync(ct);
            var occupiedCounts = await db.Units
                .Where(u => projectIds.Contains(u.ProjectId) && (!string.IsNullOrEmpty(u.OwnerEmail) || u.OwnerId.HasValue))
                .GroupBy(u => u.ProjectId)
                .Select(g => new { ProjectId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.ProjectId, g => g.Count, ct);

            var result = projectList.Select(p => new
            {
                p.Id,
                AdministratorId = p.AdministratorId,
                p.BuilderId,
                p.Name,
                p.Description,
                p.Address,
                Location = p.Address,
                District = p.District ?? "Jesús María",
                p.TotalUnits,
                OccupiedUnits = occupiedCounts.GetValueOrDefault(p.Id, 0),
                WaterSupplyCode = p.SedapalSupplyCode,
                ElectricitySupplyCode = p.LuzDelSurSupplyCode,
                p.SedapalSupplyCode,
                p.LuzDelSurSupplyCode,
                Status = string.Equals(p.Status, "Activo", StringComparison.OrdinalIgnoreCase) ? "active" : p.Status.ToLowerInvariant(),
                p.ImageUrl,
                p.StructureDefined,
                p.CreatedAt,
                CommonAreas = commonAreas.Where(c => c.ProjectId == p.Id).Select(c => new
                {
                    c.Id,
                    c.Name,
                    Type = c.Subsystem.ToString(),
                    c.Location,
                    c.Description
                })
            });
            return Results.Ok(result);
        }).RequireAuthorization();

        condos.MapGet("/{id:int}", async (int id, ClaimsPrincipal user, IoBuildDbContext db, CancellationToken ct) =>
        {
            var p = await db.Projects.FindAsync([id], ct);
            if (p is null || !await CanReadProjectAsync(user, db, p, ct)) return Results.NotFound();
            var commonAreas = await db.CommonAreas.Where(c => c.ProjectId == id).ToListAsync(ct);
            var occupiedUnits = await db.Units.CountAsync(u => u.ProjectId == id && (!string.IsNullOrEmpty(u.OwnerEmail) || u.OwnerId.HasValue), ct);
            return Results.Ok(new
            {
                p.Id,
                AdministratorId = p.AdministratorId,
                p.BuilderId,
                p.Name,
                p.Description,
                p.Address,
                Location = p.Address,
                District = p.District ?? "Jesús María",
                p.TotalUnits,
                OccupiedUnits = occupiedUnits,
                WaterSupplyCode = p.SedapalSupplyCode,
                ElectricitySupplyCode = p.LuzDelSurSupplyCode,
                p.SedapalSupplyCode,
                p.LuzDelSurSupplyCode,
                Status = string.Equals(p.Status, "Activo", StringComparison.OrdinalIgnoreCase) ? "active" : p.Status.ToLowerInvariant(),
                p.ImageUrl,
                p.StructureDefined,
                p.CreatedAt,
                CommonAreas = commonAreas.Select(c => new
                {
                    c.Id,
                    c.Name,
                    Type = c.Subsystem.ToString(),
                    c.Location,
                    c.Description
                })
            });
        }).RequireAuthorization();

        condos.MapPost("", async (CreateProjectRequest request, ClaimsPrincipal user, CoreBusinessService service, CancellationToken ct) =>
        {
            var tokenBuilderId = SelfId(user);
            var builderId = request.BuilderId.HasValue && request.BuilderId.Value > 0 ? request.BuilderId.Value : tokenBuilderId;
            var project = await service.CreateProjectAsync(request.Name, request.Description, request.Location, request.TotalUnits, builderId, request.ImageUrl, ct);
            return Results.Created($"/api/v1/condominiums/{project.Id}", project);
        }).RequireAuthorization();

        condos.MapPut("/{id:int}", async (int id, CreateProjectRequest request, ClaimsPrincipal user, IoBuildDbContext db, CancellationToken ct) =>
        {
            var item = await db.Projects.FindAsync([id], ct);
            if (item is null || !OwnsProject(user, item)) return Results.NotFound();
            item.Name = request.Name;
            item.Description = request.Description;
            item.Location = request.Location;
            item.TotalUnits = request.TotalUnits;
            await db.SaveChangesAsync(ct);
            return Results.Ok(item);
        }).RequireAuthorization();

        condos.MapPatch("/{id:int}", async (int id, [FromBody] System.Text.Json.JsonElement body, ClaimsPrincipal user, IoBuildDbContext db, CancellationToken ct) =>
        {
            var p = await db.Projects.FindAsync([id], ct);
            if (p is null || !OwnsProject(user, p)) return Results.NotFound();
            if (body.TryGetProperty("status", out var statusProp))
            {
                p.Status = statusProp.GetString() ?? p.Status;
                await db.SaveChangesAsync(ct);
            }
            return Results.Ok(p);
        }).RequireAuthorization();

        condos.MapDelete("/{id:int}", async (int id, ClaimsPrincipal user, IoBuildDbContext db, CancellationToken ct) =>
        {
            var item = await db.Projects.FindAsync([id], ct);
            if (item is null || !OwnsProject(user, item)) return Results.NotFound();

            var clients = await db.Clients.Where(c => c.ProjectId == id).ToListAsync(ct);
            if (clients.Count > 0) db.Clients.RemoveRange(clients);

            var devices = await db.Devices.Where(d => d.ProjectId == id).ToListAsync(ct);
            if (devices.Count > 0) db.Devices.RemoveRange(devices);

            var devProjections = await db.DeviceProjections.Where(d => d.ProjectId == id).ToListAsync(ct);
            if (devProjections.Count > 0) db.DeviceProjections.RemoveRange(devProjections);

            var units = await db.Units.Where(u => u.ProjectId == id).ToListAsync(ct);
            if (units.Count > 0) db.Units.RemoveRange(units);

            var unitProjections = await db.UnitProjections.Where(u => u.ProjectId == id).ToListAsync(ct);
            if (unitProjections.Count > 0) db.UnitProjections.RemoveRange(unitProjections);

            var projectProjections = await db.ProjectProjections.Where(p => p.ProjectId == id).ToListAsync(ct);
            if (projectProjections.Count > 0) db.ProjectProjections.RemoveRange(projectProjections);

            db.Projects.Remove(item);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).RequireAuthorization();

        // ── Residents Directory Endpoint ──
        app.MapGet("/api/v1/residents", async (
            [FromQuery] int? condominiumId,
            [FromQuery] int? projectId,
            ClaimsPrincipal user,
            IoBuildDbContext db,
            CancellationToken ct) =>
        {
            if (!IsAdministrator(user))
            {
                return Results.Forbid();
            }

            var targetProjectId = condominiumId ?? projectId ?? 1;
            var project = await db.Projects.FindAsync([targetProjectId], ct);
            // La administradora solo consulta el padrón de los condominios que gestiona
            if (project is null || project.BuilderId != SelfId(user)) return Results.Forbid();

            var units = await db.Units.Where(u => u.ProjectId == targetProjectId && (!string.IsNullOrEmpty(u.ResidentFullName) || u.OwnerId.HasValue)).ToListAsync(ct);
            var ownerIds = units.Where(u => u.OwnerId.HasValue).Select(u => u.OwnerId!.Value).ToList();
            var profilesByUserId = await db.Profiles.Where(p => ownerIds.Contains(p.UserId)).ToDictionaryAsync(p => p.UserId, ct);

            // Sin datos de relleno: un campo ausente viaja como null y la interfaz lo muestra como no registrado
            var residents = units.Select(u =>
            {
                var profile = u.OwnerId.HasValue && profilesByUserId.TryGetValue(u.OwnerId.Value, out var prof) ? prof : null;
                return new
                {
                    id = u.Id,
                    userId = u.OwnerId,
                    fullName = u.ResidentFullName ?? profile?.Name ?? $"Residente Dpto. {u.UnitNumber}",
                    dni = (string?)null,
                    email = u.ResidentEmail ?? u.OwnerEmail,
                    phoneNumber = profile?.PhoneNumber,
                    photoUrl = profile?.PhotoUrl,
                    condominiumId = u.ProjectId,
                    condominiumName = project.Name,
                    unitId = u.Id,
                    unitNumber = u.UnitNumber,
                    floor = u.FloorNumber,
                    areaM2 = u.AreaM2,
                    aliquotPercentage = u.AliquotPercentage,
                    relation = "owner",
                    collectionStatus = "current"
                };
            });
            return Results.Ok(residents);
        }).RequireAuthorization();
    }
}

