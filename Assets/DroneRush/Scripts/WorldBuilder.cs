using System.Collections.Generic;
using UnityEngine;

namespace DroneRush
{
    /// <summary>
    /// 街・コース・ドローン本体をコードだけで組み立てる（外部アセット不要）。
    /// 乱数シードが固定なので毎回同じコースになり、ベストタイムを競える。
    /// </summary>
    public static class WorldBuilder
    {
        static Material baseMaterial;

        /// <summary>
        /// 今のレンダーパイプライン（URP / Built-in どちらでも）の既定マテリアルを複製して色を付ける。
        /// Shader.Find を使わないので、ビルド時にシェーダーが抜け落ちる心配がない。
        /// </summary>
        public static Material Mat(Color color)
        {
            if (!baseMaterial)
            {
                var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                baseMaterial = tmp.GetComponent<Renderer>().sharedMaterial;
                Object.Destroy(tmp);
            }
            var m = new Material(baseMaterial) { color = color };
            return m;
        }

        static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 scale, Material mat, bool collider = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        // ---------------------------------------------------------------- コース

        public static List<Ring> BuildCourse(Transform root, int ringCount, int seed)
        {
            var rand = new System.Random(seed);
            float R(float min, float max) => min + (float)rand.NextDouble() * (max - min);

            // 街の中心を一周するループ状に配置
            var points = new List<Vector3>();
            for (int i = 0; i < ringCount; i++)
            {
                float a = (i + 0.5f) / ringCount * Mathf.PI * 2f;
                float radius = (i % 2 == 0) ? R(45f, 70f) : R(80f, 115f);
                points.Add(new Vector3(Mathf.Sin(a) * radius, R(6f, 32f), Mathf.Cos(a) * radius));
            }

            var idle = Mat(new Color(0.85f, 0.85f, 0.9f));
            var next = Mat(new Color(1f, 0.55f, 0.05f));
            var done = Mat(new Color(0.2f, 0.85f, 0.35f));

            var rings = new List<Ring>();
            for (int i = 0; i < ringCount; i++)
            {
                Vector3 from = i == 0 ? Vector3.up * 3f : points[i - 1];
                Vector3 dir = points[i] - from;
                dir.y = 0f;

                var go = new GameObject($"Ring {i + 1}");
                go.transform.SetParent(root, false);
                go.transform.position = points[i];
                go.transform.rotation = Quaternion.LookRotation(dir.sqrMagnitude > 0.01f ? dir : Vector3.forward);

                var ring = go.AddComponent<Ring>();
                ring.index = i;
                ring.Build(3.2f, idle, next, done);
                rings.Add(ring);
            }
            return rings;
        }

        // ---------------------------------------------------------------- 街

        public static void BuildCity(Transform root, IList<Ring> rings, int seed)
        {
            var rand = new System.Random(seed + 1);
            float R(float min, float max) => min + (float)rand.NextDouble() * (max - min);

            // 地面
            var ground = Box("Ground", root, new Vector3(0f, -0.5f, 0f), new Vector3(400f, 1f, 400f), Mat(new Color(0.32f, 0.45f, 0.3f)));
            ground.isStatic = true;

            // 道路っぽい線
            var road = Mat(new Color(0.22f, 0.22f, 0.24f));
            for (int i = -6; i <= 6; i++)
            {
                Box("Road", root, new Vector3(i * 24f - 12f, 0.01f, 0f), new Vector3(6f, 0.02f, 330f), road, false);
                Box("Road", root, new Vector3(0f, 0.01f, i * 24f - 12f), new Vector3(330f, 0.02f, 6f), road, false);
            }

            // スタート地点のヘリパッド
            Box("Pad", root, new Vector3(0f, 0.05f, 0f), new Vector3(6f, 0.1f, 6f), Mat(new Color(0.95f, 0.8f, 0.1f)), false);

            var palette = new[]
            {
                Mat(new Color(0.75f, 0.78f, 0.82f)), Mat(new Color(0.55f, 0.6f, 0.68f)),
                Mat(new Color(0.85f, 0.8f, 0.7f)),   Mat(new Color(0.45f, 0.5f, 0.58f)),
                Mat(new Color(0.65f, 0.72f, 0.8f)),
            };
            var roof = Mat(new Color(0.3f, 0.32f, 0.36f));

            // コースの経路（スタート→各リング）を線分として持っておく
            var path = new List<Vector3> { Vector3.up * 3f };
            foreach (var r in rings) path.Add(r.transform.position);

            for (int gx = -6; gx < 6; gx++)
            for (int gz = -6; gz < 6; gz++)
            {
                // 道路の間の区画（24m間隔）
                Vector3 c = new Vector3(gx * 24f, 0f, gz * 24f);
                if (c.magnitude < 16f) continue; // スタート周辺は空ける

                float w = R(9f, 15f), d = R(9f, 15f), h = R(8f, 48f);
                if (rand.NextDouble() < 0.15) continue; // ところどころ空き地

                // 経路の真下にある建物は、通り道より低くする（くぐれる高さを確保）
                float clearance = ClearHeight(path, c, Mathf.Max(w, d) * 0.5f + 5f);
                if (clearance < float.MaxValue) h = Mathf.Min(h, clearance - 5f);
                if (h < 3f) continue;

                var b = Box("Building", root, c + Vector3.up * h * 0.5f, new Vector3(w, h, d), palette[rand.Next(palette.Length)]);
                b.isStatic = true;
                Box("Roof", b.transform, new Vector3(0f, 0.5f, 0f), new Vector3(1.02f, 0.02f, 1.02f), roof, false);
            }
        }

