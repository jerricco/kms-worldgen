
export class Chunk {
    static SIZE = 50;
    static TILE_COUNT = this.SIZE * this.SIZE;

    public chunkX: number;
    public chunkY: number;
    public isActive: boolean = true;

    public elevations: Float32Array;   // 4 bytes per tile
    public regionIds: Int32Array;      // 4 bytes per tile (IDs pointing to a global Region Registry)
    public moisture: Float32Array;     // 4 bytes per tile
    public temperatures: Float32Array; // 4 bytes per tile
    public materials: Float32Array;    // 4 bytes per tile

    constructor(chunkX: number, chunkY: number) {
        this.chunkX = chunkX;
        this.chunkY = chunkY;

        this.elevations   = new Float32Array(Chunk.TILE_COUNT);
        this.regionIds    = new Int32Array(Chunk.TILE_COUNT);
        this.moisture     = new Float32Array(Chunk.TILE_COUNT);
        this.temperatures = new Float32Array(Chunk.TILE_COUNT);
        this.materials    = new Float32Array(Chunk.TILE_COUNT);
    }

    // Fast inline index helper mapping local 2D space to 1D space
    static getLocalIndex(x: number, y: number): number {
        return x * this.TILE_COUNT + y;
    }
}