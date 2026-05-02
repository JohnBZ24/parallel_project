using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace parallel_project
{
    public class ArenaLoader
    {
        public async Task<Player> LoadLocalTeamAsync(string localName, string localSlot, System.Action<string> log, CancellationToken token)
        {
            Player local = new Player(localName, localSlot);

            Hero? warrior = null;
            Hero? mage = null;
            Hero? archer = null;

            CountdownEvent countdown = new CountdownEvent(3);

            _ = Task.Run(async () =>
            {
                try
                {
                    log("Loading " + localSlot + " Warrior...");
                    await Task.Delay(450, token);
                    warrior = new Hero(HeroKind.Warrior, "Warrior", 120, 14, 6);
                }
                finally
                {
                    countdown.Signal();
                    log("Activity 2: CountdownEvent.Signal() called. Remaining: " + countdown.CurrentCount);
                }
            }, token);

            _ = Task.Run(async () =>
            {
                try
                {
                    log("Loading " + localSlot + " Mage...");
                    await Task.Delay(650, token);
                    mage = new Hero(HeroKind.Mage, "Mage", 90, 18, 7);
                }
                finally
                {
                    countdown.Signal();
                    log("Activity 2: CountdownEvent.Signal() called. Remaining: " + countdown.CurrentCount);
                }
            }, token);

            _ = Task.Run(async () =>
            {
                try
                {
                    log("Loading " + localSlot + " Archer...");
                    await Task.Delay(550, token);
                    archer = new Hero(HeroKind.Archer, "Archer", 100, 16, 9);
                }
                finally
                {
                    countdown.Signal();
                    log("Activity 2: CountdownEvent.Signal() called. Remaining: " + countdown.CurrentCount);
                }
            }, token);

            await Task.Run(() => countdown.Wait(token), token);

            local.Heroes = new List<Hero>
            {
                warrior ?? new Hero(HeroKind.Warrior, "Warrior", 120, 14, 6),
                mage ?? new Hero(HeroKind.Mage, "Mage", 90, 18, 7),
                archer ?? new Hero(HeroKind.Archer, "Archer", 100, 16, 9),
            };

            log(localSlot + " heroes loaded.");
            return local;
        }
    }
}
