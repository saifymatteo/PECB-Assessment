using Microsoft.AspNetCore.Mvc;
using SupportDesk.Api.Contracts;
using SupportDesk.Api.Domain;
using SupportDesk.Api.Features;
using SupportDesk.Api.Infrastructure.ErrorHandling;

namespace SupportDesk.Api.Controllers;

[ApiController]
[Route("api/tickets")]
[Produces("application/json")]
public class TicketsController(ITicketService tickets) : ControllerBase
{
    /// <summary>Paginated list with server-side search and filters (status, priority, agent, overdue-only).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<TicketListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<TicketListItemDto>>> List(
        [FromQuery] TicketListQuery query, CancellationToken cancellationToken)
        => Ok(await tickets.ListAsync(query, cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(TicketDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketDetailDto>> GetById(Guid id, CancellationToken cancellationToken)
        => await tickets.GetByIdAsync(id, cancellationToken) is { } ticket
            ? Ok(ticket)
            : throw new NotFoundException("Ticket", id);

    [HttpPost]
    [ProducesResponseType(typeof(TicketDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TicketDetailDto>> Create(
        [FromBody] CreateTicketRequest request, CancellationToken cancellationToken)
    {
        var ticket = await tickets.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = ticket.Id }, ticket);
    }

    /// <summary>Updates editable fields. Status and assignment have dedicated endpoints.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(TicketDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TicketDetailDto>> Update(
        Guid id, [FromBody] UpdateTicketRequest request, CancellationToken cancellationToken)
        => Ok(await tickets.UpdateAsync(id, request, cancellationToken));

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await tickets.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>Assigns (agentId) or unassigns (null agentId) an agent; inactive agents are rejected (rule 4).</summary>
    [HttpPut("{id:guid}/assignment")]
    [ProducesResponseType(typeof(TicketDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<TicketDetailDto>> AssignAgent(
        Guid id, [FromBody] AssignAgentRequest request, CancellationToken cancellationToken)
        => Ok(await tickets.AssignAgentAsync(id, request.AgentId, cancellationToken));

    /// <summary>
    /// The only way to change a ticket's status. Illegal transitions are rejected with
    /// the list of currently allowed moves.
    /// </summary>
    [HttpPost("{id:guid}/status")]
    [ProducesResponseType(typeof(TicketDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<TicketDetailDto>> ChangeStatus(
        Guid id, [FromBody] ChangeStatusRequest request, CancellationToken cancellationToken)
        => Ok(await tickets.ChangeStatusAsync(id, request.Status, cancellationToken));

    [HttpPost("{id:guid}/comments")]
    [ProducesResponseType(typeof(TicketCommentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TicketCommentDto>> AddComment(
        Guid id, [FromBody] AddCommentRequest request, CancellationToken cancellationToken)
    {
        var comment = await tickets.AddCommentAsync(id, request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, comment);
    }
}
