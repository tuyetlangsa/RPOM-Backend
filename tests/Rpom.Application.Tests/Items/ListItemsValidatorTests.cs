using FluentAssertions;
using NSubstitute;
using Rpom.Application.Abstraction.Configuration;
using Rpom.Application.Items.ListItems;

namespace Rpom.Application.Tests.Items;

public class ListItemsValidatorTests
{
    private static ListItems.Validator ValidatorWithMax(int max)
    {
        var config = Substitute.For<IConfigValueService>();
        config.GetAsync("pagination.max_page_size", Arg.Any<CancellationToken>())
            .Returns(max.ToString());
        return new ListItems.Validator(config);
    }

    private static ListItems.Query Query(int pageNumber, int pageSize)
        => new(null, null, null, pageNumber, pageSize);

    [Fact]
    public async Task PageSize_AboveConfiguredMax_Fails()
    {
        var result = await ValidatorWithMax(50).ValidateAsync(Query(1, 100));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task PageSize_WithinConfiguredMax_Passes()
    {
        var result = await ValidatorWithMax(50).ValidateAsync(Query(1, 30));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task PageNumber_BelowOne_Fails()
    {
        var result = await ValidatorWithMax(50).ValidateAsync(Query(0, 10));
        result.IsValid.Should().BeFalse();
    }
}
