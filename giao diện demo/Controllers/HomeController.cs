using giao_dien_demo.Data;
using giao_dien_demo.Data;
using giao_diện_demo.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore; // Để dùng hàm CountAsync
using System.Diagnostics;
using System.Threading.Tasks;

namespace giao_dien_demo.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        // Tiêm Database Context vào qua Constructor
        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        // --- CẬP NHẬT TRANG CHỦ DASHBOARD ---
        public async Task<IActionResult> Index()
        {
            // 1. Đếm số nhân viên đã duyệt (Đang hoạt động)
            ViewBag.EmployeeCount = await _context.Employees.CountAsync(e => e.Status == "Active" || e.Status == "Probation");

            // 2. Đếm tổng số hợp đồng hiện có
            ViewBag.ContractCount = await _context.Contracts.CountAsync();

            // 3. Đếm số lượng báo cáo (Nếu bạn có bảng Reports)
            // ViewBag.ReportCount = await _context.Reports.CountAsync();

            // Mẹo: Bạn có thể thêm các ViewBag khác cho Chấm công, Lương... tương tự

            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}