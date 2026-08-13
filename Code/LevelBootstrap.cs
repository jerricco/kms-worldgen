using Sandbox.Systems.Map;

namespace Sandbox;

[Category("Scene Orchestration")]
public sealed class LevelBootstrap : Component
{
	// editor properties for updating generation
	[Property] public string Seed = "aborio rice";
	[Property] public Vector2 GeneratePosition = new Vector2( 0, 0 );
	[Property] public int RevealRadius = 8;
	[Property, ReadOnly] public bool IsGenerating = false;
	[Property, ReadOnly] public bool MapExists = false;

	protected override void OnDestroy()
	{
		MapGeneratorSystem.Current.Cleanup();
	}

	[Button, Title("Generate Radius"), Description("Queue chunks to generate with a Reveal Radius from the above inspector field.")]
	public void PushRevealRadius()
	{
		if ( MapExists )
		{
			if ( IsGenerating )
			{
				Log.Error( "Generation already in progress! Exiting..." );
				return;
			}
			// update the map
			IsGenerating = true;
			Log.Info($"Queueing a radius of <color=green>{RevealRadius} chunks...</color>");
			MapGeneratorSystem.Current.GetChunkGenerationTasks( GeneratePosition, RevealRadius );
			IsGenerating = false; // @TODO: Move these to MapGenerationSystem for better async tracking
		}
		else GenerateMap();
	}
	
    [Button( "Regenerate Map" )]
    public void GenerateMap()
    {
	    if ( IsGenerating )
	    {
		    Log.Error( "Generation already in progress! Exiting..." );
		    return;
	    }
	    
	    if ( MapExists ) ClearMap();
	    
	    if ( Seed != MapGeneratorSystem.Current.Settings.SeedText )
	    {
		    Log.Warning( "A new seed was entered! Regenerating." );
		    ClearMap();
	    }
	    
	    IsGenerating = true;
	    Log.Info($"Seed text '{MapGeneratorSystem.Current.Settings.SeedText}' generating a new map...");
	    MapGeneratorSystem.Current.GenerateWorld( GeneratePosition, RevealRadius, Seed );
	    
	    IsGenerating = false;
	    MapExists = true;
    }

    [Button( "Clear Map Generation" )]
    public void ClearMap()
    {
	    if ( IsGenerating )
	    {
		    Log.Warning( "Generating already in progress! Exiting..." );
		    return;
	    }
	    
	    Log.Warning( "Clearing out old level generation..." );
	    MapGeneratorSystem.Current.Cleanup(); // cleanup old renderers
	    MapGeneratorSystem.Current.InitializeScene( Seed ); // ensure any new seed is set.
	    MapExists = false;
    }
}
