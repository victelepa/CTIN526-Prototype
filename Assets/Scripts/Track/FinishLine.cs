using System;
using UnityEngine;

namespace RaceSabotage
{
    [RequireComponent(typeof(Collider2D))]
    public class FinishLine : MonoBehaviour
    {
        [SerializeField] bool freezeOnFinish = true;

        public event Action<PlayerMotor> Finished;

        void Reset() => GetComponent<Collider2D>().isTrigger = true;

        void OnTriggerEnter2D(Collider2D other)
        {
            if (other.attachedRigidbody == null) return;

            PlayerMotor motor = other.attachedRigidbody.GetComponent<PlayerMotor>();
            if (motor == null || motor.Frozen) return;

            if (freezeOnFinish) motor.Frozen = true;
            Finished?.Invoke(motor);
        }
    }
}
