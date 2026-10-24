using System.Collections.Generic;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// Credit Pickup item dropped when enemies are defeated (based on DropRate).
    /// Magnets towards player based on PickupRadius, adds credits on collection,
    /// and supports vacuuming when a room is cleared.
    /// </summary>
    public class CreditPickup : MonoBehaviour
    {
        private static readonly List<CreditPickup> activeCredits = new List<CreditPickup>();

        [SerializeField] private int creditAmount = 1;
        [SerializeField] private float attractRadius = 4f;
        [SerializeField] private float moveSpeed = 8f;
        [SerializeField] private float vacuumMoveSpeed = 22f;

        private Transform player;
        private bool isBeingAttracted = false;
        private bool isVacuumed = false;
        private static Sprite creditSprite;

        public int CreditAmount => creditAmount;

        private void OnEnable()
        {
            if (!activeCredits.Contains(this))
            {
                activeCredits.Add(this);
            }
        }

        private void OnDisable()
        {
            activeCredits.Remove(this);
        }

        private void Start()
        {
            SpriteRenderer sr = GetComponent<SpriteRenderer>();
            if (sr != null && sr.sprite == null)
            {
                sr.sprite = GetCreditSprite();
                sr.color = new Color(1f, 0.85f, 0.2f, 1f); // Neon Gold
                sr.sortingOrder = -5;
            }
            FindPlayer();
        }

        private void FindPlayer()
        {
            if (player != null) return;
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
            {
                player = playerObj.transform;
            }
        }

        public void SetCreditAmount(int amount)
        {
            creditAmount = Mathf.Max(1, amount);
        }

        public void TriggerVacuum()
        {
            isBeingAttracted = true;
            isVacuumed = true;
        }

        public static void VacuumAllActiveCredits()
        {
            for (int i = activeCredits.Count - 1; i >= 0; i--)
            {
                if (activeCredits[i] != null && activeCredits[i].gameObject.activeInHierarchy)
                {
                    activeCredits[i].TriggerVacuum();
                }
            }
        }

        public static GameObject CreatePickup(Vector3 position, int amount = 1)
        {
            GameObject obj = new GameObject("CreditPickup");
            obj.transform.position = position;
            SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
            sr.sprite = GetCreditSprite();
            sr.color = new Color(1f, 0.85f, 0.2f, 1f);
            sr.sortingOrder = -5;

            CircleCollider2D col = obj.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.25f;

            CreditPickup pickup = obj.AddComponent<CreditPickup>();
            pickup.SetCreditAmount(amount);
            return obj;
        }

        public static Sprite GetCreditSprite()
        {
            if (creditSprite != null) return creditSprite;
            const int size = 32;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            Vector2 center = new Vector2(size * 0.5f - 0.5f, size * 0.5f - 0.5f);
            float rOuter = size * 0.42f;
            float rInner = size * 0.32f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    if (dist <= rInner)
                    {
                        tex.SetPixel(x, y, new Color(1f, 0.95f, 0.4f, 1f));
                    }
                    else if (dist <= rOuter)
                    {
                        tex.SetPixel(x, y, new Color(0.9f, 0.65f, 0.1f, 1f));
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }
            tex.Apply();
            creditSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size * 1.5f);
            return creditSprite;
        }

        private void Update()
        {
            if (player == null)
            {
                FindPlayer();
                if (player == null) return;
            }

            if (PlayerStats.Instance != null)
            {
                attractRadius = PlayerStats.Instance.PickupRadius;
            }

            float distance = Vector2.Distance(transform.position, player.position);
            if (distance <= attractRadius)
            {
                isBeingAttracted = true;
            }

            if (isBeingAttracted)
            {
                float currentPullSpeed = isVacuumed ? Mathf.Max(moveSpeed, vacuumMoveSpeed) : moveSpeed;
                transform.position = Vector2.MoveTowards(transform.position, player.position, currentPullSpeed * Time.deltaTime);

                if (distance < 0.35f)
                {
                    Collect();
                }
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Player"))
            {
                Collect();
            }
        }

        private void Collect()
        {
            if (CurrencyManager.Instance != null)
            {
                CurrencyManager.Instance.AddCredits(creditAmount);
            }
            else if (GameOverManager.Instance != null)
            {
                GameOverManager.Instance.AddRunCredits(creditAmount);
            }

            if (AudioManager.Instance != null && AudioManager.Instance.expSFX != null)
            {
                AudioManager.Instance.PlaySFX(AudioManager.Instance.expSFX);
            }

            Destroy(gameObject);
        }
    }
}

// Global scope alias for convenience
public class CreditPickup : Combat.CreditPickup { }
