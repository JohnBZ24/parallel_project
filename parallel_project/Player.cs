using System.Collections.Generic;

namespace parallel_project
{
    public class Player
    {
        public string Name { get; set; }
        public string Slot { get; set; } // "P1" or "P2"
        public List<Hero> Heroes { get; set; }

        //
        // Creates a player container (name + slot) with an empty hero list.
        //
        // @param name: Display name for the player.
        // @param slot: Slot id (usually "P1" or "P2").
        //
        public Player(string name, string slot = "")
        {
            Name = name;
            Slot = slot;
            Heroes = new List<Hero>();
        }

        //
        // Returns the first living hero in this player's roster.
        //
        // @returns: First hero with Health > 0, or null if all are dead/missing.
        //
        public Hero? GetFirstAliveHero()
        {
            for (int i = 0; i < Heroes.Count; i++)
            {
                if (Heroes[i] != null && Heroes[i].IsAlive)
                    return Heroes[i];
            }
            return null;
        }

        //
        // Checks whether the player has at least one living hero.
        //
        // @returns: true if any hero has Health > 0.
        //
        public bool HasAliveHeroes()
        {
            for (int i = 0; i < Heroes.Count; i++)
            {
                if (Heroes[i] != null && Heroes[i].IsAlive)
                    return true;
            }

            return false;
        }

        //
        // Builds a list containing only living heroes.
        //
        // @returns: New list of heroes whose Health > 0.
        //
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
