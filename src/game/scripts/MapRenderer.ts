import * as pc from 'playcanvas'
import { MapGenerator, type MapSettings } from '../../lib/generation/generator';
import { REGION_PALETTES } from '../../data/color';
import { Chunk } from '../../lib/generation/chunk';

export class MapRenderer extends pc.Script {
    static scriptName = 'MapRenderer';

    /**
     * Determines the colouring for the map topology.
     * 
     * @attribute
     * @type {{ [KEY: string]: string }}
     * @title Colour Palette
     */
    palette = REGION_PALETTES['MAP']
    
    /**
     * Object of perlin grid generation.
     * 
     * @attribute
     * @type {MapGenerator}
     * @title Map Generation
     */
    generation?: MapGenerator
    gridEntity?: pc.Entity;

    tileSize = 1
    targetLayer = 1

    width: number = MapGenerator.DEFAULT_WIDTH;
    height: number = MapGenerator.DEFAULT_HEIGHT;
    seed ?: string;
    config: MapSettings = {} as MapSettings;
    shouldUpdateMap: boolean = true;

    initialize() {
        if (!this.seed) return; // we'll be ok - it's just typescript having a hissy
        this.generation = new MapGenerator(this.seed, this.width, this.height, this.config);
        this.createVisualGrid()
    }
    
    update() {
        // We should never try to run this update unless BOTH:
        // - there has been a generation happen correctly in initialize
        // - shouldUpdateMap has been set against the MapGenerator to true elsewhere prior.
        if (!this.shouldUpdateMap || !this.generation) return;
        
        const seedUpdated = this.generation.seed !== this.seed
        const widthUpdated = this.generation.width !== this.width
        const heightUpdated = this.generation.height !== this.height
        
        if (seedUpdated || widthUpdated || heightUpdated) {
            this.generation = new MapGenerator(
                this.seed || MapGenerator.DEFAULT_SEED, 
                this.width || MapGenerator.DEFAULT_WIDTH, 
                this.height || MapGenerator.DEFAULT_HEIGHT, 
                this.config || MapGenerator.GENERATOR_DEFAULTS
            );

            this.createVisualGrid()
        }
    }

    public createVisualGrid(): pc.Entity {
        if (this.gridEntity) {
            this.gridEntity.enabled = false;
            this.gridEntity.destroy();
        }

        // @DEBUG
        const CHUNK_LIMIT = 15; 
        const chunkEntities = [];
        for (let x = 0; x < CHUNK_LIMIT; x++) {
            for (let y = 0; y < CHUNK_LIMIT; y++) {
                const localIndex = Chunk.getLocalIndex(x, y);
                const chunk = this.generation?.chunks[localIndex];
                chunkEntities.push(chunk?.buildMesh(this.app.graphicsDevice, this.tileSize));
            }
        }

        if (chunkEntities.length === 0) return new pc.Entity("EmptyMapGrid");

        const gridEntity = new pc.Entity("MapGridEntity");
        chunkEntities.forEach((entity) => entity ? gridEntity.addChild(entity) : null)
        this.gridEntity = gridEntity

        // @NOTE: this should be immediately flagged first before regenerating the map or it will never update
        this.shouldUpdateMap = false; 
        this.app.root.fire('map:rendered', this.generation) // fire a global event that the map has re-rendered

        return gridEntity;
    }
}