using UnityEngine;

/// <summary>
/// Describes a battle arena look: which prefabs to tile/scatter and the lighting mood.
/// Loaded at runtime from Resources/Arenas by <see cref="ArenaBuilder"/>.
/// Prefab sizes and pivots are measured at runtime, so any modular kit can be plugged in.
/// </summary>
[CreateAssetMenu(fileName = "NewArenaTheme", menuName = "RPG/Arena Theme")]
public class ArenaThemeSO : ScriptableObject
{
    [Header("Structure")]
    [Tooltip("Floor tile variants. The first is used to measure the tile size; others are only used if the same size.")]
    public GameObject[] floorTiles;
    [Tooltip("Wall segment, tiled along the two back walls.")]
    public GameObject wall;
    [Tooltip("Pillar placed at wall corners and at intervals along the walls.")]
    public GameObject column;

    [Header("Decoration")]
    [Tooltip("Mesh shown on the wall for each torch. A real light is added by the builder.")]
    public GameObject torch;
    [Tooltip("Props scattered in the corners, away from the fighting area.")]
    public GameObject[] props;
    [Tooltip("Small props placed on top of tables / crates (candles, bottles, books).")]
    public GameObject[] smallProps;
    [Tooltip("Optional ambient particle effect (dust motes) centred over the arena.")]
    public GameObject ambientParticles;

    [Header("Layout (world units)")]
    [Tooltip("Half-size of the walkable floor along X and Z, measured from the arena centre.")]
    public Vector2 floorHalfExtents = new Vector2(9f, 9f);
    [Tooltip("The centre of the fighting area. Players stand around x=-2.5, enemies around x=+2.5.")]
    public Vector3 arenaCenter = new Vector3(0f, 0f, -1f);
    [Tooltip("Props and columns are never placed inside this half-size area around the arena centre.")]
    public Vector2 clearHalfExtents = new Vector2(5.5f, 5f);
    [Tooltip("Extra yaw (degrees) applied to wall pieces if the model's front faces the wrong way.")]
    public float wallYawOffset = 0f;
    [Tooltip("Height of torches above the floor, as a fraction of measured wall height.")]
    [Range(0.2f, 0.9f)] public float torchHeightFraction = 0.55f;
    [Tooltip("Distance between torches along the walls.")]
    public float torchSpacing = 5f;
    [Min(0)] public int propCount = 9;

    [Header("Legacy scene content")]
    [Tooltip("When true, the hand-placed 'Dungeon' root and the scene's own 'Directional Light' in BattleScene are deactivated at runtime so they don't clash with the generated arena. Nothing is deleted.")]
    public bool disableLegacySceneDecor = true;

    [Header("Mood")]
    public Color ambientColor = new Color(0.30f, 0.29f, 0.38f);
    public Color directionalColor = new Color(1f, 0.88f, 0.75f);
    [Range(0f, 2f)] public float directionalIntensity = 0.85f;
    public Color fogColor = new Color(0.07f, 0.06f, 0.09f);
    [Range(0f, 0.1f)] public float fogDensity = 0.018f;
    public Color backgroundColor = new Color(0.04f, 0.035f, 0.05f);
}
