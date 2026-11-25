using System.Collections.Generic;
using Enemies.Core;
using UnityEngine;

/// <summary>
/// A crescent of blade energy that flies straight and cuts through every enemy once.
/// Used by the evolved Nano-Blade (two waves, aim direction and the opposite one) and by the
/// Nano-Barrier Resonance (8 waves when the barrier breaks).
/// </summary>
public class BladeWave : MonoBehaviour
{
    private Vector2 dir = Vector2.right;
    private float damage, radius, speed, life, age;
    private bool forceCrit;
    private readonly HashSet<int> struck = new HashSet<int>();
    private readonly List<Collider2D> found = new List<Collider2D>();

    public static BladeWave Spawn(Vector2 position, Vector2 direction, float damage, float radius, float speed, float life, Color color, bool forceCrit)
    {
        GameObject go = new GameObject("BladeWave");
        go.transform.position = position;
        Vector2 d = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        go.transform.localScale = Vector3.one * Mathf.Max(0.5f, radius);

        // the sprite sits behind the hit centre, so the leading edge starts close to the player and flies out from there
        GameObject vis = new GameObject("Visual");
        vis.transform.SetParent(go.transform, false);
        vis.transform.localPosition = new Vector3(-0.55f, 0f, 0f);
        SpriteRenderer sr = vis.AddComponent<SpriteRenderer>();
        sr.sprite = WeaponVfx.BladeCrescent(130f);
        sr.color = color;
        sr.sortingOrder = 19;

        BladeWave w = go.AddComponent<BladeWave>();
        w.dir = d; w.damage = damage; w.radius = radius; w.speed = speed; w.life = life; w.forceCrit = forceCrit;
        return w;
    }

    private void Update()
    {
        age += Time.deltaTime;
        transform.position += (Vector3)(dir * speed * Time.deltaTime);

        int n = CombatUtil.QueryEnemies(transform.position, radius * 0.9f, found);
        for (int i = 0; i < n; i++)
        {
            EnemyBase eb = found[i].GetComponentInParent<EnemyBase>();
            if (eb == null) continue;
            GameObject root = eb.gameObject;
            if (!struck.Add(root.GetInstanceID())) continue;
            EvolvedWeaponBehaviors.DealEvolvedDamageToEnemy(root, damage, forceCrit);
        }

        if (age >= life) Destroy(gameObject);
    }
}
