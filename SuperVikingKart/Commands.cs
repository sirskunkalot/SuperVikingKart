using System.Collections.Generic;
using Jotunn.Entities;
using Jotunn.Managers;

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

    public override List<string> CommandOptionList()
    {
        var options = new List<string> { "list" };
        foreach (var effect in BuffBlockComponent.AllEffects)
            options.Add(effect.Name);
        return options;
    }

    public override void Run(string[] args, Terminal context)
    {
        if (args.Length == 0)
        {
            context.AddString("Usage: svk_buff <name> or svk_buff list");
            return;
        }

        var localPlayer = Player.m_localPlayer;
        if (!localPlayer)
        {
            context.AddString("No local player");
            return;
        }

        if (args[0].ToLower() == "list")
        {
            context.AddString("Available effects:");
            foreach (var effect in BuffBlockComponent.AllEffects)
                context.AddString($"  {effect.Name} ({effect.StatusEffect}) - {effect.Target} {effect.Type}");
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
            context.AddString($"Effect '{search}' not found. Use 'svk_buff list' to see available effects.");
            return;
        }

        var se = ObjectDB.instance.GetStatusEffect(found.StatusEffect.GetStableHashCode());
        if (se == null)
        {
            context.AddString($"Status effect '{found.StatusEffect}' not found in ObjectDB");
            return;
        }

        localPlayer.GetSEMan().AddStatusEffect(se, true);
        context.AddString($"Applied {found.Name} ({found.Type}) to {localPlayer.GetPlayerName()}");
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

    private Terminal _context;

    public override List<string> CommandOptionList()
    {
        var options = new List<string> { "register", "leave", "start", "reset", "results", "list" };
        foreach (var race in RaceManager.GetAllRaces())
            options.Add(race.RaceId);
        return options;
    }

    public override void Run(string[] args, Terminal context)
    {
        _context = context;

        if (args.Length == 0)
        {
            PrintUsage();
            return;
        }

        var player = Player.m_localPlayer;
        if (!player)
        {
            context.AddString("No local player");
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
                    context.AddString("Usage: svk_race register <raceId>");
                    return;
                }

                RegisterPlayer(player, args[1]);
                break;
            case "leave":
                if (args.Length < 2)
                {
                    context.AddString("Usage: svk_race leave <raceId>");
                    return;
                }

                LeaveRace(player, args[1]);
                break;
            case "start":
                if (args.Length < 2)
                {
                    context.AddString("Usage: svk_race start <raceId>");
                    return;
                }

                StartRace(args[1]);
                break;
            case "reset":
                if (args.Length < 2)
                {
                    context.AddString("Usage: svk_race reset <raceId>");
                    return;
                }

                ResetRace(args[1]);
                break;
            case "results":
                if (args.Length < 2)
                {
                    context.AddString("Usage: svk_race results <raceId>");
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
        _context.AddString("Usage: svk_race <subcommand> [args]");
        _context.AddString("  list              - List all races");
        _context.AddString("  register <raceId> - Register for a race");
        _context.AddString("  leave <raceId>    - Leave a race");
        _context.AddString("  start <raceId>    - Start countdown");
        _context.AddString("  reset <raceId>    - Reset a race");
        _context.AddString("  results <raceId>  - Show results");
        _context.AddString("  <raceId>          - Shorthand for register");
    }

    private void ListRaces()
    {
        var count = 0;
        foreach (var race in RaceManager.GetAllRaces())
        {
            _context.AddString(
                $"  [{race.RaceId}] \"{race.Name}\" State: {race.State}, Contestants: {race.Contestants.Count}");
            count++;
        }

        if (count == 0)
            _context.AddString("No active races");
    }

    private void RegisterPlayer(Player player, string raceId)
    {
        var race = RaceManager.GetRace(raceId);
        if (race == null)
        {
            _context.AddString($"Race [{raceId}] not found");
            return;
        }

        if (race.State != RaceState.Idle)
        {
            _context.AddString($"Race [{raceId}] is already underway");
            return;
        }

        if (race.IsRegistered(player.GetZDOID()))
        {
            _context.AddString($"Already registered for [{raceId}]");
            return;
        }

        RaceManager.SendRegister(raceId, player.GetPlayerName(), player.GetZDOID());
        _context.AddString($"Registered for [{raceId}]");
    }

    private void LeaveRace(Player player, string raceId)
    {
        var race = RaceManager.GetRace(raceId);
        if (race == null)
        {
            _context.AddString($"Race [{raceId}] not found");
            return;
        }

        if (!race.IsRegistered(player.GetZDOID()))
        {
            _context.AddString($"Not registered for [{raceId}]");
            return;
        }

        if (race.State == RaceState.Countdown)
        {
            _context.AddString($"Cannot leave [{raceId}] during countdown");
            return;
        }

        if (race.State == RaceState.Finished)
        {
            _context.AddString($"Race [{raceId}] is already finished");
            return;
        }

        RaceManager.SendUnregister(raceId, player.GetZDOID());
        _context.AddString(race.State == RaceState.Racing
            ? $"Left race [{raceId}] - DNF"
            : $"Left race [{raceId}]");
    }

    private void StartRace(string raceId)
    {
        var race = RaceManager.GetRace(raceId);
        if (race == null)
        {
            _context.AddString($"Race [{raceId}] not found");
            return;
        }

        if (race.State != RaceState.Idle)
        {
            _context.AddString($"Race [{raceId}] is not idle (State: {race.State})");
            return;
        }

        if (race.Contestants.Count == 0)
        {
            _context.AddString($"Race [{raceId}] has no contestants");
            return;
        }

        RaceManager.SendStartCountdown(raceId);
        _context.AddString($"Race [{raceId}] countdown started");
    }

    private void ResetRace(string raceId)
    {
        var race = RaceManager.GetRace(raceId);
        if (race == null)
        {
            _context.AddString($"Race [{raceId}] not found");
            return;
        }

        RaceManager.SendReset(raceId);
        _context.AddString($"Race [{raceId}] reset");
    }

    private void ShowResults(string raceId)
    {
        var race = RaceManager.GetRace(raceId);
        if (race == null)
        {
            _context.AddString($"Race [{raceId}] not found");
            return;
        }

        _context.AddString(race.GetResultsText());
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

    private Terminal _context;

    public override List<string> CommandOptionList()
    {
        return new List<string>
        {
            "create", "remove", "addplayer", "setname",
            "setlaps", "forcestart", "forcereset",
            "checkpoint", "lap", "finish", "state"
        };
    }

    public override void Run(string[] args, Terminal context)
    {
        _context = context;

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
                    context.AddString("Usage: svk_race_admin create <raceId> [laps] [name]");
                    return;
                }

                var createLaps = args.Length > 2 && int.TryParse(args[2], out var l) ? l : 1;
                var createName = args.Length > 3 ? string.Join(" ", args, 3, args.Length - 3) : null;
                CreateRace(args[1], createName, createLaps);
                break;

            case "remove":
                if (args.Length < 2)
                {
                    context.AddString("Usage: svk_race_admin remove <raceId>");
                    return;
                }

                RemoveRace(args[1]);
                break;

            case "addplayer":
                if (args.Length < 3)
                {
                    context.AddString("Usage: svk_race_admin addplayer <raceId> <playerName>");
                    return;
                }

                AddPlayer(args[1], args[2]);
                break;

            case "setname":
                if (args.Length < 3)
                {
                    context.AddString("Usage: svk_race_admin setname <raceId> <name>");
                    return;
                }

                SetName(args[1], string.Join(" ", args, 2, args.Length - 2));
                break;

            case "setlaps":
                if (args.Length < 3)
                {
                    context.AddString("Usage: svk_race_admin setlaps <raceId> <count>");
                    return;
                }

                SetLaps(args[1], args[2]);
                break;

            case "setdescription":
                if (args.Length < 3)
                {
                    context.AddString("Usage: svk_race_admin setdescription <raceId> <description>");
                    return;
                }

                SetDescription(args[1], string.Join(" ", args, 2, args.Length - 2));
                break;

            case "forcestart":
                if (args.Length < 2)
                {
                    context.AddString("Usage: svk_race_admin forcestart <raceId>");
                    return;
                }

                ForceStart(args[1]);
                break;

            case "forcereset":
                if (args.Length < 2)
                {
                    context.AddString("Usage: svk_race_admin forcereset <raceId>");
                    return;
                }

                ForceReset(args[1]);
                break;

            case "checkpoint":
                if (args.Length < 3)
                {
                    context.AddString("Usage: svk_race_admin checkpoint <raceId> <checkpointIndex> [playerName]");
                    return;
                }

                SimulateCheckpoint(args);
                break;

            case "lap":
                if (args.Length < 2)
                {
                    context.AddString("Usage: svk_race_admin lap <raceId> [playerName]");
                    return;
                }

                SimulateLap(args);
                break;

            case "finish":
                if (args.Length < 2)
                {
                    context.AddString("Usage: svk_race_admin finish <raceId> [playerName]");
                    return;
                }

                SimulateFinish(args);
                break;

            case "state":
                if (args.Length < 2)
                {
                    context.AddString("Usage: svk_race_admin state <raceId>");
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
        _context.AddString("Usage: svk_race_admin <subcommand> [args]");
        _context.AddString("  create <raceId>                       - Create a new race");
        _context.AddString("  remove <raceId>                       - Remove a race");
        _context.AddString("  addplayer <raceId> <playerName>       - Add a player by name");
        _context.AddString("  setname <raceId> <name>               - Rename a race");
        _context.AddString("  setlaps <raceId> <count>              - Set lap count");
        _context.AddString("  setdescription <raceId> <description> - Set lap count");
        _context.AddString("  forcestart <raceId>                   - Start race regardless of state");
        _context.AddString("  forcereset <raceId>                   - Reset race regardless of state");
        _context.AddString("  lap <raceId> [playerName]             - Simulate a lap completion");
        _context.AddString("  finish <raceId> [playerName]          - Simulate finishing all laps");
        _context.AddString("  state <raceId>                        - Show detailed race state");
    }

    /// <summary>
    /// Broadcasts race creation to all clients.
    /// </summary>
    private void CreateRace(string raceId, string name = null, int laps = 1)
    {
        if (RaceManager.GetRace(raceId) != null)
        {
            _context.AddString($"Race [{raceId}] already exists");
            return;
        }

        var displayName = string.IsNullOrEmpty(name) ? raceId : name;
        RaceManager.SendCreateRace(raceId, displayName, laps);
        _context.AddString($"Race [{raceId}] created - \"{displayName}\" ({laps} laps)");
    }

    /// <summary>
    /// Broadcasts race removal to all clients.
    /// </summary>
    private void RemoveRace(string raceId)
    {
        if (RaceManager.GetRace(raceId) == null)
        {
            _context.AddString($"Race [{raceId}] not found");
            return;
        }

        RaceManager.SendRemoveRace(raceId);
        _context.AddString($"Race [{raceId}] removed");
    }

    /// <summary>
    /// Adds a player to a race by name. Searches all connected players.
    /// </summary>
    private void AddPlayer(string raceId, string playerName)
    {
        var race = RaceManager.GetRace(raceId);
        if (race == null)
        {
            _context.AddString($"Race [{raceId}] not found");
            return;
        }

        if (race.State != RaceState.Idle)
        {
            _context.AddString($"Race [{raceId}] is not idle (State: {race.State})");
            return;
        }

        var player = FindPlayerByName(playerName);
        if (player == null)
        {
            _context.AddString($"Player '{playerName}' not found");
            return;
        }

        if (race.IsRegistered(player.GetZDOID()))
        {
            _context.AddString($"Player '{playerName}' already registered");
            return;
        }

        RaceManager.SendRegister(raceId, player.GetPlayerName(), player.GetZDOID());
        _context.AddString($"Added {player.GetPlayerName()} to [{raceId}]");
    }

    /// <summary>
    /// Broadcasts a name change for an existing race to all clients.
    /// </summary>
    private void SetName(string raceId, string name)
    {
        var race = RaceManager.GetRace(raceId);
        if (race == null)
        {
            _context.AddString($"Race [{raceId}] not found");
            return;
        }

        RaceManager.SendSetName(raceId, name);
        _context.AddString($"Race [{raceId}] renamed to \"{name}\"");
    }

    /// <summary>
    /// Broadcasts a lap change for an existing race to all clients.
    /// </summary>
    private void SetLaps(string raceId, string countStr)
    {
        if (!int.TryParse(countStr, out var count) || count < 1)
        {
            _context.AddString("Invalid lap count");
            return;
        }

        var race = RaceManager.GetRace(raceId);
        if (race == null)
        {
            _context.AddString($"Race [{raceId}] not found");
            return;
        }

        RaceManager.SendSetLaps(raceId, count);
        _context.AddString($"Race [{raceId}] laps set to {count}");
    }

    /// <summary>
    /// Broadcasts a description for an existing race to all clients.
    /// </summary>
    private void SetDescription(string raceId, string description)
    {
        var race = RaceManager.GetRace(raceId);
        if (race == null)
        {
            _context.AddString($"Race [{raceId}] not found");
            return;
        }

        RaceManager.SendSetDescription(raceId, description);
        _context.AddString($"Race [{raceId}] description set to \"{description}\"");
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
            _context.AddString($"Race [{raceId}] not found");
            return;
        }

        if (race.Contestants.Count == 0)
        {
            _context.AddString($"Race [{raceId}] has no contestants");
            return;
        }

        RaceManager.SendState(raceId, RaceState.Idle);
        RaceManager.SendStartCountdown(raceId);
        _context.AddString($"Race [{raceId}] force started");
    }

    /// <summary>
    /// Resets a race regardless of current state.
    /// </summary>
    private void ForceReset(string raceId)
    {
        var race = RaceManager.GetRace(raceId);
        if (race == null)
        {
            _context.AddString($"Race [{raceId}] not found");
            return;
        }

        RaceManager.SendState(raceId, RaceState.Finished);
        RaceManager.SendReset(raceId);
        _context.AddString($"Race [{raceId}] force reset");
    }

    /// <summary>
    /// Simulates a checkpoint passing for a player. Uses local player if no name given.
    /// </summary>
    private void SimulateCheckpoint(string[] args)
    {
        var raceId = args[1];
        var race = RaceManager.GetRace(raceId);
        if (race == null)
        {
            _context.AddString($"Race [{raceId}] not found");
            return;
        }

        if (race.State != RaceState.Racing)
        {
            _context.AddString($"Race [{raceId}] is not racing (State: {race.State})");
            return;
        }

        if (!int.TryParse(args[2], out var checkpointIndex) || checkpointIndex < 0)
        {
            _context.AddString("Invalid checkpoint index");
            return;
        }

        var player = args.Length > 3 ? FindPlayerByName(args[3]) : Player.m_localPlayer;
        if (player == null)
        {
            _context.AddString(args.Length > 3 ? $"Player '{args[3]}' not found" : "No local player");
            return;
        }

        var contestant = race.GetContestant(player.GetZDOID());
        if (contestant == null)
        {
            _context.AddString($"Player '{player.GetPlayerName()}' not in race [{raceId}]");
            return;
        }

        if (contestant.Finished)
        {
            _context.AddString($"Player '{player.GetPlayerName()}' already finished");
            return;
        }

        RaceManager.SendCheckpoint(raceId, player.GetZDOID(), checkpointIndex);
        _context.AddString($"Checkpoint {checkpointIndex} recorded for {player.GetPlayerName()}");
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
            _context.AddString($"Race [{raceId}] not found");
            return;
        }

        if (race.State != RaceState.Racing)
        {
            _context.AddString($"Race [{raceId}] is not racing (State: {race.State})");
            return;
        }

        var player = args.Length > 2 ? FindPlayerByName(args[2]) : Player.m_localPlayer;
        if (player == null)
        {
            _context.AddString(args.Length > 2 ? $"Player '{args[2]}' not found" : "No local player");
            return;
        }

        var contestant = race.GetContestant(player.GetZDOID());
        if (contestant == null)
        {
            _context.AddString($"Player '{player.GetPlayerName()}' not in race [{raceId}]");
            return;
        }

        if (contestant.Finished)
        {
            _context.AddString($"Player '{player.GetPlayerName()}' already finished");
            return;
        }

        RaceManager.SendLap(raceId, player.GetZDOID());
        _context.AddString($"Lap recorded for {player.GetPlayerName()}");
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
            _context.AddString($"Race [{raceId}] not found");
            return;
        }

        if (race.State != RaceState.Racing)
        {
            _context.AddString($"Race [{raceId}] is not racing (State: {race.State})");
            return;
        }

        var player = args.Length > 2 ? FindPlayerByName(args[2]) : Player.m_localPlayer;
        if (player == null)
        {
            _context.AddString(args.Length > 2 ? $"Player '{args[2]}' not found" : "No local player");
            return;
        }

        var contestant = race.GetContestant(player.GetZDOID());
        if (contestant == null)
        {
            _context.AddString($"Player '{player.GetPlayerName()}' not in race [{raceId}]");
            return;
        }

        if (contestant.Finished)
        {
            _context.AddString($"Player '{player.GetPlayerName()}' already finished");
            return;
        }

        // Send laps for all remaining
        var remaining = race.TotalLaps - contestant.CurrentLap;
        for (var i = 0; i < remaining; i++)
            RaceManager.SendLap(raceId, player.GetZDOID());

        _context.AddString($"Finished {player.GetPlayerName()} ({remaining} laps sent)");
    }

    /// <summary>
    /// Shows detailed state of a race including all contestants.
    /// </summary>
    private void ShowState(string raceId)
    {
        var race = RaceManager.GetRace(raceId);
        if (race == null)
        {
            _context.AddString($"Race [{raceId}] not found");
            return;
        }

        _context.AddString($"Race [{race.RaceId}] \"{race.Name}\"");
        _context.AddString($"  State: {race.State}");
        _context.AddString($"  Laps: {race.TotalLaps}");
        _context.AddString($"  Contestants: {race.Contestants.Count}");

        if (race.State == RaceState.Racing)
            _context.AddString($"  Elapsed: {RaceUtils.FormatTime(ZNet.instance.m_netTime - race.RaceStartTime)}");

        foreach (var c in race.GetLiveRanking())
        {
            string status;
            if (c.IsDnf)
            {
                var cpInfo = c.LastCheckpointIndex > 0 ? $", CP {c.LastCheckpointIndex}" : "";
                status = $"DNF - Lap {c.CurrentLap}/{race.TotalLaps}{cpInfo}";
            }
            else if (c.Finished)
                status = $"P{c.Position} - {RaceUtils.FormatTime(c.FinishTime)}";
            else if (race.State == RaceState.Racing)
            {
                var cpInfo = c.LastCheckpointIndex > 0 ? $", CP {c.LastCheckpointIndex}" : "";
                status = $"Lap {c.CurrentLap}/{race.TotalLaps}{cpInfo} ({RaceUtils.FormatTime(c.LastCheckpointTime)})";
            }
            else
                status = "Registered";

            _context.AddString($"    {c.PlayerName} ({c.PlayerId}): {status}");
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