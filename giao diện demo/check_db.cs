using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace DbCheck
{
    class Program
    {
        static void Main(string[] args)
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json")
                .Build();

            var optionsBuilder = new DbContextOptionsBuilder<giao_dien_demo.Data.ApplicationDbContext>();
            optionsBuilder.UseSqlServer(config.GetConnectionString("DefaultConnection"));

            using var context = new giao_dien_demo.Data.ApplicationDbContext(optionsBuilder.Options);
            
            var holidays = context.Holidays.Where(h => h.Status == "ACTIVE").ToList();
            Console.WriteLine($"Active Holidays: {holidays.Count}");
            foreach (var h in holidays) {
                Console.WriteLine($"- {h.HolidayName}: {h.FromDate} to {h.ToDate}, Scope: {h.ScopeType} {h.ScopeValue}");
            }

            var employees = context.Employees.ToList();
            Console.WriteLine($"Total Employees: {employees.Count}");

            var attendances = context.Attendances.ToList();
            Console.WriteLine($"Total Attendances: {attendances.Count}");
        }
    }
}