        /// <summary>点 p から水平距離 margin 以内を通る経路の最低高度（無ければ MaxValue）</summary>
        static float ClearHeight(List<Vector3> path, Vector3 p, float margin)
        {
            float min = float.MaxValue;
            Vector2 q = new Vector2(p.x, p.z);
            for (int i = 0; i < path.Count - 1; i++)
            {
                Vector2 a = new Vector2(path[i].x, path[i].z);
                Vector2 b = new Vector2(path[i + 1].x, path[i + 1].z);
                Vector2 ab = b - a;
                float t = ab.sqrMagnitude < 0.001f ? 0f : Mathf.Clamp01(Vector2.Dot(q - a, ab) / ab.sqrMagnitude);
                if (Vector2.Distance(q, a + ab * t) < margin)
                {
                    float y = Mathf.Lerp(path[i].y, path[i + 1].y, t);
                    min = Mathf.Min(min, Mathf.Min(y, Mathf.Min(path[i].y, path[i + 1].y)));
                }
            }
            return min;
        }

        // ---------------------------------------------------------------- ドローン

        public static DroneController BuildDrone(Transform root)
        {
            var go = new GameObject("Drone");
            go.transform.SetParent(root, false);

            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 1f;
            var col = go.AddComponent<SphereCollider>();
            col.radius = 0.55f;

            var body = new GameObject("Body").transform;
            body.SetParent(go.transform, false);

            var dark = Mat(new Color(0.12f, 0.12f, 0.14f));
            var accent = Mat(new Color(1f, 0.35f, 0.1f));
            var prop = Mat(new Color(0.8f, 0.85f, 0.9f));

            Box("Core", body, Vector3.zero, new Vector3(0.45f, 0.14f, 0.6f), dark, false);
            Box("Nose", body, new Vector3(0f, 0.02f, 0.32f), new Vector3(0.18f, 0.08f, 0.08f), accent, false);

            var propellers = new List<Transform>();
            for (int i = 0; i < 4; i++)
            {
                float a = (45f + i * 90f) * Mathf.Deg2Rad;
                Vector3 tip = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * 0.55f;

                var arm = Box("Arm", body, tip * 0.5f, new Vector3(0.06f, 0.05f, 0.55f), dark, false);
                arm.transform.localRotation = Quaternion.Euler(0f, 45f + i * 90f, 0f);
                Box("Motor", body, tip + Vector3.up * 0.04f, new Vector3(0.1f, 0.1f, 0.1f), i < 2 ? accent : dark, false);

                var p = new GameObject("Propeller").transform;
                p.SetParent(body, false);
                p.localPosition = tip + Vector3.up * 0.1f;
                Box("Blade", p, Vector3.zero, new Vector3(0.5f, 0.01f, 0.06f), prop, false);
                propellers.Add(p);
            }

            var drone = go.AddComponent<DroneController>();
            drone.body = body;
            drone.propellers = propellers.ToArray();
            return drone;
        }
    }
}
