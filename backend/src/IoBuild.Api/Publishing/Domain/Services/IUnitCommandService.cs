using IoBuild.Api.Publishing.Domain.Services.Commands;

namespace IoBuild.Api.Publishing.Domain.Services;

public interface IUnitCommandService
{
    Task<int> Handle(CreateUnitCommand command, CancellationToken ct = default);
    Task Handle(AssignUnitOwnerEmailCommand command, CancellationToken ct = default);
}
