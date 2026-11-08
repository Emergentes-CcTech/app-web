namespace IoBuild.Api.Shared.Domain.Model;

/// <summary>
/// Marker for aggregate roots. All top-level aggregates (IamUser, Project,
/// Device, etc.) are aggregate roots. Used for documentation and future
/// generic repository constraints.
/// </summary>
public interface IAggregateRoot
{
}
