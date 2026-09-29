using System;
using System.Collections.Generic;
using HabitatShift.Core;
using UnityEngine;

namespace HabitatShift.Runtime
{
    // Presentation-only effect runner. It never writes ContinuousSession or RuntimeState.
    public sealed class GameplayFxController : MonoBehaviour
    {
        struct ActiveFx { public GameObject gameObject; public string key; public float releaseAt; }
        sealed class FxParts { public ParticleSystem[] particles; public ParticleSystemRenderer[] renderers; }
        sealed class CaptureGhost { public SpriteRenderer renderer; public Vector3 start, end; public Vector2 targetBoard; public Color color; public float startAt, baseScale; }

        readonly Dictionary<string, Queue<GameObject>> pools = new Dictionary<string, Queue<GameObject>>();
        readonly Dictionary<GameObject, FxParts> fxParts = new Dictionary<GameObject, FxParts>();
        readonly List<ActiveFx> active = new List<ActiveFx>();
        readonly List<CaptureGhost> ghosts = new List<CaptureGhost>();
        readonly Queue<CaptureGhost> ghostPool = new Queue<CaptureGhost>();
        readonly HashSet<string> seenEvents = new HashSet<string>();
        readonly Dictionary<string, float> blockedCooldown = new Dictionary<string, float>();
        readonly Dictionary<string, Sprite> sproutSprites = new Dictionary<string, Sprite>();
        Camera worldCamera;
        bool vfxEnabled = true;
        bool reducedMotion;
        float lastTrailAt;

        static readonly Dictionary<string, string> ResourcePrefabs = new Dictionary<string, string>
        {
            { "shine", "HabitatShift/Fx/Shine_ellow" }, { "sparkle", "HabitatShift/Fx/Sparkle_ellow" },
            { "flash", "HabitatShift/Fx/Flash_round_ellow" }, { "dust", "HabitatShift/Fx/Dust_permanently_blue" },
            { "confetti", "HabitatShift/Fx/Confetti_blast_multicolor" }, { "star", "HabitatShift/Fx/Area_star_ellow" }
        };

        public void Initialize(Camera camera)
        {
            worldCamera = camera;
            foreach (var key in ResourcePrefabs.Keys)
                Prewarm(key, key == "dust" ? 3 : key == "sparkle" || key == "shine" ? 12 : key == "flash" ? 8 : 3);
            foreach (var color in new[] { "orange", "blue", "green", "yellow", "purple", "cyan", "pink", "red", "white" })
                SproutSprite(color);
            // L18 has eight board Sproutlings; an assist can emit several captures at once.
            for (var i = 0; i < 8; i++)
            {
                var ghost = CreateGhost(); ghost.renderer.gameObject.SetActive(false); ghostPool.Enqueue(ghost);
            }
        }

        public void SetSettings(bool vfx, bool reduced)
        {
            if ((!vfx && vfxEnabled) || (reduced && !reducedMotion)) StopAll();
            vfxEnabled = vfx;
            reducedMotion = reduced;
        }

        public void Play(IList<PresentationEvent> events, LevelDto level)
        {
            foreach (var e in events)
            {
                if (string.IsNullOrEmpty(e.id) || !seenEvents.Add(e.id)) continue;
                if (e.kind == PresentationKind.Undo) { StopAll(); continue; }
                if (!vfxEnabled) continue;
                var color = ColorFor(e.color);
                if (reducedMotion)
                {
                    if (e.kind == PresentationKind.Collection || e.kind == PresentationKind.ElevatorRelease ||
                        e.kind == PresentationKind.HabitatComplete || e.kind == PresentationKind.AssistSuccess ||
                        e.kind == PresentationKind.Win)
                    {
                        var position = e.kind == PresentationKind.Win
                            ? new Vector2(level.cols * .5f, level.rows * .5f)
                            : e.kind == PresentationKind.Collection || e.kind == PresentationKind.HabitatComplete
                                ? e.targetPosition : e.position;
                        Spawn("flash", position, color, .38f, .16f, 10);
                    }
                    continue;
                }
                switch (e.kind)
                {
                    case PresentationKind.Collection:
                        SpawnGhost(e.color, e.position, e.targetPosition, color);
                        break;
                    case PresentationKind.ElevatorRelease:
                        Spawn("flash", e.position, color, .48f, .32f, 8);
                        Spawn("sparkle", e.position, color, .30f, .38f, 9);
                        break;
                    case PresentationKind.HabitatComplete:
                        Spawn("shine", e.targetPosition, color, .76f, .72f, 9);
                        Spawn("sparkle", e.targetPosition, color, .40f, .46f, 10);
                        break;
                    case PresentationKind.AssistSuccess:
                        Spawn("star", e.position, color, .52f, .55f, 8);
                        break;
                    case PresentationKind.BlockedContact:
                        var cooldownKey = e.subject;
                        if (!blockedCooldown.TryGetValue(cooldownKey, out var at) || Time.unscaledTime - at > .22f)
                        {
                            blockedCooldown[cooldownKey] = Time.unscaledTime;
                            Spawn("flash", e.position, color, .20f, .18f, 8);
                        }
                        break;
                    case PresentationKind.Win:
                        var center = new Vector2(level.cols * .5f, level.rows * .48f);
                        Spawn("confetti", center, Color.white, Mathf.Min(level.cols, level.rows) * .10f, 1.25f, 14, false);
                        Spawn("shine", center, new Color(1f, .82f, .28f), 1.05f, .80f, 13);
                        break;
                }
            }
        }

