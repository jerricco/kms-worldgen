using System.Threading.Tasks;
using Sandbox.GameObjectSystems.Map;

namespace Sandbox;

[Category("Scene Orchestration")]
public sealed class LevelBootstrap : Component
{
	[Property]
	public Vector2 GeneratePosition { get; set; } = new(0, 0);

	[Property, ReadOnly]
	public bool GenerationComplete { get; set; }

	[Property, ReadOnly]
	public bool GenerationReady { get; set; } = true;

	[Property]
	public int RevealRadius { get; set; }

	// editor properties for updating generation
	[Property]
	public string Seed { get; set; } = "aborio rice";

	protected override void OnStart()
	{
		var mapSystem = MapGeneratorSystem.Current;
		if (string.IsNullOrWhiteSpace(this.Seed))
		{
			this.Seed = mapSystem.Settings.SeedText;
		}

		if (!mapSystem.SceneReady)
		{
			mapSystem.InitializeScene(this.Seed);
		}
	}

	protected override void OnDestroy()
        => MapGeneratorSystem.Current.Cleanup();

    [Button]
	[Title("Generate Radius")]
	[Description("Queue chunks to generate with a Reveal Radius from the above inspector field.")]
	public async Task PushRevealRadius()
	{
		if (!this.GenerationReady || !this.GenerationComplete)
		{
			Log.Warning("Can't push new chunks! Exiting...");
			return;
		}

		if (this.Seed != MapGeneratorSystem.Current.Settings.SeedText)
		{
			Log.Warning("Can't reveal a different seed! Hit Regenerate Map to restart!");
			return;
		}

		this.GenerationComplete = false;
		this.GenerationReady = false;

		Log.Info($"Queueing a radius of <color=green>{this.RevealRadius} chunks...</color>");
		await MapGeneratorSystem.Current.GetChunkGenerationTasksAsync(this.GeneratePosition, this.RevealRadius);

		this.GenerationReady = true;
		this.GenerationComplete = true;
	}

	[Button("Regenerate Map")]
	public async Task GenerateMap()
	{
		// Try get a mapManager and start it
		if (!MapGeneratorSystem.Current.SceneReady)
		{
			Log.Warning(
				"A MapGeneratorSystem wasn't initialised! It was probably in the editor, " +
				"so if you see this message in-game, panic"
			);
			MapGeneratorSystem.Current.InitializeScene(this.Seed);
		}

		if (!this.GenerationReady)
		{
			Log.Warning("Generating in progress! Exiting...");
			return;
		}

		if (this.GenerationComplete)
		{
			this.OnDestroy();
			this.OnStart();
		}

		this.GenerationComplete = false;
		this.GenerationReady = false;

		Log.Info($"Scene for seed '{MapGeneratorSystem.Current.Settings.SeedText}' starting...");
		await MapGeneratorSystem.Current.GenerateWorldAsync(this.GeneratePosition, this.RevealRadius, this.Seed);
		this.GenerationReady = true;
		this.GenerationComplete = true;
	}

	[Button("Clear Map Generation")]
	public void ClearMap()
	{
		this.GenerationReady = false;
		this.GenerationComplete = false;

		this.OnDestroy();
		this.OnStart();

		this.GenerationReady = true;
	}
}
