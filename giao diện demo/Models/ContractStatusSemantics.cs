using System;

namespace giao_dien_demo.Models;

/// <summary>
/// Canonical persisted Contract lifecycle values and the derived date filters
/// used by monitoring surfaces. Contract.Status remains a stored string.
/// </summary>
public static class ContractStatusSemantics
{
    public const string Draft = "Chờ duyệt";
    public const string AwaitingEmployeeSignature = "Chờ nhân viên ký";
    public const string AwaitingFinalApproval = "Đã ký (Chờ duyệt cuối)";
    public const string Effective = "Còn hiệu lực";
    public const string Cancelled = "Đã hủy";

    // Existing HR and Leader dashboard behavior defines this monitoring window.
    public const int ExpiringWindowDays = 30;

    // Derived filter value; it is never persisted as Contract.Status.
    public const string ExpiredFilter = "__EXPIRED__";

    public static bool IsEffective(Contract contract) =>
        string.Equals(contract.Status, Effective, StringComparison.Ordinal);

    public static bool IsExpired(Contract contract, DateTime today) =>
        IsEffective(contract)
        && contract.StartDate.Date <= today.Date
        && contract.EndDate != default
        && contract.EndDate.Date < today.Date;

    public static bool IsCurrentlyEffective(Contract contract, DateTime today) =>
        IsEffective(contract)
        && contract.StartDate.Date <= today.Date
        && (contract.EndDate == default || contract.EndDate.Date >= today.Date);

    public static bool IsExpiring(Contract contract, DateTime today) =>
        IsCurrentlyEffective(contract, today)
        && contract.EndDate != default
        && contract.EndDate.Date >= today.Date
        && contract.EndDate.Date <= today.Date.AddDays(ExpiringWindowDays);
}
