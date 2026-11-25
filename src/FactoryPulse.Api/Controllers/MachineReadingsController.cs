using FactoryPulse.Application.Monitoring;
using Microsoft.AspNetCore.Mvc;

namespace FactoryPulse.Api.Controllers;

[ApiController]
[Route("api/machines/{id}/readings")]
public sealed class MachineReadingsController(IMachineQueries machines, IMachineTelemetryQueries telemetry) : ControllerBase
{
    [HttpGet("latest")]
    [ProducesResponseType<MachineTelemetry>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MachineTelemetry>> Latest(Guid id, CancellationToken cancellationToken)
    {
        MachineTelemetry? reading = await telemetry.GetLatestAsync(id, cancellationToken);
        if (reading is not null)
        {
            return reading;
        }

        return await machines.ExistsAsync(id, cancellationToken)
            ? NoContent()
            : Problem(title: "Machine not found.", statusCode: StatusCodes.Status404NotFound);
    }
}
