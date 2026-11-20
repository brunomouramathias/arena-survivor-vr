using UnityEngine;

namespace ArenaSurvivor.Player
{
    public class Waypoint : MonoBehaviour
    {
        [Header("Waypoint Settings")]
        [SerializeField] private int waypointIndex = -1;
        [SerializeField] private bool autoRegister = true;

        private void Awake()
        {
            if (autoRegister && WaypointManager.Instance != null)
            {
                WaypointManager.Instance.RegisterWaypoint(transform);
            }
        }

        private void OnDestroy()
        {
            if (WaypointManager.Instance != null)
            {
                WaypointManager.Instance.UnregisterWaypoint(transform);
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, 0.5f);
            
            #if UNITY_EDITOR
            UnityEditor.Handles.Label(transform.position + Vector3.up * 0.7f, $"Waypoint {waypointIndex}");
            #endif
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 1f);
        }
    }
}