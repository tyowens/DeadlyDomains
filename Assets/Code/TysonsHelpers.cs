#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

public static class TysonsHelpers
{
    public static T? FirstOrNull<T>(this IEnumerable<T> source)
    where T : struct
    => source.Cast<T?>().FirstOrDefault();

    public static void ForEach<T>(this IEnumerable<T> source, Action<T> action)
    {
        foreach (T element in source)
        {
            action(element);
        }
    }

    public static WeaponNetworkStruct SerializeWeapon(Weapon? weapon)
    {
        if (weapon == null)
        {
            // Use -1 range to indicate a null weapon
            return new WeaponNetworkStruct
            {
                Range = -1
            };
        }

        return new WeaponNetworkStruct
        {
            Id = weapon.ItemId,
            Name = weapon.Name,
            Type = weapon.Type,
            MinDamage = weapon.MinDamage,
            MaxDamage = weapon.MaxDamage,
            FireRate = weapon.FireRate,
            Speed = weapon.Speed,
            Range = weapon.Range,
            ShotType = weapon.ShotType
        };
    }

    public static Weapon? DeserializeWeapon(WeaponNetworkStruct weaponNetworkStruct)
    {
        // Check for -1 range to determine if this is a null weapon
        if (weaponNetworkStruct.Range == -1)
        {
            return null;
        }

        return new Weapon()
        {
            ItemId = weaponNetworkStruct.Id,
            Name = weaponNetworkStruct.Name.ToString(),
            Type = weaponNetworkStruct.Type.ToString(),
            MinDamage = weaponNetworkStruct.MinDamage,
            MaxDamage = weaponNetworkStruct.MaxDamage,
            FireRate = weaponNetworkStruct.FireRate,
            Speed = weaponNetworkStruct.Speed,
            Range = weaponNetworkStruct.Range,
            ShotType = weaponNetworkStruct.ShotType
        };
    }
}