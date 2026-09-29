using System.Collections.Generic;
using HabitatShift.Core;
using System;
using System.Linq;
using UnityEngine;

namespace HabitatShift.Runtime
{
    // A view of the committed or preview state. Objects are reused while a gesture is active.
    public sealed class HabitatPresentation : MonoBehaviour
    {
        readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        readonly Dictionary<string, SpriteRenderer> boardParts = new Dictionary<string, SpriteRenderer>();
        readonly Dictionary<string, SpriteRenderer> movingParts = new Dictionary<string, SpriteRenderer>();
        readonly Dictionary<string, TextMesh> queueCounts = new Dictionary<string, TextMesh>();
        readonly HashSet<string> usedQueueCounts = new HashSet<string>();
        readonly Dictionary<string, SproutlingMotion> motions = new Dictionary<string, SproutlingMotion>();
        readonly HashSet<string> usedMovingParts = new HashSet<string>();
        readonly Dictionary<string, HabitatView> habitatViews = new Dictionary<string, HabitatView>();
        readonly Dictionary<string, HabitatMotion> habitatMotions = new Dictionary<string, HabitatMotion>();
        readonly HashSet<string> assistCandidates = new HashSet<string>();
        readonly HashSet<string> assistSproutCandidates = new HashSet<string>();
        string previewHabitat; Vector2 previewAnchor; bool previewValid;
        Sprite background;
        SpriteRenderer backdrop;
        int boardLevel = -1;
        string draggingHabitat;
        Vector2 dragDirection;
        bool motionEnabled = true;

        sealed class HabitatView
        {
            public string color;
            public List<Vector2Int> shape;
            public string key;
            public Sprite sprite;
            public Vector2 offset, size;
        }

        public void Initialize(Camera camera)
        {
            background = LoadRequired("botanical_workshop_background_v1");
            if (background != null)
                backdrop = Create("Botanical Workshop Background", background, -30);
            FitBackdrop(camera);
        }

        public void FitBackdrop(Camera camera)
        {
            if (backdrop == null) return;
            var height = camera.orthographicSize * 2f;
            var width = height * Mathf.Max(1, Screen.width) / Mathf.Max(1, Screen.height);
            var native = backdrop.sprite.bounds.size;
            var cover = Mathf.Max(width / native.x, height / native.y);
            backdrop.transform.position = new Vector3(camera.transform.position.x, camera.transform.position.y, 5f);
            backdrop.transform.localScale = Vector3.one * cover;
        }

        public void Clear()
        {
            foreach (var part in boardParts.Values) part.gameObject.SetActive(false);
            foreach (var part in movingParts.Values) part.gameObject.SetActive(false);
            foreach (var count in queueCounts.Values) count.gameObject.SetActive(false);
            habitatViews.Clear();
            assistCandidates.Clear();
            assistSproutCandidates.Clear(); previewHabitat = null;
            boardLevel = -1;
        }

        public void SetPresentationMotion(bool enabled, string dragId, Vector2 direction)
        {
            motionEnabled = enabled;
            draggingHabitat = dragId;
            dragDirection = direction;
        }

        public void SetAssistCandidates(IEnumerable<string> ids)
        {
            assistCandidates.Clear();
            if (ids == null) return;
            foreach (var id in ids) assistCandidates.Add(id);
        }

        public void SetAssistSproutCandidates(IEnumerable<string> ids) { assistSproutCandidates.Clear(); if (ids == null) return; foreach (var id in ids) assistSproutCandidates.Add(id); }
        public void SetSettlePreview(string habitatId, Vector2 anchor, bool valid) { previewHabitat = habitatId; previewAnchor = anchor; previewValid = valid; }
        public void ClearSettlePreview() { previewHabitat = null; }

