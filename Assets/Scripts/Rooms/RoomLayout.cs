using UnityEngine;

/// <summary>
/// Sits on the root of every room prefab (Assets/Resources/RoomPrefabs/Room_01 ... Room_18).
/// It only points at the useful parts of the prefab so the game can find them. Open a room prefab in Unity
/// to edit it: move the player start / door slots, change the floor picture, add props under "Decor",
/// add solid blocks (with a Collider2D) under "Obstacles", change the light colour, the room size, ...
/// </summary>
public class RoomLayout : MonoBehaviour
{
    [Header("Size (the scene walls are re-shaped to this)")]
    public Vector2 size = new Vector2(48f, 32f);
    public float wallThickness = 2f;

    [Header("Places")]
    public Transform playerSpawn;
    public Transform[] doorSlots = new Transform[2];
    public RoomDoorway[] doorways = new RoomDoorway[2];

    [Header("Look")]
    public SpriteRenderer floor;
    public SpriteRenderer lightOverlay;
    [Tooltip("0 none, 1 soft flicker, 2 rhythm, 3 red pulse, 4 lightning, 5 mist, 6 glitch")]
    public int fxMode;
    public Color accent = Color.white;
}
