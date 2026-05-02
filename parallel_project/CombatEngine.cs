using System;
using System.Threading.Tasks;

namespace parallel_project
{
    public class CombatEngine
    {
        public sealed class AttackResult
        {
            public string AttackerSlot { get; set; } = ""; // "P1" or "P2"
            public string DefenderSlot { get; set; } = ""; // "P1" or "P2"
            public HeroKind AttackerKind { get; set; }
            public HeroKind DefenderKind { get; set; }
            public int Damage { get; set; }
            public int DefenderHpAfter { get; set; }
            public bool DefenderDied { get; set; }
            public bool GameOver { get; set; }
            public string GameWinnerSlot { get; set; } = "";
        }

        /// <summary>
        /// Applies an attack from one player's chosen hero to the other player's chosen hero.
        /// </summary>
        /// <param name="p1">Player state for slot "P1".</param>
        /// <param name="p2">Player state for slot "P2".</param>
        /// <param name="attackerSlot">Slot of the attacking player ("P1" or "P2").</param>
        /// <param name="attackerKind">Requested attacking hero kind; falls back to first alive if invalid/dead.</param>
        /// <param name="defenderSlot">Slot of the defending player ("P1" or "P2").</param>
        /// <param name="defenderKind">Requested defending hero kind; falls back to first alive if invalid/dead.</param>
        /// <returns>
        /// A result object describing who attacked whom, damage, defender HP, and whether the game ended.
        /// </returns>
        /// <remarks>
        /// Logic: Host-authoritative resolution. If the selected attacker/defender isn't alive, it picks the
        /// first living hero on that side to ensure the round can progress.
        /// </remarks>
        public Task<AttackResult> ResolveAttackAsync(Player p1, Player p2, string attackerSlot, HeroKind attackerKind, string defenderSlot, HeroKind defenderKind)
        {
            // Host-authoritative: chosen attacker attacks chosen defender.
            return Task.Run(() =>
            {
                Player attackerPlayer = attackerSlot == "P1" ? p1 : p2;
                Player defenderPlayer = defenderSlot == "P1" ? p1 : p2;

                Hero? attacker = FindHero(attackerPlayer, attackerKind);
                if (attacker == null || !attacker.IsAlive)
                    attacker = attackerPlayer.GetFirstAliveHero();

                Hero? defender = FindHero(defenderPlayer, defenderKind);
                if (defender == null || !defender.IsAlive)
                    defender = defenderPlayer.GetFirstAliveHero();

                if (attacker == null || defender == null)
                {
                    return new AttackResult
                    {
                        AttackerSlot = attackerSlot,
                        DefenderSlot = defenderSlot,
                        GameOver = true,
                        GameWinnerSlot = attacker != null ? attackerSlot : (attackerSlot == "P1" ? "P2" : "P1")
                    };
                }

                defender.TakeDamage(attacker.Attack);
                bool died = !defender.IsAlive;
                bool gameOver = !defenderPlayer.HasAliveHeroes();

                return new AttackResult
                {
                    AttackerSlot = attackerSlot,
                    DefenderSlot = defenderSlot,
                    AttackerKind = attacker.Kind,
                    DefenderKind = defender.Kind,
                    Damage = attacker.Attack,
                    DefenderHpAfter = defender.Health,
                    DefenderDied = died,
                    GameOver = gameOver,
                    GameWinnerSlot = gameOver ? attackerSlot : ""
                };
            });
        }

        /// <summary>
        /// Finds a hero of the given kind within a player's roster.
        /// </summary>
        /// <param name="p">Player to search.</param>
        /// <param name="kind">Hero kind to match.</param>
        /// <returns>The matching hero instance, or null if not found.</returns>
        private static Hero? FindHero(Player p, HeroKind kind)
        {
            for (int i = 0; i < p.Heroes.Count; i++)
            {
                if (p.Heroes[i] != null && p.Heroes[i].Kind == kind)
                    return p.Heroes[i];
            }
            return null;
        }
    }
}
