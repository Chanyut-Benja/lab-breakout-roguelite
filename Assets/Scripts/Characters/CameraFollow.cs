using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0, 0, -10);

    [SerializeField] private Transform mapBoundaries;
    [SerializeField] private bool clampToMap = true;

    private Camera followCamera;
    private Transform cachedMapRoot;
    private BoxCollider2D[] mapWalls;

    private void Awake()
    {
        followCamera = GetComponent<Camera>();
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player == null) return;
            target = player.transform;
        }

        Vector3 position = target.position + offset;
        if (clampToMap && followCamera.orthographic && TryGetMapBounds(out Bounds bounds))
        {
            float halfHeight = followCamera.orthographicSize;
            float halfWidth = halfHeight * followCamera.aspect;
            position.x = ClampAxis(position.x, bounds.min.x, bounds.max.x, halfWidth);
            position.y = ClampAxis(position.y, bounds.min.y, bounds.max.y, halfHeight);
        }

        // Only the camera moves. The floor remains an independent scene object.
        transform.position = position;
    }

    private bool TryGetMapBounds(out Bounds bounds)
    {
        bounds = new Bounds();
        if (mapBoundaries == null)
        {
            GameObject map = GameObject.Find("MapBoundaries");
            if (map == null) return false;
            mapBoundaries = map.transform;
        }

        if (cachedMapRoot != mapBoundaries || mapWalls == null)
        {
            cachedMapRoot = mapBoundaries;
            mapWalls = mapBoundaries.GetComponentsInChildren<BoxCollider2D>();
        }

        bool foundWall = false;
        foreach (BoxCollider2D wall in mapWalls)
        {
            if (wall == null || !wall.enabled || !wall.gameObject.activeInHierarchy) continue;
            if (!foundWall)
            {
                bounds = wall.bounds;
                foundWall = true;
            }
            else
            {
                bounds.Encapsulate(wall.bounds);
            }
        }
        return foundWall;
    }

    private static float ClampAxis(float desired, float minimum, float maximum, float halfView)
    {
        // If the whole map fits on this axis, keep that axis centered.
        if (maximum - minimum <= halfView * 2f) return (minimum + maximum) * 0.5f;
        return Mathf.Clamp(desired, minimum + halfView, maximum - halfView);
    }
}
