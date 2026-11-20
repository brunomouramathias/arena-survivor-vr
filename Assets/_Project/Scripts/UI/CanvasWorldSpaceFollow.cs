using UnityEngine;

namespace ArenaSurvivor.UI
{
    public class CanvasWorldSpaceFollow : MonoBehaviour
    {
        [Header("Target")]
        public Camera targetCamera;
        [SerializeField] private Transform targetTransform;

        [Header("Positioning")]
        public Vector3 offset = new Vector3(0f, 0.2f, 1.5f);
        [SerializeField] private float followSpeed = 10f;
        [SerializeField] private float rotationSpeed = 5f;
        public bool followPosition = true;
        public bool followRotation = true;
        public bool onlyYawRotation = true;

        [Header("Distance Constraints")]
        [SerializeField] private float minDistance = 1f;
        [SerializeField] private float maxDistance = 3f;

        private void Start()
        {
            if (targetCamera == null)
                targetCamera = Camera.main;
            
            if (targetTransform == null && targetCamera != null)
                targetTransform = targetCamera.transform;
        }

        private void LateUpdate()
        {
            if (targetTransform == null)
            {
                if (targetCamera != null)
                    targetTransform = targetCamera.transform;
                else
                    return;
            }

            if (followPosition)
            {
                Vector3 targetPos = targetTransform.position + targetTransform.TransformDirection(offset);
                
                // Clamp distance
                float dist = Vector3.Distance(transform.position, targetPos);
                if (dist > maxDistance)
                {
                    targetPos = transform.position + (targetPos - transform.position).normalized * maxDistance;
                }
                else if (dist < minDistance)
                {
                    targetPos = transform.position + (targetPos - transform.position).normalized * minDistance;
                }

                transform.position = Vector3.Lerp(transform.position, targetPos, followSpeed * Time.deltaTime);
            }

            if (followRotation)
            {
                Quaternion targetRot;
                if (onlyYawRotation)
                {
                    Vector3 forward = targetTransform.forward;
                    forward.y = 0f;
                    if (forward.sqrMagnitude > 0.001f)
                        targetRot = Quaternion.LookRotation(forward);
                    else
                        targetRot = transform.rotation;
                }
                else
                {
                    targetRot = targetTransform.rotation;
                }

                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
            }
        }
    }
}