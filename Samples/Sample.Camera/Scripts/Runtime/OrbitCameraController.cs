using UnityEngine;
using UnityEngine.EventSystems;

namespace UnityRFramework.Sample.CameraTools
{
    /// <summary>
    /// 面向查看器和三维演示的轻量环绕相机控制器。
    /// </summary>
    public sealed class OrbitCameraController : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private float distance = 5f;
        [SerializeField] private Vector2 distanceRange = new Vector2(1f, 20f);
        [SerializeField] private Vector2 verticalAngleRange = new Vector2(-80f, 80f);
        [SerializeField] private float rotateSpeed = 0.2f;
        [SerializeField] private float panSpeed = 0.01f;
        [SerializeField] private float zoomSpeed = 0.5f;

        private Vector2 _angles;
        private Vector3 _initialTargetPosition;
        private Vector2 _initialAngles;
        private float _initialDistance;

        public Transform Target { get => target; set => target = value; }
        public float Distance => distance;

        private void Awake()
        {
            distanceRange.x = Mathf.Min(distanceRange.x, distanceRange.y);
            distance = Mathf.Clamp(distance, distanceRange.x, distanceRange.y);
            _angles = new Vector2(transform.eulerAngles.y, NormalizeAngle(transform.eulerAngles.x));
            _initialAngles = _angles;
            _initialDistance = distance;
            _initialTargetPosition = target != null ? target.position : Vector3.zero;
            UpdateTransform();
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            if (Input.touchCount > 0)
            {
                HandleTouch();
            }
            else
            {
                HandleMouse();
            }

            UpdateTransform();
        }

        public void Focus(Transform newTarget, float newDistance = -1f)
        {
            target = newTarget;
            if (newDistance > 0f)
            {
                distance = Mathf.Clamp(newDistance, distanceRange.x, distanceRange.y);
            }

            UpdateTransform();
        }

        public void ResetView()
        {
            if (target != null)
            {
                target.position = _initialTargetPosition;
            }

            _angles = _initialAngles;
            distance = _initialDistance;
            UpdateTransform();
        }

        private void HandleMouse()
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            if (Input.GetMouseButton(0))
            {
                _angles.x += Input.GetAxis("Mouse X") * rotateSpeed * 100f * Time.deltaTime;
                _angles.y = Mathf.Clamp(_angles.y - Input.GetAxis("Mouse Y") * rotateSpeed * 100f * Time.deltaTime,
                    verticalAngleRange.x, verticalAngleRange.y);
            }

            if (Input.GetMouseButton(1))
            {
                Pan(new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")));
            }

            distance = Mathf.Clamp(distance - Input.GetAxis("Mouse ScrollWheel") * zoomSpeed * 10f,
                distanceRange.x, distanceRange.y);
        }

        private void HandleTouch()
        {
            if (Input.touchCount == 1)
            {
                Touch touch = Input.GetTouch(0);
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(touch.fingerId))
                {
                    return;
                }

                if (touch.phase == TouchPhase.Moved)
                {
                    _angles.x += touch.deltaPosition.x * rotateSpeed;
                    _angles.y = Mathf.Clamp(_angles.y - touch.deltaPosition.y * rotateSpeed,
                        verticalAngleRange.x, verticalAngleRange.y);
                }
                return;
            }

            Touch first = Input.GetTouch(0);
            Touch second = Input.GetTouch(1);
            float previousDistance = (first.position - first.deltaPosition - (second.position - second.deltaPosition)).magnitude;
            float currentDistance = (first.position - second.position).magnitude;
            distance = Mathf.Clamp(distance - (currentDistance - previousDistance) * zoomSpeed * 0.01f,
                distanceRange.x, distanceRange.y);
        }

        private void Pan(Vector2 input)
        {
            target.position += (-transform.right * input.x - transform.up * input.y) * panSpeed;
        }

        private void UpdateTransform()
        {
            if (target == null)
            {
                return;
            }

            Quaternion rotation = Quaternion.Euler(_angles.y, _angles.x, 0f);
            transform.position = target.position + rotation * Vector3.back * distance;
            transform.rotation = rotation;
        }

        private static float NormalizeAngle(float angle)
        {
            angle %= 360f;
            return angle > 180f ? angle - 360f : angle;
        }
    }
}
