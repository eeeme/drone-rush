using System;
using UnityEngine;

namespace DroneRush
{
    /// <summary>
    /// くぐるリング。中央のトリガーにドローンが入ると、それが「次のリング」なら通過扱い。
    /// 見た目は小さなキューブを円形に並べたもの（外部アセット不要）。
    /// </summary>
    public class Ring : MonoBehaviour
    {
        public int index;
        public event Action<Ring> Passed;

        Renderer[] segments;
        Material idleMat, nextMat, doneMat;
        bool isNext, isDone;
        float pulse;

        public void Build(float radius, Material idle, Material next, Material done)
        {
            idleMat = idle; nextMat = next; doneMat = done;

            const int count = 20;
            segments = new Renderer[count];
            float segLen = 2f * Mathf.PI * radius / count * 1.05f;
            for (int i = 0; i < count; i++)
            {
                float a = i * Mathf.PI * 2f / count;
                var seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                seg.name = "Segment";
                DestroyImmediate(seg.GetComponent<Collider>()); // 枠には当たり判定なし（遊びやすさ優先）
                seg.transform.SetParent(transform, false);
                seg.transform.localPosition = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * radius;
                seg.transform.localRotation = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg + 90f);
                seg.transform.localScale = new Vector3(segLen, 0.35f, 0.35f);
                segments[i] = seg.GetComponent<Renderer>();
            }

            var trigger = gameObject.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(radius * 1.7f, radius * 1.7f, 1.2f);

            SetState(false, false);
        }

        public void SetState(bool next, bool done)
        {
            isNext = next; isDone = done;
            var mat = done ? doneMat : next ? nextMat : idleMat;
            foreach (var r in segments) r.sharedMaterial = mat;
            transform.localScale = Vector3.one;
        }

        void Update()
        {
            if (!isNext) return;
            pulse += Time.deltaTime * 4f;
            transform.localScale = Vector3.one * (1f + Mathf.Sin(pulse) * 0.05f);
        }

        void OnTriggerEnter(Collider other)
        {
            if (!isNext || isDone) return;
            if (other.GetComponentInParent<DroneController>() == null) return;
            Passed?.Invoke(this);
        }
    }
}
