using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DroneRush
{
    /// <summary>
    /// ゲーム全体の進行。リングを順番にくぐってゴールするまでのタイムアタック。
    ///   Ready（スタート待ち） → Countdown（3,2,1） → Playing → Finished（結果表示・リトライ）
    /// 建物に激突するとペナルティ +2 秒。ベストタイムは端末に保存。
    ///
    /// シーンに何も置かなくても、再生すると自動でこのオブジェクトが生成されて全部組み上がる。
    /// （自分でシーンを作り込むときは autoBootstrap を false に）
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static bool autoBootstrap = true;

        [Header("コース")]
        public int ringCount = 12;
        public int courseSeed = 2026;
        public float crashPenalty = 2f;

        enum State { Ready, Countdown, Playing, Finished }
        State state;

        DroneController drone;
        DroneCamera droneCam;
        Hud hud;
        List<Ring> rings;
        Transform guide;
        int nextRing;
        float elapsed;
        float penaltyShowUntil;

        const string BestKey = "DroneRush.BestTime";
        static readonly Vector3 StartPos = new Vector3(0f, 1.2f, 0f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (!autoBootstrap || FindAnyObjectByType<GameManager>() != null) return;
            new GameObject("DroneRush").AddComponent<GameManager>();
        }

        void Awake()
        {
#if !UNITY_WEBGL
            // WebGL はブラウザの描画タイミングに任せる（ここで指定すると逆にカクつく）
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Screen.autorotateToPortrait = Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = Screen.autorotateToLandscapeRight = true;
            Screen.orientation = ScreenOrientation.AutoRotation;
#endif

            var world = new GameObject("World").transform;
            EnsureLight();

            rings = WorldBuilder.BuildCourse(world, ringCount, courseSeed);
            WorldBuilder.BuildCity(world, rings, courseSeed);
            foreach (var r in rings) r.Passed += OnRingPassed;

            drone = WorldBuilder.BuildDrone(world);
            drone.Crashed += OnCrashed;

            guide = BuildGuideArrow(world);

            hud = Hud.Create();
            drone.leftStick = hud.LeftStick;
            drone.rightStick = hud.RightStick;
            hud.MainButton.onClick.AddListener(OnMainButton);
            hud.CamButton.onClick.AddListener(() => droneCam.Toggle());

            SetupCamera();
            EnterReady();
        }

        void EnsureLight()
        {
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) return;
            var go = new GameObject("Sun");
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.shadows = LightShadows.Soft;
            go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        void SetupCamera()
        {
            var cam = Camera.main;
            if (!cam)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 600f;
            cam.fieldOfView = 65f;
            droneCam = cam.GetComponent<DroneCamera>();
            if (!droneCam) droneCam = cam.gameObject.AddComponent<DroneCamera>();
            droneCam.target = drone.transform;
        }

        Transform BuildGuideArrow(Transform parent)
        {
            var root = new GameObject("GuideArrow").transform;
            root.SetParent(parent, false);
            var mat = WorldBuilder.Mat(new Color(1f, 0.55f, 0.05f));
            for (int i = 0; i < 3; i++)
            {
                // 「>」形を3つ並べたシェブロン
                for (int s = -1; s <= 1; s += 2)
                {
                    var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    DestroyImmediate(bar.GetComponent<Collider>());
                    bar.GetComponent<Renderer>().sharedMaterial = mat;
                    bar.transform.SetParent(root, false);
                    bar.transform.localPosition = new Vector3(s * 0.12f, 0f, i * 0.35f);
                    bar.transform.localRotation = Quaternion.Euler(0f, -s * 40f, 0f);
                    bar.transform.localScale = new Vector3(0.06f, 0.06f, 0.35f);
                }
            }
            return root;
        }

        // ---------------------------------------------------------------- 状態遷移

        void EnterReady()
        {
            state = State.Ready;
            nextRing = 0;
            elapsed = 0f;
            for (int i = 0; i < rings.Count; i++) rings[i].SetState(i == 0, false);

            Vector3 toFirst = rings[0].transform.position - StartPos;
            drone.ResetTo(StartPos, Mathf.Atan2(toFirst.x, toFirst.z) * Mathf.Rad2Deg);
            drone.controlEnabled = false;
            droneCam.Snap();

            hud.CenterText.text = "DRONE RUSH";
            hud.SubText.text = $"Fly through {rings.Count} rings in order.\nLeft stick: altitude & turn   Right stick: move";
            hud.MainButtonLabel.text = "START";
            hud.MainButton.gameObject.SetActive(true);
            UpdateHud();
        }

        void OnMainButton()
        {
            if (state == State.Ready) StartCoroutine(Countdown());
            else if (state == State.Finished) EnterReady();
        }

        IEnumerator Countdown()
        {
            state = State.Countdown;
            hud.MainButton.gameObject.SetActive(false);
            hud.SubText.text = "";
            for (int i = 3; i > 0; i--)
            {
                hud.CenterText.text = i.ToString();
                yield return new WaitForSeconds(0.8f);
            }
            hud.CenterText.text = "GO!";
            state = State.Playing;
            drone.controlEnabled = true;
            yield return new WaitForSeconds(0.7f);
            if (state == State.Playing) hud.CenterText.text = "";
        }

        void OnRingPassed(Ring ring)
        {
            if (state != State.Playing || ring.index != nextRing) return;
            ring.SetState(false, true);
            nextRing++;
            if (nextRing < rings.Count) rings[nextRing].SetState(true, false);
            else Finish();
        }

        void OnCrashed(float impact)
        {
            if (state != State.Playing) return;
            elapsed += crashPenalty;
            penaltyShowUntil = Time.time + 1.2f;
#if UNITY_ANDROID || UNITY_IOS
            Handheld.Vibrate();
#endif
        }

        void Finish()
        {
            state = State.Finished;
            drone.controlEnabled = false;

            float best = PlayerPrefs.GetFloat(BestKey, 0f);
            bool record = best <= 0f || elapsed < best;
            if (record)
            {
                PlayerPrefs.SetFloat(BestKey, elapsed);
                PlayerPrefs.Save();
            }

            hud.CenterText.text = record ? "NEW RECORD!" : "FINISH!";
            hud.SubText.text = $"Time  {Format(elapsed)}" + (record ? "" : $"\nBest  {Format(best)}");
            hud.MainButtonLabel.text = "RETRY";
            hud.MainButton.gameObject.SetActive(true);
            UpdateHud();
        }

        // ---------------------------------------------------------------- 毎フレーム

        void Update()
        {
            if (state == State.Playing) elapsed += Time.deltaTime;
            UpdateHud();
            UpdateGuide();
        }

        void UpdateHud()
        {
            hud.TimerText.text = Format(elapsed);
            hud.RingText.text = $"RING {Mathf.Min(nextRing, rings.Count)}/{rings.Count}";
            float best = PlayerPrefs.GetFloat(BestKey, 0f);
            hud.BestText.text = best > 0f ? $"BEST {Format(best)}" : "";
            hud.PenaltyText.text = Time.time < penaltyShowUntil ? $"CRASH +{crashPenalty:0.0}s" : "";
        }

        void UpdateGuide()
        {
            bool show = state != State.Finished && nextRing < rings.Count;
            guide.gameObject.SetActive(show);
            if (!show) return;

            Transform d = drone.transform;
            Vector3 anchor = droneCam.IsFpv
                ? d.position + d.forward * 2.5f + Vector3.down * 0.7f
                : d.position + Vector3.up * 1.1f;
            guide.position = anchor;
            Vector3 dir = rings[nextRing].transform.position - anchor;
            if (dir.sqrMagnitude > 0.01f) guide.rotation = Quaternion.LookRotation(dir);
        }

        static string Format(float t)
        {
            int m = (int)(t / 60f);
            return $"{m}:{t - m * 60f:00.00}";
        }
    }
}
