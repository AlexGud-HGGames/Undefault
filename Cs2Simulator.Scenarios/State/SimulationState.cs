using System.Collections.Generic;

namespace Cs2Simulator.Scenarios.State;

/// <summary>
/// Mutable in-memory model of the simulated CS2 world.
/// Scenarios mutate this between ticks; <see cref="Cs2PayloadBuilder"/>
/// renders it into a wire payload.
/// </summary>
public sealed class SimulationState
{
    public const string DefaultPlayerSteamId = "76561198000000001";
    public const string DefaultPlayerName = "LocalPlayer";
    public const int DefaultPlayerObserverSlot = 0;

    public SimulationState()
    {
        Clock = new SimulatedClock();
    }

    public SimulatedClock Clock { get; }

    // provider
    public string ProviderName { get; set; } = "Counter-Strike: Global Offensive";
    public int ProviderAppId { get; set; } = 730;
    public int ProviderVersion { get; set; } = 13902;
    public string ProviderSteamId { get; set; } = DefaultPlayerSteamId;

    // map
    public string MapMode { get; set; } = "competitive";
    public string MapName { get; set; } = "de_mirage";
    public string MapPhase { get; set; } = "warmup";
    public int MapRound { get; set; }
    public string MatchId { get; set; } = "match_local_sim";
    public Cs2Simulator.Scenarios.Models.Cs2TeamScore TeamCt { get; set; } = new()
    {
        Score = 0,
        ConsecutiveRoundLosses = 0,
        TimeoutsRemaining = 4,
        MatchesWonThisSeries = 0
    };
    public Cs2Simulator.Scenarios.Models.Cs2TeamScore TeamT { get; set; } = new()
    {
        Score = 0,
        ConsecutiveRoundLosses = 0,
        TimeoutsRemaining = 4,
        MatchesWonThisSeries = 0
    };
    public int NumMatchesToWinSeries { get; set; }
    public int CurrentSpectators { get; set; }
    public int SouvenirsTotal { get; set; }

    // round (shape-only for the host today; kept for fidelity)
    public string RoundPhase { get; set; } = "freezetime";
    public string? BombState { get; set; }
    public string? WinTeam { get; set; }

    // player
    public string PlayerSteamId { get; set; } = DefaultPlayerSteamId;
    public string PlayerName { get; set; } = DefaultPlayerName;
    public int PlayerObserverSlot { get; set; } = DefaultPlayerObserverSlot;
    public string PlayerTeam { get; set; } = "T";
    public string PlayerActivity { get; set; } = "playing";
    public string PlayerPosition { get; set; } = "0.00, 0.00, 0.00";
    public string PlayerForward { get; set; } = "1.00, 0.00, 0.00";

    public int PlayerHealth { get; set; } = 100;
    public int PlayerArmor { get; set; } = 0;
    public bool PlayerHelmet { get; set; }
    public int PlayerFlashed { get; set; }
    public int PlayerSmoked { get; set; }
    public int PlayerBurning { get; set; }
    public int PlayerMoney { get; set; } = 800;
    public int PlayerRoundKills { get; set; }
    public int PlayerRoundKillHeadshots { get; set; }
    public int PlayerRoundTotalDamage { get; set; }
    public int PlayerEquipValue { get; set; } = 200;
    public bool PlayerDefuseKit { get; set; }

    public Dictionary<string, Cs2Simulator.Scenarios.Models.Cs2Weapon> PlayerWeapons { get; }
        = new()
        {
            ["weapon_0"] = new Cs2Simulator.Scenarios.Models.Cs2Weapon
            {
                Name = "weapon_knife_t",
                PaintKit = "default",
                Type = "Knife",
                State = "holstered"
            },
            ["weapon_1"] = new Cs2Simulator.Scenarios.Models.Cs2Weapon
            {
                Name = "weapon_glock",
                PaintKit = "default",
                Type = "Pistol",
                AmmoClip = 20,
                AmmoClipMax = 20,
                AmmoReserve = 120,
                State = "active"
            }
        };

    public int PlayerKills { get; set; }
    public int PlayerAssists { get; set; }
    public int PlayerDeaths { get; set; }
    public int PlayerMvps { get; set; }
    public int PlayerScore { get; set; }
}
