using UnityEngine;

[CreateAssetMenu(fileName = "New Character", menuName = "Custom/Character Definition")]
public class CharacterDefinitionSO : ScriptableObject
{
    public string characterName;
    [Tooltip("Short archetype label shown on the hero select screen, e.g. TANK.")]
    public string role;
    [TextArea(3, 5)]
    public string description;
    public Sprite icon;
    public GameObject characterPrefab;
}
