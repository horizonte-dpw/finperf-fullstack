using System.Net;
using System.Net.Http.Json;
using FinPerf.Api.Data;
using FinPerf.Api.Domain;
using FinPerf.Api.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace FinPerf.Tests;

public sealed class PortfoliosApiIntegrationTests : IClassFixture<PortfoliosApiIntegrationTests.TestApiFactory>
{
    private readonly HttpClient _client;

    public PortfoliosApiIntegrationTests(TestApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetPerformanceRange_ValidRequest_Returns200WithCompoundedReturn()
    {
        var response = await _client.GetAsync("/api/portfolios/1/performance?from=2023-08-01&to=2023-08-03");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<RangePerformanceResponse>();
        Assert.NotNull(body);
        Assert.Equal(1, body.PortfolioId);
        Assert.Equal(0.0395, body.CumulativeReturn!.Value, precision: 10);
        Assert.Equal(60m, body.PnL);
        Assert.Equal(3, body.DaysCount);
    }

    [Fact]
    public async Task GetPerformanceRange_FromGreaterThanTo_Returns400ProblemDetails()
    {
        var response = await _client.GetAsync("/api/portfolios/1/performance?from=2023-08-10&to=2023-08-01");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(400, problem.Status);
    }

    [Fact]
    public async Task GetPerformanceSummary_UnknownPortfolio_Returns404ProblemDetails()
    {
        var response = await _client.GetAsync("/api/portfolios/999/performance/summary?asOf=2023-08-03");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(404, problem.Status);
    }

    [Fact]
    public async Task GetPerformanceSummary_OutOfRangeDate_Returns400ProblemDetails()
    {
        var response = await _client.GetAsync("/api/portfolios/1/performance/summary?asOf=2030-01-01");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(400, problem.Status);
    }

    public sealed class TestApiFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IPerformanceRepository>();
                services.AddScoped<IPerformanceRepository, FakePerformanceRepository>();
            });
        }
    }

    private sealed class FakePerformanceRepository : IPerformanceRepository
    {
        private static readonly PortfolioDto Portfolio1 = new(
            1,
            "Portafolio 1",
            new DateOnly(2015, 01, 01),
            "CLP",
            new DateOnly(2023, 08, 01),
            new DateOnly(2023, 08, 03));

        private static readonly IReadOnlyList<DailyPerformancePoint> Points =
        [
            new(new DateOnly(2023, 08, 01), 0.10, 100m),
            new(new DateOnly(2023, 08, 02), -0.10, -90m),
            new(new DateOnly(2023, 08, 03), 0.05, 50m)
        ];

        public Task<IReadOnlyList<PortfolioDto>> GetPortfoliosAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<PortfolioDto>>([Portfolio1]);

        public Task<PortfolioDto?> GetPortfolioByIdAsync(int portfolioId, CancellationToken ct) =>
            Task.FromResult(portfolioId == 1 ? Portfolio1 : null);

        public Task<DateBounds?> GetDataDateBoundsAsync(int portfolioId, CancellationToken ct) =>
            Task.FromResult(portfolioId == 1
                ? new DateBounds(new DateOnly(2023, 08, 01), new DateOnly(2023, 08, 03))
                : null);

        public Task<IReadOnlyList<DailyPerformancePoint>> GetDailyPerformanceAsync(
            int portfolioId,
            DateOnly from,
            DateOnly to,
            CancellationToken ct)
        {
            var filtered = Points.Where(p => p.AsOfDate >= from && p.AsOfDate <= to).ToList();
            return Task.FromResult<IReadOnlyList<DailyPerformancePoint>>(filtered);
        }
    }
}
