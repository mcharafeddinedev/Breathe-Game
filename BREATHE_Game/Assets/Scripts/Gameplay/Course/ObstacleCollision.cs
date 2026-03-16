using UnityEngine;

namespace Breathe.Gameplay
{
    // Stuns AI boats on contact. Needs a trigger Collider2D + kinematic Rigidbody2D.
    [RequireComponent(typeof(Collider2D))]
    [RequireComponent(typeof(Rigidbody2D))]
    public class ObstacleCollision : MonoBehaviour
    {
        private void OnTriggerEnter2D(Collider2D other)
        {
            var ai = other.GetComponentInParent<AICompanionController>();
            if (ai != null && !ai.IsStunned)
            {
                ai.TriggerStun();
                Debug.Log($"[Obstacle] AI boat hit {gameObject.name} — stunned!");
            }
        }
    }
}
