using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DroneRush
{
    /// <summary>
    /// 画面UI（uGUI）をコードで組み立てる。左右のバーチャルスティック、タイマー、リング数、
    /// カメラ切替ボタン、中央メッセージ、スタート/リトライボタン。
    /// 1920x1080 基準で自動スケールし、ノッチ等のセーフエリアにも合わせる。
    /// </summary>
    public class Hud : MonoBehaviour
    {
        public VirtualJoystick LeftStick { get; private set; }
        public VirtualJoystick RightStick { get; private set; }
        public Text TimerText { get; private set; }
        public Text RingText { get; private set; }
        public Text BestText { get; private set; }
        public Text CenterText { get; private set; }
        public Text SubText { get; private set; }
        public Text PenaltyText { get; private set; }
        public Button MainButton { get; private set; }
        public Text MainButtonLabel { get; private set; }
        public Button CamButton { get; private set; }

        Font font;
        Sprite circle;
        CanvasScaler scaler;
        GameObject rotateHint;
        RectTransform safeArea;
        Rect lastSafe;

        public static Hud Create()
        {
            EnsureEventSystem();

            var go = new GameObject("HUD");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f; // 横長スマホでは高さ基準のほうが崩れにくい
            go.AddComponent<GraphicRaycaster>();

            var hud = go.AddComponent<Hud>();
            hud.scaler = scaler;
            hud.Build();
            return hud;
        }

        static void EnsureEventSystem()
        {
            if (EventSystem.current != null || FindAnyObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            es.AddComponent<StandaloneInputModule>();
#endif
        }

        void Build()
        {
            font = LoadFont();
            circle = MakeCircleSprite(128);

            safeArea = NewRect("SafeArea", (RectTransform)transform);
            Stretch(safeArea);
            ApplySafeArea();

            // --- スティック（画面の左半分・右半分の下側がそれぞれタッチ領域）
            LeftStick = MakeStick("LeftStick", new Vector2(0f, 0f), new Vector2(0.5f, 0.75f), "UP/DOWN · TURN");
            RightStick = MakeStick("RightStick", new Vector2(0.5f, 0f), new Vector2(1f, 0.75f), "MOVE");

            // --- 上部の情報表示
            RingText = MakeText("Rings", safeArea, 48, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(40f, -30f), new Vector2(500f, 70f));
            TimerText = MakeText("Timer", safeArea, 64, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(600f, 80f));
            BestText = MakeText("Best", safeArea, 34, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(600f, 50f));
            PenaltyText = MakeText("Penalty", safeArea, 48, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(600f, 60f));
            PenaltyText.color = new Color(1f, 0.35f, 0.3f);

            CamButton = MakeButton("CamButton", safeArea, "CAM", new Vector2(1f, 1f), new Vector2(-120f, -70f), new Vector2(180f, 90f), out _);

            // --- 中央メッセージとボタン
            CenterText = MakeText("Center", safeArea, 120, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, 140f), new Vector2(1600f, 180f));
            SubText = MakeText("Sub", safeArea, 44, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(1600f, 120f));
            MainButton = MakeButton("MainButton", safeArea, "START", new Vector2(0.5f, 0.5f), new Vector2(0f, -110f), new Vector2(420f, 130f), out var label);
            MainButtonLabel = label;

            // 縦持ちのとき（主にブラウザ版）に横向きを促す
            var hint = MakeText("RotateHint", safeArea, 56, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, 300f));
            hint.text = "Rotate your phone\nto landscape";
            var hintBg = hint.gameObject.AddComponent<Outline>();
            hintBg.effectColor = new Color(0f, 0f, 0f, 0.8f);
            rotateHint = hint.gameObject;
            rotateHint.SetActive(false);
        }

        void Update()
        {
            if (Screen.safeArea != lastSafe) ApplySafeArea();

            bool portrait = Screen.height > Screen.width;
            scaler.matchWidthOrHeight = portrait ? 0f : 1f;
            if (rotateHint.activeSelf != portrait) rotateHint.SetActive(portrait);
        }

        void ApplySafeArea()
        {
            lastSafe = Screen.safeArea;
            if (Screen.width <= 0 || Screen.height <= 0) return;
            safeArea.anchorMin = new Vector2(lastSafe.xMin / Screen.width, lastSafe.yMin / Screen.height);
            safeArea.anchorMax = new Vector2(lastSafe.xMax / Screen.width, lastSafe.yMax / Screen.height);
        }

        // ---------------------------------------------------------------- 部品

        VirtualJoystick MakeStick(string name, Vector2 anchorMin, Vector2 anchorMax, string caption)
        {
            var zone = NewRect(name, safeArea);
            zone.anchorMin = anchorMin;
            zone.anchorMax = anchorMax;
            zone.offsetMin = zone.offsetMax = Vector2.zero;
            var zoneImg = zone.gameObject.AddComponent<Image>();
            zoneImg.color = new Color(0f, 0f, 0f, 0f); // 透明だがタッチは受け取る

            bool isLeft = anchorMin.x < 0.25f;
            var bg = NewRect("Background", zone);
            bg.anchorMin = bg.anchorMax = new Vector2(isLeft ? 0f : 1f, 0f);
            bg.sizeDelta = new Vector2(300f, 300f);
            bg.anchoredPosition = new Vector2(isLeft ? 260f : -260f, 240f);
            var bgImg = bg.gameObject.AddComponent<Image>();
            bgImg.sprite = circle;
            bgImg.color = new Color(1f, 1f, 1f, 0.18f);
            bgImg.raycastTarget = false;

            var knob = NewRect("Knob", bg);
            knob.sizeDelta = new Vector2(130f, 130f);
            var knobImg = knob.gameObject.AddComponent<Image>();
            knobImg.sprite = circle;
            knobImg.color = new Color(1f, 1f, 1f, 0.55f);
            knobImg.raycastTarget = false;

            var cap = MakeText("Caption", bg, 26, TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), new Vector2(0f, -30f), new Vector2(400f, 40f));
            cap.text = caption;
            cap.color = new Color(1f, 1f, 1f, 0.6f);

            var stick = zone.gameObject.AddComponent<VirtualJoystick>();
            stick.background = bg;
            stick.knob = knob;
            return stick;
        }

        Text MakeText(string name, RectTransform parent, int size, TextAnchor align, Vector2 anchor, Vector2 pos, Vector2 box)
        {
            var rt = NewRect(name, parent);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(anchor.x, anchor.y);
            rt.anchoredPosition = pos;
            rt.sizeDelta = box;
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.alignment = align;
            t.color = Color.white;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            var shadow = rt.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
            shadow.effectDistance = new Vector2(3f, -3f);
            return t;
        }

        Button MakeButton(string name, RectTransform parent, string caption, Vector2 anchor, Vector2 pos, Vector2 size, out Text label)
        {
            var rt = NewRect(name, parent);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(1f, 0.55f, 0.05f, 0.92f);
            var btn = rt.gameObject.AddComponent<Button>();

            label = MakeText("Label", rt, Mathf.RoundToInt(size.y * 0.42f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, size);
            label.text = caption;
            return btn;
        }

        static RectTransform NewRect(string name, RectTransform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        static Font LoadFont()
        {
            // Unity 2022.2 以降は LegacyRuntime.ttf、それ以前は Arial.ttf が組み込みフォント
            Font f = null;
            try { f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
            if (!f) { try { f = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { } }
            return f;
        }

        static Sprite MakeCircleSprite(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            float r = size * 0.5f;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                byte a = (byte)(Mathf.Clamp01(r - d) * 255f); // 縁を1pxだけ滑らかに
                px[y * size + x] = new Color32(255, 255, 255, a);
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }
    }
}
