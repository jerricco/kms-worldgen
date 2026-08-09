using Sandbox.Generation;
using Sandbox.Generator;
using System;
using System.Threading.Tasks;

namespace Sandbox;

[Category("Scene Orchestration")]
public sealed class LevelBootstrap : Component
{
	[Property] public GenerationSettings Settings { get; set; }
	public GameObject MapManagerGo { get; set; }
    public MapGenerator Generator { get; set; }

    protected async override void OnStart()
    {
	    if (Settings == null) {
		    throw new InvalidOperationException("Critical GenerationSettings object could not be loaded");
	    }
	    
	    Log.Info($"Scene for seed '{Settings.SeedText}' starting...");
	    MapManagerGo = new GameObject(true, "MapManager");
        Generator = MapManagerGo.Components.Create<MapGenerator>();
    }

    protected override void OnDestroy()
    {
	    Generator.Destroy();
	    MapManagerGo.Destroy();
    }
    
    [Button( "Regenerate Map" )]
    public void GenerateMap()
    {
	    if ( Generator == null ) return;
		Generator.Generate();    
    }
}
