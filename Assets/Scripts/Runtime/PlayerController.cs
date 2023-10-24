using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public float speed;

    // Update is called once per frame
    void Update()
    {
        var delta = Vector3.zero;
        if (Input.GetKey(KeyCode.W))
        {
            delta += transform.forward * speed * Time.deltaTime;
        }

        if (Input.GetKey(KeyCode.S))
        {
            delta -= transform.forward * speed * Time.deltaTime;
        }

        if (Input.GetKey(KeyCode.A))
        {
            delta -= transform.right * speed * Time.deltaTime;
        }

        if (Input.GetKey(KeyCode.D))
        {
            delta += transform.right * speed * Time.deltaTime;
        }

        transform.position += delta;
    }
}
