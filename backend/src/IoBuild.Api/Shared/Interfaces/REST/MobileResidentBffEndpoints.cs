using System.Security.Claims;
using IoBuild.Api.Analytics.Application.Internal.QueryServices;
using IoBuild.Api.Analytics.Domain.Services;
using IoBuild.Api.Persistence;
using IoBuild.Api.Proration.Domain.Model.Aggregates;
using IoBuild.Api.Publishing.Domain.Model.Aggregates;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace IoBuild.Api.Shared.Interfaces.REST;

/// <summary>
/// Backend for Frontend (BFF) exclusivo para la aplicación móvil de Android del Propietario/Residente.
/// Consolida y agrega múltiples Bounded Contexts (IAM, Publishing, Proration, Analytics, Devices, Profiles)
/// en respuestas optimizadas de una sola llamada por vista móvil, garantizando aislamiento estricto de unidad (Ley N.° 27157).
/// </summary>
public static class MobileResidentBffEndpoints
{
    private static readonly object AiRateLimitLock = new();
    private static readonly TimeSpan AiRateLimitWindow = TimeSpan.FromMinutes(1);
    private const int DefaultAiMaxRequestsPerMinute = 5;

    private static int SelfId(ClaimsPrincipal user) =>
        int.TryParse(
            user.FindFirst(ClaimTypes.Sid)?.Value
            ?? user.FindFirst("sid")?.Value
            ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? user.FindFirst("sub")?.Value,
            out var id) ? id : 0;

    private static string? SelfEmail(ClaimsPrincipal user) =>
        (user.FindFirst(ClaimTypes.Email)?.Value ?? user.FindFirst("email")?.Value)?.Trim().ToLowerInvariant();

