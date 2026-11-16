using UnityEngine;
using UnityEngine.InputSystem;

namespace TickToTalk
{
    public sealed partial class PrisonPresentation
    {
        bool emoteMenuOpen;
        int suppressLookFrames;
        Texture2D emoteWheelTexture;
        const float EmoteWheelRadius = 205f;
        const float EmoteWheelCenterRadius = 67f;
        static readonly Vector2 EmoteWheelCenter = new Vector2(640f, 360f);
        static readonly string[] EmoteNames = { "손 흔들기", "환호", "엄지 아래로" };
        static readonly string[] EmoteIcons = { "1", "2", "3" };

        public static int EmoteWheelSelection(Vector2 point)
        {
            Vector2 offset = point - EmoteWheelCenter;
            float distance = offset.magnitude;
            if (distance < EmoteWheelCenterRadius || distance > EmoteWheelRadius) return 0;
            float angle = Mathf.Atan2(offset.x, -offset.y) * Mathf.Rad2Deg;
            if (angle < 0) angle += 360f;
            return 1 + Mathf.FloorToInt((angle + 60f) % 360f / 120f);
        }

        Vector2 EmotePointer()
        {
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            Vector2 mouse = Mouse.current.position.ReadValue();
            return new Vector2((mouse.x - (Screen.width - 1280f * scale) * .5f) / scale,
                (Screen.height - mouse.y - (Screen.height - 720f * scale) * .5f) / scale);
        }

        void EnsureEmoteWheel()
        {
            if (emoteWheelTexture) return;
            const int size = 512;
            emoteWheelTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            { name = "Emote selection wheel", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                Vector2 offset = new Vector2(x + .5f - size * .5f, y + .5f - size * .5f);
                float radius = offset.magnitude;
                float outer = Mathf.Clamp01((EmoteWheelRadius * size / 440f - radius) / 2f);
                float inner = Mathf.Clamp01((radius - EmoteWheelCenterRadius * size / 440f) / 2f);
                if (outer * inner <= 0) continue;
                float angle = Mathf.Atan2(offset.x, offset.y) * Mathf.Rad2Deg;
                if (angle < 0) angle += 360f;
                int segment = 1 + Mathf.FloorToInt((angle + 60f) % 360f / 120f);
                float seam = Mathf.Abs((angle + 60f) % 120f);
                var color = segment == 1 ? new Color(.14f, .34f, .42f) : segment == 2 ?
                    new Color(.32f, .23f, .4f) : new Color(.37f, .28f, .18f);
                if (seam < 1.2f || seam > 118.8f) color = new Color(.07f, .09f, .12f);
                color.a = outer * inner * .94f;
                pixels[y * size + x] = color;
            }
            emoteWheelTexture.SetPixels32(pixels);
            emoteWheelTexture.Apply(false, true);
        }

        void UpdateEmoteMenu()
        {
            if (emoteMenuOpen && (session.View == null || session.View.phase != 1)) CloseEmoteMenu();
            var keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.backquoteKey.wasPressedThisFrame || session.View == null || session.View.phase != 1 || SettingsOpen || WardrobeOpen || TabletOpen || (!emoteMenuOpen && panel != "")) return;
            if (emoteMenuOpen) CloseEmoteMenu();
            else
            {
                suppressLookFrames = 2;
                emoteMenuOpen = true;
                session.Command("move");
                session.SetTestVertical(0);
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        void CloseEmoteMenu()
        {
            if (!emoteMenuOpen) return;
            emoteMenuOpen = false;
            suppressLookFrames = 2;
            if (session.View != null && session.View.phase == 1 && !SettingsOpen && !WardrobeOpen && !TabletOpen)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        void DrawEmoteMenu()
        {
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                int choice = EmoteWheelSelection(Event.current.mousePosition);
                if (choice != 0) { ChooseEmote(choice); Event.current.Use(); return; }
            }
            EnsureEmoteWheel();
            var previous = GUI.color;
            GUI.color = new Color(.025f, .035f, .05f, .62f);
            GUI.DrawTexture(new Rect(0, 0, 1280, 720), Texture2D.whiteTexture);
            GUI.color = Color.white;
            var wheelRect = new Rect(EmoteWheelCenter.x - 220, EmoteWheelCenter.y - 220, 440, 440);
            GUI.DrawTexture(wheelRect, emoteWheelTexture);
            int selected = Mouse.current != null ? EmoteWheelSelection(EmotePointer()) : 0;
            if (selected != 0)
            {
                float angle = (selected - 1) * 120f * Mathf.Deg2Rad;
                Vector2 marker = EmoteWheelCenter + new Vector2(Mathf.Sin(angle), -Mathf.Cos(angle)) * 152f;
                GUI.color = new Color(.78f, .95f, 1f, .25f);
                GUI.DrawTexture(new Rect(marker.x - 62, marker.y - 39, 124, 78), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }
            var iconStyle = new GUIStyle(title) { alignment = TextAnchor.MiddleCenter, fontSize = 32 };
            var nameStyle = new GUIStyle(body) { alignment = TextAnchor.MiddleCenter, fontSize = 18 };
            for (int i = 0; i < 3; i++)
            {
                float angle = i * 120f * Mathf.Deg2Rad;
                Vector2 position = EmoteWheelCenter + new Vector2(Mathf.Sin(angle), -Mathf.Cos(angle)) * 150f;
                Label(position.x - 65, position.y - 43, 130, 45, EmoteIcons[i], iconStyle);
                Label(position.x - 78, position.y + 3, 156, 35, EmoteNames[i], nameStyle);
            }
            Label(EmoteWheelCenter.x - 62, EmoteWheelCenter.y - 32, 124, 40, "감정표현", nameStyle);
            GUI.color = previous;
        }

        void ChooseEmote(int selected)
        {
            session.Command("emote", selected.ToString());
            CloseEmoteMenu();
        }

        void DisposeEmoteMenu()
        {
            if (!emoteWheelTexture) return;
            if (Application.isPlaying) Destroy(emoteWheelTexture);
            else DestroyImmediate(emoteWheelTexture);
            emoteWheelTexture = null;
        }
    }
}
