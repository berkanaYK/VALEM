using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VALE.Api.Configuration;
using VALE.Api.Controllers;
using VALE.Api.Data;
using VALE.Api.Domain;
using VALE.Api.Services;
using VALE.Contracts;
using Xunit;

namespace VALE.Api.Tests;

/// <summary>
/// Negative authorization tests deliberately use identifiers owned by a second tenant.
/// Object and branch probes must receive the same non-enumerating 404 used for an unknown identifier.
/// </summary>
public sealed class BolaAuthorizationTests
{
    [Fact]
    public async Task Collection_endpoints_do_not_enumerate_another_tenant()
    {
        await using var harness = await MultiTenantHarness.CreateAsync();

        var branches = OkValue(await harness.Controller<BranchesController>().GetAll(default));
        Assert.NotEmpty(branches);
        Assert.All(branches, branch => Assert.NotEqual(harness.BranchBId, branch.Id));

        var notifications = OkValue(await harness.Controller<NotificationsController>().Get(false, 100, default));
        var ownNotification = Assert.Single(notifications);
        Assert.Equal(harness.NotificationAId, ownNotification.Id);

        var registrations = OkValue(await harness.Controller<TenantRegistrationController>().GetPending(default));
        Assert.NotEmpty(registrations);
        Assert.All(registrations, registration => Assert.Equal(harness.CompanyAId, registration.CompanyId));

        var pushRegistrations = OkValue(await harness.Controller<PushController>().Registrations(default));
        var ownPushRegistration = Assert.Single(pushRegistrations);
        Assert.Equal(harness.PushRegistrationAId, ownPushRegistration.Id);

        var auditEntries = OkValue(await harness.Controller<AuditController>().Get(null, null, null, 100, default));
        Assert.NotEmpty(auditEntries);
        Assert.All(auditEntries, entry => Assert.NotEqual(harness.AuditEntryBId, entry.Id));

        var users = OkValue(await harness.Controller<AdminController>().GetUsers(default));
        Assert.NotEmpty(users);
        Assert.DoesNotContain(users, user => user.Id == harness.UserBId);
    }

    [Fact]
    public async Task Foreign_ticket_cannot_be_read_updated_deleted_or_actioned()
    {
        await using var harness = await MultiTenantHarness.CreateAsync();
        var tickets = harness.Service<TicketService>();

        await AssertObjectDeniedAsync(() => tickets.GetDetailAsync(harness.TicketBId, default));
        await AssertObjectDeniedAsync(() => tickets.UpdateDetailsAsync(
            harness.TicketBId,
            new UpdateTicketDetailsRequest(
                LicensePlate: "06 FOREIGN",
                Brand: "Other",
                Model: null,
                Color: null,
                Year: null,
                FuelType: null,
                Transmission: null,
                CustomerName: null,
                CustomerPhone: null,
                KeyTag: null,
                ParkingSpot: null,
                Notes: "must not change",
                HourlyRate: 999m,
                PhotoBase64: null),
            default));
        await AssertObjectDeniedAsync(() => tickets.DeleteAsync(harness.TicketBId, "BOLA negative test", default));
        await AssertObjectDeniedAsync(() => tickets.UpdateStatusAsync(harness.TicketBId, TicketStatus.Requested, default));
        await AssertObjectDeniedAsync(() => tickets.CheckoutAsync(harness.TicketBId, PaymentMethod.Card, default));

        harness.Db.ChangeTracker.Clear();
        var foreignTicket = await harness.Db.ParkingTickets
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(ticket => ticket.Id == harness.TicketBId);
        Assert.Equal(harness.CompanyBId, foreignTicket.CompanyId);
        Assert.Equal(TicketStatus.Received, foreignTicket.Status);
        Assert.Null(foreignTicket.DeletedAt);
        Assert.Equal(100m, foreignTicket.HourlyRate);
    }

    [Fact]
    public async Task Foreign_branch_cannot_be_used_for_queries_creation_dashboard_reports_or_audit()
    {
        await using var harness = await MultiTenantHarness.CreateAsync();
        var tickets = harness.Service<TicketService>();

        await AssertObjectDeniedAsync(() => tickets.QueryAsync(harness.BranchBId, null, null, true, 1, 50, default));
        await AssertObjectDeniedAsync(() => tickets.GetDashboardAsync(harness.BranchBId, default));
        await AssertObjectDeniedAsync(() => tickets.CreateAsync(
            new CreateTicketRequest(
                BranchId: harness.BranchBId,
                LicensePlate: "06 NEW 06",
                Brand: null,
                Model: null,
                Color: null,
                CustomerName: null,
                CustomerPhone: null,
                KeyTag: null,
                ParkingSpot: null,
                Notes: null,
                HourlyRate: null),
            default));

        await AssertObjectDeniedAsync(() => harness.Controller<ReportsController>()
            .Summary(harness.BranchBId, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow, default));
        await AssertObjectDeniedAsync(() => harness.Controller<AuditController>()
            .Get(harness.BranchBId, null, null, 100, default));

        Assert.Equal(1, await harness.Db.ParkingTickets.CountAsync(ticket => ticket.CompanyId == harness.CompanyBId));
    }

