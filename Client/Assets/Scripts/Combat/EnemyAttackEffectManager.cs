using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static NetworkMessages;

/// <summary>
/// Manages per-shape visual effects when enemies attack players.
/// Subscribes to CombatAction events and dispatches shape-specific coroutines.
/// Each die shape (Sphere, D4, D6, D8, D10, D20) has a unique attack animation.
/// </summary>
public class EnemyAttackEffectManager : MonoBehaviour
{
    private EnemyNetworkManager _enemyNetworkManager;
    private readonly List<GameObject> _activeEffectObjects = new List<GameObject>();

    private void Start()
    {
        _enemyNetworkManager = FindObjectOfType<EnemyNetworkManager>();
        if (_enemyNetworkManager == null)
        {
            Debug.LogError("[EnemyAttackEffectManager] EnemyNetworkManager not found!");
            return;
        }

        NetworkManager.OnCombatAction += HandleCombatAction;
        Debug.Log("[EnemyAttackEffectManager] Subscribed to OnCombatAction");
    }

    private void OnDestroy()
    {
        NetworkManager.OnCombatAction -= HandleCombatAction;
    }

    private void HandleCombatAction(CombatActionMessage msg)
    {
        if (msg.AttackType != "EnemyAttack") return;

        EnemyBase enemy = _enemyNetworkManager.GetNetworkEnemy(msg.AttackerId);
        if (enemy == null || !enemy.gameObject.activeInHierarchy) return;

        var shape = EnemyShapeFactory.GetShapeForLevel(enemy.Level);
        float intensity = CalculateIntensity(enemy.Level, shape);

        // Flash the enemy's color as a universal attack indicator
        StartCoroutine(AttackFlash(enemy));

        // Dispatch shape-specific effect
        switch (shape)
        {
            case EnemyShapeFactory.DieShape.Sphere:
                StartCoroutine(SphereAttackEffect(enemy, msg, intensity));
                break;
            case EnemyShapeFactory.DieShape.D4:
                StartCoroutine(D4AttackEffect(enemy, msg, intensity));
                break;
            case EnemyShapeFactory.DieShape.D6:
                StartCoroutine(D6AttackEffect(enemy, intensity));
                break;
            case EnemyShapeFactory.DieShape.D8:
                StartCoroutine(D8AttackEffect(enemy, intensity));
                break;
            case EnemyShapeFactory.DieShape.D10:
                StartCoroutine(D10AttackEffect(enemy, intensity));
                break;
            case EnemyShapeFactory.DieShape.D20:
                StartCoroutine(D20AttackEffect(enemy, intensity));
                break;
        }
    }

    /// <summary>
    /// Calculate intensity (0-1) within the level tier for scaling effects.
    /// </summary>
    private float CalculateIntensity(int level, EnemyShapeFactory.DieShape shape)
    {
        switch (shape)
        {
            case EnemyShapeFactory.DieShape.Sphere: return Mathf.InverseLerp(1, 3, level);
            case EnemyShapeFactory.DieShape.D4:     return Mathf.InverseLerp(4, 5, level);
            case EnemyShapeFactory.DieShape.D6:     return Mathf.InverseLerp(6, 7, level);
            case EnemyShapeFactory.DieShape.D8:     return Mathf.InverseLerp(8, 9, level);
            case EnemyShapeFactory.DieShape.D10:    return Mathf.InverseLerp(10, 19, level);
            case EnemyShapeFactory.DieShape.D20:    return Mathf.InverseLerp(20, 30, level);
            default: return 0f;
        }
    }

    // ── Shared helpers ──────────────────────────────────────────────────

    private Material CreateGhostMaterial(Color color, float alpha)
    {
        var mat = new Material(Shader.Find("Sprites/Default"));
        color.a = alpha;
        mat.color = color;
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.renderQueue = 3000;
        return mat;
    }

    private GameObject CreateEffectObject(string name)
    {
        var obj = new GameObject($"EnemyAttackEffect_{name}");
        _activeEffectObjects.Add(obj);
        return obj;
    }

    private void DestroyEffectObject(GameObject obj)
    {
        _activeEffectObjects.Remove(obj);
        if (obj != null) Destroy(obj);
    }

    // ── Universal attack flash ──────────────────────────────────────────

