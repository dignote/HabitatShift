#if UNITY_EDITOR
using System;
using HabitatShift.Core;
using HabitatShift.Runtime;
using UnityEditor;
using UnityEngine;

namespace HabitatShift.LevelLabR10
{
    // Editor-only bridge: production presentation -> isolated camera -> RenderTexture -> EditorWindow.
    internal sealed class R10RuntimePreviewHost : IDisposable
    {
        GameObject root;
        Camera camera;
        HabitatPresentation presentation;
        RenderTexture texture;
        const int PreviewLayer = 31;
        int width, height;
        public string Error { get; private set; }
        public RenderTexture Texture { get { return texture; } }

        public void Render(LevelDto level, RuntimeState state, int requestedWidth, int requestedHeight)
        {
            Error = null;
            try
            {
                Ensure(Mathf.Max(64, requestedWidth), Mathf.Max(64, requestedHeight));
                Fit(level);
                presentation.Render(state, level);
                AssignPreviewLayer(root.transform);
                camera.Render();
            }
            catch (Exception exception)
            {
                Error = exception.Message;
            }
        }

        void Ensure(int requestedWidth, int requestedHeight)
        {
            if (root == null)
            {
                root = new GameObject("Level Lab Runtime Preview") { hideFlags = HideFlags.HideAndDontSave };
                camera = root.AddComponent<Camera>();
                camera.hideFlags = HideFlags.HideAndDontSave;
                camera.orthographic = true;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.08f, .10f, .08f, 1f);
                camera.enabled = false;
                camera.cullingMask = 1 << PreviewLayer;
                root.layer = PreviewLayer;
                presentation = root.AddComponent<HabitatPresentation>();
                presentation.hideFlags = HideFlags.HideAndDontSave;
                presentation.Initialize(camera);
                presentation.AllowTransientBoardSkin = true;
            }
            if (texture != null && width == requestedWidth && height == requestedHeight) return;
            ReleaseTexture();
            width = requestedWidth; height = requestedHeight;
            texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                name = "Level Lab Runtime Preview",
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.Create();
            camera.targetTexture = texture;
        }

        void Fit(LevelDto level)
        {
            var aspect = width / (float)Mathf.Max(1, height);
            var boardWidth = level.cols + BoardViewport.MarginCells * 2f;
            var boardHeight = level.rows + BoardViewport.MarginCells * 2f;
            camera.orthographicSize = Mathf.Max(boardHeight * .5f, boardWidth / (2f * Mathf.Max(.01f, aspect)));
            var center = BoardViewport.ToWorld(new Vector2(level.cols * .5f, level.rows * .5f));
            camera.transform.position = new Vector3(center.x, center.y, -10f);
            presentation.FitBackdrop(camera);
        }

        static void AssignPreviewLayer(Transform parent)
        {
            for (var i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                child.gameObject.hideFlags = HideFlags.HideAndDontSave;
                child.gameObject.layer = PreviewLayer;
                AssignPreviewLayer(child);
            }
        }

        void ReleaseTexture()
        {
            if (camera != null) camera.targetTexture = null;
            if (texture == null) return;
            texture.Release();
            UnityEngine.Object.DestroyImmediate(texture);
            texture = null;
        }

        public void Dispose()
        {
            ReleaseTexture();
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            root = null; camera = null; presentation = null;
        }
    }
}
#endif