    [Fact]
    public async Task Unassigned_same_tenant_branch_group_cannot_be_read_or_mutated()
    {
        await using var harness = await MultiTenantHarness.CreateAsync();
        harness.AuthenticateAsTenantA(Roles.BranchManager);
        var tickets = harness.Service<TicketService>();

        await AssertObjectDeniedAsync(() => tickets.QueryAsync(harness.BranchAUnassignedId, null, null, true, 1, 50, default));
        await AssertObjectDeniedAsync(() => tickets.GetDashboardAsync(harness.BranchAUnassignedId, default));
        await AssertObjectDeniedAsync(() => tickets.GetDetailAsync(harness.TicketAUnassignedId, default));
        await AssertObjectDeniedAsync(() => tickets.UpdateDetailsAsync(
            harness.TicketAUnassignedId,
            new UpdateTicketDetailsRequest(
                LicensePlate: "06 A 099",
                Brand: "Other branch",
                Model: null,
                Color: null,
                Year: null,
                FuelType: null,
                Transmission: null,
                CustomerName: null,
                CustomerPhone: null,
                KeyTag: null,
                ParkingSpot: null,
                Notes: "must not change",
                HourlyRate: 999m,
                PhotoBase64: null),
            default));
        await AssertObjectDeniedAsync(() => tickets.DeleteAsync(harness.TicketAUnassignedId, "branch BOLA test", default));
        await AssertObjectDeniedAsync(() => tickets.UpdateStatusAsync(harness.TicketAUnassignedId, TicketStatus.Requested, default));
        await AssertObjectDeniedAsync(() => tickets.CheckoutAsync(harness.TicketAUnassignedId, PaymentMethod.Card, default));
        await AssertObjectDeniedAsync(() => tickets.CreateAsync(
            new CreateTicketRequest(
                BranchId: harness.BranchAUnassignedId,
                LicensePlate: "06 A 100",
                Brand: null,
                Model: null,
                Color: null,
                CustomerName: null,
                CustomerPhone: null,
                KeyTag: null,
                ParkingSpot: null,
                Notes: null,
                HourlyRate: null),
            default));
        await AssertObjectDeniedAsync(() => harness.Controller<ReportsController>()
            .Summary(harness.BranchAUnassignedId, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow, default));

        harness.AuthenticateAsTenantA(Roles.Auditor);
        await AssertObjectDeniedAsync(() => harness.Controller<AuditController>()
            .Get(harness.BranchAUnassignedId, null, null, 100, default));

        harness.Db.ChangeTracker.Clear();
        var ticket = await harness.Db.ParkingTickets.AsNoTracking()
            .SingleAsync(row => row.Id == harness.TicketAUnassignedId);
        Assert.Equal(TicketStatus.Received, ticket.Status);
        Assert.Null(ticket.DeletedAt);
        Assert.Equal(100m, ticket.HourlyRate);
        Assert.Equal(2, await harness.Db.ParkingTickets.CountAsync(row => row.CompanyId == harness.CompanyAId));
    }

