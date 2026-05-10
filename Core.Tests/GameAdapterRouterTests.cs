using Core.Adapters;
using FluentAssertions;

namespace Core.Tests;

public sealed class GameAdapterRouterTests
{
    [Fact]
    public void Resolves_RegisteredEntries_ByPathAndAppId()
    {
        var cs2 = new GameAdapterRegistration("cs2", 730, "/gsi", "Counter-Strike 2 GSI");
        var dota = new GameAdapterRegistration("dota2", 570, "/gsi/dota", "Dota 2 GSI");
        var router = new GameAdapterRouter(new[] { cs2, dota });

        router.Registrations.Should().BeEquivalentTo(new[] { cs2, dota });

        router.TryResolveByPath("/gsi", out var byCs2Path).Should().BeTrue();
        byCs2Path.Should().Be(cs2);

        router.TryResolveByPath("/GSI", out var caseInsensitive).Should().BeTrue();
        caseInsensitive.Should().Be(cs2);

        router.TryResolveByAppId(570, out var byDotaAppId).Should().BeTrue();
        byDotaAppId.Should().Be(dota);
    }

    [Fact]
    public void Returns_False_For_Unknown_Path_Or_AppId()
    {
        var router = new GameAdapterRouter(new[]
        {
            new GameAdapterRegistration("cs2", 730, "/gsi", "Counter-Strike 2 GSI"),
        });

        router.TryResolveByPath("/missing", out var byPath).Should().BeFalse();
        byPath.Should().BeNull();

        router.TryResolveByAppId(123, out var byAppId).Should().BeFalse();
        byAppId.Should().BeNull();
    }

    [Fact]
    public void Allows_Registrations_Without_AppId()
    {
        var registration = new GameAdapterRegistration("internal", AppId: null, "/gsi/internal", "Internal harness");
        var router = new GameAdapterRouter(new[] { registration });

        router.TryResolveByPath("/gsi/internal", out var resolved).Should().BeTrue();
        resolved.Should().Be(registration);

        router.TryResolveByAppId(0, out var unused).Should().BeFalse();
        unused.Should().BeNull();
    }

    [Fact]
    public void Rejects_Duplicate_Endpoint_Paths()
    {
        var entries = new[]
        {
            new GameAdapterRegistration("cs2", 730, "/gsi", "CS2"),
            new GameAdapterRegistration("cs2-clone", 731, "/gsi", "Clone"),
        };

        Action act = () => new GameAdapterRouter(entries);

        act.Should().Throw<ArgumentException>().WithMessage("*Duplicate*endpoint path*/gsi*");
    }

    [Fact]
    public void Rejects_Duplicate_AppIds()
    {
        var entries = new[]
        {
            new GameAdapterRegistration("cs2", 730, "/gsi", "CS2"),
            new GameAdapterRegistration("cs2-mirror", 730, "/gsi/mirror", "Mirror"),
        };

        Action act = () => new GameAdapterRouter(entries);

        act.Should().Throw<ArgumentException>().WithMessage("*Duplicate*AppId 730*");
    }

    [Fact]
    public void Rejects_Empty_TitleId_Or_EndpointPath()
    {
        Action emptyTitle = () => new GameAdapterRouter(new[]
        {
            new GameAdapterRegistration(string.Empty, 730, "/gsi", "CS2"),
        });
        emptyTitle.Should().Throw<ArgumentException>();

        Action emptyPath = () => new GameAdapterRouter(new[]
        {
            new GameAdapterRegistration("cs2", 730, string.Empty, "CS2"),
        });
        emptyPath.Should().Throw<ArgumentException>();
    }
}
