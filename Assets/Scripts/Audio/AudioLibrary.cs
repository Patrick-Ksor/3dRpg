using UnityEngine;

/// <summary>
/// Every clip the game plays by name, in one asset (Resources/AudioLibrary) so builds include them.
/// Swap a clip here to change a sound everywhere; per-attack sounds stay on the AttackAction assets.
/// </summary>
[CreateAssetMenu(fileName = "AudioLibrary", menuName = "Custom/Audio Library")]
public class AudioLibrary : ScriptableObject
{
    [Header("Music")]
    public AudioClip menuMusic;
    [Tooltip("Picked by encounter tier (1-3). Add more to cycle through.")]
    public AudioClip[] battleMusic;
    public AudioClip bossMusic;

    [Header("UI")]
    public AudioClip uiHover;
    public AudioClip uiClick;
    public AudioClip uiConfirm;
    public AudioClip uiBack;
    public AudioClip uiDenied;
    public AudioClip pause;
    public AudioClip unpause;

    [Header("Battle")]
    public AudioClip encounterStart;
    public AudioClip miss;
    public AudioClip block;
    public AudioClip enemyDeath;
    public AudioClip heal;
    public AudioClip buff;
    public AudioClip debuff;
    public AudioClip itemUse;
    public AudioClip victory;
    public AudioClip defeat;
    public AudioClip footstep;
}