    [Fact]
    public async Task Foreign_user_registration_notification_and_push_objects_cannot_be_changed()
    {
        await using var harness = await MultiTenantHarness.CreateAsync();
        var admin = harness.Controller<AdminController>();

        await AssertObjectDeniedAsync(() => admin.GetUser(harness.UserBId, default));
        await AssertObjectDeniedAsync(() => admin.UpdateUserStatus(
            harness.UserBId,
            new UpdateUserStatusRequest(false),
            default));
        await AssertObjectDeniedAsync(() => admin.UpdateUser(
            harness.UserBId,
            new UpdateAdminUserRequest(
                FullName: "Foreign user",
                PhoneNumber: null,
                EmployeeCode: null,
                JobTitle: null,
                BranchId: harness.BranchAId,
                IsActive: false,
                Roles: [Roles.Valet]),
            default));
        await AssertObjectDeniedAsync(() => admin.GetUserBranches(harness.UserBId, default));
        await AssertObjectDeniedAsync(() => admin.UpdateUserBranches(
            harness.UserBId,
            new UpdateUserBranchMembershipsRequest(harness.BranchBId, [harness.BranchBId]),
            default));

        await AssertObjectDeniedAsync(() => admin.UpdateUserBranches(
            harness.UserAStaffId,
            new UpdateUserBranchMembershipsRequest(
                harness.BranchAId,
                [harness.BranchAId, harness.BranchBId]),
            default));

        await AssertObjectDeniedAsync(() => harness.Controller<TenantRegistrationController>()
            .Decide(harness.RegistrationBId, new RegistrationDecisionRequest(true), default));
        var notifications = harness.Controller<NotificationsController>();
        await AssertObjectDeniedAsync(() => notifications
            .SetRead(harness.NotificationBId, new MarkNotificationReadRequest(), default));
        await AssertObjectDeniedAsync(() => notifications
            .SetRead(harness.NotificationAStaffId, new MarkNotificationReadRequest(), default));
        await notifications.ReadAll(default);

        var push = harness.Controller<PushController>();
        await push.Unregister(new PushRegistrationRequest(harness.PushTokenB, "android", "attacker"), default);
        await push.Unregister(new PushRegistrationRequest(harness.PushTokenAStaff, "android", "attacker"), default);

        harness.Db.ChangeTracker.Clear();
        var foreignUser = await harness.Db.Users.AsNoTracking().SingleAsync(user => user.Id == harness.UserBId);
        var foreignRegistration = await harness.Db.RegistrationRequests.AsNoTracking().SingleAsync(row => row.Id == harness.RegistrationBId);
        var ownNotification = await harness.Db.Notifications.AsNoTracking().SingleAsync(row => row.Id == harness.NotificationAId);
        var sameTenantNotification = await harness.Db.Notifications.AsNoTracking().SingleAsync(row => row.Id == harness.NotificationAStaffId);
        var foreignNotification = await harness.Db.Notifications.AsNoTracking().SingleAsync(row => row.Id == harness.NotificationBId);
        var sameTenantPush = await harness.Db.PushRegistrations.AsNoTracking().SingleAsync(row => row.Id == harness.PushRegistrationAStaffId);
        var foreignPush = await harness.Db.PushRegistrations.AsNoTracking().SingleAsync(row => row.Id == harness.PushRegistrationBId);

        Assert.True(foreignUser.IsActive);
        Assert.Equal("Pending", foreignRegistration.Status);
        Assert.True(ownNotification.IsRead);
        Assert.False(sameTenantNotification.IsRead);
        Assert.False(foreignNotification.IsRead);
        Assert.Equal(harness.UserAStaffId, sameTenantPush.UserId);
        Assert.True(sameTenantPush.IsActive);
        Assert.Equal(harness.CompanyBId, foreignPush.CompanyId);
        Assert.Equal(harness.UserBId, foreignPush.UserId);
        Assert.True(foreignPush.IsActive);
        Assert.False(await harness.Db.UserBranchMemberships.AsNoTracking().AnyAsync(membership =>
            membership.UserId == harness.UserAStaffId && membership.BranchId == harness.BranchBId));
    }

    [Fact]
    public async Task Foreign_push_token_cannot_be_reassigned_to_the_calling_tenant()
    {
        await using var harness = await MultiTenantHarness.CreateAsync();
        var push = harness.Controller<PushController>();

        await AssertOwnershipConflictAsync(() => push.Register(
            new PushRegistrationRequest(harness.PushTokenB, "android", "attacker"),
            default));
        await AssertOwnershipConflictAsync(() => push.Register(
            new PushRegistrationRequest(harness.PushTokenAStaff, "android", "attacker"),
            default));

        harness.Db.ChangeTracker.Clear();
        var foreignPush = await harness.Db.PushRegistrations.AsNoTracking()
            .SingleAsync(row => row.Id == harness.PushRegistrationBId);
        var sameTenantPush = await harness.Db.PushRegistrations.AsNoTracking()
            .SingleAsync(row => row.Id == harness.PushRegistrationAStaffId);
        Assert.Equal(harness.CompanyBId, foreignPush.CompanyId);
        Assert.Equal(harness.UserBId, foreignPush.UserId);
        Assert.True(foreignPush.IsActive);
        Assert.Equal(harness.CompanyAId, sameTenantPush.CompanyId);
        Assert.Equal(harness.UserAStaffId, sameTenantPush.UserId);
        Assert.True(sameTenantPush.IsActive);
    }

    [Fact]
    public async Task Legacy_registration_cannot_bypass_email_ownership_verification()
    {
        await using var harness = await MultiTenantHarness.CreateAsync();
        var email = "legacy-bypass@example.test";
        var before = await harness.Db.Users.CountAsync();

        var error = await Assert.ThrowsAsync<ApiException>(() => harness.Controller<AuthController>().Register(
            new RegisterRequest(
                FullName: "Legacy Registration",
                Email: email,
                Password: "Strong!Password1",
                PhoneNumber: null,
                BranchCode: "A1",
                EmployeeCode: null),
            default));

        Assert.Equal(StatusCodes.Status410Gone, error.StatusCode);
        Assert.Equal(before, await harness.Db.Users.CountAsync());
        Assert.Null(await harness.Users.FindByEmailAsync(email));
    }

