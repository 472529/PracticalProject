using UnityEngine;

public class CameraController : MonoBehaviour
{
    public Transform target;
    public float smoothSpeed = 0.125f;
    public Vector3 offset;
    Vector3 velocity = Vector3.zero;
    private Transform xFormCam;
    private Transform xFormParent;

    private Vector3 localRotation;
    private float camDis = 10f;

    public float mouseSens = 4f;
    public float scrollSens = 2f;
    public float orbitDamp = 10f;
    public float scrollDamp = 6f;

    public bool CameraDisable = false;

    void Start()
    {
        this.xFormCam = this.transform;
        this.xFormParent = this.transform.parent;
    }
    void LateUpdate()
    {
        //Vector3 desiredPos = target.position + offset;
        //Vector3 smoothPos = Vector3.SmoothDamp(transform.position, desiredPos, ref velocity, smoothSpeed);
        //transform.position = smoothPos;

        if (Input.GetKeyDown(KeyCode.LeftShift))
        {
            CameraDisable = !CameraDisable;
        }
        
        if (!CameraDisable)
        {
            if(Input.GetAxis("Mouse X") != 0 || Input.GetAxis("Mouse Y") != 0)
            {
                localRotation.x += Input.GetAxis("Mouse X") * mouseSens;
                localRotation.y -= Input.GetAxis("Mouse Y") * mouseSens;

                localRotation.y = Mathf.Clamp(localRotation.y, 0f, 90f);
            }

            if(Input.GetAxis("Mouse ScrollWheel") != 0)
            {
                float scrollAmount = Input.GetAxis("Mouse ScrollWheel") * scrollSens;
                //makes camera zoom slower when closer and faster when far away
                scrollAmount *= (this.camDis * 0.3f);

                this.camDis += scrollAmount * -1f;

                // limits the zoom on camera
                this.camDis = Mathf.Clamp(this.camDis, 1.5f, 100f);
            }
        }

        //actual camera rig transformations
        Quaternion QT = Quaternion.Euler(localRotation.y, localRotation.x, 0);
        this.xFormParent.rotation = Quaternion.Lerp(this.xFormParent.rotation, QT, Time.deltaTime * orbitDamp);

        if(this.xFormCam.localPosition.z != this.camDis * -1f)
        {
            this.xFormCam.localPosition = new Vector3(0f, 0f, Mathf.Lerp(this.xFormCam.localPosition.z, this.camDis * -1f, Time.deltaTime * scrollDamp));
        }

        transform.LookAt(target);
    }
}
