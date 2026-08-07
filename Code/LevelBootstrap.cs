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
    protected override void OnStart()
    {
	    if (Settings == null) {
		    throw new InvalidOperationException("Critical GenerationSettings object could not be loaded");
	    }
	    
        Log.Info($"Scene for seed '{Settings.SeedText}' starting...");
        Generator = Scene.GetAllComponents<MapGenerator>().FirstOrDefault();
        
        float startTime = RealTime.Now; // @DEBUG
        Task genTask = Generator?.Generate();
        genTask.ContinueWith( t =>
        {
	        if ( t.IsFaulted )
		        Log.Warning( $"Chunk generation failed! Reason: {t.Exception.Message}" );
	        else
		        Log.Info( $"All requested chunks generated! Took {RealTime.Now - startTime} s" );
	        
	        
	        Log.Info( "===========================================================" ); // end generation block
        } );
    }
}
