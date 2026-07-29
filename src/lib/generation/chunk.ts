import * as pc from 'playcanvas';
import { REGION_PALETTES } from '../../data/color'; // @TODO: this needs to not be shit
import { RegionID } from '../../lib/generation/regions';

export class Chunk {
    static SIZE = 50;
    static TILE_COUNT = this.SIZE * this.SIZE;

    public chunkX: number;
    public chunkY: number;

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

    constructor(chunkX: number, chunkY: number) {
        this.chunkX = chunkX;
        this.chunkY = chunkY;

        this.elevations   = new Float32Array(Chunk.TILE_COUNT);
        this.regionIds    = new Int32Array(Chunk.TILE_COUNT);
        this.moisture     = new Float32Array(Chunk.TILE_COUNT);
        this.temperatures = new Float32Array(Chunk.TILE_COUNT);
        this.materials    = new Float32Array(Chunk.TILE_COUNT);
    }

    buildMesh(
        graphicsDevice: pc.GraphicsDevice,
        tileSize: number = 1,
    ): pc.Entity {
        const palette = REGION_PALETTES['MAP']

        const positions: number[] = [];
        const colors: number[] = [];
        const normals: number[] = [];
        const indices: number[] = [];
        let vertexIndex = 0;

        // Base world coordinate offset where this chunk begins on X/Z plane
        const chunkBaseX = this.chunkX * Chunk.SIZE * tileSize;
        const chunkBaseZ = this.chunkY * Chunk.SIZE * tileSize;

        for (let x = 0; x < Chunk.SIZE; x++) {
            for (let z = 0; z < Chunk.SIZE; z++) {
                const localIndex = x * Chunk.SIZE + z;

                // Fetch colors
                const regionId = this.regionIds[localIndex];
                // @TODO: make this better
                const regionStr = Object.keys(RegionID).find((r) => RegionID[r] === regionId) || 'VOID';
                const tileColor = palette[regionStr] || new pc.Color(0.5, 0.5, 0.5);

                // Local positional offsets relative to the chunk's global space positioning
                const xPos = chunkBaseX + (x * tileSize);
                const zPos = chunkBaseZ + (z * tileSize);
                const yPos = 0; // Flat mapping for X/Z space layout

                // Build 4 flat corner vertices per tile
                positions.push(
                    xPos, yPos, zPos + tileSize, // bottom-left
                    xPos + tileSize, yPos, zPos + tileSize, // bottom-right
                    xPos + tileSize, yPos, zPos,            // top-right
                    xPos, yPos, zPos             // top-left
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

    // Fast inline index helper mapping local 2D space to 1D space
    static getLocalIndex(x: number, y: number): number {
        return x * this.SIZE + y;
    }
}