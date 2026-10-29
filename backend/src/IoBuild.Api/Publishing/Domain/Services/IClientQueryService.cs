using IoBuild.Api.Persistence;
using IoBuild.Api.Publishing.Domain.Services.Queries;

namespace IoBuild.Api.Publishing.Domain.Services;

public interface IClientQueryService
{
    Task<IEnumerable<Client>> Handle(GetAllClientsQuery query, CancellationToken ct = default);
    Task<Client?> Handle(GetClientByIdQuery query, CancellationToken ct = default);
    Task<IEnumerable<Client>> Handle(GetClientsByBuilderIdQuery query, CancellationToken ct = default);
    Task<IEnumerable<Client>> Handle(GetClientsByProjectIdQuery query, CancellationToken ct = default);
}
