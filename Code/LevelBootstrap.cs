using Sandbox.Generation;

namespace Sandbox;

public sealed class LevelBootstrap : Component
{
    [Property] public MapGenerator Generator { get; set; }

    [Property] public GenerationSettings Settings { get; set; } = new();
    protected override void OnStart()
    {
        Log.Info($"Scene for seed '{Settings.SeedText}' starting...");
        Generator = Scene.GetAllComponents<MapGenerator>().FirstOrDefault();
        Generator?.Generate();
    }
	protected override void OnUpdate()
	{

	}
}
