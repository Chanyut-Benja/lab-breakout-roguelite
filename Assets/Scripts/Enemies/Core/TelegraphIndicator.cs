using UnityEngine;

namespace Enemies.Core
{
    /// <summary>
    /// Utility component for rendering directional line and circular AoE telegraph warnings.
    /// Uses LineRenderer so it works dynamically out of the box with zero asset dependencies.
    /// </summary>
    public class TelegraphIndicator : MonoBehaviour
    {
        private LineRenderer lineRenderer;
        private static Material defaultLineMaterial;

        private void Awake()
        {
            SetupLineRenderer();
        }

        private void SetupLineRenderer()
        {
            lineRenderer = GetComponent<LineRenderer>();
            if (lineRenderer == null)
            {
                lineRenderer = gameObject.AddComponent<LineRenderer>();
            }

            if (defaultLineMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (shader != null)
                {
                    defaultLineMaterial = new Material(shader);
                }
            }

            lineRenderer.material = defaultLineMaterial;
            lineRenderer.useWorldSpace = true;
            lineRenderer.startWidth = 0.15f;
            lineRenderer.endWidth = 0.15f;
            lineRenderer.startColor = new Color(1f, 0.2f, 0.2f, 0.6f);
            lineRenderer.endColor = new Color(1f, 0.2f, 0.2f, 0.2f);
            lineRenderer.enabled = false;
        }

        /// <summary>
        /// Displays a directional line telegraph from start position toward direction for given distance.
        /// </summary>
        public void ShowLine(Vector3 start, Vector2 direction, float length, float width = 0.2f)
        {
            if (lineRenderer == null) SetupLineRenderer();

            lineRenderer.enabled = true;
            lineRenderer.loop = false;
            lineRenderer.positionCount = 2;
            lineRenderer.startWidth = width;
            lineRenderer.endWidth = width;
            lineRenderer.startColor = new Color(1f, 0.15f, 0.15f, 0.8f);
            lineRenderer.endColor = new Color(1f, 0.3f, 0.1f, 0.3f);

            Vector3 end = start + (Vector3)(direction.normalized * length);
            lineRenderer.SetPosition(0, start);
            lineRenderer.SetPosition(1, end);
        }

        /// <summary>
        /// Displays a circular AoE warning telegraph on the ground.
        /// </summary>
        public void ShowCircle(Vector3 center, float radius, int segments = 24, float width = 0.08f)
        {
            if (lineRenderer == null) SetupLineRenderer();

            lineRenderer.enabled = true;
            lineRenderer.loop = true;
            lineRenderer.positionCount = segments;
            lineRenderer.startWidth = width;
            lineRenderer.endWidth = width;
            lineRenderer.startColor = new Color(1f, 0.1f, 0.1f, 0.75f);
            lineRenderer.endColor = new Color(1f, 0.1f, 0.1f, 0.75f);

            float deltaTheta = (2f * Mathf.PI) / segments;
            float theta = 0f;

            for (int i = 0; i < segments; i++)
            {
                float x = radius * Mathf.Cos(theta);
                float y = radius * Mathf.Sin(theta);
                lineRenderer.SetPosition(i, center + new Vector3(x, y, 0f));
                theta += deltaTheta;
            }
        }

        /// <summary>
        /// Hides the telegraph indicator.
        /// </summary>
        public void Hide()
        {
            if (lineRenderer != null)
            {
                lineRenderer.enabled = false;
            }
        }

        private void OnDisable()
        {
            Hide();
        }
    }
}
