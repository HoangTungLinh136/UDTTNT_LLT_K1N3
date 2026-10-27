using giao_dien_demo.Services.AI;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using giao_dien_demo.Data;
using giao_dien_demo.Hubs;
using giao_dien_demo.Models;
using giao_dien_demo.Services;
using giao_dien_demo.Authorization;
using System;
using System.IO;
using System.Security.Claims;
using System.Linq;
using Microsoft.AspNetCore.Authentication;


var builder = WebApplication.CreateBuilder(args);


// =========================================================
// DEVELOPMENT LOGGING
// =========================================================

if (builder.Environment.IsDevelopment())
{
    builder.Logging.ClearProviders();
    builder.Logging.AddConsole();
    builder.Logging.AddDebug();
}


// =========================================================
// MVC
// =========================================================

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add<
        giao_dien_demo.Filters.FileValidationFilter
    >();
});


// =========================================================
// AVA / AI
// =========================================================

builder.Services.AddHttpClient<AiService>();

builder.Services.AddHttpClient(
    "AvaGateway",
    client =>
    {
        client.BaseAddress = new Uri(
            builder.Configuration["Ava:BaseUrl"]
            ?? "http://127.0.0.1:8001/"
        );

        // First RAG request may cold-load the local embedding model.
        client.Timeout = TimeSpan.FromSeconds(240);
    }
);

builder.Services.AddScoped<
    IAvaGatewayService,
    AvaGatewayService
>();

builder.Services.AddScoped<
    IAvaConversationStore,
    AvaConversationStore
>();

builder.Services.AddScoped<
    AiContextBuilderService
>();


// =========================================================
// AVA PERSONAL MEMORY
// PHASE 04 + PHASE 05
// =========================================================

builder.Services.AddScoped<
    IMemoryExtractor,
    MemoryExtractor
>();

builder.Services.AddScoped<
    IUserMemoryService,
    UserMemoryService
>();

builder.Services.AddScoped<
    IUserMemoryContextProvider,
    UserMemoryContextProvider
>();


// =========================================================
// NOTIFICATION SETTINGS
// =========================================================

builder.Services.AddSingleton<
    NotificationConfigService
>();

builder.Services.AddSingleton<
    IntegrationConfigService
>();

builder.Services.AddScoped<
    NotificationEngineService
>();

builder.Services.AddScoped<
    IInAppNotificationService,
    InAppNotificationService
>();


// =========================================================
// AUDIT / APPROVAL
// =========================================================

builder.Services.AddScoped<
    IAuditLogService,
    AuditLogService
>();

builder.Services.AddScoped<
    IApprovalEngine,
    ApprovalEngineService
>();


// =========================================================
// WORKFLOW POLICIES
// =========================================================

builder.Services.AddScoped<
    IWorkflowPolicy<Leave>,
    LeaveWorkflowPolicy
>();

builder.Services.AddScoped<
    LeaveWorkflowPolicy
>();

builder.Services.AddScoped<
    ILeaveService,
    LeaveService
>();

builder.Services.AddScoped<
    IWorkflowPolicy<Contract>,
    ContractWorkflowPolicy
>();

builder.Services.AddScoped<
    ContractWorkflowPolicy
>();

builder.Services.AddScoped<
    IWorkflowPolicy<AttendanceAdjustmentRequest>,
    AdjustmentWorkflowPolicy
>();

builder.Services.AddScoped<
    AdjustmentWorkflowPolicy
>();

builder.Services.AddScoped<
    IWorkflowPolicy<Proposal>,
    ProposalWorkflowPolicy
>();

builder.Services.AddScoped<
    ProposalWorkflowPolicy
>();

builder.Services.AddScoped<
    IWorkflowPolicy<TransferRecord>,
    TransferWorkflowPolicy
>();

builder.Services.AddScoped<
    IWorkflowPolicy<AppointmentRecord>,
    AppointmentWorkflowPolicy
>();

builder.Services.AddScoped<
    IWorkflowPolicy<DismissalRecord>,
    DismissalWorkflowPolicy
>();

builder.Services.AddScoped<
    IWorkflowPolicy<OffboardingRecord>,
    OffboardingWorkflowPolicy
>();

builder.Services.AddScoped<
    IWorkflowPolicy<OnboardingRecord>,
    OnboardingWorkflowPolicy
>();

builder.Services.AddScoped<
    IWorkflowPolicy<CourseEnrollment>,
    CourseEnrollmentWorkflowPolicy
>();

builder.Services.AddScoped<
    IWorkflowPolicy<BenefitEnrollment>,
    BenefitEnrollmentWorkflowPolicy
>();

builder.Services.AddScoped<
    IWorkflowPolicy<RewardRecord>,
    RewardWorkflowPolicy