    /// <summary>
    /// Brief orange flash on the enemy to indicate it's attacking.
    /// </summary>
    private IEnumerator AttackFlash(EnemyBase enemy)
    {
        if (enemy == null) yield break;

        Renderer renderer = enemy.GetComponent<Renderer>();
        if (renderer == null) yield break;

        Color originalColor = renderer.material.color;
        Color attackColor = new Color(1f, 0.5f, 0f, originalColor.a); // Orange flash

        renderer.material.color = attackColor;
        yield return new WaitForSeconds(0.15f);

        if (enemy != null && renderer != null && enemy.IsAlive())
            renderer.material.color = originalColor;
    }

    // ── Sphere (Levels 1-3): Ghost sphere lunges toward player ──────────

    private IEnumerator SphereAttackEffect(EnemyBase enemy, CombatActionMessage msg, float intensity)
    {
        if (enemy == null) yield break;

        float duration = Mathf.Lerp(0.3f, 0.45f, intensity);
        float lungeDistance = Mathf.Lerp(1.5f, 2.5f, intensity);

        // Create ghost sphere
        GameObject ghost = CreateEffectObject("SphereLunge");
        var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.transform.SetParent(ghost.transform);
        Destroy(sphere.GetComponent<Collider>());

        Renderer ghostRenderer = sphere.GetComponent<Renderer>();
        Color ghostColor = new Color(1f, 0.3f, 0.3f);
        ghostRenderer.material = CreateGhostMaterial(ghostColor, 0.6f);

        Vector3 startPos = enemy.transform.position;
        Vector3 targetDir = (msg.Position.ToVector3() - startPos).normalized;
        targetDir.y = 0;
        Vector3 endPos = startPos + targetDir * lungeDistance;

        ghost.transform.position = startPos;
        ghost.transform.localScale = enemy.transform.localScale * 0.8f;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (ghost == null) yield break;

            float t = elapsed / duration;
            // Lunge out and back arc
            float posT = Mathf.Sin(t * Mathf.PI);
            ghost.transform.position = Vector3.Lerp(startPos, endPos, posT);

            // Fade out in second half
            float alpha = t < 0.5f ? 0.6f : Mathf.Lerp(0.6f, 0f, (t - 0.5f) * 2f);
            ghostColor.a = alpha;
            ghostRenderer.material.color = ghostColor;

            elapsed += Time.deltaTime;
            yield return null;
        }

