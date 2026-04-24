using System.Text.Json;
using Core.Models;
using Cs2Simulator.Scenarios.Json;
using Cs2Simulator.Scenarios.Models;
using Cs2Simulator.Scenarios.State;
using FluentAssertions;
using GsiHost.Dtos;

namespace Cs2Simulator.Tests;

public sealed class Cs2PayloadContractTests
{
    [Fact]
    public void RoundTrip_PreservesContractFields_ViaGsiSnapshotMapper()
    {
        var state = new SimulationState
        {
            MapPhase = "live",
            MapRound = 7,
            PlayerHealth = 73,
            PlayerArmor = 42,
            PlayerSteamId = "76561198000123456"
        };
        state.Clock.Advance(TimeSpan.FromSeconds(15));

        var payload = Cs2PayloadBuilder.Build(state);
        var snapshot = HostMappingHelper.RoundTrip(payload);

        snapshot.Timestamp.ToUnixTimeSeconds().Should().Be(state.Clock.UnixSeconds);
        snapshot.PlayerId.Should().Be("76561198000123456");

        var round = snapshot.GetModule<RoundModule>();
        round.Should().NotBeNull();
        round!.Round.Should().Be(7);
        round.Phase.Should().Be("live");

        var vitals = snapshot.GetModule<VitalsModule>();
        vitals.Should().NotBeNull();
        vitals!.Health.Should().Be(73);
        vitals.Armor.Should().Be(42);
        vitals.IsAlive.Should().BeTrue();
    }

    [Fact]
    public void RoundTrip_DeadPlayer_IsNotAlive()
    {
        var state = new SimulationState { PlayerHealth = 0 };

        var payload = Cs2PayloadBuilder.Build(state);
        var snapshot = HostMappingHelper.RoundTrip(payload);

        var vitals = snapshot.GetModule<VitalsModule>();
        vitals!.IsAlive.Should().BeFalse();
        vitals.Health.Should().Be(0);
    }

    [Fact]
    public void Json_OmitsNullableFields_WhenNotSet()
    {
        var state = new SimulationState
        {
            BombState = null,
            WinTeam = null
        };
        var payload = Cs2PayloadBuilder.Build(state);

        var json = Cs2PayloadJson.Serialize(payload);
        using var doc = JsonDocument.Parse(json);

        var round = doc.RootElement.GetProperty("round");
        round.TryGetProperty("bomb", out _).Should().BeFalse();
        round.TryGetProperty("win_team", out _).Should().BeFalse();
    }

    [Fact]
    public void Json_IncludesBombAndWinTeam_WhenSet()
    {
        var state = new SimulationState
        {
            BombState = "planted",
            WinTeam = "T"
        };
        var payload = Cs2PayloadBuilder.Build(state);

        var json = Cs2PayloadJson.Serialize(payload);
        using var doc = JsonDocument.Parse(json);

        var round = doc.RootElement.GetProperty("round");
        round.GetProperty("bomb").GetString().Should().Be("planted");
        round.GetProperty("win_team").GetString().Should().Be("T");
    }

    [Fact]
    public void Json_UsesSnakeCaseKeys_OnNestedFields()
    {
        var state = new SimulationState();
        var payload = Cs2PayloadBuilder.Build(state);

        var json = Cs2PayloadJson.Serialize(payload);
        using var doc = JsonDocument.Parse(json);

        var player = doc.RootElement.GetProperty("player");
        player.TryGetProperty("match_stats", out _).Should().BeTrue();
        var state2 = player.GetProperty("state");
        state2.TryGetProperty("round_kills", out _).Should().BeTrue();
        state2.TryGetProperty("round_totaldmg", out _).Should().BeTrue();
        var map = doc.RootElement.GetProperty("map");
        var teamCt = map.GetProperty("team_ct");
        teamCt.TryGetProperty("consecutive_round_losses", out _).Should().BeTrue();
        map.TryGetProperty("num_matches_to_win_series", out _).Should().BeTrue();
    }

    [Fact]
    public void Json_Deserializes_AsGsiPayloadDto()
    {
        var state = new SimulationState();
        var payload = Cs2PayloadBuilder.Build(state);

        var json = Cs2PayloadJson.Serialize(payload);
        var dto = JsonSerializer.Deserialize<GsiPayloadDto>(json);

        dto.Should().NotBeNull();
        dto!.Provider.Should().NotBeNull();
        dto.Map.Should().NotBeNull();
        dto.Player.Should().NotBeNull();
    }
}
