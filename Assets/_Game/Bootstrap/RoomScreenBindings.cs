using Game.Client.Rooms;
using UnityEngine;

namespace Game.Bootstrap
{
    // Keeps the existing scene's serialized references and script GUID.
    [DisallowMultipleComponent]
    public sealed class RoomScreenBindings : MonoBehaviour
    {
        [SerializeField] private RoomPasswordModalView passwordModalPrefab;
        [SerializeField] private Transform modalParent;

        public RoomPasswordModalView PasswordModalPrefab => passwordModalPrefab;
        public Transform ModalParent => modalParent;
    }
}
