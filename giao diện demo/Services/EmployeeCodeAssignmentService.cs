using System.Data;
using System.Text.RegularExpressions;
using giao_dien_demo.Data;
using giao_dien_demo.Models;
using Microsoft.EntityFrameworkCore;

namespace giao_dien_demo.Services;

/// <summary>Assigns the canonical production employee identifier.</summary>
public interface IEmployeeCodeAssignmentService
{
    Task<string> EnsureAssignedAsync(Employee employee, CancellationToken cancellationToken = default);
}

public sealed class EmployeeCodeAssignmentService : IEmployeeCodeAssignmentService
{
    private static readonly Regex CanonicalCode = new("^NV(?<number>[0-9]{3})$", RegexOptions.CultureInvariant);
    private const int MaxAttempts = 3;
    private readonly ApplicationDbContext _context;
    private readonly IAuditLogService? _auditLogService;

    public EmployeeCodeAssignmentService(ApplicationDbContext context, IAuditLogService? auditLogService = null)
    {
        _context = context;
        _auditLogService = auditLogService;
    }

    public async Task<string> EnsureAssignedAsync(Employee employee, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(employee.EmployeeCode)) return employee.EmployeeCode;
        if (employee.Id <= 0) throw new InvalidOperationException("Employee phải được lưu trước khi cấp mã.");

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            try
            {
                var current = await _context.Employees.SingleAsync(e => e.Id == employee.Id, cancellationToken);
                if (!string.IsNullOrWhiteSpace(current.EmployeeCode))
                {
                    await transaction.CommitAsync(cancellationToken);
                    employee.EmployeeCode = current.EmployeeCode;
                    return current.EmployeeCode;
                }

                var canonicalCodes = await _context.Employees
                    .AsNoTracking()
                    .Where(e => e.EmployeeCode != null && e.EmployeeCode.StartsWith("NV") && e.EmployeeCode.Length == 5)
                    .Select(e => e.EmployeeCode!)
                    .ToListAsync(cancellationToken);
                var max = canonicalCodes
                    .Select(code => CanonicalCode.Match(code))
                    .Where(match => match.Success)
                    .Select(match => int.Parse(match.Groups["number"].Value, System.Globalization.CultureInfo.InvariantCulture))
                    .DefaultIfEmpty(0)
                    .Max();

                if (max >= 999)
                    throw new InvalidOperationException("Không thể cấp mã nhân viên: đã đạt giới hạn NV999.");

                var candidate = $"NV{max + 1:D3}";
                current.EmployeeCode = candidate;
                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                employee.EmployeeCode = candidate;
                if (_auditLogService is not null)
                {
                    await _auditLogService.LogAsync(
                        "Assign", "Employee", "EmployeeCode",
                        employee.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        "EmployeeCodeAssigned", "Success",
                        newValues: "EmployeeCode assigned by canonical NV allocator");
                }
                return candidate;
            }
            catch (DbUpdateException) when (attempt < MaxAttempts)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                _context.ChangeTracker.Clear();
                employee.EmployeeCode = null;
            }
        }

        throw new DbUpdateException("Không thể cấp mã nhân viên duy nhất sau nhiều lần thử.");
    }
}
