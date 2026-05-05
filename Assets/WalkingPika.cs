using UnityEngine;

public class WalkingPika : MonoBehaviour
{
    public float speed = 2f;
    public float durasi = 8f;

    private float timer = 0f;
    private bool isWalking = true;

    private Animator anim;

    void Start()
    {
        anim = GetComponent<Animator>();
        anim.SetBool("isWalking", true);
    }

    void Update()
    {
        if (isWalking)
        {
            transform.Translate(Vector3.forward * speed * Time.deltaTime);

            timer += Time.deltaTime;

            if (timer >= durasi)
            {
                isWalking = false;

                anim.SetBool("isWalking", false);

                Debug.Log("Pikachu berhenti!");
            }
        }
    }
}