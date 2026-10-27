using System.Security.Claims;
using System.Text;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using giao_dien_demo.Authorization;
using giao_dien_demo.Controllers;
using giao_dien_demo.Data;
using giao_dien_demo.Models;
using giao_dien_demo.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HRManagement.Tests;

public sealed class ManagerProfileTests
{
    [Fact]
    public async Task MGR07B_EMAIL_001_002_003_manager_can_save_and_reload_own_employee_email()
    {
        await using var fixture = await ProfileFixture.CreateAsync();
        await fixture.EmployeeController.UpdateMyProfile(new Employee { Email = "manager.contact@example.com" }, null);
        var persisted = await fixture.Context.Employees.AsNoTracking().SingleAsync(e => e.Id == fixture.ManagerEmployeeId);
        var profile = Assert.IsType<ManagerProfileViewModel>(Assert.IsType<ViewResult>(await fixture.Controller.Profile()).Model);
        Assert.Equal("manager.contact@example.com", persisted.Email);
        Assert.Equal("manager.contact@example.com", profile.Email);
    }

    [Fact]
    public async Task MGR07B_EMAIL_004_invalid_email_is_rejected_without_overwriting_value()
    {
        await using var fixture = await ProfileFixture.CreateAsync();
        var result = await fixture.EmployeeController.UpdateMyProfile(new Employee { Email = "not-an-email" }, null);
        Assert.IsType<ViewResult>(result);
        Assert.Equal("old@example.com", (await fixture.Context.Employees.AsNoTracking().SingleAsync(e => e.Id == fixture.ManagerEmployeeId)).Email);
    }

    [Fact]
    public async Task MGR07B_EMAIL_005_profile_email_does_not_change_account_email()
    {
        await using var fixture = await ProfileFixture.CreateAsync();
        await fixture.EmployeeController.UpdateMyProfile(new Employee { Email = "profile@example.com" }, null);
        Assert.Equal("manager-a@test", (await fixture.Context.Users.AsNoTracking().SingleAsync(u => u.Username == "manager-a")).Email);
    }

    [Fact]
    public async Task MGR07B_EMAIL_006_other_employee_email_cannot_be_updated_by_identity_tampering()
    {
        await using var fixture = await ProfileFixture.CreateAsync();
        var other = await fixture.Context.Employees.AsNoTracking().SingleAsync(e => e.EmployeeCode == "E_B1");
        await fixture.EmployeeController.UpdateMyProfile(new Employee { Id = other.Id, Email = "attacker@example.com" }, null);
        Assert.Null((await fixture.Context.Employees.AsNoTracking().SingleAsync(e => e.Id == other.Id)).Email);
    }

    [Fact]
    public async Task MGR07B_CCCD_001_002_003_citizen_id_saves_and_edit_page_reloads_it()
    {
        await using var fixture = await ProfileFixture.CreateAsync();
        await fixture.EmployeeController.UpdateMyProfile(new Employee { CitizenId = "0123456789" }, null);
        var persisted = await fixture.Context.Employees.AsNoTracking().SingleAsync(e => e.Id == fixture.ManagerEmployeeId);
        var editModel = Assert.IsType<Employee>(Assert.IsType<ViewResult>(await fixture.EmployeeController.MyProfile()).Model);
        Assert.Equal("0123456789", persisted.CitizenId);
        Assert.Equal("0123456789", editModel.CitizenId);
    }

    [Fact]
    public async Task MGR07B_CCCD_004_invalid_citizen_id_is_rejected()
    {
        await using var fixture = await ProfileFixture.CreateAsync();
        var result = await fixture.EmployeeController.UpdateMyProfile(new Employee { CitizenId = "ABC" }, null);
        Assert.IsType<ViewResult>(result);
        Assert.Equal("CITIZEN-A1", (await fixture.Context.Employees.AsNoTracking().SingleAsync(e => e.Id == fixture.ManagerEmployeeId)).CitizenId);
    }

    [Fact]
    public async Task MGR07B_CCCD_005_other_employee_citizen_id_cannot_be_updated()
    {
        await using var fixture = await ProfileFixture.CreateAsync();
        var other = await fixture.Context.Employees.AsNoTracking().SingleAsync(e => e.EmployeeCode == "E_B1");
        await fixture.EmployeeController.UpdateMyProfile(new Employee { Id = other.Id, CitizenId = "0123456789" }, null);
        Assert.Null((await fixture.Context.Employees.AsNoTracking().SingleAsync(e => e.Id == other.Id)).CitizenId);
    }

    [Fact]
    public async Task MGR07B_CCCD_006_citizen_update_cannot_mutate_authoritative_work_fields()
    {
        await using var fixture = await ProfileFixture.CreateAsync();
        await fixture.EmployeeController.UpdateMyProfile(new Employee { CitizenId = "012345678912", Department = "FORGED", Position = "FORGED", JobTitle = "FORGED", Salary = 1m }, null);
        var employee = await fixture.Context.Employees.AsNoTracking().SingleAsync(e => e.Id == fixture.ManagerEmployeeId);
        Assert.Equal("012345678912", employee.CitizenId);
        Assert.Equal("UNIT_A", employee.Department);
        Assert.Equal("Staff", employee.Position);
        Assert.Equal("Employee", employee.JobTitle);
        Assert.Equal(0m, employee.Salary);
    }