        public void Render(RuntimeState state, LevelDto level)
        {
            if (state == null) return;
            if (boardLevel != level.id) BuildBoard(level);
            usedMovingParts.Clear();
            usedQueueCounts.Clear();

            foreach (var elevator in state.elevators)
            {
                Place(movingParts, "elevator/" + elevator.id,
                    LoadBoard("board_elevator_" + elevator.direction.ToLowerInvariant()), elevator.entry,
                    Vector2.one, Color.white, 4);
                var remaining = elevator.queue == null ? 0 : elevator.queue.Count - elevator.nextIndex;
                for (var i = 0; i < Mathf.Min(3, Mathf.Max(0, remaining)); i++)
                {
                    var item = elevator.queue[elevator.nextIndex + i];
                    var size = i == 0 ? .34f : .27f;
                    Place(movingParts, "elevator/" + elevator.id + "/next/" + i,
                        LoadSprout(item.color),
                        elevator.entry + new Vector2(-.29f + i * .27f, -.23f),
                        Vector2.one * size, Color.white, 7);
                }
                if (remaining > 0) PlaceQueueCount(elevator.id, elevator.entry, remaining);
            }
            foreach (var habitat in state.habitats) DrawHabitat(habitat);
            DrawSettlePreview(state);
            foreach (var sprout in state.sprouts) DrawSprout(sprout, state);
            foreach (var entry in movingParts)
                if (!usedMovingParts.Contains(entry.Key)) entry.Value.gameObject.SetActive(false);
            foreach (var count in queueCounts)
                if (!usedQueueCounts.Contains(count.Key)) count.Value.gameObject.SetActive(false);
        }

        void PlaceQueueCount(string elevatorId, Vector2 entry, int remaining)
        {
            if (!queueCounts.TryGetValue(elevatorId, out var count))
            {
                var go = new GameObject("Queue count " + elevatorId);
                go.transform.SetParent(transform, false);
                count = go.AddComponent<TextMesh>();
                count.font = Resources.Load<Font>("HabitatShift/Fonts/LilitaOne-Regular");
                count.fontSize = 64;
                count.characterSize = .035f;
                count.anchor = TextAnchor.MiddleCenter;
                count.alignment = TextAlignment.Center;
                count.color = new Color(.18f, .10f, .06f);
                var renderer = go.GetComponent<MeshRenderer>();
                if (count.font != null) renderer.sharedMaterial = count.font.material;
                renderer.sortingOrder = 8;
                queueCounts.Add(elevatorId, count);
            }
            usedQueueCounts.Add(elevatorId);
            count.gameObject.SetActive(true);
            count.text = remaining.ToString();
            count.transform.position = BoardViewport.ToWorld(entry + new Vector2(.31f, -.30f), -1f);
        }

        void BuildBoard(LevelDto level)
        {
            foreach (var part in boardParts.Values) part.gameObject.SetActive(false);
            habitatViews.Clear();
            boardLevel = level.id;
            var center = new Vector2(level.cols * .5f, level.rows * .5f);
            PlaceSliced(boardParts, "card", LoadBoard("board_card"), center,
                new Vector2(level.cols + .34f, level.rows + .34f), -10);

            for (var y = 0; y < level.rows; y++)
            for (var x = 0; x < level.cols; x++)
            {
                if (!Playable(level, x, y)) continue;
                var position = new Vector2(x + .5f, y + .5f);
                var key = "tile/" + x + "/" + y;
                Place(boardParts, key, LoadBoard("board_tile"), position,
                    Vector2.one, Color.white, -1);
            }
            foreach (var obstacle in level.obstacles ?? new IntPair[0])
                Place(boardParts, "stone/" + obstacle.x + "/" + obstacle.y,
                    LoadBoard("board_blocker_stone"),
                    new Vector2(obstacle.x + .5f, obstacle.y + .5f), Vector2.one, Color.white, 1);
        }


        void DrawSettlePreview(RuntimeState state)
        {
            if (string.IsNullOrEmpty(previewHabitat)) return;
            var habitat = state.habitats.FirstOrDefault(h => h.id == previewHabitat);
            if (habitat == null) return;
            var sprite = LoadBoard(previewValid ? "board_preview_valid" : "board_preview_invalid");
            foreach (var cell in habitat.shape)
                Place(movingParts, "preview/" + habitat.id + "/" + cell.x + "/" + cell.y, sprite, previewAnchor + cell + Vector2.one*.5f, Vector2.one, Color.white, 2);
        }