>();

builder.Services.AddScoped<
    RewardWorkflowPolicy
>();


// =========================================================
// HR SERVICES
// =========================================================

builder.Services.AddScoped<
    IAttendanceCalculationService,
    AttendanceCalculationService
>();

builder.Services.AddScoped<
    IPayrollCalculationService,
    PayrollCalculationService
>();

builder.Services.AddScoped<
    IBackupService,
    BackupService
>();

builder.Services.AddScoped<
    ISystemConfigService,
    SystemConfigService
>();

builder.Services.AddSingleton<
    ITelemetryStore,
    TelemetryStore
>();

builder.Services.AddScoped<
    IProtectedFileService,
    ProtectedFileService
>();

builder.Services.AddScoped<
    IManagerScopeService,
    ManagerScopeService
>();

builder.Services.AddScoped<
    IManagerAssignmentService,
    ManagerAssignmentService
>();

builder.Services.AddScoped<
    IEmployeeCodeAssignmentService,
    EmployeeCodeAssignmentService
>();

builder.Services.AddScoped<
    IAdminAccountNotificationService,
    AdminAccountNotificationService
>();

builder.Services.AddScoped<
    ILeadershipScopeService,
    LeadershipScopeService
>();

builder.Services.AddScoped<
    ICameraCredentialProtector,
    CameraCredentialProtector
>();

builder.Services.AddScoped<
    IKPIManagementService,
    KPIManagementService
>();


// =========================================================
// DATA PROTECTION
// =========================================================

builder.Services
    .AddDataProtection()
    .PersistKeysToFileSystem(
        new DirectoryInfo(
            Path.Combine(
                builder.Environment.ContentRootPath,
                "App_Data",
                "Keys"
            )
        )
    )
    .SetApplicationName("HRMS");


// =========================================================
// EFFECTIVE SERVICES
// =========================================================

builder.Services.AddScoped<
    IAppointmentEffectiveService,
    AppointmentEffectiveService
>();

builder.Services.AddScoped<
    ITransferEffectiveService,
    TransferEffectiveService
>();

builder.Services.AddScoped<
    IDismissalEffectiveService,
    DismissalEffectiveService
>();


// =========================================================
// BACKGROUND SERVICES
// =========================================================

builder.Services.AddHostedService<
    AutoBackupService
>();

builder.Services.AddHostedService<
    SystemMonitoringAgent
>();

builder.Services.AddHostedService<
    EmployeeLifecycleBackgroundService
>();

builder.Services.AddHostedService<
    SalaryReminderService
>();

builder.Services.AddHostedService<
    CameraStreamService
>();

builder.Services.AddHostedService<
    OffboardingEffectiveService
>();

builder.Services.AddHostedService<
    NotificationBackgroundWorker
>();


// =========================================================
// HTTP CONTEXT
// =========================================================

builder.Services.AddHttpContextAccessor();


// =========================================================
// SESSION / AUTHENTICATION
// =========================================================

builder.Services.AddDistributedMemoryCache();

builder.Services
    .AddAuthentication(
        CookieAuthenticationDefaults.AuthenticationScheme
    )
    .AddCookie(options =>
    {
        options.LoginPath =
            "/Account/Login";

        options.LogoutPath =
            "/Account/Logout";

        options.AccessDeniedPath =
            "/Account/AccessDenied";

        options.Cookie.HttpOnly = true;

        options.Cookie.SameSite =
            Microsoft.AspNetCore.Http.SameSiteMode.Lax;

        options.Cookie.SecurePolicy =
            builder.Environment.IsDevelopment()
                ? Microsoft.AspNetCore.Http.CookieSecurePolicy.SameAsRequest
                : Microsoft.AspNetCore.Http.CookieSecurePolicy.Always;

        options.ExpireTimeSpan =
            TimeSpan.FromMinutes(30);

        options.SlidingExpiration = true;

        options.Events =
            new CookieAuthenticationEvents
            {
                OnValidatePrincipal =
                    async context =>
                    {
                        var username =
                            context.Principal?
                                .FindFirstValue(
                                    ClaimTypes.NameIdentifier
                                );

                        var cookieRole =
                            context.Principal?
                                .FindFirstValue(
                                    ClaimTypes.Role
                                );

                        if (
                            string.IsNullOrEmpty(username)
                            ||
                            string.IsNullOrEmpty(cookieRole)
                        )
                        {
                            context.RejectPrincipal();

                            await context.HttpContext
                                .SignOutAsync(
                                    CookieAuthenticationDefaults
                                        .AuthenticationScheme
                                );

                            return;
                        }

                        var dbContext =
                            context.HttpContext
                                .RequestServices
                                .GetRequiredService<
                                    ApplicationDbContext
                                >();

                        var dbUser =
                            await dbContext.Users
                                .Where(
                                    u =>
                                        u.Username
                                        == username
                                )
                                .Select(
                                    u => new
                                    {
                                        u.CanLogin,
                                        u.Role
                                    }
                                )
                                .SingleOrDefaultAsync();

                        if (
                            dbUser == null
                            ||
                            dbUser.CanLogin != true
                            ||
                            string.IsNullOrEmpty(
                                dbUser.Role
                            )
                            ||
                            dbUser.Role
                            != cookieRole
                        )
                        {
                            context.RejectPrincipal();

                            await context.HttpContext
                                .SignOutAsync(
                                    CookieAuthenticationDefaults
                                        .AuthenticationScheme
                                );
                        }
                    }
            };
    });


