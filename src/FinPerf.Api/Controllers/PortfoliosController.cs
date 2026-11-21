using FinPerf.Api.Models;
using FinPerf.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace FinPerf.Api.Controllers;

[ApiController]
[Route("api/portfolios")]
public sealed class PortfoliosController(PerformanceService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<PortfolioDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPortfolios(CancellationToken ct)
    {
        var portfolios = await service.GetPortfoliosAsync(ct);
        return Ok(portfolios);
    }

    [HttpGet("{id:int}/performance/summary")]
    [ProducesResponseType<PerformanceSummaryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSummary(int id, [FromQuery] DateOnly? asOf, CancellationToken ct)
    {
        if (asOf is null)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Parámetro requerido",
                detail: "El parámetro 'asOf' (YYYY-MM-DD) es obligatorio.");
        }

        var result = await service.GetSummaryAsync(id, asOf.Value, ct);
        return ToActionResult(result);
    }

    [HttpGet("{id:int}/performance")]
    [ProducesResponseType<RangePerformanceResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRange(
        int id,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken ct)
    {
        if (from is null || to is null)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Parámetros requeridos",
                detail: "Los parámetros 'from' y 'to' (YYYY-MM-DD) son obligatorios.");
        }

        var result = await service.GetRangePerformanceAsync(id, from.Value, to.Value, ct);
        return ToActionResult(result);
    }

    [HttpGet("{id:int}/performance/monthly")]
    [ProducesResponseType<MonthlyPerformanceResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMonthly(
        int id,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken ct)
    {
        if (from is null || to is null)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Parámetros requeridos",
                detail: "Los parámetros 'from' y 'to' (YYYY-MM-DD) son obligatorios.");
        }

        var result = await service.GetMonthlyPerformanceAsync(id, from.Value, to.Value, ct);
        return ToActionResult(result);
    }

    private IActionResult ToActionResult<T>(ServiceResult<T> result)
    {
        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        return Problem(
            statusCode: result.Error!.StatusCode,
            title: result.Error.Title,
            detail: result.Error.Detail);
    }
}
