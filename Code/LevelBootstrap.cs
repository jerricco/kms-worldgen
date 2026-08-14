using System.Threading.Tasks;
using Sandbox.Systems.Map;

namespace Sandbox;

[Category("Scene Orchestration")]
public sealed class LevelBootstrap : Component
{
	[Property]
	public Vector2 GeneratePosition { get; set; } = new(0, 0);

	[Property, ReadOnly]
	public bool IsGenerating { get; set; }

	[Property, ReadOnly]
	public bool MapExists { get; set; } = true;

    [Property]
    public int RevealRadius { get; set; } = 8;

    [Property]
    public string Seed { get; set; } = "aborio rice";

	protected override void OnDestroy()
        => MapGeneratorSystem.Current.Cleanup();

    [Button]
	[Title("Generate Radius")]
	[Description("Queue chunks to generate with a Reveal Radius from the above inspector field.")]
	public async Task PushRevealRadius()
	{
        if (this.MapExists)
        {
            if (this.IsGenerating)
            {
                Log.Error("Generation already in progress! Exiting...");
                return;
            }

            // update the map
            this.IsGenerating = true;
            Log.Info($"Queueing a radius of <color=green>{this.RevealRadius} chunks...</color>");
            await MapGeneratorSystem.Current.GetChunkGenerationTasksAsync(this.GeneratePosition, this.RevealRadius);
            this.IsGenerating = false; // @TODO: Move these to MapGenerationSystem for better async tracking
        }
        else
        {
            this.GenerateMap();
        }
	}

    [Button( "Regenerate Map" )]
    public async Task GenerateMap()
    {
	    if (this.IsGenerating)
	    {
		    Log.Error( "Generation already in progress! Exiting..." );
		    return;
	    }

	    if (this.RevealRadius > 32)
	    {
		    Log.Error("Radius can be no larger than 32 chunks!");
		    return;
	    }

	    if (this.MapExists) this.ClearMap();

	    if (this.Seed != MapGeneratorSystem.Current.Settings.SeedText)
	    {
		    Log.Warning( "A new seed was entered! Regenerating." );
            this.ClearMap();
	    }

        this.IsGenerating = true;
	    Log.Info($"Seed text '{MapGeneratorSystem.Current.Settings.SeedText}' generating a new map...");
        await MapGeneratorSystem.Current.GenerateWorldAsync(this.GeneratePosition, this.RevealRadius, this.Seed);

        this.IsGenerating = false;
        this.MapExists = true;
    }

    [Button( "Clear Map Generation" )]
    public void ClearMap()
    {
	    if (this.IsGenerating)
	    {
		    Log.Warning( "Generating already in progress! Exiting..." );
		    return;
	    }

	    Log.Warning( "Clearing out old level generation..." );
	    MapGeneratorSystem.Current.Cleanup(); // cleanup old renderers
	    MapGeneratorSystem.Current.InitializeScene(this.Seed); // ensure any new seed is set.
        this.MapExists = false;
    }
}
