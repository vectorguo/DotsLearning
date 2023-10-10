using Cinemachine;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public CinemachineVirtualCamera virtualCamera;
    public float cameraMinHeight;
    public float cameraMaxHeight;

    public float speed;

    private Vector3 mousePosition;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            mousePosition = Input.mousePosition;
        }
        else if (Input.GetMouseButton(0))
        {
            var deltaMousePosition = Input.mousePosition - mousePosition;
            transform.Rotate(Vector3.up * deltaMousePosition.x * 0.2f);

            var t = virtualCamera.GetCinemachineComponent<CinemachineTransposer>();
            var followOffset = t.m_FollowOffset;
            followOffset.y -= deltaMousePosition.y * 0.02f;
            followOffset.y = Mathf.Clamp(followOffset.y, cameraMinHeight, cameraMaxHeight);
            t.m_FollowOffset = followOffset;

            mousePosition = Input.mousePosition;
        }

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