        void DrawHabitat(HabitatRuntime habitat)
        {
            if (!habitatViews.TryGetValue(habitat.id, out var view) ||
                view.color != habitat.color || view.shape.Count != habitat.shape.Count ||
                !view.shape.SequenceEqual(habitat.shape))
            {
                var x0 = habitat.shape.Min(c => c.x);
                var y0 = habitat.shape.Min(c => c.y);
                var cols = habitat.shape.Max(c => c.x) - x0 + 1;
                var rows = habitat.shape.Max(c => c.y) - y0 + 1;
                var spriteName = "tray_" + Family(habitat.color) + "_" + ShapeKey(habitat.shape, x0, y0);
                view = new HabitatView
                {
                    color = habitat.color,
                    shape = new List<Vector2Int>(habitat.shape),
                    key = "habitat/" + habitat.id,
                    sprite = LoadBoard(spriteName),
                    offset = new Vector2(x0 + cols * .5f, y0 + rows * .5f),
                    size = new Vector2(cols + 56f / 192f, rows + 56f / 192f)
                };
                habitatViews[habitat.id] = view;
            }
            // One baked footprint has a seamless basin and a continuous rim at the outside only.
            if (assistCandidates.Contains(habitat.id))
                Place(movingParts, view.key + "/assist", view.sprite, habitat.anchor + view.offset,
                    view.size * 1.06f, new Color(1f, .80f, .28f, .30f), 2);
            var renderer = Place(movingParts, view.key, view.sprite, habitat.anchor + view.offset, view.size, Color.white, 3);
            if (!habitatMotions.TryGetValue(habitat.id, out var motion))
            {
                motion = renderer.gameObject.AddComponent<HabitatMotion>();
                habitatMotions.Add(habitat.id, motion);
            }
            motion.SetPose(renderer, renderer.transform.position, renderer.transform.localScale,
                motionEnabled && habitat.id == draggingHabitat, dragDirection, motionEnabled);
        }

        void DrawSprout(SproutRuntime sprout, RuntimeState state)
        {
            var sprite = LoadSprout(sprout.color);
            var size = FitSproutInCell(sprite);
            var position = sprout.position + new Vector2(0f, -.02f);
            var highlighted = assistSproutCandidates.Contains(sprout.id);
            var renderer = Place(movingParts, "sprout/body/" + sprout.id, sprite, position,
                size * (highlighted ? 1.10f : 1f), highlighted ? new Color(1f,.88f,.45f) : Color.white, 6);
            if (!motions.TryGetValue(sprout.id, out var motion))
            {
                motion = renderer.gameObject.AddComponent<SproutlingMotion>();
                motions.Add(sprout.id, motion);
            }
            motion.MotionEnabled = motionEnabled;
            motion.SetPose(renderer.transform.position, renderer.transform.localScale);

            var coverage = 0f;
            foreach (var habitat in state.habitats)
                if (habitat.color == sprout.color)
                    coverage = Mathf.Max(coverage, ContinuousSession.CaptureCoverage(habitat, sprout.position));
            if (coverage > .001f && coverage <= ContinuousSession.CaptureThreshold)
            {
                var glow = Color.Lerp(Color.white, GameplayFxController.ColorFor(sprout.color), .45f);
                glow.a = .22f + coverage * .18f;
                Place(movingParts, "sprout/glow/" + sprout.id, sprite, position,
                    size * 1.12f, glow, 5);
            }
        }

        static Vector2 FitSproutInCell(Sprite sprite)
        {
            var native = sprite.bounds.size;
            var scale = Mathf.Min(.86f / native.x, .94f / native.y);
            return native * scale;
        }

