using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ThirdPersonMovement : MonoBehaviour
{

	public float floorOffsetY;
	public float moveSpeed = 6f;
	public float rotateSpeed = 10f;
	public float slopeLimit = 45f;
	public float slopeInfluence = 5f;
	public float jumpPower = 10f;
	public float characterHangOffset = 1.4f;

	Rigidbody rb;
	Animator anim;
	float vertical;
	float horizontal;

	[SerializeField]
	Vector3 moveDirection;
	float inputAmount;
	Vector3 raycastFloorPos;
	Vector3 floorMovement;

	[SerializeField]
	Vector3 gravity;
	Vector3 CombinedRaycast;

	float jumpFalloff = 2f;
	bool jump_input_down;
	bool drop_input_down;
	float slopeAmount;
	Vector3 floorNormal;

	[SerializeField]
	Vector3 velocity;


	public bool safeForClimbUp;


	// ledge climbing
	bool GrabLedge;

	public Vector3 LedgePos;
	public Vector3 WallNormal;

	public enum MovementControlType { Normal, Climbing };
	[SerializeField]
	MovementControlType movementControlType;

	// Use this for initialization
	void Start()
	{
		rb = GetComponent<Rigidbody>();
		anim = GetComponent<Animator>();
	}

	private void Update()
	{
		// reset movement
		moveDirection = Vector3.zero;
		// get vertical and horizontal movement input (controller and WASD/ Arrow Keys)
		vertical = Input.GetAxis("Vertical");
		horizontal = Input.GetAxis("Horizontal");

		jump_input_down = Input.GetKeyDown(KeyCode.Space);
		drop_input_down = Input.GetKeyDown(KeyCode.S);



		// base movement on camera
		Vector3 correctedVertical = vertical * Camera.main.transform.forward;
		Vector3 correctedHorizontal = horizontal * Camera.main.transform.right;

		Vector3 combinedInput = correctedHorizontal + correctedVertical;

		// make sure the input doesnt go negative or above 1;
		float inputMagnitude = Mathf.Abs(horizontal) + Mathf.Abs(vertical);
		inputAmount = Mathf.Clamp01(inputMagnitude);

		moveDirection = new Vector3((combinedInput).normalized.x, 0, (combinedInput).normalized.z);


		if (jump_input_down)
		{
			Jump();
		}

		// if hanging and cancel input is pressed, back to normal movement, and stop hanging
		if (drop_input_down && GrabLedge)
		{
			CancelHanging();
		}
		// handle animation blendtree for walking
		anim.SetFloat("Velocity", inputAmount, 0.2f, Time.deltaTime);
		anim.SetFloat("SlopeNormal", slopeAmount, 0.2f, Time.deltaTime);
	}


	private void FixedUpdate()
	{
		// if not grounded , increase down force
		if (!IsGrounded() || slopeAmount >= 0.1f && !GrabLedge)// if going down, also apply, to stop bouncing, dont move down when grabbing onto ledge
		{
			gravity += Vector3.up * Physics.gravity.y * jumpFalloff * Time.fixedDeltaTime;
		}

		switch (movementControlType)
		{
			case MovementControlType.Normal:

				// normal movement


				// rotate player to movement direction
				Quaternion rot = Quaternion.LookRotation(moveDirection);
				Quaternion targetRotation = Quaternion.Slerp(transform.rotation, rot, Time.fixedDeltaTime * inputAmount * rotateSpeed);
				transform.rotation = targetRotation;
				break;

			case MovementControlType.Climbing:
				// movement for climbing, going up and down/ left and right instead of forwards and sideways like the normal movement
				moveDirection = new Vector3(horizontal * 0.2f * inputAmount * transform.forward.z, vertical * 0.3f * inputAmount, -horizontal * 0.2f * inputAmount * transform.forward.x) + (transform.forward * 0.2f);

				// if we are hanging on a ledge, dont move up or down
				if (GrabLedge)
				{
					moveDirection.y = 0f;
				}
				// no gravity when climbing
				gravity = Vector3.zero;

				// rotate towards the wall while moving, so you always face the wall even when its curved
				Vector3 targetDir = -WallNormal;
				targetDir.y = 0;
				if (targetDir == Vector3.zero)
				{
					targetDir = transform.forward;
				}
				Quaternion tr = Quaternion.LookRotation(targetDir);
				transform.rotation = Quaternion.Slerp(transform.rotation, tr, Time.fixedDeltaTime * inputAmount * rotateSpeed);
				break;
		}

		rb.velocity = (moveDirection * GetMoveSpeed() * inputAmount) + gravity;

		// find the Y position via raycasts
		floorMovement = new Vector3(rb.position.x, FindFloor().y + floorOffsetY, rb.position.z);

		// only stick to floor when grounded
		if (floorMovement != rb.position && IsGrounded() && rb.velocity.y <= 0)
		{
			// move the rigidbody to the floor
			rb.MovePosition(floorMovement);
			gravity = Vector3.zero;
			movementControlType = MovementControlType.Normal;
			GrabLedge = false;
		}

		// ledge grab only when not on ground
		if (!IsGrounded())
		{
			LedgeGrab();

		}

		velocity = rb.velocity;
	}

	Vector3 FindFloor()
	{
		// width of raycasts around the centre of your character
		float raycastWidth = 0.25f;
		// check floor on 5 raycasts   , get the average when not Vector3.zero  
		int floorAverage = 1;

		CombinedRaycast = FloorRaycasts(0, 0, 1.6f);
		floorAverage += (getFloorAverage(raycastWidth, 0) + getFloorAverage(-raycastWidth, 0) + getFloorAverage(0, raycastWidth) + getFloorAverage(0, -raycastWidth));

		return CombinedRaycast / floorAverage;
	}

	// only add to average floor position if its not Vector3.zero
	int getFloorAverage(float offsetx, float offsetz)
	{

		if (FloorRaycasts(offsetx, offsetz, 1.6f) != Vector3.zero)
		{
			CombinedRaycast += FloorRaycasts(offsetx, offsetz, 1.6f);
			return 1;
		}
		else
		{ return 0; }
	}

	public bool IsGrounded()
	{
		if (FloorRaycasts(0, 0, 0.6f) != Vector3.zero)
		{
			slopeAmount = Vector3.Dot(transform.forward, floorNormal);
			return true;
		}
		else
		{
			return false;
		}
	}


	Vector3 FloorRaycasts(float offsetx, float offsetz, float raycastLength)
	{
		RaycastHit hit;
		// move raycast
		raycastFloorPos = transform.TransformPoint(0 + offsetx, 0 + 0.5f, 0 + offsetz);

		Debug.DrawRay(raycastFloorPos, Vector3.down, Color.magenta);
		if (Physics.Raycast(raycastFloorPos, -Vector3.up, out hit, raycastLength))
		{
			floorNormal = hit.normal;

			if (Vector3.Angle(floorNormal, Vector3.up) < slopeLimit)
			{
				return hit.point;
			}
			else
				return Vector3.zero;
		}
		else
			return Vector3.zero;
	}

	float GetMoveSpeed()
	{
		// you can add a run here, if run button : currentMovespeed = runSpeed;
		float currentMovespeed = Mathf.Clamp(moveSpeed + (slopeAmount * slopeInfluence), 0, moveSpeed + 1);
		return currentMovespeed;
	}

	void Jump()
	{
		if (IsGrounded())
		{
			gravity.y = jumpPower;
			anim.SetTrigger("Jumping");
		}

		if (safeForClimbUp && anim.GetCurrentAnimatorStateInfo(0).IsName("Hanging"))
		{
			anim.SetTrigger("ClimbUp");
		}
	}

	public void CancelHanging()
	{
		movementControlType = MovementControlType.Normal;
		GrabLedge = false;
		anim.SetTrigger("StopHanging");
	}

	public void GrabLedgePos(Vector3 ledgePos)
	{
		// set hanging animation trigger
		anim.SetTrigger("Hanging");
		anim.ResetTrigger("StopHanging");
		LedgePos = ledgePos;
		GrabLedge = true;
		// set movement type for climbing
		movementControlType = MovementControlType.Climbing;

	}

	void LedgeGrab()
	{
		if (anim.GetCurrentAnimatorStateInfo(0).IsName("Climbup"))
		{
			transform.position = Vector3.Lerp(transform.position, LedgePos + (transform.forward * 0.4f), Time.deltaTime * 5f);
		}

		if (anim.GetCurrentAnimatorStateInfo(0).IsName("To Hang"))
		{
			// lower position for hanging
			Vector3 LedgeTopPosition = new Vector3(LedgePos.x, LedgePos.y - characterHangOffset, LedgePos.z);

			// lerp the position
			transform.position = Vector3.Lerp(transform.position, LedgeTopPosition, Time.deltaTime * 5f);
			// rotate to the wall
			Quaternion rotateToWall = Quaternion.LookRotation(-WallNormal);
			Quaternion targetRotation = Quaternion.Slerp(transform.rotation, rotateToWall, Time.deltaTime * rotateSpeed);
			transform.rotation = targetRotation;
		}
	}
}
