using UnityEngine;
using UnityEngine.EventSystems;

public class Joystick : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public RectTransform joystickImage;
    public Vector3 startPos;
    public int maxDis = 70;
    public float speed = 5;

    public GameObject Player;
    public float PlayerSpeed;

    private bool m_isTouching = false;

    // Use this for initialization
    void Start()
    {
        startPos = joystickImage.position;
    }

    // Update is called once per frame
    void Update()
    {

        if (Application.platform == RuntimePlatform.Android)
        {
            if (Input.touchCount <= 0)
            {
                if (Vector3.Distance(joystickImage.position, startPos) > 0.01f)
                {
                    joystickImage.position = joystickImage.position - (joystickImage.position - startPos).normalized * speed;
                }
            }
        }
        else
        {
            if (!Input.GetMouseButton(0))
            {
                if (Vector3.Distance(joystickImage.position, startPos) > 0.01f)
                {
                    joystickImage.position = joystickImage.position - (joystickImage.position - startPos).normalized * speed;
                }
            }
        }

        if (m_isTouching)
        {
            Vector3 dir = (joystickImage.position - startPos).normalized;
            var delta = dir.y * Player.transform.forward + dir.x * Player.transform.right;
            delta.Normalize();
            Player.transform.position += delta * PlayerSpeed * Time.deltaTime;
        }

#if UNITY_EDITOR
        {
            var delta = Vector3.zero;
            if (Input.GetKey(KeyCode.W))
            {
                delta += Player.transform.forward * PlayerSpeed * Time.deltaTime;
            }

            if (Input.GetKey(KeyCode.S))
            {
                delta -= Player.transform.forward * PlayerSpeed * Time.deltaTime;
            }

            if (Input.GetKey(KeyCode.A))
            {
                delta -= Player.transform.right * PlayerSpeed * Time.deltaTime;
            }

            if (Input.GetKey(KeyCode.D))
            {
                delta += Player.transform.right * PlayerSpeed * Time.deltaTime;
            }

            Player.transform.position += delta;
        }
#endif
    }

    public void OnDrag(PointerEventData eventData)
    {
        //将UGUI的坐标转为世界坐标
        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(joystickImage, eventData.position, eventData.pressEventCamera, out var wordPos))
        {
            joystickImage.position = wordPos;
        }

        Vector3 dir = (joystickImage.position - startPos).normalized;
        if (Vector3.Distance(joystickImage.position, startPos) >= maxDis)
        {
            joystickImage.position = startPos + dir * maxDis;
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        m_isTouching = true;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        m_isTouching = false;
    }
}