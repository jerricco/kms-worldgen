import * as pc from 'playcanvas'
import { MapGenerator, type MapSettings } from '../../lib/generation/generator';
import type { Tile } from '../../lib/generation/types';
import { REGION_PALETTES } from '../../data/color';
import { hexToRgb } from '../../lib/utils';

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
        if (!this.seed) return; // we'll be ok.
        this.generation = new MapGenerator(this.seed, this.width, this.height, this.config);
        this.createVisualGrid()
    }
    
    update() {
        // We should never try to run this update unless BOTHH:
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

        const { positions, colors, normals, indices } = this.getMeshData();
        const gridEntity = new pc.Entity("Batched3DGrid");

        const mesh = new pc.Mesh(this.app.graphicsDevice);
        mesh.setPositions(positions);
        mesh.setNormals(normals);
        mesh.setColors(colors);
        mesh.setIndices(indices);
        mesh.update(pc.PRIMITIVE_TRIANGLES); 
        mesh.aabb.compute(positions)

        const material = new pc.StandardMaterial();
        material.useLighting = true;
        material.diffuseVertexColor = true;
        material.diffuseVertexColorChannel = "rgb";
        material.update();
        
        const meshInstance = new pc.MeshInstance(mesh, material, gridEntity);
        gridEntity.addComponent("render", {
            type: "box",
        });

        gridEntity.render!.meshInstances = [meshInstance]

        this.gridEntity = gridEntity
        this.entity.addChild(gridEntity);

        // @NOTE: this should be immediately flagged first before regenerating the map or it will never update
        this.shouldUpdateMap = false; 
        this.app.root.fire('map:rendered', this.generation) // fire a global event that the map has re-rendered

        return gridEntity;
    }

    getMeshData(): { [key: string]: number[] } {
        const positions: number[] = [];
        const colors: number[] = [];
        const normals: number[] = [];
        const indices: number[] = [];
        let vertexIndex = 0;

        for (let x = 0; x < this.width; x++) {
            for (let y = 0; y < this.height; y++) {
                const tile: Tile = this.generation.grid[x][y];

                // Fallback default colour if a cell profile is empty or out of bounds
                const tileColor = tile ? this.palette[tile.region.name] : new pc.Color(0.5, 0.5, 0.5);

                const xPos = x * this.tileSize;
                const zPos = y * this.tileSize;

                // Build out 4 flat corner vertices per tile
                positions.push(
                    xPos, 0, zPos + this.tileSize,                 // bottom left
                    xPos + this.tileSize, 0, zPos + this.tileSize, // bottom-right
                    xPos + this.tileSize, 0, zPos,                 // top-left
                    xPos, 0, zPos,                                 // top-right
                );

                for (let i = 0; i < 4; i++) {
                    normals.push(0, 1, 0);
                }

                const rgb = tileColor instanceof pc.Color ? tileColor : hexToRgb(tileColor);
                for (let i = 0; i < 4; i++) {
                    colors.push(rgb.r, rgb.g, rgb.b, 1.0);
                }

                indices.push(
                    vertexIndex, vertexIndex + 1, vertexIndex + 2,
                    vertexIndex, vertexIndex + 2, vertexIndex + 3
                );

                vertexIndex += 4;
            }
        }

        return { positions, colors, normals, indices }
    }
}