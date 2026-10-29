using IoBuild.Api.Publishing.Domain.Services.Commands;

namespace IoBuild.Api.Publishing.Domain.Services;

public interface IClientCommandService
{
    Task<int> Handle(CreateClientCommand command, CancellationToken ct = default);
    Task Handle(UpdateClientCommand command, CancellationToken ct = default);
    Task Handle(DeleteClientCommand command, CancellationToken ct = default);
}
