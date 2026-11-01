using ApexPost.Application.DTOs;
using ApexPost.Application.Exceptions;
using ApexPost.Domain.Common;
using ApexPost.Domain.Personnel;
using ApexPost.UnitTests.Support;
using static ApexPost.UnitTests.Support.CounterSteps;

namespace ApexPost.UnitTests.Services;

public sealed class StaffAndLoginServiceTests
{
    private const string Password = "correct-horse-battery";

    private static readonly DeactivationCommand Leaving = new() { Reason = "Left the company." };

    [Fact]
    public async Task New_staff_member_can_sign_in_with_the_password()
    {
        using var harness = new ServiceHarness();
        harness.As(harness.Admin);
        var id = await harness.StaffAdmin().CreateAsync(Details("new.clerk@apexpost.test"), Secret(Password), Ct);

        var signedIn = await harness.Login().SignInAsync(Login("NEW.clerk@apexpost.test", Password), Ct);

        Assert.NotNull(signedIn);
        Assert.Equal(id, signedIn.StaffId);
        Assert.Equal(StaffRole.CounterClerk, signedIn.Role);
        Assert.Equal(harness.Branch.Code, signedIn.Branch);
    }

    [Fact]
    public async Task Wrong_password_and_unknown_email_both_fail_after_one_hash_check()
    {
        using var harness = new ServiceHarness();
        await harness.Credentials.SetHashAsync(harness.Clerk.Id, harness.Hasher.Hash(Password), Ct);
        var login = harness.Login();

        Assert.Null(await login.SignInAsync(Login(harness.Clerk.Email, "wrong-password!"), Ct));
        Assert.Equal(1, harness.Hasher.VerifyCount);
        Assert.Null(await login.SignInAsync(Login("nobody@apexpost.test", Password), Ct));
        Assert.Equal(2, harness.Hasher.VerifyCount);
    }

    [Fact]
    public async Task Inactive_staff_cannot_sign_in_and_lose_their_session()
    {
        using var harness = new ServiceHarness();
        await harness.Credentials.SetHashAsync(harness.Clerk.Id, harness.Hasher.Hash(Password), Ct);
        harness.As(harness.Admin);
        var version = (await harness.StaffAdmin().GetAsync(harness.Clerk.Id, Ct)).Version;

        await harness.StaffAdmin().DeactivateAsync(harness.Clerk.Id, version, Leaving, Ct);

        Assert.Null(await harness.Login().SignInAsync(Login(harness.Clerk.Email, Password), Ct));
        Assert.Null(await harness.Login().RevalidateAsync(harness.Clerk.Id, Ct));
        Assert.NotNull(await harness.Login().RevalidateAsync(harness.Manager.Id, Ct));
    }

    [Fact]
    public async Task Email_must_be_unique_and_password_long_enough()
    {
        using var harness = new ServiceHarness();
        harness.As(harness.Admin);
        var staff = harness.StaffAdmin();

        var taken = await Assert.ThrowsAsync<ValidationFailedException>(() =>
            staff.CreateAsync(Details(harness.Manager.Email.ToUpperInvariant()), Secret(Password), Ct));
        var weak = await Assert.ThrowsAsync<ValidationFailedException>(() =>
            staff.CreateAsync(Details("short@apexpost.test"), Secret("short"), Ct));
        var badBranch = await Assert.ThrowsAsync<ValidationFailedException>(() =>
            staff.CreateAsync(Details("x@apexpost.test") with { HomeBranch = "BR999" }, Secret(Password), Ct));

        Assert.Contains(nameof(StaffCommand.Email), taken.Errors.Keys);
        Assert.Contains(nameof(PasswordCommand.Password), weak.Errors.Keys);
        Assert.Contains(nameof(StaffCommand.HomeBranch), badBranch.Errors.Keys);
    }

    [Fact]
    public async Task Update_with_a_stale_version_is_a_conflict()
    {
        using var harness = new ServiceHarness();
        harness.As(harness.Admin);
        var staff = harness.StaffAdmin();
        var version = (await staff.GetAsync(harness.Clerk.Id, Ct)).Version;
        await staff.UpdateAsync(harness.Clerk.Id, version, Details(harness.Clerk.Email) with { Name = "Ana" }, Ct);

        await Assert.ThrowsAsync<ConcurrencyException>(() =>
            staff.UpdateAsync(harness.Clerk.Id, version, Details(harness.Clerk.Email) with { Name = "Bea" }, Ct));

        Assert.Equal("Ana", (await staff.GetAsync(harness.Clerk.Id, Ct)).Name);
    }

