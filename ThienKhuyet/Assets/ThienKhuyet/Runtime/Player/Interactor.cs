using ThienKhuyet.Core;
using UnityEngine;

namespace ThienKhuyet.Player
{
    /// <summary>Anything the player can interact with (NPCs, pickups, doors, shrines, meditation spots...).</summary>
    public interface IInteractable
    {
        string PromptKey { get; }
        bool CanInteract { get; }
        Vector3 InteractPoint { get; }
        float InteractRadius { get; }
        void Interact(PlayerController player);
    }

    /// <summary>Finds the best interactable in front of the player and triggers it. The prompt is read by the HUD.</summary>
    public sealed class Interactor : MonoBehaviour
    {
        static readonly Collider[] buffer = new Collider[24];
        float scanTimer;

        public IInteractable Current { get; private set; }
        public string PromptKey => Current != null ? Current.PromptKey : null;

        void Update()
        {
            if (Game.Mode != GameMode.Playing || Game.Input == null)
            {
                Current = null;
                return;
            }
            scanTimer -= Time.unscaledDeltaTime;
            if (scanTimer <= 0f)
            {
                scanTimer = 0.1f;
                Scan();
            }
            if (Current != null && Game.Input.InteractPressed)
            {
                var pc = GetComponent<PlayerController>();
                if (pc != null && pc.CanInteract)
                {
                    Current.Interact(pc);
                    EventBus.Publish(new PlayerActionEvent { action = "interact" });
                }
            }
        }

        void Scan()
        {
            IInteractable best = null;
            float bestScore = float.MaxValue;
            Vector3 p = transform.position;
            int n = Physics.OverlapSphereNonAlloc(p + Vector3.up, 3.2f, buffer, GameLayers.InteractMask, QueryTriggerInteraction.Collide);
            Vector3 fwd = transform.forward;
            for (int i = 0; i < n; i++)
            {
                var it = buffer[i].GetComponentInParent<IInteractable>();
                if (it == null || !it.CanInteract) continue;
                Vector3 to = it.InteractPoint - p;
                to.y = 0f;
                float d = to.magnitude;
                if (d > it.InteractRadius) continue;
                float ang = d > 0.3f ? Vector3.Angle(fwd, to / d) : 0f;
                if (ang > 110f) continue;
                float score = d + ang * 0.02f;
                if (score < bestScore) { bestScore = score; best = it; }
            }
            Current = best;
        }
    }
}