    [Fact]
    public async Task Branch_manager_cannot_escalate_roles_or_manage_an_unassigned_branch_group()
    {
        await using var harness = await MultiTenantHarness.CreateAsync();
        harness.AuthenticateAsTenantA(Roles.BranchManager);
        var admin = harness.Controller<AdminController>();

        await AssertForbiddenAsync(() => admin.CreateUser(
            new CreateUserRequest(
                Email: "escalation@example.test",
                Password: "Strong!Password1",
                FullName: "Escalation Attempt",
                BranchId: harness.BranchAId,
                Roles: [Roles.Owner]),
            default));

        await AssertForbiddenAsync(() => admin.UpdateUser(
            harness.UserAStaffId,
            new UpdateAdminUserRequest(
                FullName: "Tenant A Staff",
                PhoneNumber: null,
                EmployeeCode: "A-STAFF",
                JobTitle: "Valet",
                BranchId: harness.BranchAId,
                IsActive: true,
                Roles: [Roles.Admin]),
            default));

        await AssertObjectDeniedAsync(() => admin.CreateUser(
            new CreateUserRequest(
                Email: "other-branch@example.test",
                Password: "Strong!Password1",
                FullName: "Other Branch Attempt",
                BranchId: harness.BranchAUnassignedId,
                Roles: [Roles.Valet]),
            default));

        await AssertForbiddenAsync(() => admin.GetUser(harness.UserAOtherBranchId, default));
        await AssertForbiddenAsync(() => admin.UpdateUserStatus(
            harness.UserAOtherBranchId,
            new UpdateUserStatusRequest(false),
            default));

        var branches = OkValue(await harness.Controller<BranchesController>().GetAll(default));
        Assert.Single(branches);
        Assert.Equal(harness.BranchAId, branches[0].Id);

        var users = OkValue(await admin.GetUsers(default));
        Assert.DoesNotContain(users, user => user.Id == harness.UserAOtherBranchId);

        var pending = OkValue(await harness.Controller<TenantRegistrationController>().GetPending(default));
        Assert.Contains(pending, request => request.Id == harness.RegistrationAId);
        Assert.DoesNotContain(pending, request => request.Id == harness.RegistrationAUnassignedId);
        await AssertObjectDeniedAsync(() => harness.Controller<TenantRegistrationController>().Decide(
            harness.RegistrationAUnassignedId,
            new RegistrationDecisionRequest(true),
            default));

        var roles = await harness.Users.GetRolesAsync(
            await harness.Users.FindByIdAsync(harness.UserAStaffId.ToString()) ?? throw new InvalidOperationException());
        Assert.Equal(new[] { Roles.Valet }, roles);
        Assert.Null(await harness.Users.FindByEmailAsync("escalation@example.test"));
        Assert.Null(await harness.Users.FindByEmailAsync("other-branch@example.test"));
        var otherBranchUser = await harness.Users.FindByIdAsync(harness.UserAOtherBranchId.ToString());
        Assert.NotNull(otherBranchUser);
        Assert.True(otherBranchUser.IsActive);
        Assert.Equal("Pending", (await harness.Db.RegistrationRequests.AsNoTracking()
            .SingleAsync(request => request.Id == harness.RegistrationAUnassignedId)).Status);
    }

