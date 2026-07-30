import * as pc from 'playcanvas';
import { Chunk } from '../../lib/generation/chunk';
import { type GameSettings } from '../GameScene';
import { MapGenerator } from '../../lib/generation/MapGenerator';

export class ChunkManager extends pc.Script {
    static scriptName = 'ChunkManager';
    static maxInitialChunks = 15;

    settings!: GameSettings;
    generator!: MapGenerator;
    chunks: Map<string, Chunk> = new Map();

    tileSize = 1;
    maxCurrentChunks = ChunkManager.maxInitialChunks;
    maxChunksX!: number;
    maxChunksY!: number;
    
    initialise() {
        if (!this.settings)
            throw new Error('ChunkManager: Cannot find config! There\'s nothing to configure chunks with!');

        this.maxChunksX = this.settings.worldWidth / this.settings.chunkSize;
        this.maxChunksY = this.settings.worldHeight / this.settings.chunkSize;

        // @TODO: throw error for an amount of max chunks that would crash the game.
    }

    // @TODO: This needs to detect if a relevant entity has triggered a chunk to update its neighors.
    // @TODO: this should cull chunk meshes if offscreen (and minimise their simulation)
    // @TODO: update chunk manager settings & refresh current grid if need detected or an event triggered.
    update() {}

    public updateChunkRadius(
        camGlobalX: number, camGlobalY: number, revealRadius: number = 4, // @TODO handle this better?
    ): void { 
        const units = this.settings.chunkSize * this.tileSize;
        const centerChunkX = Math.floor(camGlobalX / units);
        const centerChunkY = Math.floor(camGlobalY / units);

        const visibleKeys = new Set<string>();

        for (let xOffset = -revealRadius; xOffset < revealRadius; xOffset++) {
            for (let yOffset = -revealRadius; yOffset < revealRadius; yOffset++) {
                const targetX = centerChunkX + xOffset;
                const targetY = centerChunkY + yOffset;

                // clamp to stop from generating chunks out of bounds.
                // clamp by the reveal radius because it should generate circularly around the given Point<x,y>
                if (targetX < -revealRadius || targetX > revealRadius || targetY < -revealRadius || targetY > revealRadius)
                    continue;

                const chunkKey = `${targetX},${targetY}`;
                visibleKeys.add(chunkKey);

                let chunk = this.chunks.get(chunkKey);

                if (!chunk) {
                    // @TODO: split out GameSettings into ChunkSettings here.
                    chunk = new Chunk(targetX, targetY, this.generator, this.settings);
                    chunk.generate(targetX, targetY);
                    this.chunks.set(chunkKey, chunk);
                }

                if (!chunk.visualEntity) {
                    chunk.visualEntity = chunk.buildMesh(this.app.graphicsDevice);
                    this.entity.addChild(chunk.visualEntity);
                }

                chunk.visualEntity.enabled = true;
            }
        }

        // 2. Performance Clean Up: Disable or dismantle meshes that left the camera bounds
        // @TODO: I'll worry about this for areas the camera is far from, rather than leaving bounds.
        // this.chunks.forEach((chunk, key) => {
        //     if (!visibleKeys.has(key) && chunk.visualEntity) {
        //         // Option A: Just toggle visibility off to preserve heap memory if they revisit often
        //         chunk.visualEntity.enabled = false;

        //         // Option B: If RAM footprints become bloated, purge visuals entirely:
        //         // chunk.destroyVisuals();
        //     }
        // });

    }
}
