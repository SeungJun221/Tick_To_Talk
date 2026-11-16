using System.Collections.Generic;
using UnityEngine;

namespace TickToTalk
{
    public sealed partial class PrisonPresentation
    {
        readonly List<Renderer> manualScreens = new List<Renderer>();
        readonly List<int> manualRooms = new List<int>();
        readonly List<RenderTexture> manualTextures = new List<RenderTexture>();
        readonly List<string> manualText = new List<string>();
        readonly List<bool> manualDirty = new List<bool>();
        readonly List<Material> manualMaterials = new List<Material>();
        string manualDisplay;
        int manualOwner = -1;
        int manualStage = -1;
        Font manualFont;
        readonly GUIStyle manualStyle = new GUIStyle();

        void StartManualScreens()
        {
            var screenShader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Universal Render Pipeline/Lit");
            if (!screenShader)
            {
                Debug.LogError("Unable to initialize the manual screens because no compatible URP shader is available.");
                return;
            }

            manualFont = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Noto Sans CJK KR", "Arial" }, 48);
            foreach (var renderer in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (!renderer.name.StartsWith("Manual screen")) continue;
                manualScreens.Add(renderer);
                manualRooms.Add(renderer.transform.position.x < 0 ? 0 : 1);
                var texture = new RenderTexture(1024, 600, 0, RenderTextureFormat.ARGB32);
                texture.filterMode = FilterMode.Bilinear;
                texture.Create();
                manualTextures.Add(texture);
                manualText.Add("?");
                manualDirty.Add(true);
                var material = new Material(screenShader);
                material.mainTexture = texture;
                material.SetTextureScale("_BaseMap", new Vector2(-1, -1));
                material.SetTextureOffset("_BaseMap", new Vector2(1, 1));
                renderer.sharedMaterial = material;
                manualMaterials.Add(material);
            }
        }

        static string FitManual(string text)
        {
            if (string.IsNullOrEmpty(text)) return "?";
            var lines = new List<string>();
            foreach (string paragraph in text.Split('\n'))
            {
                string line = "";
                foreach (char character in paragraph)
                {
                    if (line.Length == 18)
                    {
                        lines.Add(line);
                        line = "";
                    }
                    line += character;
                }
                lines.Add(line);
            }
            return string.Join("\n", lines);
        }

        internal static int EnteredSector(float z)
        {
            for (int sector = 3; sector > 0; sector--)
                if (z > NetworkSession.SectorBoundary(sector - 1) + .3f) return sector;
            return 0;
        }

        void UpdateManualScreens(PlayerView view)
        {
            int owner = session.IsTestMode ? session.InteractionRoom : view.slot;
            string content = view.phase == 1 ? session.InteractionView(owner).manual : "";
            int stage = owner == 0 ? view.stageB : view.stageA;
            float positionZ = owner == 0 ? view.az : view.bz;
            int entered = session.IsTestMode ? stage : EnteredSector(positionZ);
            if (stage > entered) stage = -1;
            if (manualOwner == owner && manualDisplay == content && manualStage == stage) return;
            manualOwner = owner;
            manualDisplay = content;
            manualStage = stage;
            for (int i = 0; i < manualScreens.Count; i++)
            {
                int sector = Mathf.RoundToInt((manualScreens[i].transform.position.z - 3.57f) / 9f);
                manualText[i] = view.phase == 1 && manualRooms[i] == owner && sector == stage ? FitManual(content) : "?";
                manualDirty[i] = true;
            }
        }

        void ResetManualScreens()
        {
            if (manualOwner == -1) return;
            manualOwner = -1;
            manualStage = -1;
            manualDisplay = null;
            for (int i = 0; i < manualText.Count; i++)
            {
                manualText[i] = "?";
                manualDirty[i] = true;
            }
        }

        void DrawManualScreens()
        {
            if (Event.current.type != EventType.Repaint) return;
            for (int i = 0; i < manualTextures.Count; i++)
            {
                if (!manualDirty[i]) continue;
                PaintManual(manualTextures[i], manualText[i], manualText[i] != "?");
                manualDirty[i] = false;
            }
        }

        void PaintManual(RenderTexture texture, string text, bool readable)
        {
            var old = RenderTexture.active;
            RenderTexture.active = texture;
            GL.Clear(true, true, new Color(.025f, .035f, .045f));
            GL.PushMatrix();
            GL.LoadPixelMatrix(0, texture.width, texture.height, 0);
            var oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.identity;
            manualStyle.font = manualFont;
            manualStyle.fontSize = readable ? 52 : 180;
manualStyle.fontStyle = readable ? FontStyle.Bold : FontStyle.Normal;
            manualStyle.normal.textColor = Color.white;
            manualStyle.alignment = readable ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter;
            manualStyle.wordWrap = false;
            manualStyle.richText = false;
            GUI.Label(new Rect(32, 20, texture.width - 64, texture.height - 40), text, manualStyle);
            GUI.matrix = oldMatrix;
            GL.PopMatrix();
            RenderTexture.active = old;
        }

        void DisposeManualScreens()
        {
            foreach (var material in manualMaterials) if (material) Destroy(material);
            foreach (var texture in manualTextures) if (texture) { texture.Release(); Destroy(texture); }
            if (manualFont) Destroy(manualFont);
        }
    

System.Collections.IEnumerator InitializeManualScreens()
{
    // CellSequence builds the repeated rooms during Start. Wait one frame so every room TV exists before discovery.
    yield return null;
    StartManualScreens();
}
}
}
