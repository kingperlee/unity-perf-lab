using UnityEngine;

namespace PerfLab.Samples.MassHud
{
    [DefaultExecutionOrder(-100)]
    internal sealed class MassHudCameraController : MonoBehaviour
    {
        private const float MoveSpeed = 12f;
        private const float FastMultiplier = 3f;
        private const float MouseSensitivity = 2.5f;
        private Vector3 _initialPosition;
        private Quaternion _initialRotation;
        private float _yaw;
        private float _pitch;

        public void CaptureInitialPose()
        {
            _initialPosition = transform.position;
            _initialRotation = transform.rotation;
            Vector3 angles = transform.eulerAngles;
            _yaw = angles.y;
            _pitch = NormalizeAngle(angles.x);
        }

        private void Update()
        {
            if (Input.GetMouseButton(1))
            {
                _yaw += Input.GetAxisRaw("Mouse X") * MouseSensitivity;
                _pitch -= Input.GetAxisRaw("Mouse Y") * MouseSensitivity;
                _pitch = Mathf.Clamp(_pitch, -85f, 85f);
                transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            }

            Vector3 movement = Vector3.zero;
            if (Input.GetKey(KeyCode.W)) movement += transform.forward;
            if (Input.GetKey(KeyCode.S)) movement -= transform.forward;
            if (Input.GetKey(KeyCode.D)) movement += transform.right;
            if (Input.GetKey(KeyCode.A)) movement -= transform.right;
            if (Input.GetKey(KeyCode.Space)) movement += Vector3.up;
            if (Input.GetKey(KeyCode.Q)) movement -= Vector3.up;

            float speed = MoveSpeed;
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                speed *= FastMultiplier;
            if (movement.sqrMagnitude > 1f) movement.Normalize();
            transform.position += movement * speed * Time.unscaledDeltaTime;

            if (Input.GetKeyDown(KeyCode.R))
            {
                transform.position = _initialPosition;
                transform.rotation = _initialRotation;
                Vector3 angles = transform.eulerAngles;
                _yaw = angles.y;
                _pitch = NormalizeAngle(angles.x);
            }
        }

        private static float NormalizeAngle(float angle)
        {
            return angle > 180f ? angle - 360f : angle;
        }
    }
}
