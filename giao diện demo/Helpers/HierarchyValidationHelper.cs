using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using giao_dien_demo.Data;
using giao_dien_demo.Models;

namespace giao_dien_demo.Helpers
{
    public enum HierarchyValidationStatus
    {
        Valid,
        EmployeeNotFound,
        ProposedManagerNotFound,
        SelfManagerForbidden,                // OD-07: Quản lý chính mình (Id == ManagerId)
        CycleDirectlyInvolvesEmployee,       // Chu trình khép kín đi qua chính nhân viên
        PreExistingCycleInManagementChain,   // Chu trình khép kín tồn tại độc lập giữa các cấp trên
        ManagerNotActive,
        UnapprovedNullManagerForbidden,      // OD-11: Khuyết quản lý không có phê duyệt
        CorruptedHierarchyChain,             // Mắt xích trung gian không tìm thấy trong DB (Fail-Closed)
        TechnicalIterationGuardTriggered     // Ngưỡng an toàn kỹ thuật phòng vệ lặp vô tận
    }

    public class HierarchyValidationResult
    {
        public HierarchyValidationStatus Status { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public bool IsValid => Status == HierarchyValidationStatus.Valid;
    }

    public static class HierarchyValidationHelper
    {
        /// <summary>
        /// Kiểm tra quan hệ quản lý theo nguyên tắc FAIL-CLOSED và phát hiện mọi dạng chu trình.
        /// BẮT BUỘC thực thi bên trong IDbContextTransaction(IsolationLevel.Serializable).
        /// </summary>
        public static async Task<HierarchyValidationResult> ValidateReportingHierarchyUniversalAsync(
            ApplicationDbContext context,
            int employeeId,
            int? proposedManagerId,
            bool isApprovedNullException = false,
            bool isTopLevelRole = false)
        {
            // 1. Kiểm tra tồn tại nhân viên (Fail-Closed)
            var employee = await context.Employees
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == employeeId);

            if (employee == null)
            {
                return new HierarchyValidationResult 
                { 
                    Status = HierarchyValidationStatus.EmployeeNotFound, 
                    ErrorMessage = $"Nhân viên ID {employeeId} không tồn tại trong hệ thống." 
                };
            }

            // 2. Kiểm soát ManagerId NULL theo OD-11 (Bắt buộc workflow/phê duyệt ngoại lệ)
            if (!proposedManagerId.HasValue)
            {
                if (isTopLevelRole || isApprovedNullException)
                {
                    return new HierarchyValidationResult { Status = HierarchyValidationStatus.Valid };
                }
                return new HierarchyValidationResult 
                { 
                    Status = HierarchyValidationStatus.UnapprovedNullManagerForbidden, 
                    ErrorMessage = "ManagerId không được để trống khi chưa có phê duyệt ngoại lệ hợp lệ theo OD-11." 
                };
            }

            // 3. Ràng buộc Self-Manager (OD-07)
            if (employeeId == proposedManagerId.Value)
            {
                return new HierarchyValidationResult 
                { 
                    Status = HierarchyValidationStatus.SelfManagerForbidden, 
                    ErrorMessage = "Vi phạm OD-07: Nhân viên không thể tự quản lý chính mình." 
                };
            }

            // 4. Kiểm tra tồn tại và trạng thái quản lý đề xuất
            var proposedManager = await context.Employees
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == proposedManagerId.Value);

            if (proposedManager == null)
            {
                return new HierarchyValidationResult 
                { 
                    Status = HierarchyValidationStatus.ProposedManagerNotFound, 
                    ErrorMessage = $"Quản lý đề xuất ID {proposedManagerId.Value} không tồn tại." 
                };
            }

            if (proposedManager.Status == "Terminated" || proposedManager.Status == "Inactive")
            {
                return new HierarchyValidationResult 
                { 
                    Status = HierarchyValidationStatus.ManagerNotActive, 
                    ErrorMessage = "Không thể gán quản lý trực tiếp đang ở trạng thái ngừng hoạt động." 
                };
            }

            // 5. Ngưỡng an toàn kỹ thuật (Technical Loop Safety Guard)
            int totalEmployeesInSystem = await context.Employees.CountAsync();
            int maxTechnicalIterations = Math.Max(totalEmployeesInSystem + 10, 100);

            // 6. Phát hiện mọi chu trình quản lý (Universal Cycle Detection)
            var visitedAncestors = new HashSet<int>();
            int? currentManagerId = proposedManagerId.Value;
            int iterationCount = 0;

            while (currentManagerId.HasValue)
            {
                iterationCount++;
                if (iterationCount > maxTechnicalIterations)
                {
                    return new HierarchyValidationResult 
                    { 
                        Status = HierarchyValidationStatus.TechnicalIterationGuardTriggered, 
                        ErrorMessage = "Phát hiện lỗi kỹ thuật: Vượt quá ngưỡng lặp an toàn của hệ thống. Nghi ngờ cấu trúc dữ liệu bị lỗi lặp vô hạn." 
                    };
                }

                // Dạng 1: Chu trình trực tiếp đi qua nhân viên đang được gán
                if (currentManagerId.Value == employeeId)
                {
                    return new HierarchyValidationResult 
                    { 
                        Status = HierarchyValidationStatus.CycleDirectlyInvolvesEmployee, 
                        ErrorMessage = $"Vi phạm OD-07: Phân công này tạo chu trình khép kín đi qua nhân viên ID {employeeId}." 
                    };
                }

                // Dạng 2: Chu trình tiền ẩn tồn tại độc lập giữa các cấp trên trong cây phân cấp
                if (visitedAncestors.Contains(currentManagerId.Value))
                {
                    return new HierarchyValidationResult 
                    { 
                        Status = HierarchyValidationStatus.PreExistingCycleInManagementChain, 
                        ErrorMessage = $"Cây quản lý bị hỏng: Phát hiện chu trình vòng lặp giữa các cấp quản lý tại ID {currentManagerId.Value}." 
                    };
                }

                visitedAncestors.Add(currentManagerId.Value);

                // Tải mắt xích tiếp theo (FAIL-CLOSED: Nếu không thấy bản ghi trong DB -> Báo lỗi dữ liệu hỏng)
                var nextLink = await context.Employees
                    .AsNoTracking()
                    .Where(e => e.Id == currentManagerId.Value)
                    .Select(e => new { e.Id, e.ManagerId })
                    .FirstOrDefaultAsync();

                if (nextLink == null)
                {
                    return new HierarchyValidationResult 
                    { 
                        Status = HierarchyValidationStatus.CorruptedHierarchyChain, 
                        ErrorMessage = $"Cây phân cấp bị lỗi: Mắt xích quản lý ID {currentManagerId.Value} không tồn tại trong CSDL." 
                    };
                }

                currentManagerId = nextLink.ManagerId;
            }

            return new HierarchyValidationResult { Status = HierarchyValidationStatus.Valid };
        }
    }
}

