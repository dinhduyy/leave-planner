using System.ComponentModel.DataAnnotations;
using LeavePlanner.Application.Services;
using LeavePlanner.Application.Models;
using LeavePlanner.Contracts;
using LeavePlanner.Domain.Strategies;
using LeavePlanner.Mapping;
using Microsoft.Extensions.DependencyInjection;

namespace LeavePlanner.Api.Tests;

public sealed class RecommendationRequestTests
{
    [Fact]
    public void PriorityContractsUseDomainEnum()
    {
        var requestPriority = typeof(RecommendationRequest).GetProperty(nameof(RecommendationRequest.Priority));
        var responsePriority = typeof(RecommendationResponse).GetProperty(nameof(RecommendationResponse.Priority));

        Assert.NotNull(requestPriority);
        Assert.NotNull(responsePriority);
        Assert.Equal(typeof(PlanningPriority?), requestPriority.PropertyType);
        Assert.Equal(typeof(PlanningPriority), responsePriority.PropertyType);
    }

    private static readonly RecommendationStrategyRegistry Strategies = new([
        new LongestBreakStrategy(), new MinimumLeaveStrategy(), new BestEfficiencyStrategy()
    ]);
    private static readonly IServiceProvider Services = new ServiceCollection()
        .AddSingleton(Strategies)
        .BuildServiceProvider();

    [Fact]
    public void NormalizeCountryAndBlankSubdivisionWithoutChangingDates()
    {
        var request = ValidRequest() with { CountryCode = " at ", SubdivisionCode = "  " };

        Assert.Empty(Validate(request));
        var query = Assert.IsType<PlanningQuery>(RecommendationRequestMapper.Map(request, Strategies));

        Assert.Equal("AT", query.CountryCode);
        Assert.Null(query.SubdivisionCode);
        Assert.Equal(new DateOnly(2026, 1, 1), query.FromDate);
        Assert.Equal(PlanningPriority.LongestBreak, query.Priority);
        Assert.Equal(1, query.MinTotalDays);
    }

    [Fact]
    public void MissingBudgetIsInvalidButZeroIsValid()
    {
        Assert.Contains("leaveBudget", Validate(ValidRequest() with { LeaveBudget = null }).Keys);
        Assert.Empty(Validate(ValidRequest() with { LeaveBudget = 0 }));
    }

    [Fact]
    public void MinimumLeaveNeedsMinimumDays()
    {
        var errors = Validate(ValidRequest() with { Priority = PlanningPriority.MinimumLeave });

        Assert.Contains("minTotalDays", errors.Keys);
    }

    [Theory]
    [InlineData((PlanningPriority)999, 3)]
    [InlineData(PlanningPriority.BestEfficiency, 0)]
    [InlineData(PlanningPriority.BestEfficiency, 8)]
    public void InvalidPriorityOrMinimumHasFieldError(PlanningPriority priority, int minimum)
    {
        var errors = Validate(ValidRequest() with { Priority = priority, MinTotalDays = minimum });

        Assert.NotEmpty(errors);
    }

    [Fact]
    public void InclusiveRangeAllows366DaysButRejects367()
    {
        var from = new DateOnly(2024, 1, 1);
        Assert.Empty(Validate(ValidRequest() with { FromDate = from, ToDate = new DateOnly(2024, 12, 31) }));
        Assert.Contains("toDate", Validate(ValidRequest() with { FromDate = from, ToDate = new DateOnly(2025, 1, 1) }).Keys);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("A")]
    [InlineData("ABC")]
    [InlineData("A1")]
    public void InvalidCountryHasFieldError(string country)
    {
        Assert.Contains("countryCode", Validate(ValidRequest() with { CountryCode = country }).Keys);
    }

    [Fact]
    public void ReversedDatesHaveToDateError()
    {
        Assert.Contains("toDate", Validate(ValidRequest() with
        {
            FromDate = new DateOnly(2026, 5, 2), ToDate = new DateOnly(2026, 5, 1)
        }).Keys);
    }

    private static RecommendationRequest ValidRequest() => new()
    {
        CountryCode = "AT",
        FromDate = new DateOnly(2026, 1, 1),
        ToDate = new DateOnly(2026, 1, 7),
        LeaveBudget = 2
    };

    private static IReadOnlyDictionary<string, string[]> Validate(RecommendationRequest request)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request, Services, null), results, true);
        return results.SelectMany(result => result.MemberNames.Select(name => (name, result.ErrorMessage)))
            .GroupBy(item => item.name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => char.ToLowerInvariant(group.Key[0]) + group.Key[1..],
                group => group.Select(item => item.ErrorMessage ?? string.Empty).ToArray());
    }
}
