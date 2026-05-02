namespace parallel_project
{
    public enum HeroKind
    {
        Warrior = 0,
        Mage = 1,
        Archer = 2
    }

    public class Hero
    {
        public HeroKind Kind { get; }
        public string Name { get; set; }
        public int Health { get; set; }
        public int MaxHealth { get; set; }
        public int Attack { get; set; }
        public int Speed { get; set; }

        public bool IsAlive
        {
            get { return Health > 0; }
        }

        public Hero(HeroKind kind, string name, int health, int attack, int speed)
        {
            Kind = kind;
            Name = name;
            Health = health;
            MaxHealth = health;
            Attack = attack;
            Speed = speed;
        }

        public string ToWire()
        {
            // Kind,Name,MaxHealth,Attack,Speed,Health
            return ((int)Kind).ToString() + "," + Name + "," + MaxHealth + "," + Attack + "," + Speed + "," + Health;
        }

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

        public void TakeDamage(int amount)
        {
            Health -= amount;
            if (Health < 0) Health = 0;
        }
    }
}
