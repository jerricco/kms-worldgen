import * as pc from 'playcanvas';
import { PALETTES } from '../../data/color'; // @TODO: this needs to not be shit
import { RegionID } from './regions';
import { MapGenerator, type GenerationSettings } from './MapGenerator';
import { hexToRgb } from '../utils';

export type SerialChunk = 
    Pick<Chunk, 'chunkX' | 'chunkY' | 'elevations' | 'regionIds' | 'moisture' | 'temperatures' | 'materials'>

export class Chunk {
    static DEFAULT_SIZE = 50;

    public chunkX: number;
    public chunkY: number;

    public worldHeight: number;
    public worldWidth: number;
    public size = Chunk.DEFAULT_SIZE;
    public tileCount: number;

    public generator: MapGenerator;
    public settings: GenerationSettings;

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

    constructor(chunkX: number, chunkY: number, generator: MapGenerator, settings: GenerationSettings) {
        this.generator = generator;
        this.settings = settings;
        
        this.chunkX = chunkX;
        this.chunkY = chunkY;

        this.size = settings.chunkSize || Chunk.DEFAULT_SIZE;

        // default to 1 chunk in case of failure somehow
        this.tileCount = this.size * this.size;
        this.worldWidth = (settings.worldWidth || this.size) / 2;
        this.worldHeight = (settings.worldHeight || this.size) / 2;


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
        const palette = PALETTES['MAP']

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
        // mesh.aabb.compute(positions); // @TODO: Determine whether I need collisions calculated

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

    generate(chunkX: number, chunkY: number) {
        const chunk = this;

        for (let x = 0; x < this.size; x++) {
            for (let y = 0; y < this.size; y++) {
                const globalX = chunkX * this.size + x;
                const globalY = chunkY * this.size + y;

                // NO, OUT OF BOUND - BACK TIGER!
                if (globalX > this.worldWidth || globalY > this.worldHeight || globalX < -this.worldWidth || globalY < -this.worldHeight)
                    continue;

                // @TODO: this is clumsy, have the chunk itself arrange the pre-generated grid data?
                // The issue is that the map size is fixed, so I need to know how much to generate
                // prior to the chunk manager taking over and doing intensive things on the screen.
                // Since we're trying to eventually simulate full nations - even under the chunk
                // fog of war, I need to do more complete generation like this until it either
                // blows out the CPU or gives me a good generation/simulation division line.
                const tile = this.generator.generate(globalX, globalY);
                const localIndex = Chunk.getLocalIndex(x, y, this.size);

                // determine tile properties
                chunk.elevations[localIndex] = tile.elevation;
                this.determineTileRegion(localIndex, tile.elevation);
            }
        }

        return chunk
    }

    determineTileRegion(localIndex: number, elevation: number) {
        // @TODO: replace elevation based regions with dynamic climatic regions
        let region: RegionID = RegionID.UNASSIGNED;

        
        // MARINE regions - first establish a seafloor
        if (elevation < this.settings.seaLevel) {
            if (elevation === this.settings.abyssalLevel) {
                region = RegionID.CRUST_FLOOR;
            } else if (elevation < this.settings.trenchLevel) {
                region = RegionID.ABYSSAL_OCEAN;
            } else if (elevation < this.settings.deepOceanLevel) {
                region = RegionID.DEEP_OCEAN;
            } else { // below oceanLevel
                region = RegionID.OCEAN;
            }
        }
        // TRANSITIONAL regions - create terminals between land and sea
        else if (elevation < this.settings.beachLevel) {
            region = RegionID.BEACH;
        }
        // FLAT TERRESTRIAL regions - mainland
        else if (elevation < this.settings.plainLevel) {
            region = RegionID.PLAIN;
        }
        // MOUNTAINOUS TERRESTRIAL REGIONS - higher elevations
        else if (elevation < this.settings.hillLevel) {
            region = RegionID.HILL;
        }
        else if (elevation < this.settings.peakLevel) {
            region = RegionID.MOUNTAIN
        } else if (elevation > this.settings.peakLevel) {
            region = RegionID.PEAK
        }

        this.regionIds[localIndex] = region;
    }

    // Fast inline index helper mapping local 2D space to 1D space
    static getLocalIndex(x: number, y: number, size: number = Chunk.DEFAULT_SIZE): number {
        return x * size + y;
    }

    // returns blank, copyable chunk data for saving
    serialize(): SerialChunk {
        return {
            chunkX: this.chunkX,
            chunkY: this.chunkY,
            elevations: new Float32Array(this.elevations),
            regionIds: new Int32Array(this.regionIds),
            moisture: new Float32Array(this.moisture),
            temperatures: new Float32Array(this.temperatures),
            materials: new Float32Array(this.materials),
        }
    }

    // unwinds save data chunk into a valid Chunk object
    static unserialize(serialData: SerialChunk, generator: MapGenerator, settings: GenerationSettings): Chunk {
        const chunk: Chunk = new Chunk(serialData.chunkX, serialData.chunkY, generator, settings);
        chunk.elevations = new Float32Array(serialData.elevations);
        chunk.regionIds = new Int32Array(serialData.regionIds);
        chunk.moisture = new Float32Array(serialData.moisture);
        chunk.temperatures = new Float32Array(serialData.temperatures);
        chunk.materials = new Float32Array(serialData.materials);

        return chunk;
    }
}