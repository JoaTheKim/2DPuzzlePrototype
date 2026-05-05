using UnityEngine;

[CreateAssetMenu(fileName = "GameVisualConfiguration", menuName = "2D Puzzle Prototype/Game Visual Configuration")]
public sealed class GameVisualConfiguration : ScriptableObject
{
    [SerializeField] private Sprite playerSprite;
    [SerializeField] private Sprite ghostSprite;
    [SerializeField] private Sprite itemSprite;

    public Sprite PlayerSprite => playerSprite;
    public Sprite GhostSprite => ghostSprite;
    public Sprite ItemSprite => itemSprite;
}
