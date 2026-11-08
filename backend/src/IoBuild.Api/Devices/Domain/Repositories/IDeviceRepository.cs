using IoBuild.Api.Persistence;

namespace IoBuild.Api.Devices.Domain.Repositories;

public interface IDeviceRepository
{
    Task<Device?> FindAsync(int id, CancellationToken ct = default);
    Task AddAsync(Device device, CancellationToken ct = default);
}