        public void TickDrag(bool dragging, string habitatColor, Vector2 resolvedPose)
        {
            if (!dragging || !vfxEnabled || reducedMotion || Time.unscaledTime < lastTrailAt + .20f) return;
            lastTrailAt = Time.unscaledTime;
            Spawn("dust", resolvedPose + new Vector2(.5f, .72f), ColorFor(habitatColor), .24f, .32f, 2);
        }

        public void ClearLevel()
        {
            StopAll();
            seenEvents.Clear();
            blockedCooldown.Clear();
            lastTrailAt = 0f;
        }

        void Update()
        {
            var now = Time.unscaledTime;
            for (var i = active.Count - 1; i >= 0; i--)
                if (now >= active[i].releaseAt) Release(active[i], i);
            for (var i = ghosts.Count - 1; i >= 0; i--)
            {
                var g = ghosts[i];
                var elapsed = now - g.startAt;
                if (elapsed < .09f)
                {
                    var t = Mathf.Clamp01(elapsed / .09f);
                    g.renderer.transform.position = g.start;
                    g.renderer.transform.localScale = new Vector3(g.baseScale * (1f + .06f * t),
                        g.baseScale * (1f - .35f * t), 1f);
                }
                else
                {
                    var t = Mathf.Clamp01((elapsed - .09f) / .24f);
                    var eased = t * t * (3f - 2f * t);
                    g.renderer.transform.position = Vector3.Lerp(g.start, g.end, eased);
                    g.renderer.transform.localScale = Vector3.one * (g.baseScale * Mathf.Lerp(.67f, .08f, eased));
                    var tint = Color.white; tint.a = 1f - Mathf.Pow(t, 3f); g.renderer.color = tint;
                }
                if (elapsed >= .33f)
                {
                    Spawn("sparkle", g.targetBoard, g.color, .32f, .40f, 10);
                    Spawn("shine", g.targetBoard, g.color, .38f, .42f, 9);
                    g.renderer.gameObject.SetActive(false); ghostPool.Enqueue(g); ghosts.RemoveAt(i);
                }
            }
        }

        void Prewarm(string key, int count)
        {
            for (var i = 0; i < count; i++)
            {
                var obj = CreateInstance(key);
                if (obj == null) return;
                obj.SetActive(false);
                Pool(key).Enqueue(obj);
            }
        }

        void Spawn(string key, Vector2 boardPosition, Color color, float scale, float lifetime, int sortingOrder, bool tint = true)
        {
            var obj = Acquire(key);
            if (obj == null) return;
            obj.transform.position = BoardViewport.ToWorld(boardPosition, 0f);
            obj.transform.localScale = Vector3.one * scale;
            obj.SetActive(true);
            var parts = fxParts[obj];
            foreach (var ps in parts.particles)
            {
                var main = ps.main;
                main.loop = false;
                main.startColor = tint ? color : Color.white;
                main.maxParticles = Mathf.Min(main.maxParticles, ParticleCap(key));
                main.stopAction = ParticleSystemStopAction.None;
                ps.Clear(true);
                ps.Play(true);
            }
            foreach (var renderer in parts.renderers) renderer.sortingOrder = sortingOrder;
            active.Add(new ActiveFx { gameObject = obj, key = key, releaseAt = Time.unscaledTime + lifetime });
        }

