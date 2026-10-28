using LeavePlanner.Application.Errors;
using LeavePlanner.Application.Services;
using LeavePlanner.Contracts;
using LeavePlanner.Mapping;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace LeavePlanner.Controllers;

[ApiController]
[Route("api/leave-plans")]
public sealed class LeavePlansController(
    LeavePlanningService planningService,
    RecommendationStrategyRegistry strategies,
    RecommendationResponseMapper mapper) : ControllerBase
{
    [HttpPost("recommendation")]
    [ProducesResponseType<RecommendationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status504GatewayTimeout)]
    public async Task<ActionResult<RecommendationResponse>> Recommend(
        [FromBody] RecommendationRequest request, CancellationToken cancellationToken)
    {
        var query = RecommendationRequestMapper.Map(request, strategies);

        var result = await planningService.RecommendAsync(query, cancellationToken);

        return result.Match<ActionResult<RecommendationResponse>>(
            value => Ok(mapper.Map(value)),
            error =>
            {
                if (error is not PlanningValidationError validation)
                {
                    throw new InvalidOperationException($"No HTTP mapping for planning error '{error.Code}'.");
                }

                ModelState.AddModelError(JsonNamingPolicy.CamelCase.ConvertName(validation.Field), validation.Description);
                return ValidationProblem();
            });
    }
}
