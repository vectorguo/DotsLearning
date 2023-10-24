using Cinemachine;
using UnityEngine;
using UnityEngine.EventSystems;

public class CameraRotate : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public CinemachineVirtualCamera virtualCamera;
    public float cameraMinHeight;
    public float cameraMaxHeight;
    public GameObject Player;

    private Vector2 m_startPosition;

    public void OnBeginDrag(PointerEventData eventData)
    {
        m_startPosition = eventData.position;
    }

    public void OnDrag(PointerEventData eventData)
    {
        var deltaMousePosition = eventData.position - m_startPosition;
        Player.transform.Rotate(Vector3.up * deltaMousePosition.x * 0.2f);

        var t = virtualCamera.GetCinemachineComponent<CinemachineTransposer>();
        var followOffset = t.m_FollowOffset;
        followOffset.y -= deltaMousePosition.y * 0.02f;
        followOffset.y = Mathf.Clamp(followOffset.y, cameraMinHeight, cameraMaxHeight);
        t.m_FollowOffset = followOffset;

        m_startPosition = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        
    }
}
