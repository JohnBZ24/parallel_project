using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace parallel_project
{
    public class ArenaLoader
    {
        /// <summary>
        /// Loads/builds the local player's team asynchronously (one task per hero) and waits for all to finish.
        /// </summary>
        /// <param name="localName">Display name for the local player.</param>
        /// <param name="localSlot">Slot identifier (typically "P1" or "P2").</param>
        /// <param name="log">Callback used to write status messages to the UI log.</param>
        /// <param name="token">Cancellation token to abort loading.</param>
        /// <returns>A <see cref="Player"/> with exactly three heroes assigned.</returns>
        /// <remarks>
        /// Logic: Starts three independent tasks (warrior/mage/archer) and coordinates completion using
        /// a <see cref="CountdownEvent"/>. If any hero task didn't populate its result (e.g., due to cancellation),
        /// a default hero instance is created as a fallback.
        /// </remarks>
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
