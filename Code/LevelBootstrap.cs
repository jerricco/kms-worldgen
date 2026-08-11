using System.Threading.Tasks;
using Sandbox.GameObjectSystems.Map;
using Sandbox.Triangulation;

namespace Sandbox;

[Category("Scene Orchestration")]
public sealed class LevelBootstrap : Component
{
	// editor properties for updating generation
	[Property] public string Seed = "aborio rice";
	[Property] public Vector2 GeneratePosition = new Vector2( 0, 0 );
	[Property] public int RevealRadius = 8;
	[Property, ReadOnly] public bool GenerationReady = true;
	[Property, ReadOnly] public bool GenerationComplete = false;

	protected override void OnStart()
	{
		var mapSystem = MapGeneratorSystem.Current;
		
		if (Seed == null) Seed = MapGeneratorSystem.Current.Settings.SeedText;
		// attach component with system configuration
		var voronoiFactory = Scene.GetAllComponents<VoronoiFactory>().FirstOrDefault();
		voronoiFactory.Settings = mapSystem.Settings;
		voronoiFactory.LineMaterial = mapSystem.VoronoiLineMaterial;

		if ( !mapSystem.SceneReady )
		{
			mapSystem.InitializeScene( Seed );
		}
	}

	protected override void OnDestroy()
	{
		MapGeneratorSystem.Current.Cleanup();
	}

	[Button, Title("Generate Radius"), Description("Queue chunks to generate with a Reveal Radius from the above inspector field.")]
	public void PushRevealRadius()
	{
		if ( !GenerationReady || !GenerationComplete )
		{
			Log.Warning( "Can't push new chunks! Exiting..." );
			return;
		}
		
		if ( Seed != MapGeneratorSystem.Current.Settings.SeedText )
		{
			Log.Warning( "Can't reveal a different seed! Hit Regenerate Map to restart!" );
			return;
		}

		GenerationComplete = false;
		GenerationReady = false;
		
		Log.Info($"Queueing a radius of <color=green>{RevealRadius} chunks...</color>");
		MapGeneratorSystem.Current.GetChunkGenerationTasks( GeneratePosition, RevealRadius );
		
		GenerationReady = true;
		GenerationComplete = true;
	}
	
    [Button( "Regenerate Map" )]
    public async Task GenerateMap()
    {
	    // Try get a mapManager and start it
	    if (!MapGeneratorSystem.Current.SceneReady)
	    {
		    Log.Warning( "A MapGeneratorSystem wasn't initialised! It was probably in the editor, " +
		                 "so if you see this message in-game, panic" );
		    MapGeneratorSystem.Current.InitializeScene( Seed );
	    }

	    if ( !GenerationReady )
	    {
		    Log.Warning( "Generating in progress! Exiting..." );
		    return;
	    }

	    if ( GenerationComplete )
	    {
		    OnDestroy();
		    await Task.Frame();
		    OnStart();
		    await Task.Frame();
	    }
	    
	    GenerationComplete = false;
	    GenerationReady = false;
	    
	    Log.Info($"Scene for seed '{MapGeneratorSystem.Current.Settings.SeedText}' starting...");
	    MapGeneratorSystem.Current.GenerateWorld( GeneratePosition, RevealRadius, Seed );
	    GenerationReady = true;
	    GenerationComplete = true;
    }

    [Button( "Clear Map Generation" )]
    public async Task ClearMap()
    {
	    if ( !GenerationReady || !GenerationComplete )
	    {
		    Log.Warning( "No generation to clear! Exiting..." );
		    return;
	    }
	    GenerationReady = false;
	    GenerationComplete = false;
	    
	    OnDestroy();
	    await Task.Frame();
	    OnStart();
	    await Task.Frame();
	    
	    GenerationReady = true;
    }
}
