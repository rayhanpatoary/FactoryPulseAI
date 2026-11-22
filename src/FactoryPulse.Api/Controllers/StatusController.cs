using FactoryPulse.Application.Status;
using Microsoft.AspNetCore.Mvc;

namespace FactoryPulse.Api.Controllers;

[ApiController]
[Route("api/status")]
public sealed class StatusController(ApplicationStatusService statusService) : ControllerBase
{
    [HttpGet]
    public ApplicationStatus Get() => statusService.GetStatus();
}
