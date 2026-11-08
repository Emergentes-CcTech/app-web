using IoBuild.Api.Publishing.Domain.Model.ValueObjects;

namespace IoBuild.Api.Publishing.Domain.Model.Aggregates;

public sealed class Client
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string AccountStatement { get; set; } = EAccountStatement.Pending.ToString();
    public int BuilderId { get; set; }
    public int ProjectId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public int? UnitId { get; set; }
    public string? UnitNumber { get; set; }

    public Client() { }

    public Client(
        string fullName,
        string projectName,
        string accountStatement,
        int builderId,
        int projectId,
        string email = "",
        string phoneNumber = "",
        string address = "",
        int? unitId = null,
        string? unitNumber = null)
    {
        FullName = fullName;
        ProjectName = projectName;
        AccountStatement = accountStatement;
        BuilderId = builderId;
        ProjectId = projectId;
        Email = email;
        PhoneNumber = phoneNumber;
        Address = address;
        UnitId = unitId;
        UnitNumber = unitNumber;
    }

    public void Update(
        string fullName,
        string projectName,
        string accountStatement,
        int builderId,
        int projectId,
        string email = "",
        string phoneNumber = "",
        string address = "",
        int? unitId = null,
        string? unitNumber = null)
    {
        FullName = fullName;
        ProjectName = projectName;
        AccountStatement = accountStatement;
        BuilderId = builderId;
        ProjectId = projectId;
        Email = email;
        PhoneNumber = phoneNumber;
        Address = address;
        UnitId = unitId;
        UnitNumber = unitNumber;
    }
}
