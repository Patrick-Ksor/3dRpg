using UnityEngine;

public enum ItemType { HealthPotion, ManaPotion }

[CreateAssetMenu(fileName = "NewItem", menuName = "RPG/Battle Item")]
public class BattleItem : ScriptableObject
{
    public string itemName;
    public ItemType itemType;
    public int potency = 30; // How much HP or MP to restore
    public Sprite icon;
}
