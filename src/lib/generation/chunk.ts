import * as pc from 'playcanvas';
import { REGION_PALETTES } from '../../data/color'; // @TODO: this needs to not be shit
import { determineTileRegion, RegionID } from '../../lib/generation/regions';
import { getGlobalTileElevation } from './analysis';
import type { GlobalGenerationMeta } from './generator';
import type { OpenSimplexNoise } from './noise';
import { hexToRgb } from '../utils';

export type ChunkSettings = {
    maxX: number, maxY: number,
    chunkSize: number,
    macroScale: number,
    islandRadius: number,
    squishFactor: number,

    seaLevel: number,     // @TODO: defaults
    abyssalLevel: number, // @TODO: defaults
    trenchLevel: number,  // @TODO: defaults
    beachLevel: number,   // @TODO: defaults
    plainLevel: number,   // @TODO: defaults
    hillLevel: number,    // @TODO: defaults
    peakLevel: number,    // @TODO: defaults
}


export class Chunk {
    static DEFAULT_SIZE = 50;

    public chunkX: number;
    public chunkY: number;

    public maxX: number;
    public maxY: number;
    public size = Chunk.DEFAULT_SIZE;
    public tileCount: number;
    public globalMeta: GlobalGenerationMeta;
    public noise: OpenSimplexNoise;

    private _isActive: boolean = true;
    get isActive():boolean {
        return this._isActive;
    }

    set isActive(value: boolean) {
        if (value === false && this.visualEntity) {
            this.destroyVisuals();
        }
        this._isActive = value;
    }

    public elevations: Float32Array;   // 4 bytes per tile
    public regionIds: Int32Array;      // 4 bytes per tile (IDs pointing to a global Region Registry)
    public moisture: Float32Array;     // 4 bytes per tile
    public temperatures: Float32Array; // 4 bytes per tile
    public materials: Float32Array;    // 4 bytes per tile

    public visualEntity: pc.Entity | null = null;

    constructor(chunkX: number, chunkY: number, settings: ChunkSettings, meta: GlobalGenerationMeta, noise: OpenSimplexNoise) {
        this.globalMeta = meta;
        this.noise = noise;
        
        this.chunkX = chunkX;
        this.chunkY = chunkY;

        this.size = settings.chunkSize || Chunk.DEFAULT_SIZE;

        // default to 1 chunk in case of failure somehow
        this.tileCount = this.size * this.size;
        this.maxX = settings.maxX || this.size;
        this.maxY = settings.maxY || this.size;

        this.elevations   = new Float32Array(this.tileCount);
        this.regionIds    = new Int32Array(this.tileCount);
        this.moisture     = new Float32Array(this.tileCount);
        this.temperatures = new Float32Array(this.tileCount);
        this.materials    = new Float32Array(this.tileCount);
    }

    buildMesh(
        graphicsDevice: pc.GraphicsDevice,
        tileSize: number = 1,
    ): pc.Entity {
        const palette = REGION_PALETTES['MAP']

        const positions: number[] = [];
        const colors   : number[] = [];
        const normals  : number[] = [];
        const indices  : number[] = [];
        let vertexIndex = 0;

        // Base world coordinate offset where this chunk begins on X/Z plane
        const chunkBaseX = this.chunkX * this.size * tileSize;
        const chunkBaseZ = this.chunkY * this.size * tileSize;

        for (let x = 0; x < this.size; x++) {
            for (let z = 0; z < this.size; z++) {
                const localIndex = x * this.size + z;

                // Fetch colors
                const regionId = this.regionIds[localIndex];
                const regionStr = Object.keys(RegionID).find((r) => RegionID[r] === regionId) || 'VOID';
                const tileColor = palette[regionStr] ? hexToRgb(palette[regionStr]) : new pc.Color(0.5, 0.5, 0.5);

                // Local positional offsets relative to the chunk's global space positioning
                const xPos = chunkBaseX + (x * tileSize);
                const zPos = chunkBaseZ + (z * tileSize);
                const yPos = 0; // Flat mapping for X/Z space layout

                // Build 4 flat corner vertices per tile
                positions.push(
                    xPos, yPos, zPos + tileSize,            // bottom-left
                    xPos + tileSize, yPos, zPos + tileSize, // bottom-right
                    xPos + tileSize, yPos, zPos,            // top-right
                    xPos, yPos, zPos                        // top-left
                );

                for (let i = 0; i < 4; i++) {
                    normals.push(0, 1, 0); // Flat normal faces pointing UP
                }

                for (let i = 0; i < 4; i++) {
                    colors.push(tileColor.r, tileColor.g, tileColor.b, 1.0);
                }

                indices.push(
                    vertexIndex, vertexIndex + 1, vertexIndex + 2,
                    vertexIndex, vertexIndex + 2, vertexIndex + 3
                );

                vertexIndex += 4;
            }
        }

        // Allocate WebGL Mesh Data structures cleanly
        const mesh = new pc.Mesh(graphicsDevice);
        mesh.setPositions(positions);
        mesh.setNormals(normals);
        mesh.setColors(colors);
        mesh.setIndices(indices);
        mesh.update(pc.PRIMITIVE_TRIANGLES);
        mesh.aabb.compute(positions);

        const material = new pc.StandardMaterial();
        material.useLighting = true;
        material.diffuseVertexColor = true;
        material.diffuseVertexColorChannel = "rgb";
        material.update();

        const entity = new pc.Entity(`Chunk_${this.chunkX}_${this.chunkY}`);
        const meshInstance = new pc.MeshInstance(mesh, material, entity);

        entity.addComponent("render");
        entity.render!.meshInstances = [meshInstance];

        return entity;
    }

    destroyVisuals(): void {
        if (this.visualEntity) {
            this.visualEntity.destroy();
            this.visualEntity = null;
        }
    }

    generate(chunkX: number, chunkY: number, settings: ChunkSettings) {
        const chunk = this;

        for (let x = 0; x < this.size; x++) {
            for (let y = 0; y < this.size; y++) {
                const globalX = chunkX * this.size + x;
                const globalY = chunkY * this.size + y;

                // NO, OUT OF BOUND - BACK TIGER!
                if (globalX >= this.maxX || globalY >= this.maxY || globalX < 0 || globalY < 0)
                    continue;

                const elevation = getGlobalTileElevation(globalX, globalY, settings, this.globalMeta, this.noise);
                const localIndex = Chunk.getLocalIndex(x, y, this.size);

                chunk.elevations[localIndex] = elevation;
                determineTileRegion(globalX, globalY, localIndex, chunk, elevation, settings, this.globalMeta, this.noise);
            }
        }

        console.log(chunk)
        return chunk
    }

    // Fast inline index helper mapping local 2D space to 1D space
    static getLocalIndex(x: number, y: number, size: number = Chunk.DEFAULT_SIZE): number {
        return x * size + y;
    }
}