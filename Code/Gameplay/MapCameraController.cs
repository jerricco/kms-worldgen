using System;
using System.IO;

namespace Sandbox.Gameplay;

[Category("Game Control")]
public sealed class MapCameraController : Component
{
	private CameraComponent _camera;
	private bool _isPanning;
	private Vector2 _lastMousePosition;
	private Vector3 _targetPosition;

	private float _targetZoom;

	// @TODO: Data-driven controls so that multiple cameras can inherit these values.
	[Property] [Header("Zoom Settings")]
	public float MinZoom { get; set; } = 2f;
	[Property]
	public float BaseZoom { get; set; } = 2000f;

	[Property]
	public float MaxZoom { get; set; } = 15000f;

	[Property]
	public float ZoomSensitivity { get; set; } = 0.15f;

	[Property]
	public float ZoomSmoothness { get; set; } = 15f;

	[Property] [Header("Pan Settings")]
	public float PanSpeed { get; set; } = 1f;

	protected override void OnStart()
	{
		this._camera = this.GetComponent<CameraComponent>();
		if (this._camera == null)
		{
			throw new FileLoadException("Can't find a loaded main camera!");
		}

		this._camera.Orthographic = true;// Force the camera into orthographic view if this component is attached_camera.ClearFlags = ClearFlags.All; // show a flat color if there's nothing rendering in the world
		_camera.BackgroundColor = Color.Black; // ensure we always show black if there's nothing rendered
		this._camera.WorldRotation = Rotation.From(90, 90, 0);// lock rotation
		// init zoom
		this._targetZoom = this._camera.OrthographicHeight = BaseZoom;
		// init panning
		this._targetPosition = this._camera.WorldPosition;
		this._targetPosition.z = 100f;
		this._camera.WorldPosition = this._targetPosition;
	}

	protected override void OnUpdate()
	{
		if (this._camera == null)
		{
			return;
		}

		this.HandleZoom();
		this.HandlePanning();
	}

	private void HandleZoom()
	{
		var scrollDelta = Input.MouseWheel.y;
		if (MathF.Abs(scrollDelta) > 0.001f)
		{
			this._targetZoom -= scrollDelta * this._targetZoom * this.ZoomSensitivity;
			this._targetZoom = this._targetZoom.Clamp(this.MinZoom, this.MaxZoom * 1.1f);
		}

		this._camera.OrthographicHeight = MathX.Lerp(this._camera.OrthographicHeight, this._targetZoom, RealTime.Delta * this.ZoomSmoothness);
	}

	private void HandlePanning()
	{
		Mouse.Visibility = MouseVisibility.Visible;

		// On left-mouse down
		if (Input.Pressed("attack1"))
		{
			this._isPanning = true;
			this._lastMousePosition = Mouse.Position;
		}

		// On left-mouse release
		if (Input.Released("attack1"))
		{
			this._isPanning = false;
		}

		// On left-mouse drag
		if (this._isPanning && Input.Down("attack1"))
		{
			var currentMousePosition = Mouse.Position;
			var mouseDelta = currentMousePosition - this._lastMousePosition;
			this._lastMousePosition = currentMousePosition;

			if (mouseDelta == Vector2.Zero)
			{
				return;
			}

			var screenWidth = Screen.Size.x;
			var screenHeight = Screen.Size.y;
			var worldUnitsPerPixelX = this._camera.OrthographicHeight * (screenWidth / screenHeight) / screenWidth;
			var worldUnitsPerPixelY = this._camera.OrthographicHeight / screenHeight;
			var worldDeltaX = -mouseDelta.x * worldUnitsPerPixelX * this.PanSpeed;
			var worldDeltaY = mouseDelta.y * worldUnitsPerPixelY * this.PanSpeed;

			// Apply translation directly onto target vector , locked to Z=100
			this._targetPosition.x += worldDeltaX;
			this._targetPosition.y += worldDeltaY;
			this._targetPosition.z = 100f;

			// lerp the result with the current WorldPos
			// Do i need to lerp? We'll find out.
			this.WorldPosition = Vector3.Lerp(this.WorldPosition, this._targetPosition, Time.Delta * 15f);
		}
	}
}
