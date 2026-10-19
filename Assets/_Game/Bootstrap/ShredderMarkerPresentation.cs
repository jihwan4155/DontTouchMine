using System.Collections.Generic;
using Game.Client.Match;
using Game.Core.Match;
using UnityEngine;

namespace Game.Bootstrap
{
    internal sealed class ShredderMarkerPresentation
    {
        private readonly INetworkMatchHudView view;
        private readonly List<Transform> shredders = new();
        private Camera worldCamera;
        private static readonly Vector3 ShredderMarkerWorldOffset = Vector3.up * 1.5f;
        public ShredderMarkerPresentation(INetworkMatchHudView view) => this.view = view;

        public void FindSceneReferences()
        {
            shredders.Clear();
            var interactables = UnityEngine.Object.FindObjectsByType<ShredderInteractable>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            foreach (var interactable in interactables)
            {
                if (interactable != null)
                {
                    shredders.Add(interactable.transform);
                }
            }

            worldCamera = Camera.main;
        }

        private bool TryGetVisibleShredder(Camera camera, out Transform nearest)
        {
            nearest = null;
            var origin = camera.transform.position;
            var bestDistance = float.MaxValue;
            for (var index = shredders.Count - 1; index >= 0; index--)
            {
                var candidate = shredders[index];
                if (candidate == null)
                {
                    shredders.RemoveAt(index);
                    continue;
                }

                var world = candidate.position + ShredderMarkerWorldOffset;
                if (!IsOnScreen(camera.WorldToViewportPoint(world)) ||
                    IsWorldOccluded(origin, world, candidate))
                {
                    continue;
                }

                var distance = (world - origin).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    nearest = candidate;
                }
            }

            return nearest != null;
        }

        public void Update(MatchPhase phase)
        {
            var activePhase = phase == MatchPhase.Hiding ||
                              phase == MatchPhase.Searching;
            if (!activePhase)
            {
                view.SetShredderMarker(default, false);
                return;
            }

            if (shredders.Count == 0 || worldCamera == null)
            {
                FindSceneReferences();
            }

            if (worldCamera == null ||
                !TryGetVisibleShredder(worldCamera, out var shredder))
            {
                view.SetShredderMarker(default, false);
                return;
            }

            view.SetShredderMarker(
                worldCamera.WorldToScreenPoint(shredder.position + ShredderMarkerWorldOffset),
                true);
        }

        /// <summary>
        /// Viewport 좌표가 카메라 앞이고 화면 안일 때만 라벨을 붙인다.
        /// 화면 가장자리에 고정하지 않아서, 다른 방에 있는 파쇄기가 가장자리 마커로 남지 않는다.
        /// </summary>
        internal static bool IsOnScreen(Vector3 viewportPoint) =>
            viewportPoint.z > 0f &&
            viewportPoint.x >= 0f && viewportPoint.x <= 1f &&
            viewportPoint.y >= 0f && viewportPoint.y <= 1f;

        /// <summary>
        /// 카메라에서 파쇄기 위 라벨 지점까지 벽이 가로막으면 숨긴다.
        /// 파쇄기 자신의 콜라이더는 가림으로 보지 않는다.
        /// </summary>
        internal static bool IsWorldOccluded(Vector3 origin, Vector3 target, Transform shredder)
        {
            var toTarget = target - origin;
            var distance = toTarget.magnitude;
            if (distance <= 0.001f)
            {
                return false;
            }

            if (!Physics.Raycast(
                    origin,
                    toTarget / distance,
                    out var hit,
                    distance,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            if (shredder != null &&
                (hit.transform == shredder || hit.transform.IsChildOf(shredder)))
            {
                return false;
            }

            return true;
        }
    }
}