        DestroyEffectObject(ghost);
    }

    // ── D4 (Levels 4-5): Ghost tetrahedron spike thrust ─────────────────

    private IEnumerator D4AttackEffect(EnemyBase enemy, CombatActionMessage msg, float intensity)
    {
        if (enemy == null) yield break;

        float duration = Mathf.Lerp(0.3f, 0.4f, intensity);
        float thrustDistance = Mathf.Lerp(1.5f, 2.5f, intensity);

        // Create ghost tetrahedron
        GameObject ghost = CreateEffectObject("D4Thrust");
        var tetra = TetrahedronMesh.CreateTetrahedron();
        tetra.transform.SetParent(ghost.transform);
        Destroy(tetra.GetComponent<Collider>());

        Renderer ghostRenderer = tetra.GetComponent<Renderer>();
        Color ghostColor = new Color(0.8f, 1f, 0.3f);
        ghostRenderer.material = CreateGhostMaterial(ghostColor, 0.6f);

        Vector3 startPos = enemy.transform.position;
        Vector3 targetDir = (msg.Position.ToVector3() - startPos).normalized;
        targetDir.y = 0;
        if (targetDir.sqrMagnitude < 0.01f) targetDir = Vector3.forward;
        Vector3 endPos = startPos + targetDir * thrustDistance;

        ghost.transform.position = startPos;
        ghost.transform.localScale = Vector3.one * 0.8f;

        // Tip the tetrahedron forward so apex points at the player
        ghost.transform.rotation = Quaternion.LookRotation(targetDir) * Quaternion.Euler(90f, 0f, 0f);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (ghost == null) yield break;

            float t = elapsed / duration;
            // Quick thrust out, slower retract
            float posT = t < 0.3f
                ? Mathf.Lerp(0f, 1f, t / 0.3f)
                : Mathf.Lerp(1f, 0f, (t - 0.3f) / 0.7f);

            ghost.transform.position = Vector3.Lerp(startPos, endPos, posT);

            float alpha = Mathf.Lerp(0.6f, 0f, t);
            ghostColor.a = alpha;
            ghostRenderer.material.color = ghostColor;

            elapsed += Time.deltaTime;
            yield return null;
        }

        DestroyEffectObject(ghost);
    }

    // ── D6 (Levels 6-7): Scale up then slam down with squash ────────────

    private IEnumerator D6AttackEffect(EnemyBase enemy, float intensity)
    {
        if (enemy == null) yield break;

        float duration = Mathf.Lerp(0.35f, 0.5f, intensity);
        float scaleBoost = Mathf.Lerp(1.3f, 1.6f, intensity);
        float windupTime = duration * 0.4f;
        float slamTime = duration * 0.6f;

        Vector3 originalScale = enemy.transform.localScale;

        // Phase 1: Wind up — scale up uniformly
        float elapsed = 0f;
        while (elapsed < windupTime)
        {
            if (enemy == null) yield break;

            float t = elapsed / windupTime;
            float s = Mathf.Lerp(1f, scaleBoost, t);
            enemy.transform.localScale = originalScale * s;

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Phase 2: Slam — squash vertically, stretch horizontally
        elapsed = 0f;
        while (elapsed < slamTime)
        {
            if (enemy == null) yield break;

            float t = elapsed / slamTime;

            if (t < 0.3f)
            {
                // Quick squash
                float squashT = t / 0.3f;
                float yScale = Mathf.Lerp(scaleBoost, 0.6f, squashT);
                float xzScale = Mathf.Lerp(scaleBoost, scaleBoost * 1.2f, squashT);
                enemy.transform.localScale = new Vector3(
                    originalScale.x * xzScale,
                    originalScale.y * yScale,
                    originalScale.z * xzScale
                );
            }
            else
            {
                // Recover to original
                float recoverT = (t - 0.3f) / 0.7f;
                float yScale = Mathf.Lerp(0.6f, 1f, recoverT);
                float xzScale = Mathf.Lerp(scaleBoost * 1.2f, 1f, recoverT);
                enemy.transform.localScale = new Vector3(
                    originalScale.x * xzScale,
                    originalScale.y * yScale,
                    originalScale.z * xzScale
                );
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Ensure scale is restored
        if (enemy != null)
            enemy.transform.localScale = originalScale;
    }

    // ── D8 (Levels 8-9): Spinning LineRenderer ring ─────────────────────

    private IEnumerator D8AttackEffect(EnemyBase enemy, float intensity)
    {
        if (enemy == null) yield break;

        float duration = Mathf.Lerp(0.4f, 0.55f, intensity);
        float ringRadius = Mathf.Lerp(1.2f, 2f, intensity);
        int segments = 24;

        GameObject ringObj = CreateEffectObject("D8Ring");
        var lr = ringObj.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = segments + 1;
        lr.startWidth = 0.15f;
        lr.endWidth = 0.15f;
        lr.loop = false;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;

        Color ringColor = new Color(0.3f, 0.7f, 1f);
        lr.material = CreateGhostMaterial(ringColor, 0.8f);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (enemy == null || ringObj == null) { DestroyEffectObject(ringObj); yield break; }

            float t = elapsed / duration;
            Vector3 center = enemy.transform.position;
            float currentRadius = ringRadius * Mathf.Lerp(0.3f, 1f, Mathf.Min(t * 3f, 1f));
            float rotation = t * 720f; // 2 full spins

            // Build ring positions
            for (int i = 0; i <= segments; i++)
            {
                float angle = (i / (float)segments) * 360f + rotation;
                float rad = angle * Mathf.Deg2Rad;
                Vector3 pos = center + new Vector3(Mathf.Cos(rad) * currentRadius, 0.5f, Mathf.Sin(rad) * currentRadius);
                lr.SetPosition(i, pos);
            }

            // Fade out
            float alpha = Mathf.Lerp(0.8f, 0f, t);
            ringColor.a = alpha;
            lr.startColor = ringColor;
            lr.endColor = ringColor;

            elapsed += Time.deltaTime;
            yield return null;
        }

        DestroyEffectObject(ringObj);
    }

    // ── D10 (Levels 10-19): Expanding shockwave ring + enemy pulse ──────

    private IEnumerator D10AttackEffect(EnemyBase enemy, float intensity)
    {
        if (enemy == null) yield break;

        float duration = Mathf.Lerp(0.4f, 0.55f, intensity);
        float maxRadius = Mathf.Lerp(2f, 3.5f, intensity);

        // Create shockwave ring (flattened cylinder)
        GameObject ring = CreateEffectObject("D10Shockwave");
        var cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cylinder.transform.SetParent(ring.transform);
        Destroy(cylinder.GetComponent<Collider>());

        Renderer ringRenderer = cylinder.GetComponent<Renderer>();
        Color ringColor = new Color(1f, 0.4f, 0.8f);
        ringRenderer.material = CreateGhostMaterial(ringColor, 0.5f);

        Vector3 originalScale = enemy.transform.localScale;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (enemy == null) { DestroyEffectObject(ring); yield break; }
            if (ring == null) yield break;

            float t = elapsed / duration;

            // Expanding ring
            float radius = Mathf.Lerp(0.2f, maxRadius, t);
            ring.transform.position = enemy.transform.position;
            cylinder.transform.localScale = new Vector3(radius * 2f, 0.02f, radius * 2f);

            float alpha = Mathf.Lerp(0.5f, 0f, t);
            ringColor.a = alpha;
            ringRenderer.material.color = ringColor;

            // Enemy pulse (scale bounce)
            float pulse = 1f + 0.2f * Mathf.Sin(t * Mathf.PI * 2f) * (1f - t);
            enemy.transform.localScale = originalScale * pulse;

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Restore scale
        if (enemy != null)
            enemy.transform.localScale = originalScale;

        DestroyEffectObject(ring);
    }

    // ── D20 (Levels 20+): Multiple spikes burst outward ─────────────────

    private IEnumerator D20AttackEffect(EnemyBase enemy, float intensity)
    {
        if (enemy == null) yield break;

        float duration = Mathf.Lerp(0.4f, 0.6f, intensity);
        int spikeCount = Mathf.RoundToInt(Mathf.Lerp(6, 12, intensity));
        float spikeDistance = Mathf.Lerp(2f, 3.5f, intensity);

        Vector3 origin = enemy.transform.position;
        GameObject container = CreateEffectObject("D20Spikes");
        container.transform.position = origin;

        // Create spike primitives
        var spikes = new List<Transform>();
        var spikeRenderers = new List<Renderer>();
        Color spikeColor = new Color(1f, 0.2f, 0.2f);

        for (int i = 0; i < spikeCount; i++)
        {
            var spike = GameObject.CreatePrimitive(PrimitiveType.Cube);
            spike.transform.SetParent(container.transform);
            Destroy(spike.GetComponent<Collider>());

            spike.transform.localScale = new Vector3(0.15f, 0.15f, 0.6f);
            spike.transform.position = origin;

            // Distribute evenly around a circle
            float angle = (i / (float)spikeCount) * 360f;
            spike.transform.rotation = Quaternion.Euler(0f, angle, 0f);

            var r = spike.GetComponent<Renderer>();
            r.material = CreateGhostMaterial(spikeColor, 0.7f);
            spikeRenderers.Add(r);
            spikes.Add(spike.transform);
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (container == null) yield break;

            float t = elapsed / duration;

            // Burst outward
            float dist = spikeDistance * Mathf.Lerp(0f, 1f, Mathf.Sqrt(t));

            for (int i = 0; i < spikes.Count; i++)
            {
                if (spikes[i] == null) continue;

                float angle = (i / (float)spikeCount) * 360f;
                float rad = angle * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
                spikes[i].position = origin + dir * dist;

                // Fade out
                float alpha = Mathf.Lerp(0.7f, 0f, t);
                Color c = spikeColor;
                c.a = alpha;
                spikeRenderers[i].material.color = c;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        DestroyEffectObject(container);
    }

    // ── Cleanup ─────────────────────────────────────────────────────────

    /// <summary>
    /// Stop all running effects and destroy all effect objects.
    /// Called during level transitions via EnemyNetworkManager.CleanupCombatObjects.
    /// </summary>
    public void CleanupAllEffects()
    {
        StopAllCoroutines();

        for (int i = _activeEffectObjects.Count - 1; i >= 0; i--)
        {
            if (_activeEffectObjects[i] != null)
                Destroy(_activeEffectObjects[i]);
        }
        _activeEffectObjects.Clear();

        // Safety sweep for any orphaned effect objects
        foreach (var obj in FindObjectsOfType<GameObject>())
        {
            if (obj.name.StartsWith("EnemyAttackEffect_"))
                Destroy(obj);
        }

        Debug.Log("[EnemyAttackEffectManager] Cleaned up all attack effects");
    }
}
