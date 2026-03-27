using System.Collections;
using System.Collections.Generic;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace SuperVikingKart;

internal static class Commands
{
    public static void Register()
    {
        CommandManager.Instance.AddConsoleCommand(new ForceBuffCommand());
        CommandManager.Instance.AddConsoleCommand(new RaceCommand());
        CommandManager.Instance.AddConsoleCommand(new RaceAdminCommand());
    }
}

/// <summary>
/// Debug command to force-apply a specific buff or debuff.
/// Requires devcommands.
/// </summary>
internal class ForceBuffCommand : ConsoleCommand
{
    public override string Name => "svk_buff";
    public override string Help => "Force a specific buff. Usage: svk_buff <name> or svk_buff list";
    public override bool IsCheat => true;
    public override bool IsNetwork => true;

    public override List<string> CommandOptionList()
    {
        var options = new List<string> { "list" };
        foreach (var effect in BuffBlockComponent.AllEffects)
            options.Add(effect.Name);
        return options;
    }

    public override void Run(string[] args)
    {
        if (args.Length == 0)
        {
            Console.instance.Print("Usage: svk_buff <name> or svk_buff list");
            return;
        }

        var localPlayer = Player.m_localPlayer;
        if (!localPlayer)
        {
            Console.instance.Print("No local player");
            return;
        }

        if (args[0].ToLower() == "list")
        {
            Console.instance.Print("Available effects:");
            foreach (var effect in BuffBlockComponent.AllEffects)
                Console.instance.Print($"  {effect.Name} ({effect.StatusEffect}) - {effect.Target} {effect.Type}");
            return;
        }

        var search = string.Join(" ", args).ToLower();
        BuffDefinition found = null;

        foreach (var effect in BuffBlockComponent.AllEffects)
        {
            if (effect.Name.ToLower() == search || effect.StatusEffect.ToLower() == search)
            {
                found = effect;
                break;
            }
        }

        if (found == null)
        {
            Console.instance.Print($"Effect '{search}' not found. Use 'svk_buff list' to see available effects.");
            return;
        }

        var se = ObjectDB.instance.GetStatusEffect(found.StatusEffect.GetStableHashCode());
        if (se == null)
        {
            Console.instance.Print($"Status effect '{found.StatusEffect}' not found in ObjectDB");
            return;
        }

        localPlayer.GetSEMan().AddStatusEffect(se, true);
        Console.instance.Print($"Applied {found.Name} ({found.Type}) to {localPlayer.GetPlayerName()}");
    }
}

/// <summary>
/// Player race commands. Available to all players.
/// Routes actions through RaceManager's global RPCs.
/// </summary>
internal class RaceCommand : ConsoleCommand
{
    public override string Name => "svk_race";
    public override string Help => "Race participation. Usage: svk_race <subcommand> [args]";
    public override bool IsNetwork => true;

    public override List<string> CommandOptionList()
    {
        var options = new List<string> { "register", "leave", "start", "reset", "results", "list" };
        foreach (var race in RaceManager.GetAllRaces())
            options.Add(race.RaceId);
        return options;
    }

    public override void Run(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return;
        }

        var player = Player.m_localPlayer;
        if (!player)
        {
            Console.instance.Print("No local player");
            return;
        }

