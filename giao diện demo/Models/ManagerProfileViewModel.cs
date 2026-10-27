using System;
using System.Collections.Generic;

namespace giao_dien_demo.Models;

/// <summary>
/// Read-only Manager profile projection.  Identity is resolved server-side from
/// the authenticated User; this model intentionally contains no credentials or
/// salary/security fields.
/// </summary>
public sealed class ManagerProfileViewModel
{
    public bool HasLinkedEmployee { get; set; }
    public int? EmployeeId { get; set; }
    public string Username { get; set; } = "";
    public string AccountRole { get; set; } = "";
    public string? EmployeeCode { get; set; }
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Address { get; set; }
    public string? Gender { get; set; }
    public DateTime? BirthDate { get; set; }
    public string? AvatarPath { get; set; }
    public string? Department { get; set; }
    public string? Position { get; set; }
    public string? JobTitle { get; set; }
    public string? EmploymentStatus { get; set; }
    public IReadOnlyList<string> ActiveOrganizationalUnits { get; set; } = Array.Empty<string>();
    public string? Message { get; set; }
}
