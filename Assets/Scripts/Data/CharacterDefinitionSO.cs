using UnityEngine;

[CreateAssetMenu(fileName = "New Character", menuName = "Custom/Character Definition")]
public class CharacterDefinitionSO : ScriptableObject
{
    public string characterName;
    [TextArea(3, 5)]
    public string description;
    public Sprite icon;
    public GameObject characterPrefab;
}
