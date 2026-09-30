using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

namespace HabitatShift.Editor
{
    public static class BoardSpriteAtlases
    {
        const string Board = "Assets/Resources/HabitatShift/Board";
        const string Atlases = "Assets/HabitatShift/Art/Atlases";
        static readonly string[] Families =
            { "coral", "cobalt", "sage", "saffron", "lavender", "teal", "peach", "rose", "walnut" };

        [InitializeOnLoadMethod]
        static void Schedule()
        {
            EditorApplication.delayCall += EnsureAtlases;
        }

        [MenuItem("Habitat Shift/Rebuild Board Sprite Atlases")]
        public static void EnsureAtlases()
        {
            Directory.CreateDirectory(Atlases);
            foreach (var family in Families)
            {
                var path = Atlases + "/tray_" + family + ".spriteatlas";
                if (AssetDatabase.LoadAssetAtPath<SpriteAtlas>(path) != null) continue;
                var sprites = Directory.GetFiles(Board, "tray_" + family + "_*.png")
                    .Select(p => AssetDatabase.LoadAssetAtPath<Sprite>(p.Replace('\\', '/')))
                    .Where(s => s != null).Cast<Object>().ToArray();
                if (sprites.Length == 0) continue;
                var atlas = new SpriteAtlas();
                atlas.SetPackingSettings(new SpriteAtlasPackingSettings
                {
                    enableRotation = false, enableTightPacking = false, padding = 4
                });
                AssetDatabase.CreateAsset(atlas, path);
                SpriteAtlasExtensions.Add(atlas, sprites);
            }
            var boardPath = Atlases + "/board_environment.spriteatlas";
            if (AssetDatabase.LoadAssetAtPath<SpriteAtlas>(boardPath) == null)
            {
                var sprites = Directory.GetFiles(Board, "board_*.png")
                    .Select(p => AssetDatabase.LoadAssetAtPath<Sprite>(p.Replace('\\', '/')))
                    .Where(s => s != null).Cast<Object>().ToArray();
                var atlas = new SpriteAtlas();
                atlas.SetPackingSettings(new SpriteAtlasPackingSettings
                {
                    enableRotation = false, enableTightPacking = false, padding = 4
                });
                AssetDatabase.CreateAsset(atlas, boardPath);
                SpriteAtlasExtensions.Add(atlas, sprites);
            }
            AssetDatabase.SaveAssets();
        }
    }
}
