import * as pc from 'playcanvas';
import { Chunk } from '../../lib/generation/chunk';
import { type GameSettings } from '../GameScene';
import type { GlobalGenerationMeta } from '../../lib/generation/generator';
import type { OpenSimplexNoise } from '../../lib/generation/noise';

export class ChunkManager extends pc.Script {
    static scriptName = 'ChunkManager';
    static maxInitialChunks = 15;

    settings!: GameSettings;
    chunks: Map<string, Chunk> = new Map();

    tileSize = 1;
    maxCurrentChunks = ChunkManager.maxInitialChunks;
    maxChunksX!: number;
    maxChunksY!: number;
    
    initialise() {
        if (!this.settings)
            throw new Error('ChunkManager: Cannot find config! There\'s nothing to configure chunks with!');

        this.maxChunksX = this.settings.maxX / this.settings.chunkSize;
        this.maxChunksY = this.settings.maxX / this.settings.chunkSize;

        // @TODO: throw error for an amount of max chunks that would crash the game.

        // generate initial chunkset
    }

    // @TODO: This needs to detect if a relevant entity has triggered a chunk to update its neighors.
    // @TODO: this should cull chunk meshes if offscreen (and minimise their simulation)
    update() {}

    public updateChunkRadius(
        cameraGlobalX: number, cameraGlobalY: number,
        revealRadius: number = 4, // @TODO handle this better?
        settings: GameSettings,
        meta: GlobalGenerationMeta,
        noise: OpenSimplexNoise,
    ): void { 
        const units = this.settings.chunkSize * this.tileSize;
        const centerChunkX = Math.floor(cameraGlobalX / units);
        const centerChunkY = Math.floor(cameraGlobalY / units);

        const visibleKeys = new Set<string>();

        for (let xOffset = -revealRadius; xOffset <= revealRadius; xOffset++) {
            for (let yOffset = -revealRadius; yOffset <= revealRadius; yOffset++) {
                const targetX = centerChunkX + xOffset;
                const targetY = centerChunkY + yOffset;

                // clamp to stop from generating chunks out of bounds.
                if (targetX < 0 || targetX >= this.maxChunksX || targetY < 0 || targetY >= this.maxChunksY)
                    continue;

                const chunkKey = `${targetX},${targetY}`;
                visibleKeys.add(chunkKey);

                let chunk = this.chunks.get(chunkKey);

                if (!chunk) {
                    chunk = new Chunk(targetX, targetY, settings, meta, noise);
                    chunk.generate(targetX, targetY, settings);
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
