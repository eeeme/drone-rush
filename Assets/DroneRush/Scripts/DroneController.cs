using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DroneRush
{
    /// <summary>
    /// アーケード寄りのドローン操縦（モード2配置）。
    ///   左スティック : 上下 = 上昇/下降、左右 = 旋回（ヨー）
    ///   右スティック : 上下 = 前進/後退、左右 = 横移動
    /// 物理ボディは常に水平を保ち（回転はコードで制御）、見た目のモデルだけを傾けて「それっぽさ」を出す。
    /// スティックを離すとその場でホバリングする。
    /// PCでのテスト用にキーボードも使える（WASD = 左スティック、矢印キー = 右スティック）。
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class DroneController : MonoBehaviour
    {
        [Header("入力")]
        public VirtualJoystick leftStick;
        public VirtualJoystick rightStick;

        [Header("飛行性能")]
        public float maxSpeed = 18f;        // 水平最高速度 m/s
        public float climbSpeed = 8f;       // 上昇/下降速度 m/s
        public float yawSpeed = 120f;       // 旋回速度 度/s
        public float acceleration = 4f;     // 目標速度への追従の速さ（大きいほどキビキビ）
        public float maxAltitude = 80f;
        public float areaRadius = 160f;     // この半径の外に出ると押し戻される

        [Header("見た目")]
        public Transform body;              // 傾ける見た目用のモデル
        public float maxTilt = 25f;
        public Transform[] propellers;
        public float propellerSpeed = 1800f;

        /// <summary>false の間は入力を無視してホバリング（カウントダウン中など）</summary>
        public bool controlEnabled = true;

        public float Speed => Velocity.magnitude;

        Rigidbody rb;
        float yaw;
        Vector2 lastLeft, lastRight;

        Vector3 Velocity
        {
#if UNITY_6000_0_OR_NEWER
            get => rb.linearVelocity;
            set => rb.linearVelocity = value;
#else
            get => rb.velocity;
            set => rb.velocity = value;
#endif
        }

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.useGravity = false;          // 重力の代わりに速度を直接制御する（ホバリングが安定する）
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            yaw = transform.eulerAngles.y;
        }

        public void ResetTo(Vector3 position, float yawDegrees)
        {
            yaw = yawDegrees;
            rb.position = position;
            rb.rotation = Quaternion.Euler(0f, yaw, 0f);
            transform.SetPositionAndRotation(position, rb.rotation);
            Velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        void Update()
        {
            ReadInput(out lastLeft, out lastRight);

            if (body)
            {
                // 進行方向に機体を傾ける（見た目だけ）
                Quaternion tilt = Quaternion.Euler(lastRight.y * maxTilt, 0f, -lastRight.x * maxTilt);
                body.localRotation = Quaternion.Slerp(body.localRotation, tilt, 1f - Mathf.Exp(-8f * Time.deltaTime));
            }

            if (propellers != null)
            {
                float spin = propellerSpeed * (0.6f + 0.4f * Mathf.Abs(lastLeft.y)) * Time.deltaTime;
                foreach (var p in propellers)
                    if (p) p.Rotate(0f, spin, 0f, Space.Self);
            }
        }

        void FixedUpdate()
        {
            Vector2 l = controlEnabled ? lastLeft : Vector2.zero;
            Vector2 r = controlEnabled ? lastRight : Vector2.zero;

            yaw += l.x * yawSpeed * Time.fixedDeltaTime;
            rb.MoveRotation(Quaternion.Euler(0f, yaw, 0f));

            Quaternion yawRot = Quaternion.Euler(0f, yaw, 0f);
            Vector3 target = yawRot * new Vector3(r.x, 0f, r.y) * maxSpeed;
            target.y = l.y * climbSpeed;

            // 高度上限と飛行エリアの境界
            Vector3 pos = rb.position;
            if (pos.y > maxAltitude && target.y > 0f) target.y = 0f;
            Vector2 flat = new Vector2(pos.x, pos.z);
            if (flat.magnitude > areaRadius)
            {
                Vector2 back = -flat.normalized * maxSpeed * 0.5f;
                target.x = back.x;
                target.z = back.y;
            }

            Velocity = Vector3.Lerp(Velocity, target, 1f - Mathf.Exp(-acceleration * Time.fixedDeltaTime));
        }

        void ReadInput(out Vector2 left, out Vector2 right)
        {
            left = leftStick ? leftStick.Value : Vector2.zero;
            right = rightStick ? rightStick.Value : Vector2.zero;

            Vector2 kl = Vector2.zero, kr = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                kl = new Vector2(Axis(kb.dKey.isPressed, kb.aKey.isPressed), Axis(kb.wKey.isPressed, kb.sKey.isPressed));
                kr = new Vector2(Axis(kb.rightArrowKey.isPressed, kb.leftArrowKey.isPressed), Axis(kb.upArrowKey.isPressed, kb.downArrowKey.isPressed));
            }
#else
            kl = new Vector2(Axis(Input.GetKey(KeyCode.D), Input.GetKey(KeyCode.A)), Axis(Input.GetKey(KeyCode.W), Input.GetKey(KeyCode.S)));
            kr = new Vector2(Axis(Input.GetKey(KeyCode.RightArrow), Input.GetKey(KeyCode.LeftArrow)), Axis(Input.GetKey(KeyCode.UpArrow), Input.GetKey(KeyCode.DownArrow)));
#endif
            if (kl != Vector2.zero) left = kl;
            if (kr != Vector2.zero) right = kr;
        }

        static float Axis(bool pos, bool neg) => (pos ? 1f : 0f) - (neg ? 1f : 0f);

        /// <summary>建物などに勢いよくぶつかったときに発火（引数は衝突の強さ）</summary>
        public event System.Action<float> Crashed;
        public float crashThreshold = 6f;
        float lastCrashTime = -10f;

        void OnCollisionEnter(Collision collision)
        {
            float impact = collision.relativeVelocity.magnitude;
            if (impact < crashThreshold || Time.time - lastCrashTime < 1f) return;
            // 地面や屋上への着地（上向きの面）は激突扱いしない
            if (collision.GetContact(0).normal.y > 0.7f) return;
            lastCrashTime = Time.time;
            // 跳ね返って少し減速
            Velocity = Vector3.Reflect(Velocity, collision.GetContact(0).normal) * 0.3f;
            Crashed?.Invoke(impact);
        }
    }
}
