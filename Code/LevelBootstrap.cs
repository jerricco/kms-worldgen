using Sandbox.Generation;
using System;

namespace Sandbox;

public sealed class LevelBootstrap : Component
{
    [Property] public MapGenerator Generator { get; set; }

    [Property] public GenerationSettings Settings { get; set; }
    protected override void OnStart()
    {
	    if (Settings == null) {
		    throw new InvalidOperationException("Critical GenerationSettings object could not be loaded");
	    }
	    
        Log.Info($"Scene for seed '{Settings.SeedText}' starting...");
        Generator = Scene.GetAllComponents<MapGenerator>().FirstOrDefault();
        Generator?.Generate();
    }
	protected override void OnUpdate()
	{

	}
}
