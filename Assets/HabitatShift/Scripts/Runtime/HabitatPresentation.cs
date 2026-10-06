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
        readonly Dictionary<string, Sprite> dualRingSprites = new Dictionary<string, Sprite>();
        readonly Dictionary<string, Sprite> generatedTraySprites = new Dictionary<string, Sprite>();
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

        void OnDestroy()
        {
            DestroyGeneratedSprites(dualRingSprites);
            DestroyGeneratedSprites(generatedTraySprites);
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
                var deck = Place(movingParts, "elevator/" + elevator.id,
                    LoadBoard("board_elevator_deck_v2"), elevator.entry,
                    Vector2.one * 1.52f, Color.white, 1);
                deck.transform.rotation = Quaternion.Euler(0f, 0f, ElevatorAngle(elevator.direction));
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
            if (IsFullRect(level)) PlaceSliced(boardParts, "card", LoadBoard("board_card"), center,
                new Vector2(level.cols + .34f, level.rows + .34f), -10);
            else
            {
                // mask -10 (base) -> tile -1 -> frame 0 (vien ceramic lien tuc, nam TREN tile
                // nhung duoi obstacle 1 / habitat 3 / elevator 4 / sproutling 6).
                var size = new Vector2(level.cols + 56f / 192f, level.rows + 56f / 192f);
                Place(boardParts, "mask", LoadBoard(BoardMaskName(level)), center, size, Color.white, -10);
                Place(boardParts, "frame", LoadBoard(BoardFrameName(level)), center, size, Color.white, 0);
            }

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
                    LoadBoard(BlockerSpriteName(obstacle.x, obstacle.y)),
                    new Vector2(obstacle.x + .5f, obstacle.y + .5f), Vector2.one, Color.white, 1);
        }


        void DrawSettlePreview(RuntimeState state)
        {
            if (string.IsNullOrEmpty(previewHabitat)) return;
            var habitat = state.habitats.FirstOrDefault(h => h.id == previewHabitat);
            if (habitat == null) return;
            var sprite = LoadBoard(previewValid ? "board_preview_valid" : "board_preview_invalid");
            foreach (var cell in habitat.shape)
                Place(movingParts, "preview/" + habitat.id + "/" + cell.x + "/" + cell.y,
                    sprite, previewAnchor + cell + Vector2.one * .5f, Vector2.one * .92f,
                    new Color(1f, 1f, 1f, .52f), 4);
        }

        void DrawHabitat(HabitatRuntime habitat)
        {
            if (!habitatViews.TryGetValue(habitat.id, out var view) ||
                view.color != habitat.ActiveColor + "/" + (habitat.HasNextStage ? habitat.stages[habitat.stageIndex + 1].color : "") || view.shape.Count != habitat.shape.Count ||
                !view.shape.SequenceEqual(habitat.shape))
            {
                var x0 = habitat.shape.Min(c => c.x);
                var y0 = habitat.shape.Min(c => c.y);
                var cols = habitat.shape.Max(c => c.x) - x0 + 1;
                var rows = habitat.shape.Max(c => c.y) - y0 + 1;
                var dual = habitat.stages != null && habitat.stages.Count > 1;
                var spriteName = "tray_" + Family(habitat.ActiveColor) + "_" + ShapeKey(habitat.shape, x0, y0);
                view = new HabitatView
                {
                    color = habitat.ActiveColor + "/" + (habitat.HasNextStage ? habitat.stages[habitat.stageIndex + 1].color : ""),
                    shape = new List<Vector2Int>(habitat.shape),
                    key = "habitat/" + habitat.id,
                    sprite = dual ? DualRingSprite(habitat, x0, y0, cols, rows) : TraySprite(habitat, x0, y0, cols, rows, spriteName),
                    offset = new Vector2(x0 + cols * .5f, y0 + rows * .5f),
                    size = dual ? new Vector2(cols + .25f, rows + .25f) : new Vector2(cols + 56f / 192f, rows + 56f / 192f)
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
            DrawConstraintBadge(habitat, view, motion);
        }

        static void DestroyGeneratedSprites(Dictionary<string, Sprite> cache)
        {
            foreach (var sprite in cache.Values)
            {
                if (sprite == null) continue;
                var texture = sprite.texture;
                Destroy(sprite);
                if (texture != null) Destroy(texture);
            }
            cache.Clear();
        }

        Sprite TraySprite(HabitatRuntime habitat, int x0, int y0, int cols, int rows, string name)
        {
            var path = "HabitatShift/Board/" + name;
            if (sprites.TryGetValue(path, out var sprite)) return sprite;
            sprite = Resources.Load<Sprite>(path);
            if (sprite != null)
            {
                sprites.Add(path, sprite);
                return sprite;
            }

            // Authored v5 footprints are data-driven. A missing optional tray bitmap must not
            // prevent a valid level from rendering; create the same ceramic tray from its shape.
            var key = Family(habitat.ActiveColor) + "/" + ShapeKey(habitat.shape, x0, y0);
            if (generatedTraySprites.TryGetValue(key, out var generated)) return generated;
            Debug.LogWarning("Habitat Shift generated missing tray sprite: " + path);
            generated = GenerateTraySprite(habitat, x0, y0, cols, rows, key);
            generatedTraySprites.Add(key, generated);
            return generated;
        }

        Sprite GenerateTraySprite(HabitatRuntime habitat, int x0, int y0, int cols, int rows, string key)
        {
            const int pixelsPerCell = 192, padding = 28;
            var width = cols * pixelsPerCell + padding * 2;
            var height = rows * pixelsPerCell + padding * 2;
            var pixels = new Color32[width * height];
            var cells = new HashSet<Vector2Int>(habitat.shape.Select(c =>
                new Vector2Int(c.x - x0, rows - 1 - (c.y - y0))));
            var edges = OutlineEdges(cells, pixelsPerCell, padding);
            var rim = GameplayFxController.ColorFor(habitat.ActiveColor);
            var clay = new Color(.89f, .83f, .70f);
            for (var y = 0; y < height; y++) for (var x = 0; x < width; x++)
            {
                var cx = Mathf.FloorToInt((x - padding) / (float)pixelsPerCell);
                var cy = Mathf.FloorToInt((y - padding) / (float)pixelsPerCell);
                if (!cells.Contains(new Vector2Int(cx, cy))) continue;
                var distance = DistanceToOutline(x, y, edges);
                Color color;
                if (distance < 5f) color = new Color(.31f, .25f, .20f);
                else if (distance < 23f) color = Color.Lerp(rim * .58f, rim, Mathf.Clamp01((distance - 5f) / 18f));
                else if (distance < 31f) color = new Color(.98f, .91f, .76f);
                else color = clay;
                var grain = ((x * 37 + y * 73) % 17 - 8) * .002f;
                color *= .96f + .06f * y / height + grain;
                color.a = 1f;
                pixels[y * width + x] = color;
            }
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { name = "GeneratedTray/" + key, filterMode = FilterMode.Bilinear };
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            var sprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(.5f, .5f), pixelsPerCell);
            sprite.name = texture.name;
            return sprite;
        }

        static List<Vector4> OutlineEdges(HashSet<Vector2Int> cells, int pixelsPerCell, int padding)
        {
            var edges = new List<Vector4>();
            foreach (var cell in cells)
            {
                var x = cell.x * pixelsPerCell + padding;
                var y = cell.y * pixelsPerCell + padding;
                if (!cells.Contains(cell + Vector2Int.left)) edges.Add(new Vector4(x, y, x, y + pixelsPerCell));
                if (!cells.Contains(cell + Vector2Int.right)) edges.Add(new Vector4(x + pixelsPerCell, y, x + pixelsPerCell, y + pixelsPerCell));
                if (!cells.Contains(cell + Vector2Int.down)) edges.Add(new Vector4(x, y, x + pixelsPerCell, y));
                if (!cells.Contains(cell + Vector2Int.up)) edges.Add(new Vector4(x, y + pixelsPerCell, x + pixelsPerCell, y + pixelsPerCell));
            }
            return edges;
        }

        static float DistanceToOutline(int x, int y, List<Vector4> edges)
        {
            var distance = float.MaxValue;
            foreach (var edge in edges)
            {
                var dx = edge.x == edge.z ? x - edge.x : x - Mathf.Clamp(x, edge.x, edge.z);
                var dy = edge.y == edge.w ? y - edge.y : y - Mathf.Clamp(y, edge.y, edge.w);
                distance = Mathf.Min(distance, Mathf.Sqrt(dx * dx + dy * dy));
            }
            return distance;
        }

        Sprite DualRingSprite(HabitatRuntime habitat, int x0, int y0, int cols, int rows)
        {
            var next = habitat.HasNextStage ? habitat.stages[habitat.stageIndex + 1].color : "ceramic";
            var shapeKey = ShapeKey(habitat.shape, x0, y0);
            var key = shapeKey + "/" + habitat.ActiveColor + "/" + next;
            if (dualRingSprites.TryGetValue(key, out var cached)) return cached;
            const int pixelsPerCell = 128, padding = 16;
            var width = cols * pixelsPerCell + padding * 2;
            var height = rows * pixelsPerCell + padding * 2;
            var pixels = new Color32[width * height];
            // Board Y increases down, while texture pixel Y increases up.
            var cells = new HashSet<Vector2Int>(habitat.shape.Select(c =>
                new Vector2Int(c.x - x0, rows - 1 - (c.y - y0))));
            var edges = new List<Vector4>();
            foreach (var cell in cells)
            {
                var x = cell.x * pixelsPerCell + padding;
                var y = cell.y * pixelsPerCell + padding;
                if (!cells.Contains(cell + Vector2Int.left)) edges.Add(new Vector4(x, y, x, y + pixelsPerCell));
                if (!cells.Contains(cell + Vector2Int.right)) edges.Add(new Vector4(x + pixelsPerCell, y, x + pixelsPerCell, y + pixelsPerCell));
                if (!cells.Contains(cell + Vector2Int.down)) edges.Add(new Vector4(x, y, x + pixelsPerCell, y));
                if (!cells.Contains(cell + Vector2Int.up)) edges.Add(new Vector4(x, y + pixelsPerCell, x + pixelsPerCell, y + pixelsPerCell));
            }
            var outer = GameplayFxController.ColorFor(habitat.ActiveColor);
            var inner = habitat.HasNextStage ? GameplayFxController.ColorFor(next) : new Color(.85f, .77f, .61f);
            var clay = new Color(.84f, .76f, .62f);
            for (var y = 0; y < height; y++) for (var x = 0; x < width; x++)
            {
                var cx = Mathf.FloorToInt((x - padding) / (float)pixelsPerCell);
                var cy = Mathf.FloorToInt((y - padding) / (float)pixelsPerCell);
                if (!cells.Contains(new Vector2Int(cx, cy))) continue;
                var distance = DistanceToOutline(x, y, edges);
                Color color;
                if (distance < 4f) color = new Color(.34f, .27f, .22f);
                else if (distance < 15f) color = Color.Lerp(outer * .64f, outer, Mathf.Clamp01((distance - 4f) / 9f));
                else if (distance < 21f) color = new Color(.96f, .87f, .69f);
                else if (distance < 32f) color = Color.Lerp(inner * .68f, inner, Mathf.Clamp01((distance - 21f) / 10f));
                else color = clay;
                var grain = ((x * 37 + y * 73) % 17 - 8) * .002f;
                color *= .96f + .06f * y / height + grain;
                color.a = 1f;
                pixels[y * width + x] = color;
            }
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.name = "DualRing/" + key;
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            texture.filterMode = FilterMode.Bilinear;
            var sprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(.5f, .5f), pixelsPerCell);
            sprite.name = texture.name;
            dualRingSprites.Add(key, sprite);
            return sprite;
        }

        static float ElevatorAngle(string direction)
        {
            switch (direction)
            {
                case "UP": return 0f;
                case "RIGHT": return -90f;
                case "DOWN": return 180f;
                case "LEFT": return 90f;
                default: throw new InvalidOperationException("Unknown Habitat Shift elevator direction: " + direction);
            }
        }

        void DrawConstraintBadge(HabitatRuntime habitat, HabitatView view, HabitatMotion motion)
        {
            var horizontal = habitat.constraint == "HORIZONTAL_ONLY";
            var vertical = habitat.constraint == "VERTICAL_ONLY";
            if (!horizontal && !vertical) return;

            var centerX = (habitat.shape.Min(c => c.x) + habitat.shape.Max(c => c.x)) * .5f;
            var centerY = (habitat.shape.Min(c => c.y) + habitat.shape.Max(c => c.y)) * .5f;
            var edgeCell = horizontal
                ? habitat.shape.Where(c => !habitat.shape.Contains(c + Vector2Int.up))
                    .OrderBy(c => Mathf.Abs(c.x - centerX)).ThenByDescending(c => c.y).First()
                : habitat.shape.Where(c => !habitat.shape.Contains(c + Vector2Int.right))
                    .OrderBy(c => Mathf.Abs(c.y - centerY)).ThenByDescending(c => c.x).First();
            var size = horizontal ? new Vector2(.48f, .21f) : new Vector2(.21f, .48f);
            var position = habitat.anchor + edgeCell +
                (horizontal ? new Vector2(.5f, 1.035f) : new Vector2(1.035f, .5f));
            var sprite = LoadBoard(horizontal ? "board_move_horizontal" : "board_move_vertical");
            var badge = Place(movingParts, view.key + "/constraint", sprite, position, size, Color.white, 4);
            var visual = motion.VisualTransform;
            badge.transform.SetParent(visual, true);
            var parentScale = visual.lossyScale;
            badge.transform.localScale = new Vector3(
                size.x / sprite.bounds.size.x / parentScale.x,
                size.y / sprite.bounds.size.y / parentScale.y, 1f);
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
                if (habitat.ActiveColor == sprout.color)
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

        // Board full rectangle dung board_card; mask bat quy tac dung sprite sinh tu playableMask.
        static bool IsFullRect(LevelDto level)
        {
            var mask = level.playableMask;
            if (mask == null || mask.Length == 0) return true;
            return mask.Length == level.cols * level.rows;
        }

        static string BoardMaskName(LevelDto level) { return "board_mask_L" + level.id; }

        // Vien sinh tu cung playableMask; thieu asset thi LoadBoard nem loi ro rang, khong fallback hinh chu nhat.
        static string BoardFrameName(LevelDto level) { return "board_frame_L" + level.id; }

        // O bi chan (obstacle) dung 3 block da/gach cat tu .docs/sprite block.png, chon theo toa do o.
        static readonly string[] BlockerSprites =
            { "board_blocker_stone", "board_blocker_moss", "board_blocker_brick" };

        static string BlockerSpriteName(int x, int y)
        {
            var index = (x + 2 * y) % BlockerSprites.Length;
            return BlockerSprites[(index + BlockerSprites.Length) % BlockerSprites.Length];
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
                case "red": return "sproutling_rose_v1";case "white": return "sproutling_white_v2";
                case "brown": return "sproutling_walnut_v2";
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
                case "brown": return "walnut";
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


