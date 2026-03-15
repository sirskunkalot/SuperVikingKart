using System.Collections.Generic;
using Jotunn.Entities;
using Jotunn.Managers;

namespace SuperVikingKart
{
    internal static class Commands
    {
        public static void Register()
        {
            CommandManager.Instance.AddConsoleCommand(new ForceBuffCommand());
        }
    }

    internal class ForceBuffCommand : ConsoleCommand
    {
        public override string Name => "svk_buff";
        public override string Help => "Force a specific buff. Usage: svk_buff <name> or svk_buff list";
        public override bool IsCheat => true;

        public override List<string> CommandOptionList()
        {
            var options = new List<string> { "list" };
            foreach (var effect in BuffBlockComponent.AllEffects)
            {
                options.Add(effect.Name);
            }
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
                {
                    Console.instance.Print($"  {effect.Name} ({effect.StatusEffect}) - {effect.Target} {effect.Type}");
                }
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
}