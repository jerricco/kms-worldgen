using System;
using System.IO;

namespace Sandbox.Gameplay;


[Category("Game Control")]
public sealed class MapCameraController : Component
{
	// @TODO: Data-driven controls so that multiple cameras can inherit these values.
	[Property, Header( "Zoom Settings" )] 
	public float MinZoom { get; set; } = 10f;
	[Property] 
	public float BaseZoom { get; set; } = 2000f;
	[Property] 
	public float MaxZoom { get; set; } = 15000f;
	[Property]
	public float ZoomSensitivity { get; set; } = 0.15f;
	[Property]
	public float ZoomSmoothness { get; set; } = 15f;
	[Property, Header( "Pan Settings" )]
	public float PanSpeed { get; set; } = 1f;

	private CameraComponent _camera;
	private float _targetZoom;
	private bool _isPanning = false;
	private Vector3 _targetPosition;
	private Vector2 _lastMousePosition;
	
	protected override void OnStart()
	{
		_camera = GetComponent<CameraComponent>();
		if ( _camera == null ) throw new FileLoadException( "Can't find a loaded main camera!" );
		
		
		_camera.Orthographic = true; // Force the camera into orthographic view if this component is attached
		_camera.ClearFlags = ClearFlags.All; // show a flat color if there's nothing rendering in the world
		_camera.BackgroundColor = Color.Black; // ensure we always show black if there's nothing rendered
		_camera.WorldRotation = Rotation.From( 90, 90, 0 ); // lock rotation
		
		// init zoom
		_targetZoom = _camera.OrthographicHeight = BaseZoom;
		
		// init panning
		_targetPosition = _camera.WorldPosition;
		_targetPosition.z = 100f; 
		_camera.WorldPosition = _targetPosition;
	}

	protected override void OnUpdate()
	{
		if ( _camera == null ) return;

		HandleZoom();
		HandlePanning();
	}

	private void HandleZoom()
	{
		float scrollDelta = Input.MouseWheel.y;
		if ( MathF.Abs( scrollDelta ) > 0.001f )
		{
			_targetZoom -= scrollDelta * _targetZoom * ZoomSensitivity;
			_targetZoom = _targetZoom.Clamp( MinZoom, MaxZoom * 1.1f );
		}
		
		_camera.OrthographicHeight = MathX.Lerp( _camera.OrthographicHeight, _targetZoom, RealTime.Delta * ZoomSmoothness );
	}

	private void HandlePanning()
	{
		Mouse.Visibility = MouseVisibility.Visible;
		
		// On left-mouse down
		if ( Input.Pressed( "attack1" ) )
		{
			_isPanning = true;
			_lastMousePosition = Mouse.Position;
		}
		
		// On left-mouse release
		if ( Input.Released( "attack1" ) ) _isPanning = false;

		// On left-mouse drag
		if ( _isPanning && Input.Down( "attack1" ) )
		{
			Vector2 currentMousePosition = Mouse.Position;
			Vector2 mouseDelta = currentMousePosition - _lastMousePosition;
			_lastMousePosition = currentMousePosition;
			
			if ( mouseDelta == Vector2.Zero ) return;
			
			float screenWidth = Screen.Size.x;
			float screenHeight = Screen.Size.y;
			float worldUnitsPerPixelX = (_camera.OrthographicHeight * (screenWidth / screenHeight)) / screenWidth;
			float worldUnitsPerPixelY = _camera.OrthographicHeight / screenHeight;
			float worldDeltaX = -mouseDelta.x * worldUnitsPerPixelX * PanSpeed;
			float worldDeltaY = mouseDelta.y * worldUnitsPerPixelY * PanSpeed;

			// Apply translation directly onto target vector , locked to Z=100
			_targetPosition.x += worldDeltaX;
			_targetPosition.y += worldDeltaY;
			_targetPosition.z = 100f; 

			// lerp the result with the current WorldPos
			// Do i need to lerp? We'll find out.
			WorldPosition  = Vector3.Lerp( WorldPosition, _targetPosition, Time.Delta * 15f );
		}
	}
}