    [Fact]
    public async Task MGR07B_CODE_001_002_normal_employee_lifecycle_assigns_unique_code()
    {
        await using var fixture = await ProfileFixture.CreateAsync();
        var first = new Employee { Name = "New 1" }; var second = new Employee { Name = "New 2" };
        fixture.Context.Employees.AddRange(first, second);
        await fixture.Context.SaveChangesAsync();
        var service = new EmployeeCodeAssignmentService(fixture.Context);
        await service.EnsureAssignedAsync(first); await service.EnsureAssignedAsync(second);
        Assert.False(string.IsNullOrWhiteSpace(first.EmployeeCode));
        Assert.NotEqual(first.EmployeeCode, second.EmployeeCode);
        Assert.Matches("^NV[0-9]{3}$", first.EmployeeCode);
    }

    [Fact]
    public async Task MGR07B_CODE_005_onboarding_employee_creation_assigns_code()
    {
        await using var fixture = await ProfileFixture.CreateAsync();
        var onboarding = new OnboardingRecord
        {
            EmployeeCode = "FORGED",
            EmployeeName = "Onboarding employee",
            Department = "UNIT_NEW",
            JobTitle = "Staff",
            JoinDate = DateTime.Today,
            Email = "onboarding@example.com"
        };

        await new OnboardingWorkflowPolicy(new EmployeeCodeAssignmentService(fixture.Context))
            .OnApprovedAsync(onboarding, fixture.Context);

        var employee = await fixture.Context.Employees.AsNoTracking().SingleAsync(e => e.Id == onboarding.EmployeeId);
        Assert.False(string.IsNullOrWhiteSpace(employee.EmployeeCode));
        Assert.Matches("^NV[0-9]{3}$", employee.EmployeeCode);
        Assert.NotEqual("FORGED", employee.EmployeeCode);
    }

    [Fact]
    public async Task MGR07E_CODE_001_002_canonical_allocator_uses_next_nv_sequence_and_ignores_exceptions()
    {
        await using var fixture = await ProfileFixture.CreateAsync();
        await ProfileFixture.SeedCanonicalCodesAsync(fixture.Context, 6);
        fixture.Context.Employees.AddRange(
            new Employee { EmployeeCode = "EMP_P3A4_1", Name = "Legacy exception 1" },
            new Employee { EmployeeCode = "EMP_P3A4_2", Name = "Legacy exception 2" });
        var target = new Employee { Name = "Canonical target" };
        fixture.Context.Employees.Add(target);
        await fixture.Context.SaveChangesAsync();

        await new EmployeeCodeAssignmentService(fixture.Context).EnsureAssignedAsync(target);

        Assert.Equal("NV007", target.EmployeeCode);
        Assert.Equal("EMP_P3A4_1", (await fixture.Context.Employees.SingleAsync(e => e.Name == "Legacy exception 1")).EmployeeCode);
        Assert.Equal("EMP_P3A4_2", (await fixture.Context.Employees.SingleAsync(e => e.Name == "Legacy exception 2")).EmployeeCode);
    }

    [Fact]
    public async Task MGR07E_CODE_003_sequential_allocations_are_unique_and_existing_code_is_immutable()
    {
        await using var fixture = await ProfileFixture.CreateAsync();
        await ProfileFixture.SeedCanonicalCodesAsync(fixture.Context, 6);
        var first = new Employee { Name = "Sequence 1" };
        var second = new Employee { Name = "Sequence 2" };
        fixture.Context.Employees.AddRange(first, second);
        await fixture.Context.SaveChangesAsync();
        var service = new EmployeeCodeAssignmentService(fixture.Context);

        await service.EnsureAssignedAsync(first);
        await service.EnsureAssignedAsync(second);

        Assert.Equal("NV007", first.EmployeeCode);
        Assert.Equal("NV008", second.EmployeeCode);
        var existing = await fixture.Context.Employees.SingleAsync(e => e.EmployeeCode == "E_A1");
        await service.EnsureAssignedAsync(existing);
        Assert.Equal("E_A1", existing.EmployeeCode);
    }

    [Fact]
    public async Task MGR07E_CODE_004_existing_code_remains_immutable_on_repeated_assignment()
    {
        await using var fixture = await ProfileFixture.CreateAsync();
        var existing = await fixture.Context.Employees.SingleAsync(e => e.EmployeeCode == "E_A1");
        await new EmployeeCodeAssignmentService(fixture.Context).EnsureAssignedAsync(existing);
        Assert.Equal("E_A1", existing.EmployeeCode);
    }

    [Fact]
    public async Task MGR07E_CODE_005_missing_code_is_allocated_from_canonical_sequence()
    {
        await using var fixture = await ProfileFixture.CreateAsync();
        await ProfileFixture.SeedCanonicalCodesAsync(fixture.Context, 6);
        var target = new Employee { Name = "Missing code target" };
        fixture.Context.Employees.Add(target);
        await fixture.Context.SaveChangesAsync();
        await new EmployeeCodeAssignmentService(fixture.Context).EnsureAssignedAsync(target);
        Assert.Equal("NV007", target.EmployeeCode);
    }