    [Fact]
    public async Task Persistence_rejects_cross_tenant_membership_links()
    {
        await using var harness = await MultiTenantHarness.CreateAsync();
        harness.Db.UserBranchMemberships.Add(new UserBranchMembership
        {
            CompanyId = harness.CompanyAId,
            UserId = harness.UserBId,
            BranchId = harness.BranchAId,
            IsActive = true
        });

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Db.SaveChangesAsync());
        Assert.Contains("kendi firmasındaki", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Persistence_rejects_cross_tenant_ticket_links()
    {
        await using var harness = await MultiTenantHarness.CreateAsync();
        harness.Db.ParkingTickets.Add(new ParkingTicket
        {
            CompanyId = harness.CompanyAId,
            TicketNumber = $"CROSS-{Guid.NewGuid():N}",
            BranchId = harness.BranchBId,
            VehicleId = harness.VehicleBId,
            HourlyRate = 100m
        });

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Db.SaveChangesAsync());
        Assert.Contains("farklı firmalara", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Synchronous_save_cannot_bypass_tenant_invariants()
    {
        await using var harness = await MultiTenantHarness.CreateAsync();

        var first = Assert.Throws<InvalidOperationException>(() => harness.Db.SaveChanges());
        var second = Assert.Throws<InvalidOperationException>(() => harness.Db.SaveChanges(acceptAllChangesOnSuccess: false));
        Assert.Contains("SaveChangesAsync", first.Message, StringComparison.Ordinal);
        Assert.Contains("SaveChangesAsync", second.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_controller_endpoint_is_explicitly_authenticated_or_anonymous()
    {
        var controllerTypes = typeof(TicketsController).Assembly.GetTypes()
            .Where(type => !type.IsAbstract && typeof(ControllerBase).IsAssignableFrom(type));

        foreach (var controllerType in controllerTypes)
        {
            var classAuthorized = controllerType.GetCustomAttributes<AuthorizeAttribute>(true).Any();
            var classAnonymous = controllerType.GetCustomAttributes<AllowAnonymousAttribute>(true).Any();
            var actions = controllerType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(method => method.GetCustomAttributes<HttpMethodAttribute>(true).Any());

            foreach (var action in actions)
            {
                var authorized = classAuthorized || action.GetCustomAttributes<AuthorizeAttribute>(true).Any();
                var anonymous = classAnonymous || action.GetCustomAttributes<AllowAnonymousAttribute>(true).Any();
                Assert.True(authorized || anonymous, $"{controllerType.Name}.{action.Name} has no explicit authorization decision.");
            }
        }
    }

    [Theory]
    [MemberData(nameof(SensitiveEndpointPolicies))]
    public void Sensitive_endpoints_keep_their_role_policy(Type controllerType, string actionName, string expectedPolicy)
    {
        var method = controllerType.GetMethod(actionName, BindingFlags.Instance | BindingFlags.Public)
            ?? throw new InvalidOperationException($"Endpoint not found: {controllerType.Name}.{actionName}");
        var policies = controllerType.GetCustomAttributes<AuthorizeAttribute>(true)
            .Concat(method.GetCustomAttributes<AuthorizeAttribute>(true))
            .Select(attribute => attribute.Policy)
            .Where(policy => policy is not null)
            .ToList();

        Assert.Contains(expectedPolicy, policies);
        Assert.False(controllerType.GetCustomAttributes<AllowAnonymousAttribute>(true).Any());
        Assert.False(method.GetCustomAttributes<AllowAnonymousAttribute>(true).Any());
    }

    public static TheoryData<Type, string, string> SensitiveEndpointPolicies => new()
    {
        { typeof(AdminController), nameof(AdminController.GetUsers), Roles.ManageUsersPolicy },
        { typeof(AdminController), nameof(AdminController.GetUser), Roles.ManageUsersPolicy },
        { typeof(AdminController), nameof(AdminController.CreateUser), Roles.ManageUsersPolicy },
        { typeof(AdminController), nameof(AdminController.UpdateUser), Roles.ManageUsersPolicy },
        { typeof(AdminController), nameof(AdminController.UpdateUserStatus), Roles.ManageUsersPolicy },
        { typeof(AdminController), nameof(AdminController.CreateBranch), Roles.ManageBranchesPolicy },
        { typeof(AdminController), nameof(AdminController.GetUserBranches), Roles.ManageUsersPolicy },
        { typeof(AdminController), nameof(AdminController.UpdateUserBranches), Roles.ManageBranchesPolicy },
        { typeof(TenantRegistrationController), nameof(TenantRegistrationController.GetPending), Roles.ManageUsersPolicy },
        { typeof(TenantRegistrationController), nameof(TenantRegistrationController.Decide), Roles.ManageUsersPolicy },
        { typeof(TicketsController), nameof(TicketsController.GetOne), Roles.StaffPolicy },
        { typeof(TicketsController), nameof(TicketsController.Create), Roles.OperationWritePolicy },
        { typeof(TicketsController), nameof(TicketsController.UpdateDetails), Roles.RecordsEditPolicy },
        { typeof(TicketsController), nameof(TicketsController.Delete), Roles.DeleteRecordsPolicy },
        { typeof(TicketsController), nameof(TicketsController.UpdateStatus), Roles.OperationWritePolicy },
        { typeof(TicketsController), nameof(TicketsController.Checkout), Roles.FinancePolicy },
        { typeof(BranchesController), nameof(BranchesController.GetAll), Roles.StaffPolicy },
        { typeof(DashboardController), nameof(DashboardController.Get), Roles.StaffPolicy },
        { typeof(ReportsController), nameof(ReportsController.Summary), Roles.ReportsPolicy },
        { typeof(AuditController), nameof(AuditController.Get), Roles.AuditPolicy },
        { typeof(NotificationsController), nameof(NotificationsController.Get), Roles.StaffPolicy },
        { typeof(NotificationsController), nameof(NotificationsController.SetRead), Roles.StaffPolicy },
        { typeof(NotificationsController), nameof(NotificationsController.ReadAll), Roles.StaffPolicy },
        { typeof(PushController), nameof(PushController.Status), Roles.StaffPolicy },
        { typeof(PushController), nameof(PushController.Registrations), Roles.StaffPolicy },
        { typeof(PushController), nameof(PushController.Register), Roles.StaffPolicy },
        { typeof(PushController), nameof(PushController.Unregister), Roles.StaffPolicy },
        { typeof(PushController), nameof(PushController.Test), Roles.StaffPolicy }
    };

    private static async Task AssertObjectDeniedAsync(Func<Task> action)
    {
        var error = await Assert.ThrowsAsync<ApiException>(action);
        Assert.Equal(StatusCodes.Status404NotFound, error.StatusCode);
    }

    private static async Task AssertForbiddenAsync(Func<Task> action)
    {
        var error = await Assert.ThrowsAsync<ApiException>(action);
        Assert.Equal(StatusCodes.Status403Forbidden, error.StatusCode);
    }

    private static async Task AssertOwnershipConflictAsync(Func<Task> action)
    {
        var error = await Assert.ThrowsAsync<ApiException>(action);
        Assert.Contains(error.StatusCode, new[]
        {
            StatusCodes.Status403Forbidden,
            StatusCodes.Status404NotFound,
            StatusCodes.Status409Conflict
        });
    }

    private static T OkValue<T>(ActionResult<T> actionResult)
    {
        var ok = Assert.IsType<OkObjectResult>(actionResult.Result);
        return Assert.IsAssignableFrom<T>(ok.Value);
    }
}

internal sealed class MultiTenantHarness : IAsyncDisposable
{
    private readonly ServiceProvider _provider;
    private readonly HttpContextAccessor _httpContextAccessor;
    private ClaimsPrincipal? _principal;

    private MultiTenantHarness(ServiceProvider provider, HttpContextAccessor httpContextAccessor)
    {
        _provider = provider;
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid CompanyAId { get; } = Guid.NewGuid();
    public Guid CompanyBId { get; } = Guid.NewGuid();
    public Guid BranchAId { get; } = Guid.NewGuid();
    public Guid BranchAUnassignedId { get; } = Guid.NewGuid();
    public Guid BranchBId { get; } = Guid.NewGuid();
    public Guid UserAId { get; } = Guid.NewGuid();
    public Guid UserAStaffId { get; } = Guid.NewGuid();
    public Guid UserAOtherBranchId { get; } = Guid.NewGuid();
    public Guid UserBId { get; } = Guid.NewGuid();
    public Guid VehicleAId { get; } = Guid.NewGuid();
    public Guid VehicleAUnassignedId { get; } = Guid.NewGuid();
    public Guid VehicleBId { get; } = Guid.NewGuid();
    public Guid TicketAId { get; } = Guid.NewGuid();
    public Guid TicketAUnassignedId { get; } = Guid.NewGuid();
    public Guid TicketBId { get; } = Guid.NewGuid();
    public Guid RegistrationAId { get; } = Guid.NewGuid();
    public Guid RegistrationAUnassignedId { get; } = Guid.NewGuid();
    public Guid RegistrationBId { get; } = Guid.NewGuid();
    public Guid NotificationAId { get; } = Guid.NewGuid();
    public Guid NotificationAStaffId { get; } = Guid.NewGuid();
    public Guid NotificationBId { get; } = Guid.NewGuid();
    public Guid PushRegistrationAId { get; } = Guid.NewGuid();
    public Guid PushRegistrationAStaffId { get; } = Guid.NewGuid();
    public Guid PushRegistrationBId { get; } = Guid.NewGuid();
    public Guid AuditEntryAId { get; } = Guid.NewGuid();
    public Guid AuditEntryAUnassignedId { get; } = Guid.NewGuid();
    public Guid AuditEntryBId { get; } = Guid.NewGuid();
    public string PushTokenA { get; } = $"token-a-{Guid.NewGuid():N}";
    public string PushTokenAStaff { get; } = $"token-a-staff-{Guid.NewGuid():N}";
    public string PushTokenB { get; } = $"token-b-{Guid.NewGuid():N}";

    public ValeDbContext Db => Service<ValeDbContext>();
    public UserManager<AppUser> Users => Service<UserManager<AppUser>>();

    public static async Task<MultiTenantHarness> CreateAsync()
    {
        var services = new ServiceCollection();
        var accessor = new HttpContextAccessor();

        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.None));
        services.AddOptions();
        services.AddSingleton<IHttpContextAccessor>(accessor);
        services.AddDbContext<ValeDbContext>(options =>
            options.UseInMemoryDatabase($"vale-bola-{Guid.NewGuid():N}"));
        services.AddIdentityCore<AppUser>()
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<ValeDbContext>();
        services.AddSingleton<IOptions<BusinessRulesOptions>>(Options.Create(new BusinessRulesOptions
        {
            DefaultHourlyRate = 100m,
            TimeZoneId = "UTC"
        }));
        services.AddSingleton<IOptions<FirebaseOptions>>(Options.Create(new FirebaseOptions()));
        services.AddSingleton<IOptions<EmailOptions>>(Options.Create(new EmailOptions()));
        services.AddSingleton<IOptions<SeedOptions>>(Options.Create(new SeedOptions()));
        services.AddSingleton<IOptions<JwtOptions>>(Options.Create(new JwtOptions
        {
            Issuer = "VALE.Api.Tests",
            Audience = "VALE.Api.Tests",
            Key = "BOLA-tests-only-key-with-at-least-32-bytes",
            ExpiryMinutes = 60
        }));
        services.AddScoped<CurrentUserContext>();
        services.AddScoped<TenantAccessService>();
        services.AddScoped<AuditService>();
        services.AddScoped<TicketService>();
        services.AddScoped<TokenService>();
        services.AddScoped<PasswordResetCodeService>();
        services.AddScoped<OneTimeCodeService>();
        services.AddSingleton<IFeeCalculator, FeeCalculator>();
        services.AddSingleton<FirebaseAppProvider>();
        services.AddScoped<FirebasePushSender>();
        services.AddScoped<IValeEmailSender, SmtpValeEmailSender>();

        var provider = services.BuildServiceProvider();
        var harness = new MultiTenantHarness(provider, accessor);
        await harness.SeedAsync();
        harness.AuthenticateAsTenantA(Roles.Owner);
        return harness;
    }

    public T Service<T>() where T : notnull
    {
        EnsureHttpContext();
        return _provider.GetRequiredService<T>();
    }

    public T Controller<T>() where T : ControllerBase
    {
        EnsureHttpContext();
        var controller = ActivatorUtilities.CreateInstance<T>(_provider);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = _httpContextAccessor.HttpContext ?? throw new InvalidOperationException("Test principal is missing.")
        };
        return controller;
    }

    public void AuthenticateAsTenantA(params string[] roles)
    {
        var claims = new List<Claim>
        {
            new("sub", UserAId.ToString()),
            new("company_id", CompanyAId.ToString()),
            new("branch_id", BranchAId.ToString()),
            new("name", "Tenant A Owner")
        };
        claims.AddRange(roles.Select(role => new Claim("role", role)));
        _principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "BolaTest", "name", "role"));
        EnsureHttpContext();
    }

