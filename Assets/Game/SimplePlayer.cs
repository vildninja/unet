using UnityEngine;
using System.Collections;

namespace VildNinja.Game
{
    public class SimplePlayer : MonoBehaviour
    {
        public float maxVel = 7;
        public float moveForce = 30;
        public float jumpForce = 10;

        Rigidbody2D body;

        // Use this for initialization
        void Start()
        {
            body = GetComponent<Rigidbody2D>();
        }

        // Update is called once per frame
        void Update()
        {
            if (Input.GetKeyDown(KeyCode.W))
            {
                body.AddForce(new Vector2(0, jumpForce + Mathf.Max(0, -body.velocity.y)), ForceMode2D.Impulse);
            }
        }

        void FixedUpdate()
        {
            float x = body.velocity.x;
            int move = (Input.GetKey(KeyCode.A) ? -1 : 0) + (Input.GetKey(KeyCode.D) ? 1 : 0);

            if ((move == 1 && x < maxVel) || (move == -1 && x > -maxVel))
            {
                body.AddForce(new Vector2(move * moveForce, 0));
            }
            else if (move == 0)
            {
                body.AddForce(new Vector2(Mathf.Clamp(-x, -.4f, .4f) * moveForce, 0));
            }
        }
    }
}