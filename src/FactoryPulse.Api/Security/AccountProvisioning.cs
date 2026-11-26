using FactoryPulse.Infrastructure.Identity;
using FactoryPulse.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore.Storage;

namespace FactoryPulse.Api.Security;

public sealed class AccountProvisioning(
    FactoryPulseDbContext database, UserManager<MonitoringUser> users, RoleManager<IdentityRole> roles)
{
    public async Task<IdentityResult> CreateAsync(string userName, string role, string password, CancellationToken cancellationToken)
    {
        if (!IsSupportedRole(role))
        {
            return IdentityResult.Failed(new IdentityError { Code = "UnknownRole" });
        }

        await using IDbContextTransaction transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        if (!await roles.RoleExistsAsync(role))
        {
            IdentityResult createdRole = await roles.CreateAsync(new IdentityRole(role));
            if (!createdRole.Succeeded)
            {
                return createdRole;
            }
        }

        var user = new MonitoringUser { UserName = userName };
        IdentityResult createdUser = await users.CreateAsync(user, password);
        if (!createdUser.Succeeded)
        {
            return createdUser;
        }

        IdentityResult assigned = await users.AddToRoleAsync(user, role);
        if (assigned.Succeeded)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return assigned;
    }

    internal static async Task<int> RunAsync(IServiceProvider services, string[] args)
    {
        if (args.Length != 3 || string.IsNullOrWhiteSpace(args[1]) || !IsSupportedRole(args[2]))
        {
            await Console.Error.WriteLineAsync("Usage: provision-user <username> <Viewer|Operator|Administrator>. Supply the password on stdin.");
            return 2;
        }

        if (!Console.IsInputRedirected || await Console.In.ReadLineAsync() is not { Length: > 0 and <= 256 } password)
        {
            await Console.Error.WriteLineAsync("Supply a password through redirected stdin.");
            return 2;
        }

        await using AsyncServiceScope scope = services.CreateAsyncScope();
        var provisioner = scope.ServiceProvider.GetRequiredService<AccountProvisioning>();
        IdentityResult result = await provisioner.CreateAsync(args[1], args[2], password, CancellationToken.None);
        if (result.Succeeded)
        {
            await Console.Out.WriteLineAsync("Account provisioned.");
            return 0;
        }

        await Console.Error.WriteLineAsync($"Account provisioning failed ({string.Join(", ", result.Errors.Select(error => error.Code))}).");
        return 1;
    }

    private static bool IsSupportedRole(string role) =>
        role is MonitoringAccess.Viewer or MonitoringAccess.Operator or MonitoringAccess.Administrator;
}