        switch (args[0].ToLower())
        {
            case "list":
                ListRaces();
                break;
            case "register":
                if (args.Length < 2)
                {
                    Console.instance.Print("Usage: svk_race register <raceId>");
                    return;
                }

                RegisterPlayer(player, args[1]);
                break;
            case "leave":
                if (args.Length < 2)
                {
                    Console.instance.Print("Usage: svk_race leave <raceId>");
                    return;
                }

                LeaveRace(player, args[1]);
                break;
            case "start":
                if (args.Length < 2)
                {
                    Console.instance.Print("Usage: svk_race start <raceId>");
                    return;
                }

                StartRace(args[1]);
                break;
            case "reset":
                if (args.Length < 2)
                {
                    Console.instance.Print("Usage: svk_race reset <raceId>");
                    return;
                }

                ResetRace(args[1]);
                break;
            case "results":
                if (args.Length < 2)
                {
                    Console.instance.Print("Usage: svk_race results <raceId>");
                    return;
                }

                ShowResults(args[1]);
                break;
            default:
                RegisterPlayer(player, args[0]);
                break;
        }
    }

    private void PrintUsage()
    {
        Console.instance.Print("Usage: svk_race <subcommand> [args]");
        Console.instance.Print("  list              - List all races");
        Console.instance.Print("  register <raceId> - Register for a race");
        Console.instance.Print("  leave <raceId>    - Leave a race");
        Console.instance.Print("  start <raceId>    - Start countdown");
        Console.instance.Print("  reset <raceId>    - Reset a race");
        Console.instance.Print("  results <raceId>  - Show results");
        Console.instance.Print("  <raceId>          - Shorthand for register");
    }

    private void ListRaces()
    {
        var count = 0;
        foreach (var race in RaceManager.GetAllRaces())
        {
            Console.instance.Print(
                $"  [{race.RaceId}] \"{race.Name}\" State: {race.State}, Contestants: {race.Contestants.Count}");
            count++;
        }

        if (count == 0)
            Console.instance.Print("No active races");
    }

    private void RegisterPlayer(Player player, string raceId)
    {
        var race = RaceManager.GetRace(raceId);
        if (race == null)
        {
            Console.instance.Print($"Race [{raceId}] not found");
            return;
        }

        if (race.State != RaceState.Idle)
        {
            Console.instance.Print($"Race [{raceId}] is already underway");
            return;
        }

        if (race.IsRegistered(player.GetZDOID()))
        {
            Console.instance.Print($"Already registered for [{raceId}]");
            return;
        }

        RaceManager.SendRegister(raceId, player.GetPlayerName(), player.GetZDOID());
        Console.instance.Print($"Registered for [{raceId}]");
    }

    private void LeaveRace(Player player, string raceId)
    {
        var race = RaceManager.GetRace(raceId);
        if (race == null)
        {
            Console.instance.Print($"Race [{raceId}] not found");
            return;
        }

        if (!race.IsRegistered(player.GetZDOID()))
        {
            Console.instance.Print($"Not registered for [{raceId}]");
            return;
        }

        if (race.State == RaceState.Countdown)
        {
            Console.instance.Print($"Cannot leave [{raceId}] during countdown");
            return;
        }

        if (race.State == RaceState.Finished)
        {
            Console.instance.Print($"Race [{raceId}] is already finished");
            return;
        }

        RaceManager.SendUnregister(raceId, player.GetZDOID());
        Console.instance.Print(race.State == RaceState.Racing
            ? $"Left race [{raceId}] - DNF"
            : $"Left race [{raceId}]");
    }

    private void StartRace(string raceId)
    {
        var race = RaceManager.GetRace(raceId);
        if (race == null)
        {
            Console.instance.Print($"Race [{raceId}] not found");
            return;
        }

        if (race.State != RaceState.Idle)
        {
            Console.instance.Print($"Race [{raceId}] is not idle (State: {race.State})");
            return;
        }

        if (race.Contestants.Count == 0)
        {
            Console.instance.Print($"Race [{raceId}] has no contestants");
            return;
        }

        RaceManager.SendStartCountdown(raceId);
        Console.instance.Print($"Race [{raceId}] countdown started");
    }

    private void ResetRace(string raceId)
    {
        var race = RaceManager.GetRace(raceId);
        if (race == null)
        {
            Console.instance.Print($"Race [{raceId}] not found");
            return;
        }

        RaceManager.SendReset(raceId);
        Console.instance.Print($"Race [{raceId}] reset");
    }

    private void ShowResults(string raceId)
    {
        var race = RaceManager.GetRace(raceId);
        if (race == null)
        {
            Console.instance.Print($"Race [{raceId}] not found");
            return;
        }

        Console.instance.Print(race.GetResultsText());
    }
}

