using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace parallel_project
{
    public class CombatEngine
    {
        public sealed class SpeedRaceResult
        {
            public string WinnerSlot { get; set; } = ""; // "P1" or "P2"
            public HeroKind WinnerKind { get; set; }
            public int WinnerDelayMs { get; set; }
        }

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

        // Runs a "speed race" among all living heroes using Task.Delay + Task.WhenAny.
        //
        // @param p1: Player state for slot "P1".
        // @param p2: Player state for slot "P2".
        // @param token: Cancels the race.
        // @returns: SpeedRaceResult describing which hero reacted first.
        //
        // @notes
        // - Each living hero gets a Task.Delay based on their Speed stat.
        // - Task.WhenAny selects the first completed task as the winner.
        // - Remaining race tasks are cancelled after a winner is chosen.
        //
        public async Task<SpeedRaceResult> RunSpeedRaceAsync(Player p1, Player p2, CancellationToken token)
        {
            List<(string slot, Hero hero)> contenders = new List<(string slot, Hero hero)>();

            for (int i = 0; i < p1.Heroes.Count; i++)
            {
                Hero h = p1.Heroes[i];
                if (h != null && h.IsAlive)
                    contenders.Add(("P1", h));
            }

            for (int i = 0; i < p2.Heroes.Count; i++)
            {
                Hero h = p2.Heroes[i];
                if (h != null && h.IsAlive)
                    contenders.Add(("P2", h));
            }

            if (contenders.Count == 0)
            {
                return new SpeedRaceResult
                {
                    WinnerSlot = "",
                    WinnerKind = HeroKind.Warrior,
                    WinnerDelayMs = 0
                };
            }

            using CancellationTokenSource raceCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            List<Task<SpeedRaceResult>> tasks = new List<Task<SpeedRaceResult>>();

            for (int i = 0; i < contenders.Count; i++)
            {
                (string slot, Hero hero) c = contenders[i];
                tasks.Add(RaceTaskAsync(c.slot, c.hero, raceCts.Token));
            }

            Task<SpeedRaceResult> finished = await Task.WhenAny(tasks);
            SpeedRaceResult winner = await finished;

            try { raceCts.Cancel(); } catch { }
            return winner;
        }

        // Resolves a full speed-race turn: pick first hero to react (Task.WhenAny), then apply an attack.
        //
        // @param p1: Player state for slot "P1".
        // @param p2: Player state for slot "P2".
        // @param token: Cancels the race/attack.
        // @returns: AttackResult for the winning hero's attack.
        //
        public async Task<AttackResult> ResolveSpeedRaceTurnAsync(Player p1, Player p2, CancellationToken token)
        {
            SpeedRaceResult race = await RunSpeedRaceAsync(p1, p2, token);
            if (string.IsNullOrWhiteSpace(race.WinnerSlot))
            {
                return new AttackResult
                {
                    AttackerSlot = "",
                    DefenderSlot = "",
                    GameOver = true,
                    GameWinnerSlot = ""
                };
            }

            string attackerSlot = race.WinnerSlot;
            string defenderSlot = attackerSlot == "P1" ? "P2" : "P1";

            Player attackerPlayer = attackerSlot == "P1" ? p1 : p2;
            Player defenderPlayer = defenderSlot == "P1" ? p1 : p2;

            Hero? attacker = FindHero(attackerPlayer, race.WinnerKind);
            if (attacker == null || !attacker.IsAlive)
                attacker = attackerPlayer.GetFirstAliveHero();

            Hero? defender = defenderPlayer.GetFirstAliveHero();
            HeroKind defenderKind = defender != null ? defender.Kind : HeroKind.Warrior;

            return await ResolveAttackAsync(p1, p2, attackerSlot, attacker?.Kind ?? race.WinnerKind, defenderSlot, defenderKind);
        }

        private static int ComputeSpeedDelayMs(int speed)
        {
            // Higher speed -> lower delay.
            // With default stats (6/7/9) this produces noticeable differences without being too slow.
            int delay = 1200 - (speed * 100);
            if (delay < 80) delay = 80;
            if (delay > 2000) delay = 2000;
            return delay;
        }

        private static async Task<SpeedRaceResult> RaceTaskAsync(string slot, Hero hero, CancellationToken token)
        {
            int delay = ComputeSpeedDelayMs(hero.Speed);
            await Task.Delay(delay, token);
            return new SpeedRaceResult
            {
                WinnerSlot = slot,
                WinnerKind = hero.Kind,
                WinnerDelayMs = delay
            };
        }

        //
        // Applies an attack from one player's chosen hero to the other player's chosen hero.
        //
        // @param p1: Player state for slot "P1".
        // @param p2: Player state for slot "P2".
        // @param attackerSlot: "P1" or "P2".
        // @param attackerKind: Requested attacker hero kind (falls back if dead/missing).
        // @param defenderSlot: "P1" or "P2".
        // @param defenderKind: Requested defender hero kind (falls back if dead/missing).
        // @returns: AttackResult describing damage/HP and game-over state.
        //
        // @notes
        // - Host-authoritative.
        // - If the chosen hero isn't alive, this picks the first living hero so the round can continue.
        //
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

        //
        // Finds a hero of the given kind within a player's roster.
        //
        // @param p: Player to search.
        // @param kind: Kind to match.
        // @returns: Matching hero, or null if not found.
        //
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
