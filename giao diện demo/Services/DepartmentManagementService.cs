using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using giao_dien_demo.Data;
using giao_dien_demo.Models;

namespace giao_dien_demo.Services
{
    public interface IDepartmentManagementService
    {
        Task<int> SeedOfficialDepartmentsAsync();
        Task<int> BackfillLegacyEmployeesAsync();
        Task<bool> UpdateDepartmentNameAsync(int departmentId, string newOfficialName, int updatedByUserId);
    }

    public class DepartmentManagementService : IDepartmentManagementService
    {
        private readonly ApplicationDbContext _context;

        public DepartmentManagementService(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Seed danh mục 6 phòng ban chuẩn theo CR-01 (Idempotent theo Code).
        /// </summary>
        public async Task<int> SeedOfficialDepartmentsAsync()
        {
            var standardDepartments = new (string Code, string Name, string Description)[]
            {
                ("HR", "Phòng Nhân sự", "Quản trị nguồn nhân lực và chính sách đãi ngộ"),
                ("IT", "Phòng Công nghệ thông tin", "Phát triển phần mềm, hạ tầng hệ thống và an ninh mạng"),
                ("MKT", "Phòng Marketing & Truyền thông", "Phát triển thương hiệu và truyền thông nội bộ/đối ngoại"),
                ("FIN", "Phòng Tài chính Kế toán", "Quản lý dòng tiền, kế toán và ngân sách doanh nghiệp"),
                ("SALES", "Phòng Kinh doanh", "Phát triển thị trường và kinh doanh sản phẩm dịch vụ"),
                ("OPS", "Phòng Vận hành", "Quản trị quy trình vận hành và cơ sở vật chất")
            };

            int insertedCount = 0;
            foreach (var item in standardDepartments)
            {
                var existing = await _context.Departments.FirstOrDefaultAsync(d => d.Code == item.Code);
                if (existing == null)
                {
                    _context.Departments.Add(new Department
                    {
                        Code = item.Code,
                        Name = item.Name,
                        Description = item.Description,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    });
                    insertedCount++;
                }
            }

            if (insertedCount > 0)
            {
                await _context.SaveChangesAsync();
            }

            return insertedCount;
        }

        /// <summary>
        /// Chuyển đổi dữ liệu phòng ban legacy của nhân viên theo CR-02 (thực hiện theo Code, bảo toàn dữ liệu).
        /// </summary>
        public async Task<int> BackfillLegacyEmployeesAsync()
        {
            await SeedOfficialDepartmentsAsync();

            var deptHr = await _context.Departments.FirstOrDefaultAsync(d => d.Code == "HR");
            var deptIt = await _context.Departments.FirstOrDefaultAsync(d => d.Code == "IT");

            if (deptHr == null || deptIt == null)
            {
                throw new InvalidOperationException("Không tìm thấy phòng ban chuẩn HR hoặc IT để thực hiện backfill.");
            }

            var hrLegacyStrings = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Phòng nhân sự", "Phòng HR", "HR Dept"
            };

            var itLegacyStrings = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Phòng IT", "IT Software", "Phòng Quản trị hệ thống"
            };

            var allEmployees = await _context.Employees.ToListAsync();
            int updatedCount = 0;

            foreach (var emp in allEmployees)
            {
                if (string.IsNullOrWhiteSpace(emp.Department))
                {
                    emp.DepartmentId = null;
                    continue;
                }

                if (hrLegacyStrings.Contains(emp.Department.Trim()))
                {
                    emp.DepartmentId = deptHr.Id;
                    emp.Department = deptHr.Name; // Đồng bộ tên chuẩn (OD-20)
                    updatedCount++;
                }
                else if (itLegacyStrings.Contains(emp.Department.Trim()))
                {
                    emp.DepartmentId = deptIt.Id;
                    emp.Department = deptIt.Name; // Đồng bộ tên chuẩn (OD-20)
                    updatedCount++;
                }
                else if (string.Equals(emp.Department.Trim(), "Chưa xếp phòng", StringComparison.OrdinalIgnoreCase))
                {
                    emp.DepartmentId = null;
                    emp.Department = "Chưa xếp phòng"; // Bảo toàn chuỗi legacy
                    updatedCount++;
                }
            }

            await _context.SaveChangesAsync();
            return updatedCount;
        }

        /// <summary>
        /// Cập nhật tên phòng ban và đồng bộ hóa cột chuỗi Employees.Department trong cùng transaction (OD-20).
        /// </summary>
        public async Task<bool> UpdateDepartmentNameAsync(int departmentId, string newOfficialName, int updatedByUserId)
        {
            if (string.IsNullOrWhiteSpace(newOfficialName))
            {
                throw new ArgumentException("Tên phòng ban không được để trống.", nameof(newOfficialName));
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var department = await _context.Departments.FindAsync(departmentId);
                if (department == null) return false;

                string oldName = department.Name;
                department.Name = newOfficialName.Trim();
                department.UpdatedAt = DateTime.UtcNow;
                department.UpdatedByUserId = updatedByUserId;

                // Đồng bộ cột chuỗi Employees.Department của các nhân viên thuộc phòng ban
                var employeesInDept = await _context.Employees
                    .Where(e => e.DepartmentId == departmentId)
                    .ToListAsync();

                foreach (var emp in employeesInDept)
                {
                    emp.Department = newOfficialName.Trim();
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return true;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}

