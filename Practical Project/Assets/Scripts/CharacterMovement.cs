using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CharacterMovement : MonoBehaviour
{
	[SerializeField] private float moveSpeed;
	[SerializeField] private float walkSpeed;
	[SerializeField] private float runSpeed;

	[SerializeField] private Vector3 moveDirection;
	private Vector3 velocity;

	[SerializeField] private bool isGrounded;
	[SerializeField] private float groundCheckDistance;
	[SerializeField] private LayerMask groundMask;
	[SerializeField] private float gravity;

	[SerializeField] private float turnSmoothTime = 0.1f;
	private float turnSmoothVelocity;
	public Transform cam;
	public float speed = 6f;

	[SerializeField] private float jumpHeight;
	private float jumps;
	private float maxJumps = 1;

	private CharacterController controller;
	[SerializeField] private Animator animator;

	private void Start()
	{
		controller = GetComponent<CharacterController>();
		Cursor.lockState = CursorLockMode.Locked;
		animator = GetComponentInChildren<Animator>();
	}

	private void LateUpdate()
	{
		Move();

		if (Input.GetKeyDown(KeyCode.Mouse0))
		{
			StartCoroutine(Attack());
		}
	}

	private void Move()
	{
		isGrounded = Physics.CheckSphere(transform.position, groundCheckDistance, groundMask);

		float horizontal = Input.GetAxisRaw("Horizontal");
		float vertical = Input.GetAxisRaw("Vertical");

		Vector3 direction = new Vector3(horizontal, 0f, vertical).normalized;

		float moveZ = Input.GetAxis("Vertical");

		moveDirection = new Vector3(0, 0, moveZ);
		moveDirection = transform.TransformDirection(moveDirection);

		if (direction.magnitude >= 0.1f)
		{

			float targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg + cam.eulerAngles.y;
			float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref turnSmoothVelocity, turnSmoothTime);
			transform.rotation = Quaternion.Euler(0f, angle, 0f);

			Vector3 moveDirection = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
			controller.Move(moveDirection.normalized * moveSpeed * Time.deltaTime);



		}

		if (isGrounded && velocity.y < 0)
		{
			velocity.y = -1f;
		}

		
		if (isGrounded)
		{
			jumps = 0;
			animator.SetBool("Jump", false);
			if (moveDirection != Vector3.zero && !Input.GetKey(KeyCode.LeftShift))
			{
				Walk();
			}
			else if (moveDirection != Vector3.zero && Input.GetKey(KeyCode.LeftShift))
			{
				Run();
			}
			else if (moveDirection == Vector3.zero)
			{
				Idle();
			}
			

			moveDirection *= moveSpeed;

			if (Input.GetKeyDown(KeyCode.Space) && jumps < maxJumps)
			{
				StartCoroutine(Jump());
			}
		}

		if (Input.GetKeyDown(KeyCode.Space) && jumps < maxJumps)
		{
			StartCoroutine(Jump());
		}

		

		velocity.y += gravity * Time.deltaTime;
		controller.Move(velocity * Time.deltaTime);
	}

	private void Idle()
	{
		animator.SetFloat("Speed", 0f, 0.05f, Time.deltaTime);
	}

	private void Walk()
	{
		animator.SetFloat("Speed", 0.5f, 0.05f, Time.deltaTime);
		moveSpeed = walkSpeed;
	}

	private void Run()
	{
		animator.SetFloat("Speed", 1f, 0.05f, Time.deltaTime);
		moveSpeed = runSpeed;
	}

	private IEnumerator Attack()
	{
		animator.SetLayerWeight(animator.GetLayerIndex("Attack Layer"), 1);
		animator.SetTrigger("Attack");

		yield return new WaitForSeconds(0.9f);
		animator.SetLayerWeight(animator.GetLayerIndex("Attack Layer"), 0);

	}

	private IEnumerator Jump()
	{
		animator.SetLayerWeight(animator.GetLayerIndex("Jump Layer"), 1);
		animator.SetTrigger("Jump");
		jumps = jumps + 1;
		velocity.y = Mathf.Sqrt(jumpHeight * -2 * gravity);
		yield return new WaitForSeconds(1.5f);
		animator.SetLayerWeight(animator.GetLayerIndex("Jump Layer"), 0);
	}
}