        void SpawnGhost(string color, Vector2 from, Vector2 to, Color tint)
        {
            if (ghostPool.Count == 0) return;
            var ghost = ghostPool.Dequeue();
            ghost.renderer.sprite = SproutSprite(color);
            if (ghost.renderer.sprite == null) { ghostPool.Enqueue(ghost); return; }
            ghost.start = BoardViewport.ToWorld(from + new Vector2(0f, -.02f));
            ghost.end = BoardViewport.ToWorld(to);
            ghost.targetBoard = to;
            ghost.color = tint;
            ghost.startAt = Time.unscaledTime;
            ghost.baseScale = FitSproutScale(ghost.renderer.sprite);
            ghost.renderer.color = Color.white;
            ghost.renderer.transform.localScale = Vector3.one * ghost.baseScale;
            ghost.renderer.transform.position = ghost.start;
            ghost.renderer.gameObject.SetActive(true);
            ghosts.Add(ghost);
        }

        CaptureGhost CreateGhost()
        {
            var obj = new GameObject("Capture Ghost");
            obj.transform.SetParent(transform, false);
            var renderer = obj.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = 11;
            return new CaptureGhost { renderer = renderer };
        }

        static float FitSproutScale(Sprite sprite)
        {
            var native = sprite.bounds.size;
            return Mathf.Min(.86f / native.x, .94f / native.y);
        }

        GameObject Acquire(string key)
        {
            var pool = Pool(key);
            return pool.Count > 0 ? pool.Dequeue() : null;
        }

        GameObject CreateInstance(string key)
        {
            if (!ResourcePrefabs.TryGetValue(key, out var path)) return null;
            var prefab = Resources.Load<GameObject>(path);
            if (prefab == null) { Debug.LogWarning("Missing Habitat Shift FX wrapper: " + path); return null; }
            var obj = Instantiate(prefab, transform);
            obj.name = "FX " + key;
            fxParts.Add(obj, new FxParts
            {
                particles = obj.GetComponentsInChildren<ParticleSystem>(true),
                renderers = obj.GetComponentsInChildren<ParticleSystemRenderer>(true)
            });
            return obj;
        }

        Queue<GameObject> Pool(string key)
        {
            if (!pools.TryGetValue(key, out var pool)) { pool = new Queue<GameObject>(); pools.Add(key, pool); }
            return pool;
        }

        static int ParticleCap(string key)
        {
            switch (key)
            {
                case "dust": return 8;
                case "flash": return 16;
                case "confetti": return 64;
                default: return 20;
            }
        }

        void Release(ActiveFx item, int index)
        {
            foreach (var ps in fxParts[item.gameObject].particles) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            item.gameObject.SetActive(false);
            Pool(item.key).Enqueue(item.gameObject);
            active.RemoveAt(index);
        }

        void StopAll()
        {
            for (var i = active.Count - 1; i >= 0; i--) Release(active[i], i);
            for (var i = ghosts.Count - 1; i >= 0; i--)
            {
                var g = ghosts[i]; g.renderer.gameObject.SetActive(false); ghostPool.Enqueue(g);
            }
            ghosts.Clear();
        }

        Sprite SproutSprite(string color)
        {
            var key = string.IsNullOrEmpty(color) ? "moss" : color;
            if (sproutSprites.TryGetValue(key, out var sprite)) return sprite;
            var name = key == "orange" ? "coral" : key == "blue" ? "cobalt" : key == "green" ? "sage" : key == "yellow" ? "saffron" : key == "purple" ? "lavender" : key == "cyan" ? "teal" : key == "pink" ? "peach" : key == "red" ? "rose" : key == "white" ? "ivory" : "moss";
            sprite = Resources.Load<Sprite>("HabitatShift/sproutling_" + name + "_v1");
            sproutSprites[key] = sprite;
            return sprite;
        }

        public static Color ColorFor(string color)
        {
            switch (color)
            {
                case "orange": return new Color(.96f, .39f, .27f);
                case "blue": return new Color(.23f, .52f, .96f);
                case "green": return new Color(.43f, .72f, .34f);
                case "yellow": return new Color(1f, .74f, .15f);
                case "purple": return new Color(.61f, .42f, .86f);
                case "cyan": return new Color(.14f, .74f, .76f);
                case "pink": return new Color(1f, .42f, .62f);
                case "red": return new Color(.96f, .28f, .40f);case "white": return new Color(.94f, .92f, .84f);
                default: return new Color(1f, .80f, .28f);
            }
        }
    }
}
