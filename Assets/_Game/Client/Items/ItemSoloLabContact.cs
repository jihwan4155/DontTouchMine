using UnityEngine;

namespace Game.Client.Items
{
    public sealed class ItemSoloLabContact : MonoBehaviour
    {
        public ItemSoloLab lab;
        private void OnCollisionEnter(Collision collision)
        {
            if (lab != null) lab.RecordContact(collision.gameObject.name, collision.relativeVelocity.magnitude);
        }
    }
}
