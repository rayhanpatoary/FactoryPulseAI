using System.ComponentModel.DataAnnotations;
using FactoryPulse.Api.Monitoring;
using FactoryPulse.Application.Monitoring;
using Microsoft.AspNetCore.Mvc;

namespace FactoryPulse.Api.Controllers;

[ApiController]
[Route("api/alarms")]
public sealed class AlarmsController(IAlarmManagement alarms, IMachineQueries machines, IHostEnvironment environment) : ControllerBase
{
    [HttpGet]
    public Task<AlarmPage> List([FromQuery] Guid? machineId, [FromQuery] AlarmListRequest request, CancellationToken cancellationToken) =>
        alarms.GetPageAsync(request.ToFilter(machineId), cancellationToken);

    [HttpGet("active")]
    public Task<AlarmPage> Active([FromQuery] Guid? machineId, [FromQuery] AlarmListRequest request, CancellationToken cancellationToken) =>
        alarms.GetPageAsync(request.ToFilter(machineId, activeOnly: true), cancellationToken);

    [HttpGet("/api/machines/{id}/alarms")]
    [ProducesResponseType<AlarmPage>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AlarmPage>> ForMachine(Guid id, [FromQuery] AlarmListRequest request, CancellationToken cancellationToken)
    {
        if (!await machines.ExistsAsync(id, cancellationToken))
        {
            return Problem(title: "Machine not found.", statusCode: StatusCodes.Status404NotFound);
        }

        return await alarms.GetPageAsync(request.ToFilter(id), cancellationToken);
    }

    [HttpPost("{id}/acknowledge")]
    [ProducesResponseType<AlarmDetails>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AlarmDetails>> Acknowledge(
        [Range(typeof(long), "1", "9223372036854775807")] long id, CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment() && !environment.IsEnvironment("Testing"))
        {
            return Problem(title: "Alarm acknowledgement is available only in Development and Testing.",
                statusCode: StatusCodes.Status403Forbidden);
        }

        AlarmDetails? alarm = await alarms.AcknowledgeAsync(id, cancellationToken);
        return alarm is null ? Problem(title: "Alarm not found.", statusCode: StatusCodes.Status404NotFound) : alarm;
    }
}
