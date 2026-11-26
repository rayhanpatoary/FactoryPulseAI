using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using FactoryPulse.Api.Security;
using FactoryPulse.Infrastructure.Identity;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace FactoryPulse.Api.Controllers;

[ApiController]
[Route("api/auth")]
[AutoValidateAntiforgeryToken]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AuthenticationController(
    UserManager<MonitoringUser> users, SignInManager<MonitoringUser> signIn,
    IPasswordHasher<MonitoringUser> hasher, IAntiforgery antiforgery) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("csrf")]
    public IActionResult Csrf()
    {
        AntiforgeryTokenSet tokens = antiforgery.GetAndStoreTokens(HttpContext);
        Response.Cookies.Append(IdentityRegistration.RequestTokenCookie, tokens.RequestToken!, new CookieOptions
        {
            Secure = true, SameSite = SameSiteMode.Strict, Path = "/", IsEssential = true
        });
        return NoContent();
    }

    [AllowAnonymous]
    [EnableRateLimiting(IdentityRegistration.LoginLimit)]
    [HttpPost("login")]
    public async Task<ActionResult<SessionUser>> Login(
        LoginRequest request, [FromServices] IOptionsSnapshot<CookieAuthenticationOptions> cookieOptions)
    {
        MonitoringUser? user = await users.FindByNameAsync(request.UserName.Trim());
        if (user is null || await users.IsLockedOutAsync(user))
        {
            // Keep the password-hashing cost comparable when the account cannot be checked.
            _ = hasher.HashPassword(new MonitoringUser(), request.Password);
            return InvalidCredentials();
        }

        Microsoft.AspNetCore.Identity.SignInResult result =
            await signIn.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!result.Succeeded || !user.IsActive)
        {
            return InvalidCredentials();
        }

        var cookie = cookieOptions.Get(IdentityConstants.ApplicationScheme);
        DateTimeOffset expiresAt = (cookie.TimeProvider ?? TimeProvider.System).GetUtcNow().Add(cookie.ExpireTimeSpan);
        await signIn.SignInAsync(user, new AuthenticationProperties { ExpiresUtc = expiresAt });
        return Describe(await signIn.CreateUserPrincipalAsync(user), expiresAt);
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<SessionUser>> Me()
    {
        AuthenticateResult ticket = await HttpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        return Describe(User, ticket.Properties?.ExpiresUtc);
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromServices] ActiveMonitoringSessions sessions)
    {
        MonitoringUser? user = await users.GetUserAsync(User);
        if (user is null || !(await users.UpdateSecurityStampAsync(user)).Succeeded)
        {
            return Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "The session could not be revoked.");
        }

        sessions.Disconnect(user.Id);
        await signIn.SignOutAsync();
        Response.Cookies.Delete(IdentityRegistration.RequestTokenCookie, new CookieOptions
        {
            Secure = true, Path = "/", SameSite = SameSiteMode.Strict
        });
        return NoContent();
    }

    private ObjectResult InvalidCredentials() =>
        Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Sign-in was not accepted.");

    private static SessionUser Describe(ClaimsPrincipal user, DateTimeOffset? expiresAt) =>
        new(user.Identity?.Name ?? string.Empty, user.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToArray(),
            user.IsInRole(MonitoringAccess.Operator) || user.IsInRole(MonitoringAccess.Administrator), expiresAt);
}

public sealed record LoginRequest([Required, StringLength(256)] string UserName, [Required, StringLength(256)] string Password);

public sealed record SessionUser(string UserName, string[] Roles, bool CanAcknowledge, DateTimeOffset? ExpiresAtUtc);
