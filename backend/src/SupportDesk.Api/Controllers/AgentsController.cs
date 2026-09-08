using Microsoft.AspNetCore.Mvc;
using SupportDesk.Api.Contracts;
using SupportDesk.Api.Features;
using SupportDesk.Api.Infrastructure.ErrorHandling;

namespace SupportDesk.Api.Controllers;

[ApiController]
[Route("api/agents")]
[Produces("application/json")]
public class AgentsController(IAgentService agents) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AgentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AgentDto>>> List(
        [FromQuery] string? search, CancellationToken cancellationToken)
        => Ok(await agents.ListAsync(search, cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AgentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AgentDto>> GetById(Guid id, CancellationToken cancellationToken)
        => await agents.GetByIdAsync(id, cancellationToken) is { } agent
            ? Ok(agent)
            : throw new NotFoundException("Agent", id);

    [HttpPost]
    [ProducesResponseType(typeof(AgentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AgentDto>> Create(
        [FromBody] CreateAgentRequest request, CancellationToken cancellationToken)
    {
        var agent = await agents.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = agent.Id }, agent);
    }

    /// <summary>Activates or deactivates an agent (rule 4: inactive agents cannot take assignments).</summary>
    [HttpPut("{id:guid}/status")]
    [ProducesResponseType(typeof(AgentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AgentDto>> SetActive(
        Guid id, [FromBody] SetAgentActiveRequest request, CancellationToken cancellationToken)
        => Ok(await agents.SetActiveAsync(id, request.Active, cancellationToken));
}
