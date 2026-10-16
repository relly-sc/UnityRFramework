using UnityEngine;

namespace UnityRFramework.Runtime
{
    /// <summary>让对象在 LateUpdate 中朝向目标，可按轴锁定。</summary>
    [AddComponentMenu("UnityRFramework/Utility/Billboard")]
    public sealed class Billboard : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private bool reverse;
        [SerializeField] private bool lockX;
        [SerializeField] private bool lockY;
        [SerializeField] private bool lockZ;

        public Transform Target { get => target; set => target = value; }

        private void LateUpdate()
        {
            if (target == null)
            {
                if (Camera.main == null)
                {
                    return;
                }

                target = Camera.main.transform;
            }

            Vector3 direction = reverse
                ? transform.position - target.position
                : target.position - transform.position;
            if (direction.sqrMagnitude <= Mathf.Epsilon)
            {
                return;
            }

            Quaternion rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            Vector3 euler = rotation.eulerAngles;
            Vector3 current = transform.eulerAngles;
            if (lockX) euler.x = current.x;
            if (lockY) euler.y = current.y;
            if (lockZ) euler.z = current.z;
            transform.rotation = Quaternion.Euler(euler);
        }
    }
}
