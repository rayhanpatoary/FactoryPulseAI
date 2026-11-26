using System.ComponentModel.DataAnnotations;
using FactoryPulse.Api.Security;
using FactoryPulse.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace FactoryPulse.Api.Controllers;

[ApiController]
[Authorize(Policy = MonitoringAccess.Administer)]
[AutoValidateAntiforgeryToken]
[Route("api/admin/users")]
public sealed class AccountActivationController(UserManager<MonitoringUser> users, ActiveMonitoringSessions sessions)
    : ControllerBase
{
    [HttpPut("{userName}/activation")]
    public async Task<IActionResult> SetActivation(
        [StringLength(256)] string userName, AccountActivationRequest request)
    {
        MonitoringUser? user = await users.FindByNameAsync(userName);
        if (user is null)
        {
            return NotFound();
        }

        if (string.Equals(user.Id, users.GetUserId(User), StringComparison.Ordinal))
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "You cannot change activation for your own account.");
        }

        bool active = request.IsActive == true;
        if (user.IsActive == active)
        {
            return NoContent();
        }

        user.IsActive = active;
        if (!(await users.UpdateSecurityStampAsync(user)).Succeeded)
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "The account could not be updated.");
        }

        sessions.Disconnect(user.Id);
        return NoContent();
    }
}

public sealed record AccountActivationRequest([Required] bool? IsActive);
