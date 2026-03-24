# SuperVikingKart
SuperVikingKart is a Valheim multiplayer mod that adds mountable karts, collectible effect blocks, and a full race management system. Gather a friend, build a kart, and see who pulls the fastest lap around your custom-built track. Enable PvP and fire hazards for even more fun :)

## Installation
It is recommended to use a mod manager to install SuperVikingKart and all of its dependencies.

For a manual install, load the following required mods according to their respective instructions:
- [BepInExPack for Valheim](https://valheim.thunderstore.io/package/denikson/BepInExPack_Valheim)
- [Jotunn, the Valheim Library](https://valheim.thunderstore.io/package/ValheimModding/Jotunn)

Then extract the SuperVikingKart archive into `<Valheim>\BepInEx\plugins\SuperVikingKart`.

## Karts

![Karts Image](https://raw.githubusercontent.com/sirskunkalot/SuperVikingKart/refs/heads/master/resources/karts.png)

Adds a buildable **Super Viking Kart** piece to the Hammer under the Misc category.

One player mounts the kart by interacting with it. Another player pulls it by attaching as they would a regular cart. The rider can dismount at any time by interacting again or by jumping.

The rider cannot damage their own kart with melee attacks. The kart's colliders are also temporarily disabled during swings so the rider can still hit targets through it. Hit your fellow contestants and karts while they are near!

To change the color of Super Viking Kart use Alt-interact while aiming at the mount point of the cart to open a color picker. Drag the sliders for a live preview - the chosen color is broadcast to all clients and saved to the world.

Destroyed karts respawn after a configurable delay (default: 10 seconds) with a floating countdown at the destruction site. Karts removed with the Hammer do not respawn.

## Buff Blocks

![Buff Blocks Image](https://raw.githubusercontent.com/sirskunkalot/SuperVikingKart/refs/heads/master/resources/buffblocks.png)

Three types of collectible blocks that apply effects when a kart drives through them. The block hides itself and reappears after a configurable delay (default: 10 seconds).

- **Buff Block** - Applies a random beneficial effect.
- **Debuff Block** - Applies a random negative effect.
- **Mystery Block** - Applies a random effect of either type.

### Puller Buffs

| Effect | Description |
|---|---|
| Speed Boost | Significantly increases movement speed for a short duration. |
| Stamina Regen | Regenerates stamina over time for 10 seconds. |
| Stamina Burst | Instantly refills the puller's stamina bar. |

### Rider Buffs

| Effect | Description |
|---|---|
| Shield | Grants resistance to blunt, slash, and pierce damage for 15 seconds. |
| Health Regen | Regenerates health over time for 10 seconds. |
| Health Burst | Instantly fully heals the rider. |
| Ooze Bombs | Adds 5 ooze bombs to inventory. |
| Bile Bombs | Adds 2 bile bombs to inventory. |
| Smoke Bombs | Adds 5 smoke bombs to inventory. |
| Fire Arrows | Adds 20 fire arrows to inventory. |
| Harpoon | Adds a chitin spear to inventory. Will not add a second if one is already carried. |
| Berserk | Massively increases damage output for 10 seconds. |

### Shared Buffs

| Effect | Description |
|---|---|
| Living Dead | Prevents death once for both players. A fatal blow is cancelled and the player is left at 1 HP. Expires after use or after 20 seconds. |

### Puller Debuffs

| Effect | Description |
|---|---|
| Frost | Reduces movement speed by 50% for 8 seconds. |
| Tarred | Reduces movement speed by 70% for 8 seconds. |
| Bouncy | Applies an upward force to the kart, launching it into the air. |

### Rider Debuffs

| Effect | Description |
|---|---|
| Poison | Deals a small amount of poison damage every second for 10 seconds. |
| Burning | Deals moderate fire damage every second for 3 seconds. |
| Stagger | Immediately staggers the rider. |
| Disarm | Drops the rider's currently equipped weapons on the ground. |

### Shared Debuffs

| Effect | Description |
|---|---|
| Wet | Applies the vanilla Wet status effect to both players. |
| Shock | Deals lightning damage every second and reduces movement speed by 30% for 5 seconds. Applies to both players. |
| Blind | Covers the screen with a dark overlay for 5 seconds. Applies to both players. |

## Race System

### Race Board

![Race Board Image](https://raw.githubusercontent.com/sirskunkalot/SuperVikingKart/refs/heads/master/resources/raceboard.png)

Build a **Race Board** from the Hammer under the Misc category to configure and manage a race. The board displays live race status including registered players, lap progress, finish times, and final results.

Interact with the **Admin** button to open the configuration panel and set the Race ID, display name, and lap count.

Any player can use the **Register** button to sign up or leave. Any player can press **Start** once at least one contestant is registered, or **Reset** to return the race to idle.

### Race Line

![Race Line Image](https://raw.githubusercontent.com/sirskunkalot/SuperVikingKart/refs/heads/master/resources/raceline.png)

Build a **Race Line** from the Hammer under the Misc category to place start, finish, or combined start/finish lines. An arrow on the ground indicates the valid crossing direction - the line only registers crossings made in that direction.

Admins can interact with the Race Line to set its Race ID and role: **Start**, **Finish**, or **StartFinish**.

When a kart crosses a Start or StartFinish line for the first time, the kart is registered as started. Subsequent crossings of the Finish or StartFinish line record a lap. A 3-second cooldown per player prevents the same crossing from counting twice.

### Races
When a race starts, a 3-second countdown is broadcast to all registered contestants as a centre-screen message, followed by "GO!". Lap progress and finish times are tracked on the Race Board in real time. Finishing positions use dense ranking - players with the same finish time share a position so a puller/rider group is always recorded together.

Players who disconnect mid-race are automatically assigned a DNF. Players who leave voluntarily during a race are also assigned a DNF. Full results are shown to all remaining contestants once everyone has finished or received a DNF.

## Building a Race Track

![Race Track Image](https://raw.githubusercontent.com/sirskunkalot/SuperVikingKart/refs/heads/master/resources/racetrack.png)

All race pieces are linked by a shared **Race ID** - a short text string you choose. Every piece with the same Race ID is part of the same race.

### 1. Choose a Race ID
Pick a short, unique identifier. For this example: `meadows_gp`.

### 2. Place and configure the Race Board
Build a **Race Board** somewhere visible, such as near the start line. Open the **Admin** panel and set:
- **Race ID**: `meadows_gp`
- **Name**: `Meadows Grand Prix`
- **Laps**: `1`

The board will show "Waiting for players..." until someone registers.

You can place multiple boards using the same ID to display the stats and results.

### 3. Place the Start/Finish line
Build a **Race Line** and place it across the track. Make sure the arrow points in the direction of travel. Set:
- **Race ID**: `meadows_gp`
- **Role**: `StartFinish`

The first crossing records the start. Every subsequent crossing records a completed lap. For a one-lap race the second crossing finishes the race for that contestant.

### 4. Splitting start and finish (optional)
For tracks with separate start and finish points, place two Race Lines with the same Race ID:
- Beginning of track: **Role** `Start`
- End of track: **Role** `Finish`

The kart must cross the Start line before any Finish crossings are counted.

### 5. Multi-lap races
Set the **Laps** field on the Race Board to the number of finish line crossings required. The board shows each contestant's current lap and updates in real time.

### 6. Register and start
Players interact with the **Register** button to sign up, then any player presses **Start** when everyone is ready.

### 7. Verify your setup
If something isn't working, these commands can help:
- `svk_race list` - confirm the race exists and is in the Idle state
- `svk_race_admin state meadows_gp` - show full race state including all contestants
- `svk_race_admin forcestart meadows_gp` - start the race immediately without the countdown
- `svk_race_admin lap meadows_gp` - simulate a lap crossing to verify finish line logic
- `svk_race_admin forcereset meadows_gp` - reset the race back to Idle at any time

## Console Commands

### Player Commands

| Command | Description |
|---|---|
| `svk_race list` | List all active races and their states. |
| `svk_race register <raceId>` | Register the local player for a race. |
| `svk_race leave <raceId>` | Leave a race. Counts as a DNF if already underway. |
| `svk_race start <raceId>` | Start the countdown for a race. |
| `svk_race reset <raceId>` | Reset a race back to idle. |
| `svk_race results <raceId>` | Print the current results for a race. |

### Admin Commands
All admin commands require devcommands.

| Command | Description |
|---|---|
| `svk_buff list` | Print all available effects with their targets and types. |
| `svk_buff <name>` | Force-apply a specific buff or debuff to the local player. |
| `svk_race_admin create <raceId> [laps] [name]` | Create a new race. |
| `svk_race_admin remove <raceId>` | Remove a race entirely. |
| `svk_race_admin addplayer <raceId> <playerName>` | Add a connected player to a race by name. |
| `svk_race_admin setname <raceId> <name>` | Rename a race. |
| `svk_race_admin setlaps <raceId> <count>` | Set the lap count for a race. |
| `svk_race_admin forcestart <raceId>` | Start a race immediately regardless of state. |
| `svk_race_admin forcereset <raceId>` | Reset a race regardless of state. |
| `svk_race_admin lap <raceId> [playerName]` | Simulate a lap completion for a player. |
| `svk_race_admin finish <raceId> [playerName]` | Simulate finishing all remaining laps for a player. |
| `svk_race_admin state <raceId>` | Print the detailed state of a race including all contestants. |

## Configuration
Configuration is found in `<Valheim>\BepInEx\configs\de.sirskunkalot.SuperVikingKart.cfg` after starting the game once with the mod installed.

| Key | Description | Range | Default |
|---|---|---|---|
| CartRespawnTime | Time in seconds before a destroyed kart respawns. Server synced. | 2-20 | 10 |
| BuffBlockRespawnTime | Time in seconds before a collected buff block reappears. Server synced. | 2-20 | 10 |
| Debug | Enable verbose debug logging. | - | false |

## Changelog
### v0.0.1
- Initial release

## Credits
Mod created by [Jules](https://github.com/sirskunkalot).

Made with Löve and [Jötunn](https://github.com/Valheim-Modding/Jotunn).

Source available on GitHub: [https://github.com/sirskunkalot/SuperVikingKart](https://github.com/sirskunkalot/SuperVikingKart). All contributions welcome!