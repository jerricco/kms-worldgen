using Sandbox.Generation;
using Sandbox.Generator;
using System;
using System.Threading.Tasks;

namespace Sandbox;

[Category("Scene Orchestration")]
public sealed class LevelBootstrap : Component
{
    [Property] public MapGenerator Generator { get; set; }

    [Property] public GenerationSettings Settings { get; set; }
    protected override async void OnStart()
    {
	    if (Settings == null) {
		    throw new InvalidOperationException("Critical GenerationSettings object could not be loaded");
	    }
	    
        Log.Info($"Scene for seed '{Settings.SeedText}' starting...");
        Generator = Scene.GetAllComponents<MapGenerator>().FirstOrDefault();
        Generator?.Generate();
    }
}
