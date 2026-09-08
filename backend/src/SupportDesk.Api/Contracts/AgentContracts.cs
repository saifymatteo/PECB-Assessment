using System.ComponentModel.DataAnnotations;
using SupportDesk.Api.Domain;

namespace SupportDesk.Api.Contracts;

public record AgentDto(Guid Id, string FullName, string Email, string Department, bool Active);

public class CreateAgentRequest
{
    [Required, MaxLength(200)]
    public string FullName { get; init; } = null!;

    [Required, MaxLength(320), EmailAddress]
    public string Email { get; init; } = null!;

    [Required, EnumDataType(typeof(AgentDepartment))]
    public AgentDepartment Department { get; init; }

    public bool Active { get; init; } = true;
}

public class SetAgentActiveRequest
{
    [Required]
    public bool Active { get; init; }
}
