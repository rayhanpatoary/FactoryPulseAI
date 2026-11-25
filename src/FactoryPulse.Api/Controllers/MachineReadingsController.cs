using FactoryPulse.Api.Monitoring;
using FactoryPulse.Application.Monitoring;
using Microsoft.AspNetCore.Mvc;

namespace FactoryPulse.Api.Controllers;

[ApiController]
[Route("api/machines/{id}/readings")]
public sealed class MachineReadingsController(IMachineQueries machines, IMachineTelemetryQueries telemetry) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<TelemetryHistoryPage>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TelemetryHistoryPage>> History(
        Guid id, [FromQuery] TelemetryHistoryRequest request, CancellationToken cancellationToken)
    {
        TelemetryHistoryFilter? filter = request.GetFilter(ModelState);
        if (filter is null)
        {
            return ValidationProblem(ModelState);
        }

        TelemetryHistoryPage result = await telemetry.GetHistoryAsync(id, filter, cancellationToken);
        if (result.Items.Count == 0 && !await machines.ExistsAsync(id, cancellationToken))
        {
            return Problem(title: "Machine not found.", statusCode: StatusCodes.Status404NotFound);
        }

        return result;
    }

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
