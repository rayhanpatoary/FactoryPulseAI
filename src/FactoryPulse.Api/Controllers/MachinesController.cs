using FactoryPulse.Application.Monitoring;
using Microsoft.AspNetCore.Mvc;

namespace FactoryPulse.Api.Controllers;

[ApiController]
[Route("api/machines")]
public sealed class MachinesController(IMachineQueries machines) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<MachineListItem>> List(CancellationToken cancellationToken) =>
        machines.ListAsync(cancellationToken);

    [HttpGet("{id}")]
    [ProducesResponseType<MachineDetails>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MachineDetails>> Get(Guid id, CancellationToken cancellationToken)
    {
        MachineDetails? machine = await machines.GetAsync(id, cancellationToken);
        return machine is null ? Problem(title: "Machine not found.", statusCode: StatusCodes.Status404NotFound) : machine;
    }

    [HttpGet("summary")]
    public Task<MachineSummary> Summary(CancellationToken cancellationToken) =>
        machines.SummarizeAsync(cancellationToken);
}