    private void EnsureHttpContext()
    {
        if (_principal is null) return;
        _httpContextAccessor.HttpContext = new DefaultHttpContext { User = _principal };
    }

    private async Task SeedAsync()
    {
        var db = Db;
        await db.Database.EnsureCreatedAsync();

        db.Companies.AddRange(
            new Company { Id = CompanyAId, Name = "Tenant A", Code = $"A-{CompanyAId:N}" },
            new Company { Id = CompanyBId, Name = "Tenant B", Code = $"B-{CompanyBId:N}" });
        db.Branches.AddRange(
            new Branch { Id = BranchAId, CompanyId = CompanyAId, Code = "A1", Name = "A Primary", City = "Ankara", InviteCode = $"A1-{BranchAId:N}" },
            new Branch { Id = BranchAUnassignedId, CompanyId = CompanyAId, Code = "A2", Name = "A Unassigned", City = "Ankara", InviteCode = $"A2-{BranchAUnassignedId:N}" },
            new Branch { Id = BranchBId, CompanyId = CompanyBId, Code = "B1", Name = "B Primary", City = "Bursa", InviteCode = $"B1-{BranchBId:N}" });
        await db.SaveChangesAsync();

        var roleManager = Service<RoleManager<IdentityRole<Guid>>>();
        foreach (var role in Roles.All)
            Assert.True((await roleManager.CreateAsync(new IdentityRole<Guid>(role))).Succeeded);

        await CreateUserAsync(UserAId, "owner-a@example.test", "Tenant A Owner", CompanyAId, BranchAId, Roles.Owner);
        await CreateUserAsync(UserAStaffId, "staff-a@example.test", "Tenant A Staff", CompanyAId, BranchAId, Roles.Valet, "A-STAFF");
        await CreateUserAsync(UserAOtherBranchId, "other-branch-a@example.test", "Tenant A Other Branch", CompanyAId, BranchAUnassignedId, Roles.Valet, "A-OTHER");
        await CreateUserAsync(UserBId, "owner-b@example.test", "Tenant B Owner", CompanyBId, BranchBId, Roles.Owner);

        db.UserBranchMemberships.AddRange(
            new UserBranchMembership { CompanyId = CompanyAId, UserId = UserAId, BranchId = BranchAId, IsPrimary = true, IsActive = true },
            new UserBranchMembership { CompanyId = CompanyAId, UserId = UserAStaffId, BranchId = BranchAId, IsPrimary = true, IsActive = true },
            new UserBranchMembership { CompanyId = CompanyAId, UserId = UserAOtherBranchId, BranchId = BranchAUnassignedId, IsPrimary = true, IsActive = true },
            new UserBranchMembership { CompanyId = CompanyBId, UserId = UserBId, BranchId = BranchBId, IsPrimary = true, IsActive = true });

        db.Vehicles.AddRange(
            new Vehicle { Id = VehicleAId, CompanyId = CompanyAId, LicensePlate = "06 A 001", NormalizedPlate = "06A001" },
            new Vehicle { Id = VehicleAUnassignedId, CompanyId = CompanyAId, LicensePlate = "06 A 099", NormalizedPlate = "06A099" },
            new Vehicle { Id = VehicleBId, CompanyId = CompanyBId, LicensePlate = "06 B 002", NormalizedPlate = "06B002" });
        db.ParkingTickets.AddRange(
            new ParkingTicket { Id = TicketAId, CompanyId = CompanyAId, TicketNumber = $"A-{TicketAId:N}", BranchId = BranchAId, VehicleId = VehicleAId, HourlyRate = 100m, Status = TicketStatus.Received },
            new ParkingTicket { Id = TicketAUnassignedId, CompanyId = CompanyAId, TicketNumber = $"A2-{TicketAUnassignedId:N}", BranchId = BranchAUnassignedId, VehicleId = VehicleAUnassignedId, HourlyRate = 100m, Status = TicketStatus.Received },
            new ParkingTicket { Id = TicketBId, CompanyId = CompanyBId, TicketNumber = $"B-{TicketBId:N}", BranchId = BranchBId, VehicleId = VehicleBId, HourlyRate = 100m, Status = TicketStatus.Received });
        db.RegistrationRequests.AddRange(
            new RegistrationRequest { Id = RegistrationAId, CompanyId = CompanyAId, BranchId = BranchAId, ApplicantUserId = UserAStaffId, RequestedRole = Roles.Valet, Status = "Pending" },
            new RegistrationRequest { Id = RegistrationAUnassignedId, CompanyId = CompanyAId, BranchId = BranchAUnassignedId, ApplicantUserId = UserAOtherBranchId, RequestedRole = Roles.Valet, Status = "Pending" },
            new RegistrationRequest { Id = RegistrationBId, CompanyId = CompanyBId, BranchId = BranchBId, ApplicantUserId = UserBId, RequestedRole = Roles.Valet, Status = "Pending" });
        db.Notifications.AddRange(
            new ValeNotification { Id = NotificationAId, CompanyId = CompanyAId, BranchId = BranchAId, UserId = UserAId, Title = "A", Body = "Tenant A", Type = "Test" },
            new ValeNotification { Id = NotificationAStaffId, CompanyId = CompanyAId, BranchId = BranchAId, UserId = UserAStaffId, Title = "A Staff", Body = "Tenant A other user", Type = "Test" },
            new ValeNotification { Id = NotificationBId, CompanyId = CompanyBId, BranchId = BranchBId, UserId = UserBId, Title = "B", Body = "Tenant B", Type = "Test" });
        db.PushRegistrations.AddRange(
            new PushRegistration { Id = PushRegistrationAId, CompanyId = CompanyAId, UserId = UserAId, Token = PushTokenA, Platform = "android" },
            new PushRegistration { Id = PushRegistrationAStaffId, CompanyId = CompanyAId, UserId = UserAStaffId, Token = PushTokenAStaff, Platform = "android" },
            new PushRegistration { Id = PushRegistrationBId, CompanyId = CompanyBId, UserId = UserBId, Token = PushTokenB, Platform = "android" });
        db.AuditEntries.AddRange(
            new AuditEntry { Id = AuditEntryAId, CompanyId = CompanyAId, UserId = UserAId, BranchId = BranchAId, Action = "test.a", EntityType = "Test", Detail = "Tenant A" },
            new AuditEntry { Id = AuditEntryAUnassignedId, CompanyId = CompanyAId, UserId = UserAOtherBranchId, BranchId = BranchAUnassignedId, Action = "test.a2", EntityType = "Test", Detail = "Tenant A other branch" },
            new AuditEntry { Id = AuditEntryBId, CompanyId = CompanyBId, UserId = UserBId, BranchId = BranchBId, Action = "test.b", EntityType = "Test", Detail = "Tenant B" });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
    }

    private async Task CreateUserAsync(
        Guid id,
        string email,
        string fullName,
        Guid companyId,
        Guid branchId,
        string role,
        string? employeeCode = null)
    {
        var user = new AppUser
        {
            Id = id,
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = fullName,
            CompanyId = companyId,
            BranchId = branchId,
            EmployeeCode = employeeCode,
            IsActive = true,
            SecurityStamp = Guid.NewGuid().ToString("N")
        };
        Assert.True((await Users.CreateAsync(user)).Succeeded);
        Assert.True((await Users.AddToRoleAsync(user, role)).Succeeded);
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
    }
}
