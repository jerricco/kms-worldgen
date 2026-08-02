import * as pc from 'playcanvas';
import { MapGenerator } from './MapGenerator';
import { Delaunay } from "d3-delaunay";

type VoronoiFactoryContext = MapGenerator;
export interface VoronoiSite {
    id: number;
    position: pc.Vec2;
    plateId: number,
    isOceanic: boolean;
    baseElevation: number;
}

export class VoronoiFactory {
    delaunay!: Delaunay<number>;
    generator: VoronoiFactoryContext;
    sites: VoronoiSite[] = [];

    private plateCenters: pc.Vec2[] = [];
    private plateElevationBiases: number[] = [];
    private continentalFragmentationFactor: number = 0.45;
    private macroBayFrequency: number = 0.0035;
    private macroBayIntensity: number = 0.28;

    constructor(context: VoronoiFactoryContext) {
        this.generator = context;
    }
    
    generate() {
        this.#generateTectonicSpine();
        this.sites = this.#assembleVoronoiSites();
        this.delaunay = this.#buildDelaunay()
    }

    /**
     * PASS 1: THE MACRO TECTIONIC SPINE
     * Generates a linear, curved skeletal structure across the map space
     * to group separate land masses into long continental systems like the Americas.
     */
    #generateTectonicSpine(): void {
        const { halfW, halfH, settings, rng } = this.generator;
        const maxDimension = Math.max(settings.worldWidth, settings.worldHeight);

        this.plateCenters = [];
        this.plateElevationBiases = [];
        this.continentalFragmentationFactor = rng.nextRange(0.35, 0.60);

        // Randomize the bay/gulf shapes dynamically per seed
        this.macroBayFrequency = rng.nextRange(0.002, 0.005);
        this.macroBayIntensity = rng.nextRange(0.20, 0.35);

        const numPlates = rng.nextRange(6, 9);
        const spineAngle = rng.nextRange(0, Math.PI * 2);
        const spineDirectionX = Math.cos(spineAngle);
        const spineDirectionY = Math.sin(spineAngle);