        SpriteRenderer Place(Dictionary<string, SpriteRenderer> pool, string key, Sprite sprite,
            Vector2 boardPosition, Vector2 worldSize, Color color, int sortingOrder)
        {
            if (!pool.TryGetValue(key, out var renderer))
            {
                renderer = Create(key, sprite, sortingOrder);
                pool.Add(key, renderer);
            }
            if (ReferenceEquals(pool, movingParts)) usedMovingParts.Add(key);
            if (!renderer.gameObject.activeSelf) renderer.gameObject.SetActive(true);
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            renderer.transform.position = BoardViewport.ToWorld(boardPosition);
            var bounds = sprite.bounds.size;
            renderer.transform.localScale = new Vector3(worldSize.x / bounds.x, worldSize.y / bounds.y, 1f);
            return renderer;
        }

        SpriteRenderer PlaceSliced(Dictionary<string, SpriteRenderer> pool, string key, Sprite sprite,
            Vector2 boardPosition, Vector2 worldSize, int sortingOrder)
        {
            if (!pool.TryGetValue(key, out var renderer))
            {
                renderer = Create(key, sprite, sortingOrder);
                pool.Add(key, renderer);
            }
            if (!renderer.gameObject.activeSelf) renderer.gameObject.SetActive(true);
            renderer.sprite = sprite;
            renderer.drawMode = SpriteDrawMode.Sliced;
            renderer.size = worldSize;
            renderer.color = Color.white;
            renderer.sortingOrder = sortingOrder;
            renderer.transform.position = BoardViewport.ToWorld(boardPosition);
            renderer.transform.localScale = Vector3.one;
            return renderer;
        }

        SpriteRenderer Create(string name, Sprite sprite, int order)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(transform, false);
            var renderer = obj.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            return renderer;
        }

        static bool Playable(LevelDto level, int x, int y)
        {
            if (level.playableMask == null || level.playableMask.Length == 0) return true;
            foreach (var cell in level.playableMask) if (cell.x == x && cell.y == y) return true;
            return false;
        }

        static string SpriteName(string color)
        {
            switch (color)
            {
                case "orange": return "sproutling_coral_v1";
                case "blue": return "sproutling_cobalt_v1";
                case "green": return "sproutling_sage_v1";
                case "yellow": return "sproutling_saffron_v1";
                case "purple": return "sproutling_lavender_v1";
                case "cyan": return "sproutling_teal_v1";
                case "pink": return "sproutling_peach_v1";
                case "red": return "sproutling_rose_v1";case "white": return "sproutling_ivory_v1";
                default: return "sproutling_moss_v1";
            }
        }

        Sprite LoadSprout(string color)
        {
            var name = SpriteName(color);
            if (!sprites.TryGetValue(name, out var sprite))
            {
                sprite = LoadRequired(name);
                sprites.Add(name, sprite);
            }
            return sprite;
        }

        static string Family(string color)
        {
            switch (color)
            {
                case "orange": return "coral";
                case "blue": return "cobalt";
                case "green": return "sage";
                case "yellow": return "saffron";
                case "purple": return "lavender";
                case "cyan": return "teal";
                case "pink": return "peach";
                case "red": return "rose";case "white": return "ivory";
                default: throw new InvalidOperationException("Unknown Habitat color: " + color);
            }
        }

        static string ShapeKey(List<Vector2Int> shape, int x0, int y0)
        {
            return string.Join("-", shape.OrderBy(c => c.x).ThenBy(c => c.y)
                .Select(c => (c.x - x0) + "_" + (c.y - y0)));
        }

        Sprite LoadBoard(string name)
        {
            var path = "HabitatShift/Board/" + name;
            if (!sprites.TryGetValue(path, out var sprite))
            {
                sprite = Resources.Load<Sprite>(path);
                if (sprite == null) throw new InvalidOperationException("Missing Habitat Shift board sprite: " + path);
                sprites.Add(path, sprite);
            }
            return sprite;
        }

        static Sprite LoadRequired(string name)
        {
            var path = "HabitatShift/" + name;
            var sprite = Resources.Load<Sprite>(path);
            if (sprite == null) throw new InvalidOperationException("Missing Habitat Shift sprite: " + path);
            return sprite;
        }
    }
}