    [Fact]
    public async Task MGR07E_CODE_006_client_supplied_code_is_not_authoritative_for_onboarding()
    {
        await using var fixture = await ProfileFixture.CreateAsync();
        var onboarding = new OnboardingRecord { EmployeeCode = "FORGED", EmployeeName = "Forged onboarding", Department = "UNIT_NEW", JoinDate = DateTime.Today };
        await new OnboardingWorkflowPolicy(new EmployeeCodeAssignmentService(fixture.Context)).OnApprovedAsync(onboarding, fixture.Context);
        var employee = await fixture.Context.Employees.AsNoTracking().SingleAsync(e => e.Id == onboarding.EmployeeId);
        Assert.NotEqual("FORGED", employee.EmployeeCode);
        Assert.Matches("^NV[0-9]{3}$", employee.EmployeeCode);
    }

    [Fact]
    public async Task MGR07E_CODE_007_profile_update_preserves_canonical_or_existing_code()
    {
        await using var fixture = await ProfileFixture.CreateAsync();
        await fixture.EmployeeController.UpdateMyProfile(new Employee { PhoneNumber = "profile-update", EmployeeCode = "FORGED" }, null);
        Assert.Equal("E_A1", (await fixture.Context.Employees.AsNoTracking().SingleAsync(e => e.Id == fixture.ManagerEmployeeId)).EmployeeCode);
    }

    [Fact]
    public async Task MGR07E_CODE_008_workflow_style_field_update_preserves_employee_code()
    {
        await using var fixture = await ProfileFixture.CreateAsync();
        var employee = await fixture.Context.Employees.SingleAsync(e => e.Id == fixture.ManagerEmployeeId);
        employee.Department = "UNIT_WORKFLOW";
        employee.Position = "Workflow position";
        await fixture.Context.SaveChangesAsync();
        Assert.Equal("E_A1", (await fixture.Context.Employees.AsNoTracking().SingleAsync(e => e.Id == fixture.ManagerEmployeeId)).EmployeeCode);
    }

    [Fact]
    public async Task MGR07E_CODE_009_nv999_is_an_explicit_allocation_failure_without_partial_code()
    {
        await using var fixture = await ProfileFixture.CreateAsync();
        fixture.Context.Employees.Add(new Employee { EmployeeCode = "NV999", Name = "Upper bound" });
        var target = new Employee { Name = "Overflow target" };
        fixture.Context.Employees.Add(target);
        await fixture.Context.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => new EmployeeCodeAssignmentService(fixture.Context).EnsureAssignedAsync(target));

