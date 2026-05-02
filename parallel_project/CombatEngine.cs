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
