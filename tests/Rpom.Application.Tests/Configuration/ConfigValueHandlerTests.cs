using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using NSubstitute;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.User;
using Rpom.Application.Configuration;
using Rpom.Application.Configuration.GetConfigValues;
using Rpom.Application.Configuration.UpdateConfigValue;
using Rpom.Domain.Access;
using Rpom.Domain.Configuration;
using Rpom.Infrastructure.Database;
using Testcontainers.PostgreSql;

namespace Rpom.Application.Tests.Configuration;

public sealed class ConfigValueHandlerTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _db =
        new PostgreSqlBuilder().WithImage("pgvector/pgvector:pg17").Build();
    private ApplicationDbContext _ctx = null!;
    private int _staffId;

    public async Task InitializeAsync()
    {
        await _db.StartAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(
                _db.GetConnectionString(),
                o => o.MigrationsHistoryTable(HistoryRepository.DefaultTableName, Schemas.Default).UseVector())
            .UseSnakeCaseNamingConvention()
            .Options;
        _ctx = new ApplicationDbContext(options);
        await _ctx.Database.MigrateAsync();

        var now = DateTime.UtcNow;
        var role = new Role { Code = "OWNER", Name = "Owner", IsSystemRole = true, IsActive = true, CreatedAt = now, UpdatedAt = now };
        var staff = new StaffAccount { Username = "o", PasswordHash = "x", FullName = "Owner", Role = role, IsActive = true, IsLocked = false, CreatedAt = now, UpdatedAt = now };
        _ctx.AddRange(role, staff);
        _ctx.ConfigValues.Add(new ConfigValue
        {
            Code = "restaurant.vat_default_percent",
            Value = "10.00",
            ValueType = ConfigValueType.Number,
            Description = "VAT",
            UpdatedAt = now
        });
        await _ctx.SaveChangesAsync();
        _staffId = staff.Id;
    }

    public async Task DisposeAsync()
    {
        await _ctx.DisposeAsync();
        await _db.DisposeAsync();
    }

    [Fact]
    public async Task Get_ReturnsValueType()
    {
        var handler = new GetConfigValues.Handler(_ctx);
        var result = await handler.Handle(new GetConfigValues.Query(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle()
            .Which.ValueType.Should().Be(ConfigValueType.Number);
    }

    [Fact]
    public async Task Update_NumberWithNonNumeric_Fails()
    {
        var handler = new UpdateConfigValue.Handler(_ctx, Staff(), Clock());
        var result = await handler.Handle(
            new UpdateConfigValue.Command("restaurant.vat_default_percent", "abc"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Config.InvalidValueForType");
    }

    [Fact]
    public async Task Update_NumberWithNumeric_Succeeds()
    {
        var handler = new UpdateConfigValue.Handler(_ctx, Staff(), Clock());
        var result = await handler.Handle(
            new UpdateConfigValue.Command("restaurant.vat_default_percent", "12.5"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be("12.5");
    }

    [Fact]
    public async Task Update_UnknownCode_NotFound()
    {
        var handler = new UpdateConfigValue.Handler(_ctx, Staff(), Clock());
        var result = await handler.Handle(
            new UpdateConfigValue.Command("does.not.exist", "x"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Config.NotFound");
    }

    private ICurrentStaff Staff()
    {
        var s = Substitute.For<ICurrentStaff>();
        s.StaffAccountId.Returns(_staffId);
        return s;
    }

    private static IDateTimeProvider Clock()
    {
        var c = Substitute.For<IDateTimeProvider>();
        c.UtcNow.Returns(_ => DateTime.UtcNow);
        return c;
    }
}