        Assert.Null((await fixture.Context.Employees.AsNoTracking().SingleAsync(e => e.Id == target.Id)).EmployeeCode);
    }

    [Fact]
    public async Task MGR07E_CODE_010_noncanonical_legacy_code_is_not_regenerated()
    {
        await using var fixture = await ProfileFixture.CreateAsync();
        var legacy = new Employee { EmployeeCode = "EMP_P3A4_1", Name = "Legacy" };
        fixture.Context.Employees.Add(legacy);
        await fixture.Context.SaveChangesAsync();

        await new EmployeeCodeAssignmentService(fixture.Context).EnsureAssignedAsync(legacy);

        Assert.Equal("EMP_P3A4_1", legacy.EmployeeCode);
    }

    [Fact]
    public async Task MGR08_CREATE_001_authorized_hr_get_create_returns_view_model()
    {
        await using var fixture = await ProfileFixture.CreateAsync();
        var result = await fixture.EmployeeController.Create();
        var view = Assert.IsType<ViewResult>(result);
        Assert.IsType<EmployeeCreateViewModel>(view.Model);
    }

    [Fact]
    public void MGR08_CREATE_002_003_create_actions_are_hr_only_for_manager_and_employee_boundaries()
    {
        var get = typeof(EmployeeController).GetMethod(nameof(EmployeeController.Create), Type.EmptyTypes)!;
        var post = typeof(EmployeeController).GetMethod(nameof(EmployeeController.Create), new[] { typeof(EmployeeCreateViewModel) })!;
        Assert.Equal(AppPolicies.HROnly, get.GetCustomAttribute<AuthorizeAttribute>()?.Policy);
        Assert.Equal(AppPolicies.HROnly, post.GetCustomAttribute<AuthorizeAttribute>()?.Policy);
    }

    [Fact]
    public async Task MGR08_CREATE_004_005_valid_hr_post_creates_one_employee_with_canonical_code()
    {
        await using var fixture = await ProfileFixture.CreateAsync();
        var before = await fixture.Context.Employees.CountAsync();
        var result = await fixture.EmployeeController.Create(CreateModel("Created HR"));
        var created = await fixture.Context.Employees.SingleAsync(e => e.Name == "Created HR");

        Assert.Equal(before + 1, await fixture.Context.Employees.CountAsync());
        Assert.Matches("^NV[0-9]{3}$", created.EmployeeCode);
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(EmployeeController.Details), redirect.ActionName);
        Assert.Equal(created.Id, redirect.RouteValues!["id"]);
    }

    [Fact]
    public async Task MGR08_CREATE_006_forged_employee_code_is_ignored_by_create_view_model()
    {
        await using var fixture = await ProfileFixture.CreateAsync();
        var result = await fixture.EmployeeController.Create(CreateModel("Forged code", forgedEmployeeCode: "NV999"));
        Assert.IsType<RedirectToActionResult>(result);
        var created = await fixture.Context.Employees.SingleAsync(e => e.Name == "Forged code");
        Assert.NotEqual("NV999", created.EmployeeCode);
        Assert.Matches("^NV[0-9]{3}$", created.EmployeeCode);
    }

    [Fact]
    public async Task MGR08_CREATE_007_008_validation_failure_creates_no_employee_and_consumes_no_code()
    {
        await using var fixture = await ProfileFixture.CreateAsync();
        var beforeCount = await fixture.Context.Employees.CountAsync();
        var beforeCodes = await fixture.Context.Employees.Where(e => e.EmployeeCode != null).Select(e => e.EmployeeCode!).ToListAsync();
        fixture.EmployeeController.ModelState.AddModelError(nameof(EmployeeCreateViewModel.Name), "required");

        var result = await fixture.EmployeeController.Create(CreateModel("Invalid"));

        Assert.IsType<ViewResult>(result);
        Assert.Equal(beforeCount, await fixture.Context.Employees.CountAsync());
        Assert.Equal(beforeCodes.OrderBy(x => x), await fixture.Context.Employees.Where(e => e.EmployeeCode != null).Select(e => e.EmployeeCode!).OrderBy(x => x).ToListAsync());
    }

    [Fact]
    public async Task MGR08_CREATE_009_two_sequential_valid_creates_receive_distinct_codes()
    {
        await using var fixture = await ProfileFixture.CreateAsync();
        await fixture.EmployeeController.Create(CreateModel("Sequence one"));
        await fixture.EmployeeController.Create(CreateModel("Sequence two"));
        var codes = await fixture.Context.Employees.Where(e => e.Name == "Sequence one" || e.Name == "Sequence two").Select(e => e.EmployeeCode).ToListAsync();
        Assert.Equal(2, codes.Distinct().Count());
        Assert.All(codes, code => Assert.Matches("^NV[0-9]{3}$", code));
    }

    [Fact]
    public async Task MGR08_CREATE_010_existing_codes_remain_unchanged_after_create()
    {
        await using var fixture = await ProfileFixture.CreateAsync();
        var before = await fixture.Context.Employees.AsNoTracking().Where(e => e.EmployeeCode != null).Select(e => new { e.Id, e.EmployeeCode }).ToListAsync();
        await fixture.EmployeeController.Create(CreateModel("No renumbering"));
        var after = await fixture.Context.Employees.AsNoTracking().Where(e => e.EmployeeCode != null).Select(e => new { e.Id, e.EmployeeCode }).ToListAsync();
        foreach (var row in before) Assert.Equal(row.EmployeeCode, after.Single(e => e.Id == row.Id).EmployeeCode);
    }

    [Fact]
    public async Task MGR08_CREATE_011_exception_codes_do_not_influence_next_nv_allocation()
    {
        await using var fixture = await ProfileFixture.CreateAsync();
        fixture.Context.Employees.AddRange(new Employee { Name = "Legacy 1", EmployeeCode = "EMP_P3A4_1" }, new Employee { Name = "Legacy 2", EmployeeCode = "EMP_P3A4_2" });
        await fixture.Context.SaveChangesAsync();
        await fixture.EmployeeController.Create(CreateModel("After legacy"));
        Assert.Equal("NV001", (await fixture.Context.Employees.SingleAsync(e => e.Name == "After legacy")).EmployeeCode);
    }

    [Fact]
    public async Task MGR08_CREATE_012_created_employee_appears_in_hr_projection_with_same_code()
    {
        await using var fixture = await ProfileFixture.CreateAsync();
        await fixture.EmployeeController.Create(CreateModel("Projection target"));
        var rows = Assert.IsAssignableFrom<IEnumerable<Employee>>(Assert.IsType<ViewResult>(await fixture.EmployeeController.Index(null, null, "All")).Model);
        var persisted = await fixture.Context.Employees.AsNoTracking().SingleAsync(e => e.Name == "Projection target");
        Assert.Equal(persisted.EmployeeCode, rows.Single(e => e.Id == persisted.Id).EmployeeCode);
    }

    [Fact]
    public async Task MGR08_CREATE_013_details_projects_the_same_employee_code()
    {
        await using var fixture = await ProfileFixture.CreateAsync();
        await fixture.EmployeeController.Create(CreateModel("Details target"));
        var persisted = await fixture.Context.Employees.AsNoTracking().SingleAsync(e => e.Name == "Details target");
        var details = Assert.IsType<Employee>(Assert.IsType<ViewResult>(await fixture.EmployeeController.Details(persisted.Id)).Model);
        Assert.Equal(persisted.EmployeeCode, details.EmployeeCode);
    }

    [Fact]
    public void MGR08_CREATE_014_015_post_requires_antiforgery_and_whitelisted_create_contract()
    {
        var post = typeof(EmployeeController).GetMethod(nameof(EmployeeController.Create), new[] { typeof(EmployeeCreateViewModel) })!;
        Assert.NotNull(post.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
        Assert.Null(typeof(EmployeeCreateViewModel).GetProperty(nameof(Employee.EmployeeCode)));
        Assert.Null(typeof(EmployeeCreateViewModel).GetProperty(nameof(Employee.Id)));
    }

    private static EmployeeCreateViewModel CreateModel(string name, string? forgedEmployeeCode = null)
    {
        _ = forgedEmployeeCode;
        return new EmployeeCreateViewModel
        {
            Name = name,
            Department = "UNIT_NEW",
            Position = "Chính thức",
            JobTitle = "Staff",
            Salary = 100m
        };
    }

    [Fact]
    public async Task MGR07B_CODE_003_self_profile_cannot_change_employee_code()
    {
        await using var fixture = await ProfileFixture.CreateAsync();
        await fixture.EmployeeController.UpdateMyProfile(new Employee { EmployeeCode = "FORGED" }, null);
        Assert.Equal("E_A1", (await fixture.Context.Employees.AsNoTracking().SingleAsync(e => e.Id == fixture.ManagerEmployeeId)).EmployeeCode);
    }

    [Fact]
    public async Task MGR07B_CODE_004_profile_displays_persisted_employee_code()
    {
        await using var fixture = await ProfileFixture.CreateAsync();
        var profile = Assert.IsType<ManagerProfileViewModel>(Assert.IsType<ViewResult>(await fixture.Controller.Profile()).Model);
        Assert.Equal("E_A1", profile.EmployeeCode);
    }

    [Fact]
    public async Task MGR07C_CODE_003_existing_employee_code_is_not_regenerated()
    {
        await using var fixture = await ProfileFixture.CreateAsync();
        var employee = await fixture.Context.Employees.SingleAsync(e => e.Id == fixture.ManagerEmployeeId);
        var original = employee.EmployeeCode;

        await new EmployeeCodeAssignmentService(fixture.Context).EnsureAssignedAsync(employee);

        Assert.Equal(original, employee.EmployeeCode);
    }

    [Fact]
    public async Task MGR07C_CODE_004_profile_update_does_not_change_employee_code()
    {
        await using var fixture = await ProfileFixture.CreateAsync();
        await fixture.EmployeeController.UpdateMyProfile(new Employee { PhoneNumber = "0900000000", EmployeeCode = "FORGED" }, null);

        Assert.Equal("E_A1", (await fixture.Context.Employees.AsNoTracking().SingleAsync(e => e.Id == fixture.ManagerEmployeeId)).EmployeeCode);
    }

    [Fact]
    public async Task MGR07C_CODE_005_transfer_style_employee_field_update_preserves_code()
    {
        await using var fixture = await ProfileFixture.CreateAsync();
        var employee = await fixture.Context.Employees.SingleAsync(e => e.Id == fixture.ManagerEmployeeId);
        employee.Department = "UNIT_TRANSFERRED";
        employee.Position = "Transferred position";
        employee.JobTitle = "Transferred title";
        await fixture.Context.SaveChangesAsync();

        Assert.Equal("E_A1", (await fixture.Context.Employees.AsNoTracking().SingleAsync(e => e.Id == fixture.ManagerEmployeeId)).EmployeeCode);
    }

    [Fact]
    public async Task MGR07C_CODE_006_appointment_and_dismissal_style_updates_preserve_code()
    {
        await using var fixture = await ProfileFixture.CreateAsync();
        var employee = await fixture.Context.Employees.SingleAsync(e => e.Id == fixture.ManagerEmployeeId);
        employee.Position = "New position";
        employee.JobTitle = "New title";
        employee.Status = "Active";
        await fixture.Context.SaveChangesAsync();

        Assert.Equal("E_A1", (await fixture.Context.Employees.AsNoTracking().SingleAsync(e => e.Id == fixture.ManagerEmployeeId)).EmployeeCode);
    }

    [Fact]
    public async Task MGR07C_MAP_001_002_manager_and_employee_profiles_use_same_employee_code()
    {
        await using var fixture = await ProfileFixture.CreateAsync();
        var managerProfile = Assert.IsType<ManagerProfileViewModel>(Assert.IsType<ViewResult>(await fixture.Controller.Profile()).Model);
        var employeeProfile = Assert.IsType<Employee>(Assert.IsType<ViewResult>(await fixture.EmployeeController.MyProfile()).Model);

        Assert.Equal("E_A1", managerProfile.EmployeeCode);
        Assert.Equal(managerProfile.EmployeeCode, employeeProfile.EmployeeCode);
    }

    [Fact]
    public async Task MGR07C_MAP_003_004_hr_and_manager_employee_projections_use_employee_code()
    {
        await using var fixture = await ProfileFixture.CreateAsync();
        var hrRows = Assert.IsAssignableFrom<IEnumerable<Employee>>(Assert.IsType<ViewResult>(await fixture.EmployeeController.Index(null, null, "All")).Model);
        var managerRows = Assert.IsType<ManagerListViewModel<Employee>>(Assert.IsType<ViewResult>(await fixture.Controller.Employees()).Model);

        Assert.Equal("E_A1", hrRows.Single(e => e.Id == fixture.ManagerEmployeeId).EmployeeCode);
        Assert.Equal("E_A1", managerRows.Items.Single(e => e.Id == fixture.ManagerEmployeeId).EmployeeCode);
    }

    [Fact]
    public async Task MGR07C_MAP_003_report_projection_uses_employee_code_not_employee_id()
    {
        await using var fixture = await ProfileFixture.CreateAsync();
        var controller = new ReportController(fixture.Context);
        var rows = Assert.IsAssignableFrom<IEnumerable<Report>>(Assert.IsType<ViewResult>(await controller.Index(null, null, null, null)).Model);
        var export = Assert.IsType<FileContentResult>(await controller.Export(null, null, null, null));
        var csv = Encoding.UTF8.GetString(export.FileContents);

        Assert.Equal("E_A1", rows.Single(e => e.EmployeeName == "A1").EmployeeCode);
        Assert.Contains("Mã NV,Họ tên", csv);
        Assert.Contains("\"E_A1\",\"A1\"", csv);
    }

    [Fact]
    public async Task MGR07A_PROFILE_001_manager_profile_maps_real_employee_code()
    {
        await using var fixture = await ProfileFixture.CreateAsync();
        var model = Assert.IsType<ManagerProfileViewModel>(Assert.IsType<ViewResult>(await fixture.Controller.Profile()).Model);
        Assert.Equal("E_A1", model.EmployeeCode);
    }

    [Fact]
    public async Task MGR07A_PROFILE_002_phone_update_persists_and_profile_reloads_it()
    {
        await using var fixture = await ProfileFixture.CreateAsync();
        await fixture.EmployeeController.UpdateMyProfile(new Employee { PhoneNumber = "0901234567" }, null);
        var persisted = await fixture.Context.Employees.AsNoTracking().SingleAsync(e => e.Id == fixture.ManagerEmployeeId);
        var model = Assert.IsType<ManagerProfileViewModel>(Assert.IsType<ViewResult>(await fixture.Controller.Profile()).Model);
        Assert.Equal("0901234567", persisted.PhoneNumber);
        Assert.Equal("0901234567", model.PhoneNumber);
    }

    [Fact]
    public async Task MGR07A_PROFILE_003_address_update_persists()
    {
        await using var fixture = await ProfileFixture.CreateAsync();
        await fixture.EmployeeController.UpdateMyProfile(new Employee { Address = "12 Đường Mới" }, null);
        Assert.Equal("12 Đường Mới", (await fixture.Context.Employees.AsNoTracking().SingleAsync(e => e.Id == fixture.ManagerEmployeeId)).Address);
    }

    [Fact]
    public async Task MGR07A_PROFILE_004_birthdate_and_gender_updates_persist()
    {
        await using var fixture = await ProfileFixture.CreateAsync();
        var birthDate = new DateTime(1990, 5, 6);
        await fixture.EmployeeController.UpdateMyProfile(new Employee { BirthDate = birthDate, Gender = "Nữ" }, null);
        var employee = await fixture.Context.Employees.AsNoTracking().SingleAsync(e => e.Id == fixture.ManagerEmployeeId);
        Assert.Equal(birthDate, employee.BirthDate);
        Assert.Equal("Nữ", employee.Gender);
    }

    [Fact]
    public async Task MGR07A_PROFILE_005_bank_fields_persist()
    {
        await using var fixture = await ProfileFixture.CreateAsync();
        await fixture.EmployeeController.UpdateMyProfile(new Employee { BankName = "Vietcombank", BankAccountNumber = "123456789" }, null);
        var employee = await fixture.Context.Employees.AsNoTracking().SingleAsync(e => e.Id == fixture.ManagerEmployeeId);
        Assert.Equal("Vietcombank", employee.BankName);
        Assert.Equal("123456789", employee.BankAccountNumber);
    }

    [Fact]
    public async Task MGR07A_PROFILE_006_get_and_post_use_same_user_employee_row()
    {
        await using var fixture = await ProfileFixture.CreateAsync();
        await fixture.EmployeeController.UpdateMyProfile(new Employee { PhoneNumber = "same-row" }, null);
        var user = await fixture.Context.Users.AsNoTracking().SingleAsync(u => u.Username == "manager-a");
        var employee = await fixture.Context.Employees.AsNoTracking().SingleAsync(e => e.Id == user.EmployeeId);
        Assert.Equal(fixture.ManagerEmployeeId, user.EmployeeId);
        Assert.Equal("same-row", employee.PhoneNumber);
    }

    [Fact]
    public async Task MGR07A_PROFILE_007_arbitrary_employee_id_cannot_redirect_update()
    {
        await using var fixture = await ProfileFixture.CreateAsync();
        var other = await fixture.Context.Employees.AsNoTracking().SingleAsync(e => e.EmployeeCode == "E_B1");
        await fixture.EmployeeController.UpdateMyProfile(new Employee { Id = other.Id, PhoneNumber = "attacker" }, null);
        Assert.NotEqual("attacker", (await fixture.Context.Employees.AsNoTracking().SingleAsync(e => e.Id == other.Id)).PhoneNumber);
    }

    [Fact]
    public async Task MGR07A_PROFILE_008_employee_code_cannot_be_changed()
    {
        await using var fixture = await ProfileFixture.CreateAsync();
        await fixture.EmployeeController.UpdateMyProfile(new Employee { EmployeeCode = "FORGED" }, null);
        Assert.Equal("E_A1", (await fixture.Context.Employees.AsNoTracking().SingleAsync(e => e.Id == fixture.ManagerEmployeeId)).EmployeeCode);
    }

    [Fact]
    public async Task MGR07A_PROFILE_009_authoritative_work_fields_and_read_only_identity_cannot_change()
    {
        await using var fixture = await ProfileFixture.CreateAsync();
        await fixture.EmployeeController.UpdateMyProfile(new Employee { Name = "Forged Name", CitizenId = "999", Department = "UNIT_X", Position = "Forged Position", JobTitle = "Forged Title", Salary = 999m }, null);
        var employee = await fixture.Context.Employees.AsNoTracking().SingleAsync(e => e.Id == fixture.ManagerEmployeeId);
        Assert.Equal("A1", employee.Name);
        Assert.Equal("CITIZEN-A1", employee.CitizenId);
        Assert.Equal("UNIT_A", employee.Department);
        Assert.Equal("Staff", employee.Position);
        Assert.Equal("Employee", employee.JobTitle);
        Assert.Equal(0m, employee.Salary);
    }

    [Fact]
    public async Task MGR07A_PROFILE_010_manager_assignment_remains_unchanged()
    {
        await using var fixture = await ProfileFixture.CreateAsync();
        var before = await fixture.Context.ManagerAssignments.AsNoTracking().Select(a => new { a.UserId, a.OrganizationalUnit, a.EffectiveFrom, a.EffectiveTo }).ToListAsync();
        await fixture.EmployeeController.UpdateMyProfile(new Employee { Department = "UNIT_FORGED" }, null);
        var after = await fixture.Context.ManagerAssignments.AsNoTracking().Select(a => new { a.UserId, a.OrganizationalUnit, a.EffectiveFrom, a.EffectiveTo }).ToListAsync();
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task MGR07_PROFILE_001_current_manager_loads_own_linked_employee_profile()
    {
        await using var fixture = await ProfileFixture.CreateAsync();
        var result = await fixture.Controller.Profile();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ManagerProfileViewModel>(view.Model);
        Assert.True(model.HasLinkedEmployee);
        Assert.Equal("E_A1", model.EmployeeCode);
        Assert.Equal("manager-a", model.Username);
        Assert.Equal(AppRoles.Manager, model.AccountRole);
    }

    [Fact]
    public async Task MGR07_PROFILE_002_profile_identity_is_not_a_managed_employee_selector()
    {
        await using var fixture = await ProfileFixture.CreateAsync();
        var result = await fixture.Controller.Profile();
        var model = Assert.IsType<ManagerProfileViewModel>(Assert.IsType<ViewResult>(result).Model);

        Assert.Equal(fixture.ManagerEmployeeId, model.EmployeeId);
        Assert.NotEqual("E_B1", model.EmployeeCode);
    }

    [Fact]
    public async Task MGR07_PROFILE_003_zero_scope_manager_can_load_profile_and_active_units_are_filtered()
    {
        await using var fixture = await ProfileFixture.CreateAsync("manager-zero");
        var model = Assert.IsType<ManagerProfileViewModel>(Assert.IsType<ViewResult>(await fixture.Controller.Profile()).Model);

        Assert.True(model.HasLinkedEmployee);
        Assert.Equal("E_E1", model.EmployeeCode);
        Assert.Empty(model.ActiveOrganizationalUnits);
    }

    [Fact]
    public async Task MGR07_PROFILE_005_active_scope_excludes_ended_future_and_unassigned_units()
    {
        await using var fixture = await ProfileFixture.CreateAsync();
        var model = Assert.IsType<ManagerProfileViewModel>(Assert.IsType<ViewResult>(await fixture.Controller.Profile()).Model);

        Assert.Equal(new[] { "UNIT_A", "UNIT_B" }, model.ActiveOrganizationalUnits);
        Assert.DoesNotContain("UNIT_C", model.ActiveOrganizationalUnits);
        Assert.DoesNotContain("UNIT_D", model.ActiveOrganizationalUnits);
        Assert.DoesNotContain("UNIT_E", model.ActiveOrganizationalUnits);
    }

    [Fact]
    public async Task MGR07_PROFILE_008_012_self_service_update_ignores_authoritative_tampering_and_other_employee_id()
    {
        await using var fixture = await ProfileFixture.CreateAsync();
        var own = await fixture.Context.Employees.SingleAsync(e => e.EmployeeCode == "E_A1");
        var other = await fixture.Context.Employees.SingleAsync(e => e.EmployeeCode == "E_B1");
        var beforeOther = (other.PhoneNumber, other.Department, other.Position, other.JobTitle, other.Salary);
        var assignmentCount = await fixture.Context.ManagerAssignments.CountAsync();

        var result = await fixture.EmployeeController.UpdateMyProfile(new Employee
        {
            Id = other.Id,
            PhoneNumber = "0900000000",
            Address = "Updated address",
            Department = "UNIT_B",
            Position = "Tampered position",
            JobTitle = "Tampered title",
            Salary = 999999999
        }, null);

        Assert.IsType<RedirectToActionResult>(result);
        var persistedOwn = await fixture.Context.Employees.FindAsync(own.Id);
        var persistedOther = await fixture.Context.Employees.FindAsync(other.Id);
        Assert.Equal("0900000000", persistedOwn!.PhoneNumber);
        Assert.Equal("Updated address", persistedOwn.Address);
        Assert.Equal(("old@example.com", "UNIT_A", "Staff", "Employee", 0m),
            (persistedOwn.Email ?? "", persistedOwn.Department, persistedOwn.Position, persistedOwn.JobTitle, persistedOwn.Salary));
        Assert.Equal(beforeOther, (persistedOther!.PhoneNumber, persistedOther.Department, persistedOther.Position, persistedOther.JobTitle, persistedOther.Salary));
        Assert.Equal(assignmentCount, await fixture.Context.ManagerAssignments.CountAsync());
    }

    private sealed class ProfileFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        public ApplicationDbContext Context { get; }
        public ManagerScopeService Scope { get; }
        public ManagerWorkspaceController Controller { get; }
        public EmployeeController EmployeeController { get; }
        public int ManagerEmployeeId { get; }

        private ProfileFixture(SqliteConnection connection, ApplicationDbContext context, ManagerScopeService scope,
            ManagerWorkspaceController controller, EmployeeController employeeController, int managerEmployeeId)
        { _connection = connection; Context = context; Scope = scope; Controller = controller; EmployeeController = employeeController; ManagerEmployeeId = managerEmployeeId; }

        public static async Task<ProfileFixture> CreateAsync(string username = "manager-a")
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var http = new DefaultHttpContext();
            http.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, username),
                new Claim(ClaimTypes.Role, AppRoles.Manager)
            }, "Test"));
            var accessor = new FixedAccessor(http);
            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
            var context = new ApplicationDbContext(options, accessor);
            await context.Database.EnsureCreatedAsync();
            var users = new[]
            {
                new User { Username = "manager-a", FullName = "Manager A", Email = "manager-a@test", Role = AppRoles.Manager, CanLogin = true },
                new User { Username = "manager-zero", FullName = "Manager Zero", Email = "manager-zero@test", Role = AppRoles.Manager, CanLogin = true }
            };
            var employees = new[]
            {
                new Employee { EmployeeCode = "E_A1", Name = "A1", Email = "old@example.com", PhoneNumber = "old", Address = "old address", Gender = "Nam", BirthDate = new DateTime(1990, 1, 1), CitizenId = "CITIZEN-A1", Department = "UNIT_A", Position = "Staff", JobTitle = "Employee", Status = "Active" },
                new Employee { EmployeeCode = "E_B1", Name = "B1", Department = "UNIT_B", Position = "Other", JobTitle = "Employee", Status = "Active" },
                new Employee { EmployeeCode = "E_E1", Name = "E1", Department = "UNIT_E", Status = "Active" }
            };
            context.Users.AddRange(users); context.Employees.AddRange(employees);
            await context.SaveChangesAsync();
            users[0].EmployeeId = employees[0].Id; users[1].EmployeeId = employees[2].Id;
            context.ManagerAssignments.AddRange(
                new ManagerAssignment { UserId = users[0].Id, OrganizationalUnit = "UNIT_A", EffectiveFrom = DateTime.UtcNow.AddDays(-1) },
                new ManagerAssignment { UserId = users[0].Id, OrganizationalUnit = "UNIT_B", EffectiveFrom = DateTime.UtcNow.AddDays(-1) },
                new ManagerAssignment { UserId = users[0].Id, OrganizationalUnit = "UNIT_C", EffectiveFrom = DateTime.UtcNow.AddDays(-30), EffectiveTo = DateTime.UtcNow.AddDays(-1) },
                new ManagerAssignment { UserId = users[0].Id, OrganizationalUnit = "UNIT_D", EffectiveFrom = DateTime.UtcNow.AddDays(1) });
            await context.SaveChangesAsync();
            var scope = new ManagerScopeService(context, accessor);
            var manager = new ManagerWorkspaceController(context, scope, null!, null!, null!, null!) { ControllerContext = new ControllerContext { HttpContext = http } };
            var employee = new EmployeeController(context, null!, null!, new NoopAudit(), null!) { ControllerContext = new ControllerContext { HttpContext = http } };
            return new ProfileFixture(connection, context, scope, manager, employee, users[0].EmployeeId!.Value);
        }

        public static async Task SeedCanonicalCodesAsync(ApplicationDbContext context, int count)
        {
            context.Employees.AddRange(Enumerable.Range(1, count).Select(i => new Employee
            {
                EmployeeCode = $"NV{i:D3}",
                Name = $"Canonical {i}",
                Status = "Active"
            }));
            await context.SaveChangesAsync();
        }

        public async ValueTask DisposeAsync() { await Context.DisposeAsync(); await _connection.DisposeAsync(); }
    }

    private sealed class NoopAudit : IAuditLogService
    {
        public Task LogAsync(string action, string module, string resource, string resourceId, string eventType, string status, string? failureReason = null, string? oldValues = null, string? newValues = null, string? changes = null) => Task.CompletedTask;
        public Task LogLoginAsync(string username, bool isSuccess, string? failureReason = null) => Task.CompletedTask;
        public Task LogLogoutAsync(string username) => Task.CompletedTask;
        public Task LogSecurityEventAsync(string action, string resource, string resourceId, string status, string? failureReason = null) => Task.CompletedTask;
    }
    private sealed class FixedAccessor : IHttpContextAccessor { public FixedAccessor(HttpContext context) => HttpContext = context; public HttpContext? HttpContext { get; set; } }
}