        for (let p = 0; p < numPlates; p++) {
            const progress = (p / (numPlates - 1)) * 2.0 - 1.0;

            const bowIntensity = maxDimension * 0.18;
            const bowNoise = Math.sin(progress * Math.PI) * bowIntensity;

            const px = (spineDirectionX * progress * halfW * 0.6) + (-spineDirectionY * bowNoise);
            const py = (spineDirectionY * progress * halfH * 0.6) + (spineDirectionX * bowNoise);

            this.plateCenters.push(new pc.Vec2(px, py));
            this.plateElevationBiases.push(rng.nextRange(-0.15, 0.45));
        }
    }

    /**
     * PASS 2: GEOLOGICAL FIELD EVALUATION
     * Pure function that assesses a single coordinate and returns its total 
     * structural land chance value [0.0 - 1.0] and its closest plate tracking metadata.
     */
    #evaluateGeologicalField(x: number, y: number): { landChance: number, closestPlateId: number } {
        const { halfW, settings, noise } = this.generator;
        const maxDimension = Math.max(settings.worldWidth, settings.worldHeight);

        const warpedSpace = this.generator.getDomainWarpedSample(x, y);

        const macroShapeNoise = (noise.noise2D(warpedSpace.sampleX * 0.8, warpedSpace.sampleY * 0.8) + 1.0) * 0.5;
        const channelNoise = (noise.noise2D(warpedSpace.sampleY * 2.5, warpedSpace.sampleX * 2.5) + 1.0) * 0.5;

        // macro erosion pass
        // Creates sweeping coastal indentations, large seas, and wide bays carving into the core spine
        const bayNoise = noise.noise2D(x * this.macroBayFrequency, y * this.macroBayFrequency);
        const gulfCarvingPass = Math.pow((bayNoise + 1.0) * 0.5, 1.5) * this.macroBayIntensity;

        let closestPlateId = 0;
        let minPlateDistSq = Infinity;
        for (let p = 0; p < this.plateCenters.length; p++) {
            const dx = x - this.plateCenters[p].x;
            const dy = y - this.plateCenters[p].y;
            const dSq = dx * dx + dy * dy;
            if (dSq < minPlateDistSq) {
                minPlateDistSq = dSq;
                closestPlateId = p;
            }
        }
        const distToClosestPlate = Math.sqrt(minPlateDistSq);

        const plateInfluenceRadius = maxDimension * 0.42;
        const tectonicProximity = Math.max(0.0, Math.min(1.0, 1.0 - (distToClosestPlate / plateInfluenceRadius)));
        const continentalCoreMask = Math.pow(tectonicProximity, 1.2);

        let globalLandChance = pc.math.lerp(macroShapeNoise * 0.4, 0.46 + macroShapeNoise * 0.54, continentalCoreMask);

        if (channelNoise < this.continentalFragmentationFactor) {
            globalLandChance *= (channelNoise / this.continentalFragmentationFactor);
        }

        // Apply our seed-driven macro bay/gulf carving pass directly to the land profile
        globalLandChance = Math.max(0.0, globalLandChance - gulfCarvingPass);

        const distToCenter = Math.sqrt(warpedSpace.sampleX * warpedSpace.sampleX + warpedSpace.sampleY * warpedSpace.sampleY);
        const maxRadius = halfW * (settings.oceanClamp || 0.85);
        const boundaryBuffer = Math.max(0.0, Math.min(1.0, distToCenter / maxRadius));
        globalLandChance = Math.max(0.0, globalLandChance - Math.pow(boundaryBuffer, 4.0));

        return { landChance: globalLandChance, closestPlateId };
    }

    /**
     * PASS 3: ASSEMBLY
     * Implements the randomized rejection loop, sampling positions across the 
     * world grid and registering valid nodes into the final site collections.
     */
    #assembleVoronoiSites(): VoronoiSite[] {
        const { halfW, halfH, settings, rng, noise } = this.generator;
        const localSites: VoronoiSite[] = [];

        const worldW = settings.worldWidth;
        const worldH = settings.worldHeight;
        const baseSpacing = Math.max(30, settings.chunkSize || 50);
        const targetPoints = Math.floor((worldW * worldH) / (baseSpacing * baseSpacing));

        let siteIdCounter = 0;
        let attempts = 0;
        const maxAttempts = targetPoints * 12;

        while (localSites.length < targetPoints && attempts < maxAttempts) {
            attempts++;

            const rotX = -halfW + (rng.next() * worldW);
            const rotY = -halfH + (rng.next() * worldH);

            // check basic field values to determine sampling density
            const densityField = this.#evaluateGeologicalField(rotX, rotY);
            const acceptanceProbability = pc.math.lerp(0.012, 1.0, Math.pow(densityField.landChance, 1.2));

            if (rng.next() > acceptanceProbability) continue;

            // compute the twist displacement variables
            const twistFreq = 1.0 / (baseSpacing * 5.0);
            const twistAngle = noise.noise2D(rotX * twistFreq, rotY * twistFreq) * Math.PI * 2;
            const twistIntensity = baseSpacing * 0.7 * (1.0 - densityField.landChance);

            const finalX = rotX + Math.cos(twistAngle) * twistIntensity;
            const finalY = rotY + Math.sin(twistAngle) * twistIntensity;

            if (finalX < -halfW || finalX >= halfW || finalY < -halfH || finalY >= halfH) {
                continue;
            }

            // find the geological field from the final compass point.
            const finalField = this.#evaluateGeologicalField(finalX, finalY);

            const isOceanic = finalField.landChance < 0.42;
            let baseElevation = settings.seaLevel || 0.0;

            const sLevel = settings.seaLevel ?? 0.0;
            const aLevel = settings.abyssalLevel ?? -1.0;
            const pLevel = settings.peakLevel ?? 0.95;

            if (isOceanic) {
                const trueDist = Math.sqrt(finalX * finalX + finalY * finalY);
                const trueRatio = Math.max(0, Math.min(1.0, trueDist / (halfW * (settings.oceanClamp || 0.85))));
                const trenchFactor = Math.pow(trueRatio, 1.8);
                // Smoothly grades the ocean down into your custom abyssal depths
                baseElevation = pc.math.lerp(sLevel - 0.05, aLevel, trenchFactor) + (this.plateElevationBiases[finalField.closestPlateId] * 0.08);
            } else {
                // This stops points from bunching up in the Plains tier, forcing values 
                // to distribute up through Hill (0.58) and Mountain (0.70) levels.
                const landProgress = (finalField.landChance - 0.42) / 0.58;
                const exponentialRise = Math.pow(landProgress, 1.6);
                baseElevation = pc.math.lerp(sLevel + 0.02, pLevel, exponentialRise) + (this.plateElevationBiases[finalField.closestPlateId] * 0.15);
            }

            localSites.push({
                id: siteIdCounter++,
                position: new pc.Vec2(finalX, finalY),
                plateId: finalField.closestPlateId,
                isOceanic: isOceanic,
                baseElevation: Math.max(-1.0, Math.min(1.0, baseElevation))
            });
        }

        return localSites;
    }

    #buildDelaunay(): Delaunay<number> {
        const flatCoordinates = new Float64Array(this.sites.length * 2);
        for (let i = 0; i < this.sites.length; i++) {
            flatCoordinates[i * 2] = this.sites[i].position.x;
            flatCoordinates[i * 2 + 1] = this.sites[i].position.y;
        }
        return new Delaunay(flatCoordinates);
    }

    // DEBUG
    getDebugMesh(): { bodies: pc.Entity, borders: pc.Entity, dots: pc.Entity } {
        const bodyPositions: number[] = [];
        // const bodyNormals: number[] = [];
        const bodyColors: number[] = [];
        const bodyIndices: number[] = [];

        const borderPositions: number[] = [];
        const borderColors: number[] = [];
        const borderIndices: number[] = [];

        const dotPositions: number[] = [];
        const dotColors: number[] = [];
        const dotIndices: number[] = [];

        let bodyVertIndex = 0;
        let borderVertIndex = 0;
        let dotVertexIndex = 0;

        // compute voronoi region bulks
        for (const site of this.sites) {
            const r = this.generator.rng.nextRange(50, 230) / 255;
            const g = this.generator.rng.nextRange(50, 230) / 255;
            const b = this.generator.rng.nextRange(50, 230) / 255;

            const polygonVertices = this.#getPolygonVertices(site, this.sites);
            if (polygonVertices.length < 3) continue;

            const sx = site.position.x;
            const sy = site.position.y;

            // --- 1. Procedural Solid Quad Center Dots (Layered at Y = 0.02) ---
            const dPivot = dotVertexIndex;
            const dotHalfSize = 2;

            dotPositions.push(sx - dotHalfSize, 0.02, sy - dotHalfSize);
            dotPositions.push(sx + dotHalfSize, 0.02, sy - dotHalfSize);
            dotPositions.push(sx + dotHalfSize, 0.02, sy + dotHalfSize);
            dotPositions.push(sx - dotHalfSize, 0.02, sy + dotHalfSize);

            // Push black colors to all 4 corners of the quad
            for (let k = 0; k < 4; k++) {
                dotColors.push(0.0, 0.0, 0.0, 1.0);
            }

            // Triangulate the square dot
            dotIndices.push(dPivot, dPivot + 1, dPivot + 2);
            dotIndices.push(dPivot, dPivot + 2, dPivot + 3);
            dotVertexIndex += 4;

            // build mesh data for solid coloured cell body
            // pivot point is the central site position
            const pivotIdx = bodyVertIndex;
            bodyPositions.push(sx, 95, sy); // Map Y to 3D Z
            bodyColors.push(r, g, b, 1.0);
            bodyVertIndex++;

            const startPeripheralIdx = bodyVertIndex;
            for (const vertex of polygonVertices) {
                bodyPositions.push(vertex.x, 95, vertex.y);
                bodyColors.push(r, g, b, 1.0);
                bodyVertIndex++;
            }

            const numPoints = polygonVertices.length;
            for (let i = 0; i < numPoints; i++) {
                const currentPointIdx = startPeripheralIdx + i;
                const nextPointIdx = startPeripheralIdx + ((i + 1) % numPoints);
                bodyIndices.push(pivotIdx, currentPointIdx, nextPointIdx);
            }

            // build mesh data for outlines
            const startBorderIdx = borderVertIndex;
            for (const vertex of polygonVertices) {
                borderPositions.push(vertex.x, 95, vertex.y); // Small Y-offset to prevent z-fighting
                borderColors.push(0, 0, 0, 1.0); // Black outline
                borderVertIndex++;
            }

            // Form lines linking sequentially around the border loop
            for (let i = 0; i < numPoints; i++) {
                const currentLineIdx = startBorderIdx + i;
                const nextLineIdx = startBorderIdx + ((i + 1) % numPoints);
                borderIndices.push(currentLineIdx, nextLineIdx);
            }
        }

        // create the mesh instances
        const bodyMesh = new pc.Mesh(this.generator.gd);
        bodyMesh.setPositions(new Float32Array(bodyPositions));
        bodyMesh.setColors(new Float32Array(bodyColors));
        bodyMesh.setIndices(bodyIndices);
        bodyMesh.update(pc.PRIMITIVE_TRIANGLES);

        const borderMesh = new pc.Mesh(this.generator.gd);
        borderMesh.setPositions(new Float32Array(borderPositions));
        borderMesh.setColors(new Float32Array(borderColors));
        borderMesh.setIndices(borderIndices);
        borderMesh.update(pc.PRIMITIVE_LINES);

        const dotMesh = new pc.Mesh(this.generator.gd);
        dotMesh.setPositions(new Float32Array(dotPositions));
        dotMesh.setColors(new Float32Array(dotColors));
        dotMesh.setIndices(dotIndices);
        dotMesh.update(pc.PRIMITIVE_TRIANGLES);

        // create materials
        const bodyMaterial = new pc.StandardMaterial();
        bodyMaterial.useLighting = false;
        bodyMaterial.diffuseVertexColor = true;
        bodyMaterial.diffuseVertexColorChannel = "rgb";
        bodyMaterial.cull = pc.CULLFACE_BACK;
        bodyMaterial.update();

        const lineMaterial = new pc.StandardMaterial();
        lineMaterial.useLighting = false;
        lineMaterial.emissiveVertexColor = true;
        lineMaterial.emissiveVertexColorChannel = "rgb";
        lineMaterial.update();

        // --- Dot Point-Size Material Fix ---
        const flatBlackMaterial = new pc.StandardMaterial();
        flatBlackMaterial.useLighting = false;
        flatBlackMaterial.diffuseVertexColor = true;
        flatBlackMaterial.diffuseVertexColorChannel = "rgb";
        flatBlackMaterial.update();

        // entity composition
        const bodies = new pc.Entity("Voronoi_Bodies");
        const bodyMeshInstance = new pc.MeshInstance(bodyMesh, bodyMaterial, bodies);
        bodies.addComponent('render', { meshInstances: [bodyMeshInstance] });

        const borders = new pc.Entity("Voronoi_Borders");
        const borderMeshInstance = new pc.MeshInstance(borderMesh, flatBlackMaterial, borders);
        borders.addComponent('render', { meshInstances: [borderMeshInstance] });

        const dots = new pc.Entity("Voronoi_Dots");
        const dotMeshInstance = new pc.MeshInstance(borderMesh, flatBlackMaterial, dots);
        dots.addComponent('render', { meshInstances: [dotMeshInstance] });

        return { bodies, borders, dots };
    }

    #getPolygonVertices(target: VoronoiSite, sites: VoronoiSite[]): pc.Vec2[] {
        // start with a large bounding box representing the entire grid
        const halfW = this.generator.halfW;
        const halfH = this.generator.halfH;
        let polygon: pc.Vec2[] = [
            new pc.Vec2(-halfW, -halfH),
            new pc.Vec2(halfW, -halfH),
            new pc.Vec2(halfW, halfH),
            new pc.Vec2(-halfW, halfH)
        ];

        const neighbors = sites
            .filter((s) => s.id !== target.id)
            .map((s) => {
                const dx = s.position.x - target.position.x;
                const dy = s.position.y - target.position.y;
                return { site: s, distSq: dx * dx + dy * dy };
            })
            .sort((a, b) => a.distSq - b.distSq)
            .slice(0, 45);

        for (const n of neighbors) {
            const nextPolygon: pc.Vec2[] = [];

            // Calculate midpoint and normal pointing away from the target toward neighbor
            const midX = (target.position.x + n.site.position.x) / 2;
            const midY = (target.position.y + n.site.position.y) / 2;
            const normX = n.site.position.x - target.position.x;
            const normY = n.site.position.y - target.position.y;

            // Normalize vector lengths
            const len = Math.sqrt(normX * normX + normY * normY);
            if (len === 0) continue;
            const nx = normX / len;
            const ny = normY / len;

            if (polygon.length === 0) break;

            // Sutherland-Hodgman style edge clipping loop
            for (let i = 0; i < polygon.length; i++) {
                const p1 = polygon[i];
                const p2 = polygon[(i + 1) % polygon.length];

                // Positive dot product means point is on the neighbor's side (outside our cell)
                const d1 = (p1.x - midX) * nx + (p1.y - midY) * ny;
                const d2 = (p2.x - midX) * nx + (p2.y - midY) * ny;

                if (d1 <= 0) { // p1 is inside
                    nextPolygon.push(p1);
                }

                if ((d1 <= 0 && d2 > 0) || (d1 > 0 && d2 <= 0)) { // Crosses edge boundary line
                    const t = d1 / (d1 - d2);
                    const ix = p1.x + t * (p2.x - p1.x);
                    const iy = p1.y + t * (p2.y - p1.y);
                    nextPolygon.push(new pc.Vec2(ix, iy));
                }
            }
            polygon = nextPolygon;
        }

        // Sort clockwise around the central site to keep the triangle fans uniform
        const targetPos = target.position;
        return polygon.sort((a, b) => {
            return Math.atan2(a.y - targetPos.y, a.x - targetPos.x) - Math.atan2(b.y - targetPos.y, b.x - targetPos.x);
        });
    }
}
