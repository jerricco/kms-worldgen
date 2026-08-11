namespace Sandbox.Tools;

[EditorTool]
[Title( "Chunk Painter" )]
[Category( "Map" )]
[Description( "Click to spawn new chunks on the map. Right click to give LevelBootstrap a new Starting Position" )]
[Icon("cube")]
public class ChunkPainterTool : EditorTool
{
	private WidgetWindow Window;
	public override void OnEnabled()
	{
		// create a widget window. This is a window that  
		// can be dragged around in the scene view
		Window = new WidgetWindow( SceneOverlay );
		Window.Layout = Layout.Column();
		Window.Layout.Margin = 16;
 
		// Create a button for us to press
		var button = new Button( "Generate Map" );
		button.Pressed = () => Log.Info( "Pressed map generate!!" );

		// Add the button to the window's layout
		Window.Layout.Add( button );

		// Calling this function means that when your tool is deleted,
		// ui will get properly deleted too. If you don't call this and
		// you don't delete your UI in OnDisabled, it'll hang around forever.
		AddOverlay( Window, TextFlag.RightTop, 10 );
	}

	public override void OnDisabled()
	{
		Window.Destroy();
	}
}
