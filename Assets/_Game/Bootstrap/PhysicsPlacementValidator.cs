using Game.Core.Match;
using System;
using System.Collections.Generic;
using Game.Client.Interactions;
using Game.Server.Items;
using UnityEngine;

namespace Game.Bootstrap
{
    public readonly struct PlacementVolume
    {
        public PlacementVolume(string objectId, Vector3 centerOffset, Vector3 halfExtents)
        {
            if (string.IsNullOrWhiteSpace(objectId))
            {
                throw new ArgumentException("Object id is required.", nameof(objectId));
            }

            if (!IsFinite(centerOffset) ||
                !IsFinite(halfExtents) ||
                halfExtents.x <= 0f ||
                halfExtents.y <= 0f ||
                halfExtents.z <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(halfExtents));
            }

            ObjectId = objectId.Trim();
            CenterOffset = centerOffset;
            HalfExtents = halfExtents;
        }

        public string ObjectId { get; }
        public Vector3 CenterOffset { get; }
        public Vector3 HalfExtents { get; }

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) &&
                   float.IsFinite(value.y) &&
                   float.IsFinite(value.z);
        }
    }

    public sealed class PhysicsPlacementValidator : IPlacementValidator
    {
        private readonly Dictionary<string, PlacementVolume> volumesById;
        private readonly int blockingLayerMask;
        private readonly int supportLayerMask;
        private readonly float maxSupportDistance;
        private readonly float skinWidth;
        private Collider[] overlapBuffer = new Collider[32];

        public PhysicsPlacementValidator(
            IReadOnlyList<PlacementVolume> volumes,
            int blockingLayerMask,
            int supportLayerMask,
            float maxSupportDistance = 0.05f,
            float skinWidth = 0.01f)
        {
            if (volumes == null)
            {
                throw new ArgumentNullException(nameof(volumes));
            }

            if (blockingLayerMask == 0 || supportLayerMask == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(blockingLayerMask));
            }

            if (!float.IsFinite(maxSupportDistance) || maxSupportDistance < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(maxSupportDistance));
            }

            if (!float.IsFinite(skinWidth) || skinWidth < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(skinWidth));
            }

            volumesById = new Dictionary<string, PlacementVolume>(
                volumes.Count,
                StringComparer.Ordinal);
            foreach (var volume in volumes)
            {
                var minimumHalfExtent = Mathf.Min(
                    volume.HalfExtents.x,
                    volume.HalfExtents.y,
                    volume.HalfExtents.z);
                if (skinWidth >= minimumHalfExtent ||
                    !volumesById.TryAdd(volume.ObjectId, volume))
                {
                    throw new ArgumentException(
                        "Placement volumes must have unique ids and be larger than the skin width.",
                        nameof(volumes));
                }
            }

            this.blockingLayerMask = blockingLayerMask;
            this.supportLayerMask = supportLayerMask;
            this.maxSupportDistance = maxSupportDistance;
            this.skinWidth = skinWidth;
        }

        private readonly Dictionary<string, int> privateLayers = new(StringComparer.Ordinal);
        public bool RegisterPrivateCopy(string objectId, string sourceId, int layer)
        {
            if (!volumesById.TryGetValue(sourceId, out var volume)) return false;
            volumesById[objectId] = new PlacementVolume(objectId, volume.CenterOffset, volume.HalfExtents);
            privateLayers[objectId] = layer;
            return true;
        }
        public void ClearPrivateLayers() => privateLayers.Clear();

        public bool IsValid(string objectId, Pose pose) => Validate(objectId,pose,true);
        public bool IsClear(string objectId, Pose pose) => Validate(objectId,pose,false);

        public bool TryGround(string objectId, Pose pose, out Pose grounded)
        {
            grounded = pose;
            if (IsValid(objectId, pose)) return true;
            if (objectId == null || !volumesById.TryGetValue(objectId, out var volume)) return false;

            privateLayers.TryGetValue(objectId, out var layer);
            var center = pose.position + pose.rotation * volume.CenterOffset;
            var hits = Physics.RaycastAll(center, Vector3.down, 20f,
                HidingPhysicsGroups.QueryMask(layer, supportLayerMask), QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                var item = hit.collider.GetComponentInParent<CarryableItem>();
                if (item != null && item.ObjectId == objectId) continue;
                if (hit.collider.GetComponentInParent<CharacterController>() != null ||
                    hit.collider.GetComponentInParent<Game.Network.Players.PlayerAvatar>() != null) continue;
                var height = hit.point.y + RotatedVerticalExtent(pose.rotation, volume.HalfExtents) -
                    (pose.rotation * volume.CenterOffset).y + 0.01f;
                var candidate = new Pose(new Vector3(pose.position.x, height, pose.position.z), pose.rotation);
                if (!IsValid(objectId, candidate)) continue;
                grounded = candidate;
                return true;
            }
            return false;
        }

        private bool Validate(string objectId, Pose pose, bool requireSupport)
        {
            if (string.IsNullOrWhiteSpace(objectId) ||
                !volumesById.TryGetValue(objectId.Trim(), out var volume) ||
                !IsFinite(pose.position) ||
                !IsFinite(pose.rotation))
            {
                return false;
            }

            privateLayers.TryGetValue(objectId.Trim(), out var layer);
            var queryBlocking = HidingPhysicsGroups.QueryMask(layer, blockingLayerMask);
            var querySupport = HidingPhysicsGroups.QueryMask(layer, supportLayerMask);
            var center = pose.position + (pose.rotation * volume.CenterOffset);
            var halfExtents = volume.HalfExtents - (Vector3.one * skinWidth);
            var overlapCount = Physics.OverlapBoxNonAlloc(
                    center,
                    halfExtents,
                    overlapBuffer,
                    pose.rotation,
                    queryBlocking,
                    QueryTriggerInteraction.Ignore);
            while (overlapCount == overlapBuffer.Length)
            {
                Array.Resize(ref overlapBuffer, overlapBuffer.Length * 2);
                overlapCount = Physics.OverlapBoxNonAlloc(center, halfExtents, overlapBuffer, pose.rotation,
                    queryBlocking, QueryTriggerInteraction.Ignore);
            }
            for (var index = 0; index < overlapCount; index++)
            {
                // A placed item is checked again when its owner completes hiding.
                var item = overlapBuffer[index].GetComponentInParent<CarryableItem>();
                if (item != null && item.ObjectId == volume.ObjectId) continue;
                // Players are transient and use the same default physics layer
                // as props in the current scenes. They must not make a green
                // client preview fail only on authority.
                if (overlapBuffer[index].GetComponentInParent<CharacterController>() == null &&
                    overlapBuffer[index].GetComponentInParent<Game.Network.Players.PlayerAvatar>() == null)
                {
                    return false;
                }
            }

            if(!requireSupport)return true;

            // 받침면 검사: 회전한 상자의 실제 바닥까지 광선을 내린다. 예전엔 회전을 무시한 반높이(HalfExtents.y)만
            // 써서, 기울인 물건은 바닥 위에 정확히 얹혀 있어도 광선이 허공에서 끝나 "받침 없음"으로 거부됐다.
            var verticalExtent = RotatedVerticalExtent(pose.rotation, volume.HalfExtents);
            return Physics.Raycast(
                center,
                Vector3.down,
                verticalExtent + maxSupportDistance,
                querySupport,
                QueryTriggerInteraction.Ignore);
        }

        /// <summary>회전한 배치 상자의 세로 반높이. 클라이언트 미리보기와 같은 식(<see cref="PlacementVolumeMath"/>).</summary>
        public static float RotatedVerticalExtent(Quaternion rotation, Vector3 halfExtents)
        {
            return PlacementVolumeMath.RotatedVerticalExtent(rotation, halfExtents);
        }

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) &&
                   float.IsFinite(value.y) &&
                   float.IsFinite(value.z);
        }

        private static bool IsFinite(Quaternion value)
        {
            return float.IsFinite(value.x) &&
                   float.IsFinite(value.y) &&
                   float.IsFinite(value.z) &&
                   float.IsFinite(value.w);
        }
    }
}