    private static bool IsResidentOrOwner(ClaimsPrincipal user)
    {
        var roles = user.FindAll(ClaimTypes.Role)
            .Concat(user.FindAll("role"))
            .Select(c => c.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return roles.Contains("Resident") || roles.Contains("Owner") || roles.Contains("Residente") || roles.Contains("Propietario");
    }

    private static async Task<List<Unit>> GetResidentUnitsAsync(ClaimsPrincipal user, IoBuildDbContext db, CancellationToken ct)
    {
        var self = SelfId(user);
        var email = SelfEmail(user);

        var directUnits = await db.Units
            .Where(u => u.OwnerId == self || (email != null && u.OwnerEmail != null && u.OwnerEmail.ToLower() == email))
            .ToListAsync(ct);

        var projectedUnitIds = await db.UnitOwnerProjections
            .Where(p => p.OwnerUserId == self)
            .Select(p => p.UnitId)
            .ToListAsync(ct);

        if (projectedUnitIds.Count > 0)
        {
            var existingIds = directUnits.Select(u => u.Id).ToHashSet();
            var missingIds = projectedUnitIds.Where(id => !existingIds.Contains(id)).ToList();
            if (missingIds.Count > 0)
            {
                var extraUnits = await db.Units.Where(u => missingIds.Contains(u.Id)).ToListAsync(ct);
                directUnits.AddRange(extraUnits);
            }
        }

        return directUnits.OrderBy(u => u.Id).ToList();
    }

    private static string EffectivePaymentStatus(StatementOfAccount s, BillingPeriod? p)
    {
        var raw = (s.Status ?? "Pending").Trim().ToLowerInvariant();
        if (raw == "paid") return "paid";
        if (raw == "overdue") return "overdue";
        if (p is not null && p.DueDate.Date < DateTime.UtcNow.Date) return "overdue";
        return "pending";
    }

    private static int DaysOverdue(StatementOfAccount s, BillingPeriod? p)
    {
        var eff = EffectivePaymentStatus(s, p);
        if (eff != "overdue" || p is null) return 0;
        return Math.Max(0, (int)(DateTime.UtcNow.Date - p.DueDate.Date).TotalDays);
    }

    private static string MapSubsystemToType(string? subsystem) =>
        (subsystem ?? string.Empty).Trim() switch
        {
            "MainCistern" => "cistern",
            "PressurePumpsRoom" => "pump_room",
            "ElevatorsBank" => "elevator",
            "GeneralElectricalPanel" => "panel",
            _ => (subsystem ?? "cistern").ToLowerInvariant()
        };

    private static bool TryAcquireAiExplainSlot(
        int userId,
        IMemoryCache cache,
        IConfiguration configuration,
        out int retryAfterSeconds)
    {
        var maxRequests = Math.Max(1, configuration.GetValue("MobileBff:AiExplainMaxRequestsPerMinute", DefaultAiMaxRequestsPerMinute));
        var now = DateTime.UtcNow;
        var cacheKey = $"mobile-bff:ai-explain:ratelimit:{userId}";

        lock (AiRateLimitLock)
        {
            var timestamps = cache.GetOrCreate(cacheKey, entry =>
            {
                entry.SlidingExpiration = TimeSpan.FromMinutes(2);
                return new Queue<DateTime>();
            })!;

            while (timestamps.Count > 0 && now - timestamps.Peek() >= AiRateLimitWindow)
            {
                timestamps.Dequeue();
            }

            if (timestamps.Count >= maxRequests)
            {
                var oldest = timestamps.Peek();
                retryAfterSeconds = Math.Max(1, (int)Math.Ceiling((AiRateLimitWindow - (now - oldest)).TotalSeconds));
                return false;
            }

            timestamps.Enqueue(now);
            retryAfterSeconds = 0;
            return true;
        }
    }

    public static void MapMobileResidentBffEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/mobile/resident").WithTags("Mobile BFF (Resident)");

        // ── 1. BFF Overview ("Mi Consumo" / Inicio Propietario) ──
        // Agrega en 1 sola llamada: unidad asignada, condominio, última liquidación, últimas 3 cuotas,
        // consumo de agua/energía de áreas comunes vs mes anterior, proyección, desglose por área común y serie diaria.
        group.MapGet("/overview", async (
            ClaimsPrincipal user,
            IoBuildDbContext db,
            CancellationToken ct) =>
        {
            if (!IsResidentOrOwner(user))
            {
                return Results.Json(new { error = "Esta aplicación móvil es exclusiva para propietarios/residentes." }, statusCode: StatusCodes.Status403Forbidden);
            }

            var units = await GetResidentUnitsAsync(user, db, ct);
            var myUnit = units.FirstOrDefault();
            if (myUnit is null)
            {
                return Results.Ok(new
                {
                    hasUnit = false,
                    unit = (object?)null,
                    condominium = (object?)null,
                    latestStatement = (object?)null,
                    recentStatements = Array.Empty<object>(),
                    commonConsumption = (object?)null
                });
            }

            var project = await db.Projects.FindAsync([myUnit.ProjectId], ct);
            var projectIds = units.Select(u => u.ProjectId).Distinct().ToList();
            var periods = await db.BillingPeriods
                .Where(p => projectIds.Contains(p.ProjectId))
                .ToDictionaryAsync(p => p.Id, ct);

            var unitIds = units.Select(u => u.Id).ToList();
            var statements = await db.StatementOfAccounts
                .Where(s => unitIds.Contains(s.UnitId))
                .ToListAsync(ct);

            var mappedStatements = statements
                .Select(s =>
                {
                    var p = periods.GetValueOrDefault(s.BillingPeriodId);
                    var effStatus = EffectivePaymentStatus(s, p);
                    var isPaid = effStatus == "paid";
                    return new
                    {
                        id = s.Id,
                        settlementLineId = s.Id,
                        unitId = s.UnitId,
                        condominiumId = s.ProjectId,
                        period = p?.PeriodCode,
                        amount = s.IndividualAmountSoles,
                        dueDate = p?.DueDate.ToString("yyyy-MM-dd"),
                        paidAt = s.PaidAtUtc?.ToString("yyyy-MM-dd"),
                        status = s.Status.ToLowerInvariant(),
                        effectiveStatus = effStatus,
                        daysOverdue = DaysOverdue(s, p),
                        method = isPaid && !string.IsNullOrWhiteSpace(s.PaymentMethod) ? s.PaymentMethod : null,
                        reference = isPaid && !string.IsNullOrWhiteSpace(s.PaymentReference) ? s.PaymentReference : null
                    };
                })
                .OrderByDescending(x => x.period ?? string.Empty)
                .ThenByDescending(x => x.id)
                .ToList();

            var latestStatement = mappedStatements.FirstOrDefault();
            var recentStatements = mappedStatements.Take(3).ToList();

            // Cálculo de consumo comunal del mes en curso y mismo tramo del mes anterior
            var now = DateTime.UtcNow;
            var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var prevFrom = monthStart.AddMonths(-1);
            var daysInPrevMonth = DateTime.DaysInMonth(prevFrom.Year, prevFrom.Month);
            var prevTo = new DateTime(prevFrom.Year, prevFrom.Month, Math.Min(now.Day, daysInPrevMonth), now.Hour, now.Minute, now.Second, DateTimeKind.Utc);

            var excludedWaterIds = await ConsumptionFactsBuilder.NonIntakeWaterSensorIdsAsync(db, myUnit.ProjectId, ct);

            var currentPerDevice = await db.ConsumptionRecords
                .Where(c => c.ProjectId == myUnit.ProjectId && c.TimestampUtc >= monthStart && c.TimestampUtc <= now)
                .GroupBy(c => new { c.DeviceId, c.MetricType })
                .Select(g => new
                {
                    g.Key.DeviceId,
                    g.Key.MetricType,
                    Delta = g.Max(c => c.Value) - g.Min(c => c.Value),
                    LastReadingAt = g.Max(c => c.TimestampUtc)
                })
                .ToListAsync(ct);

            var prevPerDevice = await db.ConsumptionRecords
                .Where(c => c.ProjectId == myUnit.ProjectId && c.TimestampUtc >= prevFrom && c.TimestampUtc <= prevTo)
                .GroupBy(c => new { c.DeviceId, c.MetricType })
                .Select(g => new
                {
                    g.Key.DeviceId,
                    g.Key.MetricType,
                    Delta = g.Max(c => c.Value) - g.Min(c => c.Value)
                })
                .ToListAsync(ct);

            double currentWaterM3 = Math.Round(currentPerDevice.Where(r => r.MetricType == "WaterM3" && !excludedWaterIds.Contains(r.DeviceId)).Sum(r => r.Delta), 2);
            double currentEnergyKwh = Math.Round(currentPerDevice.Where(r => r.MetricType == "EnergyKwh").Sum(r => r.Delta), 2);

            double prevWaterM3 = Math.Round(prevPerDevice.Where(r => r.MetricType == "WaterM3" && !excludedWaterIds.Contains(r.DeviceId)).Sum(r => r.Delta), 2);
            double prevEnergyKwh = Math.Round(prevPerDevice.Where(r => r.MetricType == "EnergyKwh").Sum(r => r.Delta), 2);

            double? waterChangePct = prevWaterM3 > 0 ? Math.Round(((currentWaterM3 - prevWaterM3) / prevWaterM3) * 100.0, 1) : null;
            double? energyChangePct = prevEnergyKwh > 0 ? Math.Round(((currentEnergyKwh - prevEnergyKwh) / prevEnergyKwh) * 100.0, 1) : null;

            int dayOfMonth = Math.Max(1, now.Day);
            int totalDaysInMonth = DateTime.DaysInMonth(now.Year, now.Month);
            double projectedWaterM3 = Math.Round((currentWaterM3 / dayOfMonth) * totalDaysInMonth, 1);
            double projectedEnergyKwh = Math.Round((currentEnergyKwh / dayOfMonth) * totalDaysInMonth, 1);

            var commonAreas = await db.CommonAreas.Where(c => c.ProjectId == myUnit.ProjectId).ToListAsync(ct);
            var devices = await db.Devices.Where(d => d.ProjectId == myUnit.ProjectId && d.Status != "Unlinked").ToListAsync(ct);

            var areaRows = commonAreas.Select(area =>
            {
                var areaType = MapSubsystemToType(area.Subsystem.ToString());
                var meters = devices.Where(d => d.Type != "GatewayHub" && MapSubsystemToType(d.Subsystem) == areaType).ToList();
                var meterIds = meters.Select(m => m.Id).ToHashSet();
                var totals = currentPerDevice.Where(c => meterIds.Contains(c.DeviceId)).ToList();

                var areaWater = Math.Round(totals.Where(t => t.MetricType == "WaterM3" && !excludedWaterIds.Contains(t.DeviceId)).Sum(t => t.Delta), 2);
                var areaEnergy = Math.Round(totals.Where(t => t.MetricType == "EnergyKwh").Sum(t => t.Delta), 2);
                var lastReading = totals.Count > 0 ? totals.Max(t => (DateTime?)t.LastReadingAt) : null;
                var onlineCount = meters.Count(m => string.Equals(m.Status, "Online", StringComparison.OrdinalIgnoreCase));

                return new
                {
                    id = area.Id,
                    name = area.Name,
                    type = areaType,
                    subsystem = area.Subsystem.ToString(),
                    location = area.Location,
                    monitored = meters.Count > 0,
                    metersOnline = onlineCount,
                    metersTotal = meters.Count,
                    waterM3 = areaWater,
                    energyKwh = areaEnergy,
                    waterSharePct = currentWaterM3 > 0 ? Math.Round((areaWater / currentWaterM3) * 100.0, 1) : 0,
                    energySharePct = currentEnergyKwh > 0 ? Math.Round((areaEnergy / currentEnergyKwh) * 100.0, 1) : 0,
                    lastReadingAt = lastReading.HasValue ? DateTime.SpecifyKind(lastReading.Value, DateTimeKind.Utc).ToString("o") : null
                };
            }).ToList();

            var dailyRaw = await db.ConsumptionRecords
                .Where(c => c.ProjectId == myUnit.ProjectId && c.TimestampUtc >= monthStart && c.TimestampUtc <= now)
                .Where(c => !(c.MetricType == "WaterM3" && excludedWaterIds.Contains(c.DeviceId)))
                .GroupBy(c => new { c.DeviceId, c.MetricType, Day = c.TimestampUtc.Date })
                .Select(g => new { g.Key.MetricType, g.Key.Day, Delta = g.Max(c => c.Value) - g.Min(c => c.Value) })
                .ToListAsync(ct);

            var dailySeries = dailyRaw
                .GroupBy(r => r.Day)
                .OrderBy(g => g.Key)
                .Select(g => new
                {
                    date = g.Key.ToString("yyyy-MM-dd"),
                    dayLabel = g.Key.ToString("dd"),
                    waterM3 = Math.Round(g.Where(r => r.MetricType == "WaterM3").Sum(r => r.Delta), 2),
                    energyKwh = Math.Round(g.Where(r => r.MetricType == "EnergyKwh").Sum(r => r.Delta), 2)
                })
                .ToList();

            return Results.Ok(new
            {
                hasUnit = true,
                unit = new
                {
                    id = myUnit.Id,
                    unitNumber = myUnit.UnitNumber,
                    floor = myUnit.Floor,
                    areaM2 = myUnit.AreaM2,
                    aliquotPercentage = myUnit.AliquotPercentage,
                    residentFullName = !string.IsNullOrWhiteSpace(myUnit.ResidentFullName) ? myUnit.ResidentFullName : null
                },
                condominium = project is null ? null : new
                {
                    id = project.Id,
                    name = project.Name,
                    address = !string.IsNullOrWhiteSpace(project.Address) ? project.Address : null,
                    district = !string.IsNullOrWhiteSpace(project.District) ? project.District : null,
                    totalUnits = project.TotalUnits
                },
                latestStatement,
                recentStatements,
                commonConsumption = new
                {
                    period = $"{now:yyyy-MM}",
                    waterM3 = currentWaterM3,
                    energyKwh = currentEnergyKwh,
                    previousWaterM3 = prevWaterM3,
                    previousEnergyKwh = prevEnergyKwh,
                    waterChangePct,
                    energyChangePct,
                    projectedWaterM3,
                    projectedEnergyKwh,
                    monitoredAreasCount = areaRows.Count(a => a.monitored),
                    totalAreasCount = areaRows.Count,
                    areas = areaRows,
                    dailySeries
                }
            });
        }).RequireAuthorization();

        // ── 2. BFF Account Statement ("Estado de Cuenta" Propietario) ──
        // Devuelve la tarjeta de la unidad asignada (Dpto, Condominio, Alícuota Legal, Titular),
        // la cuota pendiente destacada (si existe) y el historial completo de cuotas.
        group.MapGet("/account-statement", async (
            ClaimsPrincipal user,
            IoBuildDbContext db,
            CancellationToken ct) =>
        {
            if (!IsResidentOrOwner(user))
            {
                return Results.Json(new { error = "Esta aplicación móvil es exclusiva para propietarios/residentes." }, statusCode: StatusCodes.Status403Forbidden);
            }

            var self = SelfId(user);
            var units = await GetResidentUnitsAsync(user, db, ct);
            var myUnit = units.FirstOrDefault();
            if (myUnit is null)
            {
                return Results.Ok(new
                {
                    unit = (object?)null,
                    pendingStatement = (object?)null,
                    history = Array.Empty<object>()
                });
            }

            var project = await db.Projects.FindAsync([myUnit.ProjectId], ct);
            var profile = await db.Profiles.FirstOrDefaultAsync(p => p.UserId == self, ct);
            var projectIds = units.Select(u => u.ProjectId).Distinct().ToList();
            var periods = await db.BillingPeriods
                .Where(p => projectIds.Contains(p.ProjectId))
                .ToDictionaryAsync(p => p.Id, ct);

            var unitIds = units.Select(u => u.Id).ToList();
            var statements = await db.StatementOfAccounts
                .Where(s => unitIds.Contains(s.UnitId))
                .ToListAsync(ct);

            var history = statements
                .Select(s =>
                {
                    var p = periods.GetValueOrDefault(s.BillingPeriodId);
                    var effStatus = EffectivePaymentStatus(s, p);
                    var isPaid = effStatus == "paid";
                    return new
                    {
                        id = s.Id,
                        settlementLineId = s.Id,
                        unitId = s.UnitId,
                        condominiumId = s.ProjectId,
                        period = p?.PeriodCode,
                        concept = "Cuota de áreas comunes",
                        amount = s.IndividualAmountSoles,
                        dueDate = p?.DueDate.ToString("yyyy-MM-dd"),
                        paidAt = s.PaidAtUtc?.ToString("yyyy-MM-dd"),
                        status = s.Status.ToLowerInvariant(),
                        effectiveStatus = effStatus,
                        daysOverdue = DaysOverdue(s, p),
                        method = isPaid && !string.IsNullOrWhiteSpace(s.PaymentMethod) ? s.PaymentMethod : null,
                        reference = isPaid && !string.IsNullOrWhiteSpace(s.PaymentReference) ? s.PaymentReference : null
                    };
                })
                .OrderByDescending(x => x.period ?? string.Empty)
                .ThenByDescending(x => x.id)
                .ToList();

            var pendingStatement = history
                .Where(p => p.effectiveStatus != "paid")
                .OrderBy(p => p.dueDate ?? "9999-99-99")
                .FirstOrDefault();

            var holderName = !string.IsNullOrWhiteSpace(myUnit.ResidentFullName)
                ? myUnit.ResidentFullName
                : (!string.IsNullOrWhiteSpace(profile?.Name) ? profile.Name : null);

            return Results.Ok(new
            {
                unit = new
                {
                    id = myUnit.Id,
                    unitNumber = myUnit.UnitNumber,
                    condominiumId = myUnit.ProjectId,
                    condominiumName = !string.IsNullOrWhiteSpace(project?.Name) ? project.Name : null,
                    aliquotPercentage = myUnit.AliquotPercentage,
                    holderName,
                    floor = myUnit.Floor,
                    areaM2 = myUnit.AreaM2
                },
                pendingStatement,
                history
            });
        }).RequireAuthorization();

        // ── 3. BFF Fee Explanation ("Mi Cuota Actual" / "¿Cómo se calculó mi cuota?") ──
        // Devuelve los periodos disponibles del propietario y la explicación en 3 pasos de la cuota seleccionada (o la más reciente).
        group.MapGet("/fee-explanation", async (
            [FromQuery] int? lineId,
            ClaimsPrincipal user,
            IoBuildDbContext db,
            CancellationToken ct) =>
        {
            if (!IsResidentOrOwner(user))
            {
                return Results.Json(new { error = "Esta aplicación móvil es exclusiva para propietarios/residentes." }, statusCode: StatusCodes.Status403Forbidden);
            }

            var units = await GetResidentUnitsAsync(user, db, ct);
            if (units.Count == 0)
            {
                return Results.Ok(new { availableLines = Array.Empty<object>(), explanation = (object?)null });
            }

            var unitIds = units.Select(u => u.Id).ToList();
            var statements = await db.StatementOfAccounts
                .Where(s => unitIds.Contains(s.UnitId))
                .ToListAsync(ct);

            if (statements.Count == 0)
            {
                return Results.Ok(new { availableLines = Array.Empty<object>(), explanation = (object?)null });
            }

            var periodIds = statements.Select(s => s.BillingPeriodId).Distinct().ToList();
            var periods = await db.BillingPeriods
                .Where(p => periodIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, ct);

            var sortedStatements = statements
                .OrderByDescending(s => periods.GetValueOrDefault(s.BillingPeriodId)?.PeriodCode ?? string.Empty)
                .ThenByDescending(s => s.Id)
                .ToList();

            var availableLines = sortedStatements.Select(s =>
            {
                var p = periods.GetValueOrDefault(s.BillingPeriodId);
                return new
                {
                    lineId = s.Id,
                    period = p?.PeriodCode,
                    amount = s.IndividualAmountSoles,
                    status = EffectivePaymentStatus(s, p)
                };
            }).ToList();

            var selected = lineId.HasValue
                ? sortedStatements.FirstOrDefault(s => s.Id == lineId.Value)
                : sortedStatements.FirstOrDefault();

            if (selected is null)
            {
                return Results.Forbid();
            }

            var period = periods.GetValueOrDefault(selected.BillingPeriodId);
            if (period is null)
            {
                return Results.NotFound();
            }

            var facts = await new ConsumptionFactsBuilder(db).BuildAsync(selected.ProjectId, selected.UnitId, period.PeriodCode, ct);

            var explanation = new
            {
                lineId = selected.Id,
                unitNumber = selected.UnitNumber,
                period = period.PeriodCode,
                previousPeriod = facts.PreviousPeriod,
                status = EffectivePaymentStatus(selected, period),
                dueDate = period.DueDate.ToString("yyyy-MM-dd"),
                paidAt = selected.PaidAtUtc?.ToString("yyyy-MM-dd"),
                balance = selected.BalanceSoles,
                aliquot = selected.AliquotPercentage,
                common = new
                {
                    water = period.MacroWaterAmountSoles,
                    energy = period.MacroEnergyAmountSoles,
                    other = period.OtherCommonExpensesSoles,
                    total = period.TotalCommonExpenseSoles,
                    waterM3 = period.MacroWaterM3,
                    energyKwh = period.MacroEnergyKwh,
                    waterReceiptUrl = period.WaterReceiptImageUrl,
                    energyReceiptUrl = period.EnergyReceiptImageUrl
                },
                mine = new
                {
                    water = selected.IndividualWaterSoles,
                    energy = selected.IndividualEnergySoles,
                    other = selected.IndividualOtherExpensesSoles,
                    total = selected.IndividualAmountSoles
                },
                previous = facts.PreviousStatement is { } last
                    ? new
                    {
                        waterSoles = last.WaterSoles,
                        energySoles = last.EnergySoles,
                        otherSoles = last.OtherSoles,
                        totalSoles = last.TotalSoles
                    }
                    : null,
                alerts = facts.Alerts.Where(a => a.EstimatedOvercostSoles > 0).Select(a => new
                {
                    alertType = a.Type,
                    title = a.Title,
                    detectedAt = a.DetectedAtUtc.ToString("yyyy-MM-dd"),
                    isOpen = a.Status == "Active",
                    overcost = a.EstimatedOvercostSoles,
                    myShare = Math.Round(a.EstimatedOvercostSoles * selected.AliquotPercentage / 100m, 2)
                }).ToList()
            };

            return Results.Ok(new
            {
                availableLines,
                explanation
            });
        }).RequireAuthorization();

        // ── 4. BFF Profile ("Mi Perfil" Propietario) ──
        // Consolida IAM + Profile + Unidad asignada en una sola respuesta sin datos de relleno.
        group.MapGet("/profile", async (
            ClaimsPrincipal user,
            IoBuildDbContext db,
            CancellationToken ct) =>
        {
            if (!IsResidentOrOwner(user))
            {
                return Results.Json(new { error = "Esta aplicación móvil es exclusiva para propietarios/residentes." }, statusCode: StatusCodes.Status403Forbidden);
            }

            var self = SelfId(user);
            var email = SelfEmail(user);
            var profile = await db.Profiles.FirstOrDefaultAsync(p => p.UserId == self, ct);
            var units = await GetResidentUnitsAsync(user, db, ct);
            var myUnit = units.FirstOrDefault();
            var project = myUnit is not null ? await db.Projects.FindAsync([myUnit.ProjectId], ct) : null;
            var client = myUnit is not null ? await db.Clients.FirstOrDefaultAsync(c => c.UnitId == myUnit.Id, ct) : null;

            string? fullName = !string.IsNullOrWhiteSpace(profile?.Name)
                ? profile.Name
                : (!string.IsNullOrWhiteSpace(myUnit?.ResidentFullName)
                    ? myUnit.ResidentFullName
                    : (!string.IsNullOrWhiteSpace(client?.FullName) ? client.FullName : null));

            string? phoneNumber = !string.IsNullOrWhiteSpace(profile?.PhoneNumber)
                ? profile.PhoneNumber
                : (!string.IsNullOrWhiteSpace(client?.PhoneNumber) ? client.PhoneNumber : null);

            string? secondEmail = !string.IsNullOrWhiteSpace(profile?.SecondEmail)
                ? profile.SecondEmail
                : null;

            string? photoUrl = !string.IsNullOrWhiteSpace(profile?.PhotoUrl)
                ? profile.PhotoUrl
                : (!string.IsNullOrWhiteSpace(profile?.CloudinaryReference) ? profile.CloudinaryReference : null);

            return Results.Ok(new
            {
                profileId = profile?.Id,
                userId = self,
                fullName,
                email = !string.IsNullOrWhiteSpace(email) ? email : null,
                secondEmail,
                phoneNumber,
                photoUrl,
                role = "resident",
                unit = myUnit is null ? null : new
                {
                    id = myUnit.Id,
                    unitNumber = myUnit.UnitNumber,
                    condominiumName = !string.IsNullOrWhiteSpace(project?.Name) ? project.Name : null,
                    floor = myUnit.Floor,
                    areaM2 = myUnit.AreaM2,
                    aliquotPercentage = myUnit.AliquotPercentage
                },
                sessions = (object?)null
            });
        }).RequireAuthorization();

        // ── 5. BFF Ask AI ("Pregúntale a IoBuild" desde la app móvil con límite de solicitudes por usuario) ──
        group.MapPost("/ai-explain", async (
            [FromBody] MobileAiQuestionRequest request,
            ClaimsPrincipal user,
            HttpContext httpContext,
            IMemoryCache cache,
            IConfiguration configuration,
            IoBuildDbContext db,
            IAiCognitiveService cognitive,
            CancellationToken ct) =>
        {
            if (!IsResidentOrOwner(user))
            {
                return Results.Json(new { error = "Esta aplicación móvil es exclusiva para propietarios/residentes." }, statusCode: StatusCodes.Status403Forbidden);
            }

            var self = SelfId(user);
            if (!TryAcquireAiExplainSlot(self, cache, configuration, out var retryAfterSeconds))
            {
                httpContext.Response.Headers.RetryAfter = retryAfterSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
                return Results.Json(
                    new
                    {
                        error = "Has alcanzado el límite de consultas por minuto al asistente IA. Intenta nuevamente en unos segundos.",
                        retryAfterSeconds
                    },
                    statusCode: StatusCodes.Status429TooManyRequests);
            }

            var units = await GetResidentUnitsAsync(user, db, ct);
            var myUnit = units.FirstOrDefault();
            if (myUnit is null)
            {
                return Results.BadRequest(new { error = "No tienes una unidad asignada." });
            }

            var question = string.IsNullOrWhiteSpace(request.Question)
                ? "¿Por qué varió mi cuota este mes?"
                : request.Question.Trim();
            if (question.Length > 500) question = question[..500];

            var language = request.Language == "en" ? "en" : "es";
            var result = await cognitive.ExplainConsumptionVariationAsync(
                myUnit.ProjectId,
                myUnit.Id,
                request.BillingPeriod ?? string.Empty,
                question,
                language);

            return Results.Ok(result);
        }).RequireAuthorization();
    }
}

public sealed record MobileAiQuestionRequest(string? Question = null, string? BillingPeriod = null, string? Language = "es");
