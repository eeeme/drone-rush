using UnityEngine;
using UnityEngine.EventSystems;

namespace DroneRush
{
    /// <summary>
    /// 画面上のバーチャルスティック（フローティング式）。
    /// このコンポーネントを付けた透明な「タッチ領域」のどこを触っても、その位置にスティックが出現する。
    /// 中心からのずれを -1〜1 の Value として返す。指ごとに pointerId で区別するのでマルチタッチ対応。
    /// </summary>
    public class VirtualJoystick : MonoBehaviour,
        IPointerDownHandler, IDragHandler, IPointerUpHandler, IInitializePotentialDragHandler
    {
        public RectTransform background;   // スティックの外周（タッチ領域の子）
        public RectTransform knob;         // 指に追従するつまみ（background の子）
        public bool floating = true;       // true: 触った位置にスティックを移動する

        public Vector2 Value { get; private set; }

        const int NoPointer = int.MinValue;
        int pointerId = NoPointer;
        Vector2 homePosition;

        void Start()
        {
            if (background) homePosition = background.anchoredPosition;
        }

        public void OnInitializePotentialDrag(PointerEventData eventData)
        {
            // ドラッグ開始のしきい値を無くし、指を置いた瞬間から反応させる
            eventData.useDragThreshold = false;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (pointerId != NoPointer) return;
            pointerId = eventData.pointerId;

            if (floating && background &&
                RectTransformUtility.ScreenPointToWorldPointInRectangle(
                    (RectTransform)background.parent, eventData.position, eventData.pressEventCamera, out Vector3 world))
            {
                background.position = world;
            }
            UpdateValue(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != pointerId) return;
            UpdateValue(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != pointerId) return;
            ResetStick();
        }

        void OnDisable()
        {
            ResetStick();
        }

        void ResetStick()
        {
            pointerId = NoPointer;
            Value = Vector2.zero;
            if (knob) knob.anchoredPosition = Vector2.zero;
            if (floating && background) background.anchoredPosition = homePosition;
        }

        void UpdateValue(PointerEventData eventData)
        {
            if (!background) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    background, eventData.position, eventData.pressEventCamera, out Vector2 local))
                return;

            float radius = background.rect.width * 0.5f;
            if (radius <= 0f) return;

            Vector2 v = Vector2.ClampMagnitude(local / radius, 1f);
            // 中心付近の小さなブレを無視（デッドゾーン）
            float m = v.magnitude;
            Value = m < 0.08f ? Vector2.zero : v * ((m - 0.08f) / (0.92f * m));
            if (knob) knob.anchoredPosition = v * radius;
        }
    }
}
