using System;
using System.Runtime.InteropServices.WindowsRuntime;
using Fusion;

public class Weapon : Item
{
    private Random _random = new Random();

    public string Name;
    public string Type;
    public int MinDamage;
    public int MaxDamage;
    /// <summary>
    /// Fire Rate is in attacks per second; Fire Rate of 5 will shoot once every 0.2 seconds
    /// </summary>
    public float FireRate;
    public float Speed;
    public float Range;
    public ShotType ShotType;
    public void Attack(NetworkObject attackObject)
    {
        AttackProjectile attackProjectile = attackObject.GetComponent<AttackProjectile>();
        attackProjectile.Damage = _random.Next(MinDamage, MaxDamage + 1);
        attackProjectile.Range = Range;
        attackProjectile.Speed = Speed;
    }

    public static Weapon GenerateWeapon(float targetDps)
    {
        var random = new Random();
        var fireRate = (float)decimal.Round((decimal)(random.NextDouble() * 2.5f + 0.5f), 1);
        int meanDamage = (int)(targetDps / fireRate);
        int spreadInOneDirection = random.Next(1, 8);

        return new Weapon()
        {
            Name = "Random Sword",
            Type = "1H Sword",
            MinDamage = meanDamage - spreadInOneDirection,
            MaxDamage = meanDamage - spreadInOneDirection,
            FireRate = fireRate,
            Speed = random.Next(8, 16),
            Range = random.Next(2, 15),
            EquipmentType = EquipmentType.MAIN_HAND
        };
    }
}

public enum ShotType
{
    NONE = 0,
    STRAIGHT = 1
}

public struct WeaponNetworkStruct : INetworkStruct
{
    [Networked] public Guid Id { get; set; }
    [Networked] public NetworkString<_32> Name { get; set; }
    [Networked] public NetworkString<_32> Type { get; set; }
    [Networked] public int MinDamage { get; set; }
    [Networked] public int MaxDamage { get; set; }
    [Networked] public float FireRate { get; set; }
    [Networked] public float Speed { get; set; }
    [Networked] public float Range { get; set; }
    [Networked] public ShotType ShotType { get; set; }
}