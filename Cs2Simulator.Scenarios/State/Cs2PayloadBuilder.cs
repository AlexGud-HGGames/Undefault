using System.Collections.Generic;
using Cs2Simulator.Scenarios.Models;

namespace Cs2Simulator.Scenarios.State;

public static class Cs2PayloadBuilder
{
    public static Cs2Payload Build(SimulationState state)
    {
        var weaponsCopy = new Dictionary<string, Cs2Weapon>(state.PlayerWeapons.Count);
        foreach (var pair in state.PlayerWeapons)
        {
            weaponsCopy[pair.Key] = pair.Value;
        }

        return new Cs2Payload
        {
            Provider = new Cs2Provider
            {
                Name = state.ProviderName,
                AppId = state.ProviderAppId,
                Version = state.ProviderVersion,
                SteamId = state.ProviderSteamId,
                Timestamp = state.Clock.UnixSeconds
            },
            Map = new Cs2Map
            {
                Mode = state.MapMode,
                Name = state.MapName,
                Phase = state.MapPhase,
                Round = state.MapRound,
                MatchId = state.MatchId,
                TeamCt = state.TeamCt,
                TeamT = state.TeamT,
                NumMatchesToWinSeries = state.NumMatchesToWinSeries,
                CurrentSpectators = state.CurrentSpectators,
                SouvenirsTotal = state.SouvenirsTotal
            },
            Round = new Cs2Round
            {
                Phase = state.RoundPhase,
                Bomb = state.BombState,
                WinTeam = state.WinTeam
            },
            Player = new Cs2Player
            {
                SteamId = state.PlayerSteamId,
                Name = state.PlayerName,
                ObserverSlot = state.PlayerObserverSlot,
                Team = state.PlayerTeam,
                Activity = state.PlayerActivity,
                Position = state.PlayerPosition,
                Forward = state.PlayerForward,
                State = new Cs2PlayerState
                {
                    Health = state.PlayerHealth,
                    Armor = state.PlayerArmor,
                    Helmet = state.PlayerHelmet,
                    Flashed = state.PlayerFlashed,
                    Smoked = state.PlayerSmoked,
                    Burning = state.PlayerBurning,
                    Money = state.PlayerMoney,
                    RoundKills = state.PlayerRoundKills,
                    RoundKillHeadshots = state.PlayerRoundKillHeadshots,
                    RoundTotalDamage = state.PlayerRoundTotalDamage,
                    EquipValue = state.PlayerEquipValue,
                    DefuseKit = state.PlayerDefuseKit
                },
                Weapons = weaponsCopy,
                MatchStats = new Cs2MatchStats
                {
                    Kills = state.PlayerKills,
                    Assists = state.PlayerAssists,
                    Deaths = state.PlayerDeaths,
                    Mvps = state.PlayerMvps,
                    Score = state.PlayerScore
                }
            }
        };
    }
}