    [Fact]
    public async Task Only_administrators_manage_staff_and_nobody_deactivates_themselves()
    {
        using var harness = new ServiceHarness();
        harness.As(harness.Manager);
        await Assert.ThrowsAsync<ForbiddenException>(() => harness.StaffAdmin().ListAsync(Ct));

        harness.As(harness.Admin);
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            harness.StaffAdmin().DeactivateAsync(harness.Admin.Id, 1, Leaving, Ct));
    }

    [Fact]
    public async Task Deactivation_needs_a_reason_and_can_be_undone()
    {
        using var harness = new ServiceHarness();
        harness.As(harness.Admin);
        var staff = harness.StaffAdmin();
        var version = (await staff.GetAsync(harness.Clerk.Id, Ct)).Version;

        await Assert.ThrowsAsync<ValidationFailedException>(() =>
            staff.DeactivateAsync(harness.Clerk.Id, version, new DeactivationCommand { Reason = "  " }, Ct));
        Assert.True((await staff.GetAsync(harness.Clerk.Id, Ct)).IsActive);

        await staff.DeactivateAsync(harness.Clerk.Id, version, Leaving, Ct);
        await staff.ActivateAsync(harness.Clerk.Id, version + 1, Ct);

        Assert.True((await staff.GetAsync(harness.Clerk.Id, Ct)).IsActive);
    }

    [Fact]
    public async Task New_password_replaces_the_old_one_and_must_be_long_enough()
    {
        using var harness = new ServiceHarness();
        await harness.Credentials.SetHashAsync(harness.Clerk.Id, harness.Hasher.Hash(Password), Ct);
        harness.As(harness.Admin);
        var staff = harness.StaffAdmin();

        await Assert.ThrowsAsync<ValidationFailedException>(() =>
            staff.SetPasswordAsync(harness.Clerk.Id, Secret("short"), Ct));
        Assert.NotNull(await harness.Login().SignInAsync(Login(harness.Clerk.Email, Password), Ct));

        await staff.SetPasswordAsync(harness.Clerk.Id, Secret("a-brand-new-password"), Ct);

        Assert.Null(await harness.Login().SignInAsync(Login(harness.Clerk.Email, Password), Ct));
        Assert.NotNull(await harness.Login().SignInAsync(Login(harness.Clerk.Email, "a-brand-new-password"), Ct));
    }

    [Fact]
    public async Task Administrator_edits_the_own_details_but_not_the_own_role()
    {
        using var harness = new ServiceHarness();
        harness.As(harness.Admin);
        var staff = harness.StaffAdmin();
        var own = await staff.GetAsync(harness.Admin.Id, Ct);
        var details = new StaffCommand
        {
            Name = "Renamed Admin",
            Email = own.Email,
            Role = StaffRole.Administrator,
            HomeBranch = own.HomeBranch.Value,
        };

        await staff.UpdateAsync(own.Id, own.Version, details, Ct);
        var demoted = await Assert.ThrowsAsync<ValidationFailedException>(() =>
            staff.UpdateAsync(own.Id, own.Version + 1, details with { Role = StaffRole.CounterClerk }, Ct));

        Assert.Contains(nameof(StaffCommand.Role), demoted.Errors.Keys);
        var stored = await staff.GetAsync(own.Id, Ct);
        Assert.Equal(("Renamed Admin", StaffRole.Administrator), (stored.Name, stored.Role));
    }

    [Theory]
    [InlineData(StaffRole.CounterClerk)]
    [InlineData(StaffRole.BranchManager)]
    [InlineData(StaffRole.Auditor)]
    public async Task Only_administrators_change_staff(StaffRole role)
    {
        using var harness = new ServiceHarness();
        harness.As(harness.Manager);
        harness.User.Role = role;
        var staff = harness.StaffAdmin();
        var target = harness.OtherClerk.Id;

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            staff.CreateAsync(Details("x@apexpost.test"), Secret(Password), Ct));
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            staff.UpdateAsync(target, 1, Details("y@apexpost.test"), Ct));
        await Assert.ThrowsAsync<ForbiddenException>(() => staff.DeactivateAsync(target, 1, Leaving, Ct));
        await Assert.ThrowsAsync<ForbiddenException>(() => staff.ActivateAsync(target, 1, Ct));
        await Assert.ThrowsAsync<ForbiddenException>(() => staff.SetPasswordAsync(target, Secret(Password), Ct));
    }

    [Fact]
    public async Task Closing_a_branch_stops_new_transactions_there()
    {
        using var harness = new ServiceHarness();
        harness.As(harness.Admin);
        var branch = await harness.BranchAdmin().GetAsync(harness.Branch.Code, Ct);

        await harness.BranchAdmin().CloseAsync(branch.Code, branch.Version, Ct);

        harness.As(harness.Clerk);
        await Assert.ThrowsAsync<DomainException>(harness.OpenAsync);
        harness.As(harness.Admin);
        await Assert.ThrowsAsync<ConcurrencyException>(() =>
            harness.BranchAdmin().ReopenAsync(branch.Code, branch.Version, Ct));
    }

    private static StaffCommand Details(string email) => new()
    {
        Name = "New Clerk",
        Email = email,
        Role = StaffRole.CounterClerk,
        HomeBranch = TestData.HomeBranch,
    };

    private static PasswordCommand Secret(string password) => new() { Password = password };

    private static LoginCommand Login(string email, string password) => new() { Email = email, Password = password };
}
