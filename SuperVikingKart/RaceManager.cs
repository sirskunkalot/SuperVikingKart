using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SuperVikingKart
{
    internal enum RaceState
    {
        Idle,
        Countdown,
        Racing,
        Finished
    }

    /// <summary>
    /// Represents a single player's participation in a race.
    /// Mutated in-place by Race methods; never replaced once added.
    /// </summary>
    internal class RaceContestant
    {
        public string PlayerName;
        public ZDOID PlayerId;
        public bool CrossedStart;
        public int CurrentLap;
        public bool Finished;
        public int Position;
        public float FinishTime;
        public bool IsDnf;

        public RaceContestant(string playerName, ZDOID playerId)
        {
            PlayerName = playerName;
            PlayerId = playerId;
            CrossedStart = false;
            CurrentLap = 0;
            Finished = false;
            Position = -1;
            FinishTime = -1f;
        }
    }

    /// <summary>
    /// Holds the state for a single race instance.
    /// State is kept in sync across clients via global RPCs on RaceManager.
    /// </summary>
    internal class Race
    {
        public string RaceId;
        public string Name;
        public int TotalLaps;
        public RaceState State = RaceState.Idle;
        public List<RaceContestant> Contestants = new();
        public float RaceStartTime;
        public int NextPosition = 1;

        public Race(string raceId, string name = null, int laps = 1)
        {
            RaceId = raceId;
            Name = string.IsNullOrEmpty(name) ? raceId : name;
            TotalLaps = laps;
        }

        public bool IsRegistered(ZDOID playerId)
            => Contestants.Any(c => c.PlayerId == playerId);

        public RaceContestant GetContestant(ZDOID playerId)
            => Contestants.FirstOrDefault(c => c.PlayerId == playerId);

        public bool AddContestant(string playerName, ZDOID playerId)
        {
            if (State != RaceState.Idle) return false;
            if (IsRegistered(playerId)) return false;

            Contestants.Add(new RaceContestant(playerName, playerId));
            SuperVikingKart.DebugLog($"Race [{RaceId}] - Registered {playerName}");
            return true;
        }

        public void RemoveContestant(ZDOID playerId)
        {
            var contestant = GetContestant(playerId);
            if (contestant == null) return;

            if (State == RaceState.Idle)
            {
                Contestants.Remove(contestant);
                SuperVikingKart.DebugLog($"Race [{RaceId}] - Unregistered {contestant.PlayerName}");
                return;
            }

            if (State == RaceState.Racing)
            {
                RecordDnf(playerId);
                SuperVikingKart.DebugLog($"Race [{RaceId}] - {contestant.PlayerName} left mid-race, marked DNF");
            }

            // Countdown / Finished — silently ignore
        }

        public void StartCountdown()
        {
            if (State != RaceState.Idle || Contestants.Count == 0) return;
            State = RaceState.Countdown;
            SuperVikingKart.DebugLog($"Race [{RaceId}] - Countdown started");
        }

        public void StartRace(float startTime)
        {
            State = RaceState.Racing;
            RaceStartTime = startTime;
            NextPosition = 1;
            SuperVikingKart.DebugLog($"Race [{RaceId}] - Race started (t={startTime:F3})");
        }

        public bool RecordLap(ZDOID playerId)
        {
            if (State != RaceState.Racing) return false;
            var contestant = GetContestant(playerId);
            if (contestant == null || contestant.Finished) return false;
            contestant.CurrentLap++;
            return contestant.CurrentLap > TotalLaps;
        }

        public void RecordDnf(ZDOID playerId)
        {
            if (State != RaceState.Racing) return;
            var contestant = GetContestant(playerId);
            if (contestant == null || contestant.Finished) return;
            contestant.IsDnf = true;
            contestant.Finished = true;
            SuperVikingKart.DebugLog($"Race [{RaceId}] - {contestant.PlayerName} DNF");

            if (AllFinished())
            {
                State = RaceState.Finished;
                SuperVikingKart.DebugLog($"Race [{RaceId}] - All finished (via DNF)");
            }
        }

        public void RecordFinish(ZDOID playerId, float finishTime)
        {
            if (State != RaceState.Racing) return;
            var contestant = GetContestant(playerId);
            if (contestant == null || contestant.Finished) return;
            contestant.Finished = true;
            contestant.FinishTime = finishTime;

            // Check whether anyone already finished with the same time.
            var tiedContestant = Contestants.FirstOrDefault(c =>
                c.Finished && !c.IsDnf && c.Position > 0 && c.FinishTime == finishTime);

            if (tiedContestant != null)
            {
                // Share the position already assigned to the tied group.
                contestant.Position = tiedContestant.Position;
            }
            else
            {
                contestant.Position = NextPosition++;
            }

            SuperVikingKart.DebugLog(
                $"Race [{RaceId}] - {contestant.PlayerName} finished " +
                $"P{contestant.Position} in {contestant.FinishTime:F1}s");

            if (AllFinished())
            {
                State = RaceState.Finished;
                SuperVikingKart.DebugLog($"Race [{RaceId}] - All finished");
            }
        }

        public bool AllFinished()
            => Contestants.All(c => c.Finished);

        public void Reset()
        {
            State = RaceState.Idle;
            Contestants.Clear();
            NextPosition = 1;
            SuperVikingKart.DebugLog($"Race [{RaceId}] - Reset");
        }

        public string GetResultsText()
        {
            var finished = Contestants.Where(c => c.Finished && !c.IsDnf).OrderBy(c => c.Position).ToList();
            var dnf = Contestants.Where(c => c.IsDnf).ToList();
            var stillRacing = Contestants.Where(c => !c.Finished && !c.IsDnf).ToList();

            var text = $"{Name} ({TotalLaps} laps) Results:\n";

            // Group by position so tied players appear on one line.
            foreach (var group in finished.GroupBy(c => c.Position).OrderBy(g => g.Key))
            {
                var names = string.Join(" / ", group.Select(c => c.PlayerName));
                var time = group.First().FinishTime;
                text += $"  P{group.Key} {names} - {time:F1}s\n";
            }

            foreach (var c in dnf)
                text += $"  DNF {c.PlayerName} (Lap {c.CurrentLap}/{TotalLaps})\n";

            foreach (var c in stillRacing)
                text += $"  ??? {c.PlayerName} (Lap {c.CurrentLap}/{TotalLaps})\n";

            return text;
        }
    }

    /// <summary>
    /// Manages all active races and handles global RPCs for race state changes.
    /// Acts as the single source of truth on the server; clients receive and apply
    /// state deltas via the registered RPC handlers. All public entry points are
    /// the Send* methods — direct mutation of Race objects should only happen
    /// inside RPC handlers so every peer stays in sync.
    /// </summary>
    internal static class RaceManager
    {
        private static readonly Dictionary<string, Race> Races = new();

        // --- Init ---

        /// <summary>
        /// Registers all RPC handlers and, if running as a client, requests a full
        /// state snapshot from the server to catch up on any races already in progress.
        /// Must be called once after ZRoutedRpc is available.
        /// </summary>
        public static void Init()
        {
            ZRoutedRpc.instance.Register("SuperVikingKart_Race_RequestSync", RPC_RequestSync);
            ZRoutedRpc.instance.Register<ZPackage>("SuperVikingKart_Race_SyncState", RPC_SyncState);
            ZRoutedRpc.instance.Register<string, string, int>("SuperVikingKart_Race_Create", RPC_CreateRace);
            ZRoutedRpc.instance.Register<string>("SuperVikingKart_Race_Remove", RPC_RemoveRace);
            ZRoutedRpc.instance.Register<string, string>("SuperVikingKart_Race_SetName", RPC_SetName);
            ZRoutedRpc.instance.Register<string, int>("SuperVikingKart_Race_SetLaps", RPC_SetLaps);
            ZRoutedRpc.instance.Register<string, int>("SuperVikingKart_Race_SetState", RPC_SetState);
            ZRoutedRpc.instance.Register<string, string, ZDOID>("SuperVikingKart_Race_Register", RPC_Register);
            ZRoutedRpc.instance.Register<string, ZDOID>("SuperVikingKart_Race_Unregister", RPC_Unregister);
            ZRoutedRpc.instance.Register<string>("SuperVikingKart_Race_StartCountdown", RPC_StartCountdown);
            ZRoutedRpc.instance.Register<string, int>("SuperVikingKart_Race_CountdownTick", RPC_CountdownTick);
            ZRoutedRpc.instance.Register<string, float>("SuperVikingKart_Race_Go", RPC_Go);
            ZRoutedRpc.instance.Register<string, ZDOID>("SuperVikingKart_Race_CrossedStart", RPC_CrossedStart);
            ZRoutedRpc.instance.Register<string, ZDOID, float>("SuperVikingKart_Race_Lap", RPC_Lap);
            ZRoutedRpc.instance.Register<string, ZDOID>("SuperVikingKart_Race_Dnf", RPC_Dnf);
            ZRoutedRpc.instance.Register<string>("SuperVikingKart_Race_Reset", RPC_Reset);

            if (!ZNet.instance.IsServer())
                ZRoutedRpc.instance.InvokeRoutedRPC(
                    ZRoutedRpc.instance.GetServerPeerID(),
                    "SuperVikingKart_Race_RequestSync");

            Races.Clear();
            SuperVikingKart.DebugLog("RaceManager initialized");
        }

        // --- Race Management ---

        public static Race GetRace(string raceId)
            => Races.TryGetValue(raceId, out var race) ? race : null;

        public static IEnumerable<Race> GetAllRaces() => Races.Values;

        // --- Send Methods ---

        public static void SendCreateRace(string raceId, string name, int laps)
            => ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody,
                "SuperVikingKart_Race_Create", raceId, name, laps);

        public static void SendRemoveRace(string raceId)
            => ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody,
                "SuperVikingKart_Race_Remove", raceId);

        public static void SendSetName(string raceId, string name)
            => ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody,
                "SuperVikingKart_Race_SetName", raceId, name);

        public static void SendSetLaps(string raceId, int laps)
            => ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody,
                "SuperVikingKart_Race_SetLaps", raceId, laps);

        public static void SendState(string raceId, RaceState state)
            => ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody,
                "SuperVikingKart_Race_SetState", raceId, (int)state);

        public static void SendRegister(string raceId, string playerName, ZDOID playerId)
            => ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody,
                "SuperVikingKart_Race_Register", raceId, playerName, playerId);

        public static void SendUnregister(string raceId, ZDOID playerId)
            => ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody,
                "SuperVikingKart_Race_Unregister", raceId, playerId);

        public static void SendStartCountdown(string raceId)
            => ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody,
                "SuperVikingKart_Race_StartCountdown", raceId);

        public static void SendCountdownTick(string raceId, int number)
            => ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody,
                "SuperVikingKart_Race_CountdownTick", raceId, number);

        public static void SendGo(string raceId)
            => ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody,
                "SuperVikingKart_Race_Go", raceId, Time.time);

        public static void SendCrossedStart(string raceId, ZDOID playerId)
            => ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody,
                "SuperVikingKart_Race_CrossedStart", raceId, playerId);

        public static void SendLap(string raceId, ZDOID playerId)
            => ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody,
                "SuperVikingKart_Race_Lap", raceId, playerId, Time.time);

        public static void SendDnf(string raceId, ZDOID playerId)
            => ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody,
                "SuperVikingKart_Race_Dnf", raceId, playerId);

        public static void SendReset(string raceId)
            => ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody,
                "SuperVikingKart_Race_Reset", raceId);

        // --- RPC Handlers ---

        /// <summary>
        /// Server-only. Serialises the full Races dictionary into a ZPackage
        /// and sends it directly to the requesting peer for initial catch-up.
        /// </summary>
        private static void RPC_RequestSync(long sender)
        {
            if (!ZNet.instance.IsServer()) return;

            SuperVikingKart.DebugLog($"RaceManager - Syncing state to {sender}");
            var pkg = new ZPackage();
            pkg.Write(Races.Count);

            foreach (var race in Races.Values)
            {
                pkg.Write(race.RaceId);
                pkg.Write(race.Name);
                pkg.Write(race.TotalLaps);
                pkg.Write((int)race.State);
                pkg.Write(race.RaceStartTime);
                pkg.Write(race.NextPosition);
                pkg.Write(race.Contestants.Count);

                foreach (var c in race.Contestants)
                {
                    pkg.Write(c.PlayerName);
                    pkg.Write(c.PlayerId);
                    pkg.Write(c.CrossedStart);
                    pkg.Write(c.CurrentLap);
                    pkg.Write(c.Finished);
                    pkg.Write(c.Position);
                    pkg.Write(c.FinishTime);
                    pkg.Write(c.IsDnf);
                }
            }

            ZRoutedRpc.instance.InvokeRoutedRPC(sender, "SuperVikingKart_Race_SyncState", pkg);
        }

        /// <summary>
        /// Replaces the local Races dictionary with the full snapshot sent by the server.
        /// Read order must match the write order in <see cref="RPC_RequestSync"/> exactly.
        /// </summary>
        private static void RPC_SyncState(long sender, ZPackage pkg)
        {
            SuperVikingKart.DebugLog("RaceManager - Received state sync");
            Races.Clear();

            var raceCount = pkg.ReadInt();
            for (var i = 0; i < raceCount; i++)
            {
                var race = new Race(pkg.ReadString(), pkg.ReadString())
                {
                    TotalLaps = pkg.ReadInt(),
                    State = (RaceState)pkg.ReadInt(),
                    RaceStartTime = pkg.ReadSingle(),
                    NextPosition = pkg.ReadInt()
                };

                var contestantCount = pkg.ReadInt();
                for (var j = 0; j < contestantCount; j++)
                {
                    var contestant = new RaceContestant(pkg.ReadString(), pkg.ReadZDOID())
                    {
                        CrossedStart = pkg.ReadBool(),
                        CurrentLap = pkg.ReadInt(),
                        Finished = pkg.ReadBool(),
                        Position = pkg.ReadInt(),
                        FinishTime = pkg.ReadSingle(),
                        IsDnf = pkg.ReadBool(),
                    };
                    race.Contestants.Add(contestant);
                }

                Races[race.RaceId] = race;
                SuperVikingKart.DebugLog(
                    $"RaceManager - Synced race [{race.RaceId}] " +
                    $"State: {race.State}, Contestants: {race.Contestants.Count}");
            }
        }

        /// <summary>
        /// Runs on all peers. Creates a new Race entry if one does not already exist for
        /// the given ID, ensuring idempotent handling of any duplicate broadcasts.
        /// </summary>
        private static void RPC_CreateRace(long sender, string raceId, string name, int laps)
        {
            if (Races.ContainsKey(raceId)) return;
            Races[raceId] = new Race(raceId, name, laps);
            SuperVikingKart.DebugLog($"RaceManager - Created race [{raceId}] \"{name}\" ({laps} laps)");
        }

        /// <summary>
        /// Runs on all peers. Removes the race entry entirely regardless of its current state.
        /// </summary>
        private static void RPC_RemoveRace(long sender, string raceId)
        {
            Races.Remove(raceId);
            SuperVikingKart.DebugLog($"RaceManager - Removed race [{raceId}]");
        }

        /// <summary>
        /// Runs on all peers. Updates the display name of an existing race.
        /// </summary>
        private static void RPC_SetName(long sender, string raceId, string name)
        {
            var race = GetRace(raceId);
            if (race == null) return;
            race.Name = name;
            SuperVikingKart.DebugLog($"RaceManager - Race [{raceId}] renamed to \"{name}\"");
        }

        /// <summary>
        /// Runs on all peers. Updates the total lap count of an existing race.
        /// Only meaningful before the race starts.
        /// </summary>
        private static void RPC_SetLaps(long sender, string raceId, int laps)
        {
            var race = GetRace(raceId);
            if (race == null) return;
            race.TotalLaps = laps;
            SuperVikingKart.DebugLog($"RaceManager - Laps set to {laps} for [{raceId}]");
        }

        /// <summary>
        /// Runs on all peers. Directly overwrites the race state enum.
        /// Intended for administrative corrections; normal flow uses the
        /// dedicated Start/Go/Reset RPCs instead.
        /// </summary>
        private static void RPC_SetState(long sender, string raceId, int state)
        {
            var race = GetRace(raceId);
            if (race == null) return;
            race.State = (RaceState)state;
            SuperVikingKart.DebugLog($"RaceManager - State set to {(RaceState)state} for [{raceId}]");
        }

        /// <summary>
        /// Runs on all peers. Attempts to add the player to the contestant list
        /// and shows a contextual confirmation or rejection message to that player.
        /// </summary>
        private static void RPC_Register(long sender, string raceId, string playerName, ZDOID playerId)
        {
            var race = GetRace(raceId);
            if (race == null) return;
            var added = race.AddContestant(playerName, playerId);
            var localPlayer = Player.m_localPlayer;
            if (localPlayer && localPlayer.GetZDOID() == playerId)
            {
                localPlayer.Message(MessageHud.MessageType.Center, added
                    ? $"Registered for {race.Name}!"
                    : race.State != RaceState.Idle
                        ? $"{race.Name} is already underway"
                        : $"Already registered for {race.Name}");
            }
        }

        /// <summary>
        /// Runs on all peers. Removes a contestant, issuing a DNF if the race is in progress.
        /// Notifies the leaving player and any remaining registered contestants.
        /// </summary>
        private static void RPC_Unregister(long sender, string raceId, ZDOID playerId)
        {
            var race = GetRace(raceId);
            if (race == null) return;
            var contestant = race.GetContestant(playerId);
            if (contestant == null) return;
            race.RemoveContestant(playerId);

            // Capture wasRacing before RemoveContestant potentially changes State via RecordDnf.
            var wasRacing = race.State == RaceState.Racing;
            var localPlayer = Player.m_localPlayer;
            if (localPlayer && localPlayer.GetZDOID() == playerId)
            {
                localPlayer.Message(MessageHud.MessageType.Center, wasRacing
                    ? $"You left {race.Name} - DNF"
                    : $"Unregistered from {race.Name}");
            }
            else if (localPlayer && race.IsRegistered(localPlayer.GetZDOID()) && wasRacing)
            {
                localPlayer.Message(MessageHud.MessageType.Center,
                    $"{contestant.PlayerName} left the race - DNF");
            }
        }

        /// <summary>
        /// Runs on all peers. Transitions the race to Countdown state.
        /// Only the peer whose session ID matches the sender starts the countdown
        /// coroutine, preventing duplicate tick broadcasts.
        /// </summary>
        private static void RPC_StartCountdown(long sender, string raceId)
        {
            var race = GetRace(raceId);
            if (race == null) return;
            race.StartCountdown();
            if (sender == ZDOMan.GetSessionID())
                SuperVikingKart.Instance.StartCoroutine(CountdownCoroutine(raceId));
        }

        /// <summary>
        /// Broadcasts a countdown tick each second from 3 to 1, then sends
        /// the final 0 tick and the Go RPC to start the race.
        /// Runs only on the peer that initiated the countdown.
        /// </summary>
        private static IEnumerator CountdownCoroutine(string raceId)
        {
            for (var i = 3; i > 0; i--)
            {
                SendCountdownTick(raceId, i);
                yield return new WaitForSeconds(1f);
            }

            SendCountdownTick(raceId, 0);
            SendGo(raceId);
        }

        /// <summary>
        /// Runs on all peers. Displays the countdown number to registered contestants,
        /// showing "GO!" when number is 0.
        /// </summary>
        private static void RPC_CountdownTick(long sender, string raceId, int number)
        {
            var race = GetRace(raceId);
            if (race == null) return;
            var localPlayer = Player.m_localPlayer;
            if (localPlayer && race.IsRegistered(localPlayer.GetZDOID()))
                localPlayer.Message(MessageHud.MessageType.Center, number > 0 ? number.ToString() : "GO!");
        }

        /// <summary>
        /// Runs on all peers. Transitions the race to Racing state using the sender's
        /// Time.time as the shared start timestamp. Only the server starts the disconnect
        /// watchdog to avoid duplicate DNF broadcasts.
        /// </summary>
        private static void RPC_Go(long sender, string raceId, float clientTime)
        {
            var race = GetRace(raceId);
            if (race == null) return;
            race.StartRace(clientTime);

            // Only the server runs the watchdog so DNF broadcasts aren't duplicated.
            if (ZNet.instance.IsServer())
                SuperVikingKart.Instance.StartCoroutine(DisconnectWatchdog(raceId));
        }

        /// <summary>
        /// Polls connected peers every 5 seconds and issues a DNF for any contestant
        /// whose peer is no longer present. Runs only on the server to avoid duplicate
        /// DNF broadcasts. Stops automatically once the race leaves Racing state.
        /// </summary>
        private static IEnumerator DisconnectWatchdog(string raceId)
        {
            SuperVikingKart.DebugLog($"RaceManager - Watchdog started for [{raceId}]");
            while (true)
            {
                yield return new WaitForSeconds(5f);

                var race = GetRace(raceId);
                if (race == null || race.State != RaceState.Racing)
                {
                    SuperVikingKart.DebugLog($"RaceManager - Watchdog stopping for [{raceId}]");
                    yield break;
                }

                var peers = ZNet.instance.GetConnectedPeers();
                foreach (var contestant in race.Contestants.ToList())
                {
                    if (contestant.Finished) continue;
                    var stillConnected = peers.Any(p => p.m_characterID == contestant.PlayerId);

                    // The server's own local player won't appear in the peer list.
                    var localPlayer = Player.m_localPlayer;
                    if (!stillConnected && localPlayer && localPlayer.GetZDOID() == contestant.PlayerId)
                        stillConnected = true;

                    if (!stillConnected)
                    {
                        SuperVikingKart.DebugLog(
                            $"RaceManager - Watchdog: {contestant.PlayerName} disconnected, sending DNF");
                        SendDnf(raceId, contestant.PlayerId);
                    }
                }
            }
        }

        /// <summary>
        /// Runs on all peers. Marks the contestant as having crossed the start line,
        /// credits their first lap, and shows a lap indicator to that player.
        /// Called when a kart crosses a Start or StartFinish line for the first time.
        /// </summary>
        private static void RPC_CrossedStart(long sender, string raceId, ZDOID playerId)
        {
            var race = GetRace(raceId);
            if (race == null || race.State != RaceState.Racing) return;
            var contestant = race.GetContestant(playerId);
            if (contestant == null || contestant.Finished) return;
            contestant.CrossedStart = true;
            contestant.CurrentLap++;

            var localPlayer = Player.m_localPlayer;
            if (localPlayer && localPlayer.GetZDOID() == playerId && race.TotalLaps > 1)
                localPlayer.Message(MessageHud.MessageType.Center,
                    $"Lap {contestant.CurrentLap}/{race.TotalLaps}");

            SuperVikingKart.DebugLog($"Race [{raceId}] - {contestant.PlayerName} crossed start line");
        }

        /// <summary>
        /// Runs on all clients. Increments the contestant's lap count each time they
        /// cross the finish line. On the final lap, records the finish and assigns a
        /// position. Shows finish or lap progress messages to all registered contestants.
        /// If this was the last contestant to finish, displays the full results to all.
        /// </summary>
        /// <param name="clientTime">Time.time from the client whose kart crossed the line.</param>
        private static void RPC_Lap(long sender, string raceId, ZDOID playerId, float clientTime)
        {
            var race = GetRace(raceId);
            if (race == null || race.State != RaceState.Racing) return;
            var contestant = race.GetContestant(playerId);
            if (contestant == null || contestant.Finished) return;
            var finished = race.RecordLap(playerId);
            var localPlayer = Player.m_localPlayer;

            if (finished)
            {
                // Elapsed time is relative to the shared RaceStartTime so all peers compute the same value.
                var finishTime = clientTime - race.RaceStartTime;
                race.RecordFinish(playerId, finishTime);

                if (localPlayer && localPlayer.GetZDOID() == playerId)
                    localPlayer.Message(MessageHud.MessageType.Center,
                        $"P{contestant.Position}! Time: {contestant.FinishTime:F1}s");
                else if (localPlayer && race.IsRegistered(localPlayer.GetZDOID()))
                    localPlayer.Message(MessageHud.MessageType.Center,
                        $"{contestant.PlayerName} finished P{contestant.Position}!");

                if (race.State == RaceState.Finished)
                    if (localPlayer && race.IsRegistered(localPlayer.GetZDOID()))
                        localPlayer.Message(MessageHud.MessageType.Center,
                            "Race finished!\n" + race.GetResultsText());
            }
            else
            {
                if (localPlayer && localPlayer.GetZDOID() == playerId)
                    localPlayer.Message(MessageHud.MessageType.Center,
                        $"Lap {contestant.CurrentLap}/{race.TotalLaps}");
            }
        }

        /// <summary>
        /// Runs on all peers. Marks the contestant as DNF and notifies all registered
        /// contestants. If this was the last unfinished contestant, displays the full
        /// results to all.
        /// </summary>
        private static void RPC_Dnf(long sender, string raceId, ZDOID playerId)
        {
            var race = GetRace(raceId);
            if (race == null) return;
            var contestant = race.GetContestant(playerId);
            if (contestant == null) return;
            race.RecordDnf(playerId);

            var localPlayer = Player.m_localPlayer;
            if (localPlayer && race.IsRegistered(localPlayer.GetZDOID()))
                localPlayer.Message(MessageHud.MessageType.Center,
                    $"{contestant.PlayerName} disconnected - DNF");

            if (race.State == RaceState.Finished)
                if (localPlayer && race.IsRegistered(localPlayer.GetZDOID()))
                    localPlayer.Message(MessageHud.MessageType.Center,
                        "Race finished!\n" + race.GetResultsText());
        }

        /// <summary>
        /// Runs on all peers. Clears all contestants and resets the race to Idle state,
        /// notifying the local player regardless of whether they were registered.
        /// </summary>
        private static void RPC_Reset(long sender, string raceId)
        {
            var race = GetRace(raceId);
            if (race == null) return;
            race.Reset();
            var localPlayer = Player.m_localPlayer;
            if (localPlayer)
                localPlayer.Message(MessageHud.MessageType.Center, $"{race.Name} reset");
        }
    }
}