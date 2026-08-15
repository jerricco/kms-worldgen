namespace Sandbox.Systems.Game;

public class ToolSystem: GameObjectSystem<ToolSystem>
{
	// public List<OneOf<GameTool, DebugTool>> CurrentTools;
	
	public ToolSystem( Scene scene ) : base( scene )
	{
		// register all the tools - it should be ahead of time in the scene which automatically
		// enabled debug tool availability.
		// CurrentTools
	}

	public enum GameTool
	{
		Panning
	}

	public enum DebugTool
	{
		ChunkRevealer,
		MapRevealer
	}
}
