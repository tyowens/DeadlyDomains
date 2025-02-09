using UnityEngine;
using System.Linq;

public class PracticeSwing : MonoBehaviour
{
    private float _timeSinceLastSwing = 0f;
    private GameObject _swordSwingPivot;

    // Start is called before the first frame update
    void Start()
    {
        _swordSwingPivot = GetComponentsInChildren<Transform>(includeInactive: true).First(transform => transform.name == "swordSwingPivot").gameObject;
    }

    // Update is called once per frame
    void Update()
    {
        var animators = GetComponentsInChildren<Animator>(includeInactive: true);
        _timeSinceLastSwing += Time.deltaTime;

        var visualAnimator = animators.First(animator => animator.gameObject.name == "swordSwingAnim");
        var hitboxAnimator = animators.First(animator => animator.gameObject.name == "Hitbox");

        // Hide visual sword anim after done swinging
        if (visualAnimator.gameObject.activeSelf && visualAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1)
        {
            _swordSwingPivot.SetActive(false);
        }

        if (_timeSinceLastSwing > 3f)
        {
            var allPlayers = FindObjectsOfType<PlayerMovement>();
            var closestPlayer = allPlayers.OrderBy(player => ((Vector2)player.transform.position - (Vector2)transform.position).magnitude).FirstOrDefault();

            if (closestPlayer != null)
            {
                var vectorToPlayer = (Vector2)closestPlayer.transform.position - (Vector2)transform.position;
                var angleToRotate = Vector2.SignedAngle(Vector2.up, vectorToPlayer);
                _swordSwingPivot.transform.eulerAngles = new Vector3(0f, 0f, angleToRotate);

                _swordSwingPivot.SetActive(true);

                hitboxAnimator.Play("swordSwingHitboxAnimation", -1, 0f);
                visualAnimator.Play("Loop", -1, 0f);
                
                GetComponentInChildren<SwordCollisionHandler>(includeInactive: true).ClearPlayersHit();
                _timeSinceLastSwing = 0f;
            }
        }
    }
}
