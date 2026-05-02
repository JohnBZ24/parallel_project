namespace parallel_project
{
    //
    // Hero archetype id used for UI selection and combat routing.
    //
    public enum HeroKind
    {
        Warrior = 0,
        Mage = 1,
        Archer = 2
    }

    //
    // Represents a single combat unit with stats and current health.
    //
    public class Hero
    {
        public HeroKind Kind { get; }
        public string Name { get; set; }
        public int Health { get; set; }
        public int MaxHealth { get; set; }
        public int Attack { get; set; }
        public int Speed { get; set; }

        //
        // True when the hero still has health remaining.
        //
        public bool IsAlive
        {
            get { return Health > 0; }
        }

        //
        // Creates a new hero with the given stats.
        //
        // @param kind: Archetype id.
        // @param name: Display name.
        // @param health: Starting HP (also sets MaxHealth).
        // @param attack: Damage dealt per attack.
        // @param speed: Speed stat (mostly informational in this project).
        //
        public Hero(HeroKind kind, string name, int health, int attack, int speed)
        {
            Kind = kind;
            Name = name;
            Health = health;
            MaxHealth = health;
            Attack = attack;
            Speed = speed;
        }

        //
        // Serializes this hero to a compact string for sending over the wire.
        //
        // @returns: "Kind,Name,MaxHealth,Attack,Speed,Health"
        //
        public string ToWire()
        {
            // Kind,Name,MaxHealth,Attack,Speed,Health
            return ((int)Kind).ToString() + "," + Name + "," + MaxHealth + "," + Attack + "," + Speed + "," + Health;
        }

        //
        // Parses a hero from its wire (comma-separated) representation.
        //
        // @param s: Wire string created by ToWire().
        // @returns: New Hero with parsed stats and current HP.
        // @throws: FormatException if the payload doesn't have 6 fields.
        //
        public static Hero FromWire(string s)
        {
            string[] parts = s.Split(',');
            if (parts.Length < 6)
                throw new FormatException("Invalid hero wire format");

            HeroKind kind = (HeroKind)int.Parse(parts[0]);
            string name = parts[1];
            int maxHp = int.Parse(parts[2]);
            int atk = int.Parse(parts[3]);
            int spd = int.Parse(parts[4]);
            int hp = int.Parse(parts[5]);

            Hero h = new Hero(kind, name, maxHp, atk, spd);
            h.MaxHealth = maxHp;
            h.Health = hp;
            return h;
        }

        //
        // Applies damage to this hero.
        //
        // @param amount: Damage to subtract from Health.
        // @notes: Health is clamped at 0.
        //
        public void TakeDamage(int amount)
        {
            Health -= amount;
            if (Health < 0) Health = 0;
        }
    }
}
