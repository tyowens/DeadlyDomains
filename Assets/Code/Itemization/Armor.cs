using System;
using System.Runtime.InteropServices.WindowsRuntime;
using Fusion;

public class Armor : Item
{
    public Armor(EquipmentType equipmentType, string name, string type, int armorAmount)
    {
        EquipmentType = equipmentType;
        Name = name;
        Type = type;
        ArmorAmount = armorAmount;
    }

    public string Name;
    public string Type;
    public int ArmorAmount;
}