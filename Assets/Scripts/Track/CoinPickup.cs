using UnityEngine;

namespace RaceSabotage
{
    [RequireComponent(typeof(Collider2D))]
    public class CoinPickup : MonoBehaviour
    {
        [SerializeField] int value = 1;

        void Reset() => GetComponent<Collider2D>().isTrigger = true;

        void OnTriggerEnter2D(Collider2D other)
        {
            if (other.attachedRigidbody == null) return;

            PlayerWallet wallet = other.attachedRigidbody.GetComponent<PlayerWallet>();
            if (wallet == null) return;

            wallet.Add(value);
            gameObject.SetActive(false);
        }
    }
}