/// <summary>
/// Admin-only race management and testing commands.
/// Requires devcommands.
/// </summary>
internal class RaceAdminCommand : ConsoleCommand
{
    public override string Name => "svk_race_admin";
    public override string Help => "Admin race management. Usage: svk_race_admin <subcommand> [args]";
    public override bool IsCheat => true;
    public override bool IsNetwork => true;

    public override List<string> CommandOptionList()
    {
        return new List<string>
        {
            "create", "remove", "addplayer", "setname",
            "setlaps", "forcestart", "forcereset",
            "lap", "finish", "state"
        };
    }

    public override void Run(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return;
        }

        switch (args[0].ToLower())
        {
            case "create":
                if (args.Length < 2)
                {
                    Console.instance.Print("Usage: svk_race_admin create <raceId> [laps] [name]");
                    return;
                }

                var createLaps = args.Length > 2 && int.TryParse(args[2], out var l) ? l : 1;
                var createName = args.Length > 3 ? string.Join(" ", args, 3, args.Length - 3) : null;
                CreateRace(args[1], createName, createLaps);
                break;

            case "remove":
                if (args.Length < 2)
                {
                    Console.instance.Print("Usage: svk_race_admin remove <raceId>");
                    return;
                }

                RemoveRace(args[1]);
                break;

            case "addplayer":
                if (args.Length < 3)
                {
                    Console.instance.Print("Usage: svk_race_admin addplayer <raceId> <playerName>");
                    return;
                }

                AddPlayer(args[1], args[2]);
                break;

            case "setname":
                if (args.Length < 3)
                {
                    Console.instance.Print("Usage: svk_race_admin setname <raceId> <name>");
                    return;
                }

                SetName(args[1], string.Join(" ", args, 2, args.Length - 2));
                break;

            case "setlaps":
                if (args.Length < 3)
                {
                    Console.instance.Print("Usage: svk_race_admin setlaps <raceId> <count>");
                    return;
                }

                SetLaps(args[1], args[2]);
                break;

            case "forcestart":
                if (args.Length < 2)
                {
                    Console.instance.Print("Usage: svk_race_admin forcestart <raceId>");
                    return;
                }

                ForceStart(args[1]);
                break;

            case "forcereset":
                if (args.Length < 2)
                {
                    Console.instance.Print("Usage: svk_race_admin forcereset <raceId>");
                    return;
                }

                ForceReset(args[1]);
                break;

            case "lap":
                if (args.Length < 2)
                {
                    Console.instance.Print("Usage: svk_race_admin lap <raceId> [playerName]");
                    return;
                }

                SimulateLap(args);
                break;

            case "finish":
                if (args.Length < 2)
                {
                    Console.instance.Print("Usage: svk_race_admin finish <raceId> [playerName]");
                    return;
                }

                SimulateFinish(args);
                break;

            case "state":
                if (args.Length < 2)
                {
                    Console.instance.Print("Usage: svk_race_admin state <raceId>");
                    return;
                }

                ShowState(args[1]);
                break;

            default:
                PrintUsage();
                break;
        }
    }

    private void PrintUsage()
    {
        Console.instance.Print("Usage: svk_race_admin <subcommand> [args]");
        Console.instance.Print("  create <raceId>                  - Create a new race");
        Console.instance.Print("  remove <raceId>                  - Remove a race");
        Console.instance.Print("  addplayer <raceId> <playerName>  - Add a player by name");
        Console.instance.Print("  setname <raceId> <name>          - Rename a race");
        Console.instance.Print("  setlaps <raceId> <count>         - Set lap count");
        Console.instance.Print("  forcestart <raceId>              - Start race regardless of state");
        Console.instance.Print("  forcereset <raceId>              - Reset race regardless of state");
        Console.instance.Print("  lap <raceId> [playerName]        - Simulate a lap completion");
        Console.instance.Print("  finish <raceId> [playerName]     - Simulate finishing all laps");
        Console.instance.Print("  state <raceId>                   - Show detailed race state");
    }

    /// <summary>
    /// Broadcasts race creation to all clients.
    /// </summary>
    private void CreateRace(string raceId, string name = null, int laps = 1)
    {
        if (RaceManager.GetRace(raceId) != null)
        {
            Console.instance.Print($"Race [{raceId}] already exists");
            return;
        }

        var displayName = string.IsNullOrEmpty(name) ? raceId : name;
        RaceManager.SendCreateRace(raceId, displayName, laps);
        Console.instance.Print($"Race [{raceId}] created - \"{displayName}\" ({laps} laps)");
    }

    /// <summary>
    /// Broadcasts race removal to all clients.
    /// </summary>
    private void RemoveRace(string raceId)
    {
        if (RaceManager.GetRace(raceId) == null)
        {
            Console.instance.Print($"Race [{raceId}] not found");
            return;
        }

        RaceManager.SendRemoveRace(raceId);
        Console.instance.Print($"Race [{raceId}] removed");
    }

    /// <summary>
    /// Adds a player to a race by name. Searches all connected players.
    /// </summary>
    private void AddPlayer(string raceId, string playerName)
    {
        var race = RaceManager.GetRace(raceId);
        if (race == null)
        {
            Console.instance.Print($"Race [{raceId}] not found");
            return;
        }

        if (race.State != RaceState.Idle)
        {
            Console.instance.Print($"Race [{raceId}] is not idle (State: {race.State})");
            return;
        }

        var player = FindPlayerByName(playerName);
        if (player == null)
        {
            Console.instance.Print($"Player '{playerName}' not found");
            return;
        }

        if (race.IsRegistered(player.GetZDOID()))
        {
            Console.instance.Print($"Player '{playerName}' already registered");
            return;
        }

        RaceManager.SendRegister(raceId, player.GetPlayerName(), player.GetZDOID());
        Console.instance.Print($"Added {player.GetPlayerName()} to [{raceId}]");
    }

    /// <summary>
    /// Broadcasts a name change for an existing race to all clients.
    /// </summary>
    private void SetName(string raceId, string name)
    {
        var race = RaceManager.GetRace(raceId);
        if (race == null)
        {
            Console.instance.Print($"Race [{raceId}] not found");
            return;
        }

        RaceManager.SendSetName(raceId, name);
        Console.instance.Print($"Race [{raceId}] renamed to \"{name}\"");
    }

    /// <summary>
    /// Broadcasts a lap change for an existing race to all clients.
    /// </summary>
    private void SetLaps(string raceId, string countStr)
    {
        if (!int.TryParse(countStr, out var count) || count < 1)
        {
            Console.instance.Print("Invalid lap count");
            return;
        }

        var race = RaceManager.GetRace(raceId);
        if (race == null)
        {
            Console.instance.Print($"Race [{raceId}] not found");
            return;
        }

        RaceManager.SendSetLaps(raceId, count);
        Console.instance.Print($"Race [{raceId}] laps set to {count}");
    }

    /// <summary>
    /// Starts a race immediately, skipping the countdown.
    /// Modifies state directly for testing purposes.
    /// </summary>
    private void ForceStart(string raceId)
    {
        var race = RaceManager.GetRace(raceId);
        if (race == null)
        {
            Console.instance.Print($"Race [{raceId}] not found");
            return;
        }

        if (race.Contestants.Count == 0)
        {
            Console.instance.Print($"Race [{raceId}] has no contestants");
            return;
        }

        RaceManager.SendState(raceId, RaceState.Idle);
        RaceManager.SendStartCountdown(raceId);
        Console.instance.Print($"Race [{raceId}] force started");
    }

    /// <summary>
    /// Resets a race regardless of current state.
    /// </summary>
    private void ForceReset(string raceId)
    {
        var race = RaceManager.GetRace(raceId);
        if (race == null)
        {
            Console.instance.Print($"Race [{raceId}] not found");
            return;
        }

        RaceManager.SendState(raceId, RaceState.Finished);
        RaceManager.SendReset(raceId);
        Console.instance.Print($"Race [{raceId}] force reset");
    }

    /// <summary>
    /// Simulates a lap completion for a player. Uses local player if no name given.
    /// </summary>
    private void SimulateLap(string[] args)
    {
        var raceId = args[1];
        var race = RaceManager.GetRace(raceId);
        if (race == null)
        {
            Console.instance.Print($"Race [{raceId}] not found");
            return;
        }

        if (race.State != RaceState.Racing)
        {
            Console.instance.Print($"Race [{raceId}] is not racing (State: {race.State})");
            return;
        }

        var player = args.Length > 2 ? FindPlayerByName(args[2]) : Player.m_localPlayer;
        if (player == null)
        {
            Console.instance.Print(args.Length > 2 ? $"Player '{args[2]}' not found" : "No local player");
            return;
        }

        var contestant = race.GetContestant(player.GetZDOID());
        if (contestant == null)
        {
            Console.instance.Print($"Player '{player.GetPlayerName()}' not in race [{raceId}]");
            return;
        }

        if (contestant.Finished)
        {
            Console.instance.Print($"Player '{player.GetPlayerName()}' already finished");
            return;
        }

        RaceManager.SendLap(raceId, player.GetZDOID());
        Console.instance.Print($"Lap recorded for {player.GetPlayerName()}");
    }

    /// <summary>
    /// Simulates finishing all remaining laps at once.
    /// Uses local player if no name given.
    /// </summary>
    private void SimulateFinish(string[] args)
    {
        var raceId = args[1];
        var race = RaceManager.GetRace(raceId);
        if (race == null)
        {
            Console.instance.Print($"Race [{raceId}] not found");
            return;
        }

        if (race.State != RaceState.Racing)
        {
            Console.instance.Print($"Race [{raceId}] is not racing (State: {race.State})");
            return;
        }

        var player = args.Length > 2 ? FindPlayerByName(args[2]) : Player.m_localPlayer;
        if (player == null)
        {
            Console.instance.Print(args.Length > 2 ? $"Player '{args[2]}' not found" : "No local player");
            return;
        }

        var contestant = race.GetContestant(player.GetZDOID());
        if (contestant == null)
        {
            Console.instance.Print($"Player '{player.GetPlayerName()}' not in race [{raceId}]");
            return;
        }

        if (contestant.Finished)
        {
            Console.instance.Print($"Player '{player.GetPlayerName()}' already finished");
            return;
        }

        // Send laps for all remaining
        var remaining = race.TotalLaps - contestant.CurrentLap;
        for (var i = 0; i < remaining; i++)
            RaceManager.SendLap(raceId, player.GetZDOID());

        Console.instance.Print($"Finished {player.GetPlayerName()} ({remaining} laps sent)");
    }

    /// <summary>
    /// Shows detailed state of a race including all contestants.
    /// </summary>
    private void ShowState(string raceId)
    {
        var race = RaceManager.GetRace(raceId);
        if (race == null)
        {
            Console.instance.Print($"Race [{raceId}] not found");
            return;
        }

        Console.instance.Print($"Race [{race.RaceId}] \"{race.Name}\"");
        Console.instance.Print($"  State: {race.State}");
        Console.instance.Print($"  Laps: {race.TotalLaps}");
        Console.instance.Print($"  Contestants: {race.Contestants.Count}");

        if (race.State == RaceState.Racing)
            Console.instance.Print($"  Elapsed: {Time.time - race.RaceStartTime:F1}s");

        foreach (var c in race.Contestants)
        {
            var status = c.Finished
                ? c.IsDnf
                    ? $"DNF - Lap {c.CurrentLap}/{race.TotalLaps}"
                    : $"P{c.Position} - {c.FinishTime:F1}s"
                : race.State == RaceState.Racing
                    ? $"Lap {c.CurrentLap}/{race.TotalLaps}"
                    : "Registered";
            Console.instance.Print($"    {c.PlayerName} ({c.PlayerId}): {status}");
        }
    }

    /// <summary>
    /// Finds a player by name from all connected players.
    /// Partial match, case insensitive.
    /// </summary>
    private Player FindPlayerByName(string name)
    {
        var lowerName = name.ToLower();
        foreach (var player in Player.GetAllPlayers())
        {
            if (player.GetPlayerName().ToLower().Contains(lowerName))
                return player;
        }

        return null;
    }
}