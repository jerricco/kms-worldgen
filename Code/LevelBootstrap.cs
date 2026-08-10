using Sandbox.Gameplay;
using Sandbox.GameObjectSystems.Map;
using Sandbox.Triangulation;

namespace Sandbox;

[Category("Scene Orchestration")]
public sealed class LevelBootstrap : Component
{
	public GameObject VoronoiFactoryGo { get; set; }
	public GameObject MapInteractionGo {  get; set; }
	
	[Property, ReadOnly] public bool GenerationReady = false;

	protected override void OnStart()
	{
		var mapSystem = MapGeneratorSystem.Current;
		mapSystem.InitializeScene( mapSystem.Settings.SeedText );
		
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
		if (VoronoiFactoryGo.IsValid) VoronoiFactoryGo.DestroyImmediate();
		if (MapInteractionGo.IsValid) MapInteractionGo.DestroyImmediate();
	}
	
    [Button( "Regenerate Map" )]
    public void GenerateMap()
    {
	    GenerationReady = false;
	    Log.Info($"Scene for seed '{MapGeneratorSystem.Current.Settings.SeedText}' starting...");
	    var voronoi = VoronoiFactoryGo.GetComponent<VoronoiFactory>();
	    MapGeneratorSystem.Current.GenerateWorld( new Vector2( 0, 0 ), 16, voronoi );
	    GenerationReady = true;
    }

    public bool CanGenerateMap()
    {
	    return !GenerationReady;
    }
}
