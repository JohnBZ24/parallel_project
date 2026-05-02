using System.Collections.Generic;

namespace parallel_project
{
    public class Player
    {
        public string Name { get; set; }
        public string Slot { get; set; } // "P1" or "P2"
        public List<Hero> Heroes { get; set; }

        public Player(string name, string slot = "")
        {
            Name = name;
            Slot = slot;
            Heroes = new List<Hero>();
        }

        public Hero? GetFirstAliveHero()
        {
            for (int i = 0; i < Heroes.Count; i++)
            {
                if (Heroes[i] != null && Heroes[i].IsAlive)
                    return Heroes[i];
            }
            return null;
        }

        public bool HasAliveHeroes()
        {
            for (int i = 0; i < Heroes.Count; i++)
            {
                if (Heroes[i] != null && Heroes[i].IsAlive)
                    return true;
            }

            return false;
        }

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
