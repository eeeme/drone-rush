using UnityEngine;

namespace DroneRush
{
    /// <summary>
    /// 三人称（後方追従）と FPV（機体カメラ）を切り替えられるカメラ。
    /// 三人称では建物にめり込まないよう、ドローンとの間に障害物があれば手前に寄せる。
    /// </summary>
    public class DroneCamera : MonoBehaviour
    {
        public Transform target;
        public Vector3 chaseOffset = new Vector3(0f, 2.2f, -6.5f);
        public Vector3 fpvOffset = new Vector3(0f, 0.15f, 0.45f);
        public float followSharpness = 6f;
        public float chaseFov = 65f;
        public float fpvFov = 90f;
        public LayerMask obstacleMask = ~0;

        public bool IsFpv { get; private set; }

        Camera cam;

        void Awake()
        {
            cam = GetComponent<Camera>();
        }

        public void Toggle()
        {
            IsFpv = !IsFpv;
            if (cam) cam.fieldOfView = IsFpv ? fpvFov : chaseFov;
            Snap();
        }

        public void Snap()
        {
            if (!target) return;
            transform.SetPositionAndRotation(DesiredPosition(), DesiredRotation());
        }

        void LateUpdate()
        {
            if (!target) return;

            if (IsFpv)
            {
                transform.SetPositionAndRotation(DesiredPosition(), DesiredRotation());
                return;
            }

            float t = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
            Vector3 pos = Vector3.Lerp(transform.position, DesiredPosition(), t);

            // カメラとドローンの間に建物があれば、その手前まで寄る
            Vector3 focus = target.position + Vector3.up * 0.5f;
            Vector3 dir = pos - focus;
            if (Physics.SphereCast(focus, 0.3f, dir.normalized, out RaycastHit hit, dir.magnitude, obstacleMask, QueryTriggerInteraction.Ignore)
                && hit.transform != target)
            {
                pos = focus + dir.normalized * Mathf.Max(hit.distance - 0.2f, 0.8f);
            }

            transform.position = pos;
            transform.rotation = Quaternion.LookRotation(focus + target.forward * 4f - pos, Vector3.up);
        }

        Vector3 DesiredPosition()
        {
            Quaternion yaw = Quaternion.Euler(0f, target.eulerAngles.y, 0f);
            return target.position + yaw * (IsFpv ? fpvOffset : chaseOffset);
        }

        Quaternion DesiredRotation()
        {
            if (IsFpv)
            {
                // FPV は機体の傾きも反映（見た目モデルの回転を使う）
                var drone = target.GetComponent<DroneController>();
                if (drone && drone.body) return drone.body.rotation;
                return target.rotation;
            }
            Vector3 focus = target.position + Vector3.up * 0.5f + target.forward * 4f;
            return Quaternion.LookRotation(focus - DesiredPosition(), Vector3.up);
        }
    }
}
