using System.Collections.Generic;
using UnityEngine;
using ArenaSurvivor.Core;

namespace ArenaSurvivor.Player
{
    public class WaypointManager : MonoBehaviour
    {
        public static WaypointManager Instance { get; private set; }

        [Header("Waypoints")]
        [SerializeField] private List<Transform> waypoints = new();
        [SerializeField] private float waypointReachDistance = 0.5f;

        [Header("VR Settings")]
        [SerializeField] private float teleportCooldown = 0.5f;
        [SerializeField] private LineRenderer pathPreview;
        [SerializeField] private Material pathMaterial;

        private int currentWaypointIndex = 0;
        private float lastTeleportTime;
        private Transform playerTransform;
        private InputService inputService;

        public event System.Action<int> OnWaypointChanged;
        public event System.Action<Vector3> OnPlayerTeleported;

        public int CurrentWaypointIndex => currentWaypointIndex;
        public int WaypointCount => waypoints.Count;
        public Transform CurrentWaypoint => currentWaypointIndex < waypoints.Count ? waypoints[currentWaypointIndex] : null;
        public Transform NextWaypoint => currentWaypointIndex + 1 < waypoints.Count ? waypoints[currentWaypointIndex + 1] : null;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) playerTransform = player.transform;
            inputService = InputService.Instance ?? FindAnyObjectByType<InputService>();

            if (pathPreview != null)
            {
                pathPreview.positionCount = 0;
                pathPreview.enabled = false;
            }
        }

        private void Update()
        {
            if (playerTransform == null || inputService == null) return;

            HandleWaypointNavigation();
            UpdatePathPreview();
        }

        private void HandleWaypointNavigation()
        {
            if (Time.time - lastTeleportTime < teleportCooldown) return;

            bool moveForward = inputService.MoveInput.y > 0.5f;
            bool moveBackward = inputService.MoveInput.y < -0.5f;

            if (moveForward && currentWaypointIndex < waypoints.Count - 1)
            {
                TeleportToWaypoint(currentWaypointIndex + 1);
            }
            else if (moveBackward && currentWaypointIndex > 0)
            {
                TeleportToWaypoint(currentWaypointIndex - 1);
            }
        }

        public void TeleportToWaypoint(int index)
        {
            if (index < 0 || index >= waypoints.Count || playerTransform == null) return;

            Vector3 targetPos = waypoints[index].position;
            targetPos.y = playerTransform.position.y;

            playerTransform.position = targetPos;
            currentWaypointIndex = index;
            lastTeleportTime = Time.time;

            OnWaypointChanged?.Invoke(currentWaypointIndex);
            OnPlayerTeleported?.Invoke(targetPos);

            EventBus.Publish(new PlayerTeleportedEvent { Position = targetPos, WaypointIndex = index });
        }

        public void RegisterWaypoint(Transform waypoint)
        {
            if (!waypoints.Contains(waypoint))
                waypoints.Add(waypoint);
        }

        public void UnregisterWaypoint(Transform waypoint)
        {
            waypoints.Remove(waypoint);
        }

        public Vector3 GetWaypointPosition(int index)
        {
            if (index >= 0 && index < waypoints.Count)
                return waypoints[index].position;
            return Vector3.zero;
        }

        private void UpdatePathPreview()
        {
            if (pathPreview == null || CurrentWaypoint == null) return;

            if (NextWaypoint != null)
            {
                pathPreview.enabled = true;
                pathPreview.positionCount = 2;
                pathPreview.SetPosition(0, playerTransform.position + Vector3.up * 0.1f);
                pathPreview.SetPosition(1, NextWaypoint.position + Vector3.up * 0.1f);
            }
            else
            {
                pathPreview.enabled = false;
            }
        }

        public void SetWaypoints(List<Transform> newWaypoints)
        {
            waypoints = newWaypoints;
            currentWaypointIndex = Mathf.Clamp(currentWaypointIndex, 0, waypoints.Count - 1);
        }

        private void OnDrawGizmos()
        {
            if (waypoints == null || waypoints.Count < 2) return;

            Gizmos.color = Color.cyan;
            for (int i = 0; i < waypoints.Count - 1; i++)
            {
                if (waypoints[i] != null && waypoints[i + 1] != null)
                {
                    Gizmos.DrawLine(waypoints[i].position, waypoints[i + 1].position);
                    Gizmos.DrawWireSphere(waypoints[i].position, 0.5f);
                }
            }
            if (waypoints.Count > 0 && waypoints[waypoints.Count - 1] != null)
                Gizmos.DrawWireSphere(waypoints[waypoints.Count - 1].position, 0.5f);
        }
    }

    public struct PlayerTeleportedEvent
    {
        public Vector3 Position;
        public int WaypointIndex;
    }
}