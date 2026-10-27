using System.Collections.Generic;

namespace giao_dien_demo.Models.AI;

public sealed class AvaUserContext
{
    public string UserId { get; init; } = string.Empty;
    public string? EmployeeId { get; init; }
    public string? EmployeeCode { get; init; }
    public bool IsAuthenticated { get; init; }
    public IReadOnlyList<string> Roles { get; init; } = [];
    public IReadOnlyList<string> Permissions { get; init; } = [];
    public IReadOnlyList<string> DepartmentIds { get; init; } = [];
    public IReadOnlyList<string> DepartmentNames { get; init; } = [];
}
