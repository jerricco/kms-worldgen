using System.Threading.Tasks;
using Sandbox.Gameplay;
using Sandbox.GameObjectSystems.Map;
using Sandbox.Triangulation;

namespace Sandbox;

[Category("Scene Orchestration")]
public sealed class LevelBootstrap : Component
{
	public GameObject VoronoiFactoryGo { get; set; }
	public GameObject MapInteractionGo {  get; set; }

	// editor properties for updating generation
	[Property] public string Seed;
	[Property] public Vector2 GeneratePosition = new Vector2( 0, 0 );
	[Property] public int RevealRadius = 8;
	// debug orchestration
	[Property, ReadOnly] public bool GenerationReady = false;
	[Property, ReadOnly] public bool GenerationComplete = false;

	protected override void OnStart()
	{
		var mapSystem = MapGeneratorSystem.Current;
		
		if (Seed == null) Seed = MapGeneratorSystem.Current.Settings.SeedText;
		
		mapSystem.InitializeScene( Seed );
		
		// Attach gameObjects
		VoronoiFactoryGo = new GameObject( true, "VoronoiFactory" );
		MapInteractionGo = new GameObject( true, "MapInteraction" );
		
		// attach components
		MapInteractionGo.AddComponent<TileInteractionManager>();
		var voronoiFactory = VoronoiFactoryGo.GetOrAddComponent<VoronoiFactory>();
		voronoiFactory.Settings = mapSystem.Settings;
		voronoiFactory.LineMaterial = mapSystem.VoronoiLineMaterial;
		voronoiFactory.Rng = mapSystem.Rng;
		
		GenerationReady = true; // @TODO: This will later orchestrate proper world generation
	}

	protected override void OnDestroy()
	{
		MapGeneratorSystem.Current.Cleanup();
		CleanupScene();
	}

	/// <summary>
	/// Cleans up the artifacts of this GameObjectSystem so that it can be reinitialized in the Scene.
	/// </summary>
	public void CleanupScene()
	{
		// @TODO: stop queue?
		// remove gameobjects
		if (VoronoiFactoryGo != null && VoronoiFactoryGo.IsValid) VoronoiFactoryGo.DestroyImmediate();
		if (MapInteractionGo != null && MapInteractionGo.IsValid) MapInteractionGo.DestroyImmediate();
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

    public bool CanGenerateMap()
    {
	    return !GenerationReady;
    }
}