// =========================================================
// AUTHORIZATION
// =========================================================

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(
        AppPolicies.ManagerOnly,
        policy =>
            policy
                .RequireAuthenticatedUser()
                .RequireRole(
                    AppRoles.Manager
                )
    );

    options.AddPolicy(
        AppPolicies.LeaderOnly,
        policy =>
            policy
                .RequireAuthenticatedUser()
                .RequireRole(
                    AppRoles.Leader
                )
    );

    options.AddPolicy(
        AppPolicies.HROnly,
        policy =>
            policy
                .RequireAuthenticatedUser()
                .RequireRole(
                    AppRoles.HR
                )
    );

    options.AddPolicy(
        AppPolicies.AdminOnly,
        policy =>
            policy
                .RequireAuthenticatedUser()
                .RequireRole(
                    AppRoles.Admin
                )
    );

    options.AddPolicy(
        AppPolicies.EmployeeOnly,
        policy =>
            policy
                .RequireAuthenticatedUser()
                .RequireRole(
                    AppRoles.Employee
                )
    );
});


// =========================================================
// SESSION
// =========================================================

builder.Services.AddSession(options =>
{
    options.IdleTimeout =
        TimeSpan.FromMinutes(30);

    options.Cookie.HttpOnly = true;

    options.Cookie.IsEssential = true;
});


// =========================================================
// DB CONTEXT
// =========================================================

builder.Services.AddDbContext<
    ApplicationDbContext
>(
    options =>
        options.UseSqlServer(
            builder.Configuration
                .GetConnectionString(
                    "DefaultConnection"
                )
        )
);


// =========================================================
// SIGNALR
// =========================================================

builder.Services.AddSignalR();


// =========================================================
// BUILD APP
// =========================================================

var app = builder.Build();


// =========================================================
// ERROR HANDLING
// =========================================================

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(
        "/Home/Error"
    );

    app.UseHsts();
}


// =========================================================
// MIDDLEWARE
// =========================================================

// Explicitly allow microphone capture for this same-origin application.
// Browser/Windows privacy consent is still required and cannot be bypassed by
// application code, but this prevents an accidental Permissions-Policy block.
app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        context.Response.Headers["Permissions-Policy"] =
            "microphone=(self)";
        return Task.CompletedTask;
    });

    await next();
});

app.UseHttpsRedirection();

app.UseStaticFiles();


// Performance / monitoring

app.UseMiddleware<
    giao_dien_demo.Middlewares
        .SystemMonitoringMiddlewares
>();


app.UseRouting();


// =========================================================
// SESSION
// =========================================================

app.UseSession();


// =========================================================
// MAINTENANCE
// =========================================================

app.UseMiddleware<
    giao_dien_demo.Middlewares
        .MaintenanceMiddleware
>();


// =========================================================
// AUTHENTICATION / AUTHORIZATION
// =========================================================

app.UseAuthentication();

app.UseMiddleware<
    giao_dien_demo.Middlewares
        .SessionSecurityMiddleware
>();

app.UseAuthorization();


// =========================================================
// ROUTES
// =========================================================

// Keep the site root on the login page without making every one-segment
// controller URL resolve to a nonexistent Login action.
app.MapControllerRoute(
    name: "root",
    pattern: "",
    defaults: new
    {
        controller = "Account",
        action = "Login"
    }
);


app.MapControllerRoute(
    name: "account",
    pattern: "Account",
    defaults: new
    {
        controller = "Account",
        action = "Login"
    }
);


// Employee route

app.MapControllerRoute(
    name: "employee",
    pattern: "Employee",
    defaults: new
    {
        controller = "Employee",
        action = "Index"
    }
);


// Default route

app.MapControllerRoute(
    name: "default",
    pattern:
        "{controller=Account}/{action=Index}/{id?}"
);


// =========================================================
// SIGNALR HUB
// =========================================================

app.MapHub<
    DashboardHub
>(
    "/dashboardHub"
);


// =========================================================
// RUN
// =========================================================

app.Run();

