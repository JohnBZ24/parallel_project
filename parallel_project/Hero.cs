namespace parallel_project
{
    /// <summary>
    /// Identifies a hero archetype used for UI selection and combat rules.
    /// </summary>
    public enum HeroKind
    {
        Warrior = 0,
        Mage = 1,
        Archer = 2
    }

    /// <summary>
    /// Represents a single combat unit with stats and current health.
    /// </summary>
    public class Hero
    {
        public HeroKind Kind { get; }
        public string Name { get; set; }
        public int Health { get; set; }
        public int MaxHealth { get; set; }
        public int Attack { get; set; }
        public int Speed { get; set; }

        /// <summary>
        /// True when the hero still has health remaining.
        /// </summary>
        public bool IsAlive
        {
            get { return Health > 0; }
        }

        /// <summary>
        /// Creates a new hero with the given stats.
        /// </summary>
        /// <param name="kind">Archetype identifier.</param>
        /// <param name="name">Display name.</param>
        /// <param name="health">Starting (and initial max) HP.</param>
        /// <param name="attack">Damage dealt per attack.</param>
        /// <param name="speed">Speed stat (currently informational).</param>
        public Hero(HeroKind kind, string name, int health, int attack, int speed)
        {
            Kind = kind;
            Name = name;
            Health = health;
            MaxHealth = health;
            Attack = attack;
            Speed = speed;
        }

        /// <summary>
        /// Serializes this hero to a compact string for sending over the wire.
        /// </summary>
        /// <remarks>
        /// Logic: Uses comma-separated fields in a fixed order.
        /// Format: Kind,Name,MaxHealth,Attack,Speed,Health
        /// </remarks>
        public string ToWire()
        {
            // Kind,Name,MaxHealth,Attack,Speed,Health
            return ((int)Kind).ToString() + "," + Name + "," + MaxHealth + "," + Attack + "," + Speed + "," + Health;
        }

        /// <summary>
        /// Parses a hero from its wire (comma-separated) representation.
        /// </summary>
        /// <param name="s">Wire string created by <see cref="ToWire"/>.</param>
        /// <returns>A new <see cref="Hero"/> instance with the parsed stats and current HP.</returns>
        /// <exception cref="FormatException">Thrown when the wire payload is missing required fields.</exception>
        /// <remarks>
        /// Logic: Parses integers and reconstructs the hero; current HP is applied after construction.
        /// </remarks>
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

        /// <summary>
        /// Reduces current health by the specified amount (clamped at 0).
        /// </summary>
        /// <param name="amount">Damage amount to subtract from <see cref="Health"/>.</param>
        public void TakeDamage(int amount)
        {
            Health -= amount;
            if (Health < 0) Health = 0;
        }
    }
}
