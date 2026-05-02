using System.Collections.Generic;

namespace parallel_project
{
    public class Player
    {
        public string Name { get; set; }
        public string Slot { get; set; } // "P1" or "P2"
        public List<Hero> Heroes { get; set; }

        /// <summary>
        /// Creates a player container (name + slot) and an empty hero list.
        /// </summary>
        /// <param name="name">Display name for the player.</param>
        /// <param name="slot">Network/game slot identifier (typically "P1" or "P2").</param>
        public Player(string name, string slot = "")
        {
            Name = name;
            Slot = slot;
            Heroes = new List<Hero>();
        }

        /// <summary>
        /// Returns the first living hero from this player's hero list.
        /// </summary>
        /// <remarks>
        /// Logic: Scans heroes in order and returns the first with Health &gt; 0.
        /// </remarks>
        public Hero? GetFirstAliveHero()
        {
            for (int i = 0; i < Heroes.Count; i++)
            {
                if (Heroes[i] != null && Heroes[i].IsAlive)
                    return Heroes[i];
            }
            return null;
        }

        /// <summary>
        /// Checks whether the player has at least one living hero.
        /// </summary>
        /// <remarks>
        /// Logic: Stops early as soon as a living hero is found.
        /// </remarks>
        public bool HasAliveHeroes()
        {
            for (int i = 0; i < Heroes.Count; i++)
            {
                if (Heroes[i] != null && Heroes[i].IsAlive)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Builds and returns a list containing only living heroes.
        /// </summary>
        /// <returns>A new list of heroes whose Health is greater than 0.</returns>
        public List<Hero> GetAliveHeroes()
        {
            List<Hero> alive = new List<Hero>();

            for (int i = 0; i < Heroes.Count; i++)
            {
                if (Heroes[i] != null && Heroes[i].IsAlive)
                    alive.Add(Heroes[i]);
            }

            return alive;
        }
    }
}
