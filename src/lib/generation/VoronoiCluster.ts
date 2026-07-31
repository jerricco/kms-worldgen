import * as pc from 'playcanvas';
import type { SeededRandom } from './seed';

export interface VoronoiSite {
    id: number;
    position: pc.Vec2;
    isEdge: boolean;
}

export interface VoronoiClusterConfig {
    width: number;
    height: number;
    oceanClamp: number;
    rng: SeededRandom;
}

export class VoronoiCluster {
    // exposables
    public width: number;
    public height: number;
    public oceanClamp: number;
    public sites: VoronoiSite[];
    public gd: pc.GraphicsDevice;

    // internal stuff
    private rng: SeededRandom;

    // helper properties
    private get halfW() {
        return (this.width || 0) / 2
    }

    private get halfH() {
        return (this.height || 0) / 2
    } 

    constructor(config: VoronoiClusterConfig, gd: pc.GraphicsDevice) {
        this.width = config.width;
        this.height = config.width;
        this.oceanClamp = config.oceanClamp;
        this.rng = config.rng;
        this.gd = gd;

        this.sites = this.#generateSites();
    }

    #generateSites(): VoronoiSite[] {
        let siteIdCounter = 0;
        const sites: VoronoiSite[] = [];
        
        const maxDimension = Math.max(this.width, this.height);
        const centerCount = this.rng.nextRange(3, 8); // random number of continental cores
        const centers: pc.Vec2[] = [];
        const edgePadding = maxDimension * (1.0 - this.oceanClamp);

        for (let i = 0; i < centerCount; i++) {
            const cx = -this.halfW + edgePadding + (this.rng.next() * (this.width - edgePadding * 2));
            const cy = -this.halfH + edgePadding + (this.rng.next() * (this.height - edgePadding * 2));
            centers.push(new pc.Vec2(cx, cy));
        }

        const minDistanceBetweenSites = Math.max(10, maxDimension * 0.005);
        const clusterRadius = maxDimension * 0.25
        const baseDensityChance = 0.85; // high-base density for landmasses

        for (const center of centers) {
            const sitesInCluster = this.rng.nextRange(50, 200); // highly variable cell count for seed variance

            for (let i = 0; i < sitesInCluster; i++) {
                const angle = this.rng.next() * Math.PI * 2;
                const distance = Math.pow(this.rng.next(), 1.5) * clusterRadius;

                const x = center.x + Math.cos(angle) * distance;
                const y = center.y + Math.sin(angle) * distance;

                // no out of bounds
                if (x < -this.halfW || x >= this.halfW || y < -this.halfH || y >= this.halfH) 
                    continue;

                // evaluate edge falloff
                const nx = x / this.halfW;
                const ny = y / this.halfH;
                const distanceToEdge = Math.max(Math.abs(nx), Math.abs(ny));

                // quadratic falloff
                const falloffWeight = Math.max(0, 1 - Math.pow(distanceToEdge, 2));
                const survivalChance = baseDensityChance * falloffWeight;

                if (this.rng.next() > survivalChance) continue; // too bad, so sad.

                // proximity check to prevent overlapping
                let tooClose = false;
                for (let j = 0; j < sites.length; j++) {
                    const existingPos = sites[j].position;
                    const dx = (existingPos.x - x), dy = (existingPos.y - y);
                    const distSq = (dx * dx) + (dy * dy);

                    if (distSq < minDistanceBetweenSites * minDistanceBetweenSites) {
                        tooClose = true;
                        break;
                    }
                }

                if (tooClose) continue;

                // Mark as an edge node if it is close to the grid bounds
                const margin = maxDimension * 0.02;
                const isEdge = (
                    x < -this.halfW + margin || x > this.halfW - margin ||
                    y < -this.halfH + margin || y > this.halfH - margin
                );

                sites.push({
                    id: siteIdCounter++,
                    position: new pc.Vec2(x, y),
                    isEdge: isEdge
                });
            }
        }

        return sites;
    }

    buildVoronoiMeshes(): { bodies: pc.Entity, borders: pc.Entity, dots: pc.Entity } {
        const bodyPositions:   number[] = [];
        const bodyNormals:     number[] = [];
        const bodyColors:      number[] = [];
        const bodyIndices:     number[] = [];
        
        const borderPositions: number[] = [];
        const borderColors:    number[] = [];
        const borderIndices:   number[] = [];

        const dotPositions: number[] = [];
        const dotColors: number[] = [];
        const dotIndices: number[] = [];

        let bodyVertIndex = 0;
        let borderVertIndex = 0;
        let dotVertexIndex = 0;

        // compute voronoi region bulks
        for (const site of this.sites) {
            const r = this.rng.nextRange(50, 230) / 255;
            const g = this.rng.nextRange(50, 230) / 255;
            const b = this.rng.nextRange(50, 230) / 255;

            const polygonVertices = this.getPolygonVertices(site, this.sites);
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
        const bodyMesh = new pc.Mesh(this.gd);
        bodyMesh.setPositions(new Float32Array(bodyPositions));
        bodyMesh.setColors(new Float32Array(bodyColors));
        bodyMesh.setIndices(bodyIndices);
        bodyMesh.update(pc.PRIMITIVE_TRIANGLES);

        const borderMesh = new pc.Mesh(this.gd);
        borderMesh.setPositions(new Float32Array(borderPositions));
        borderMesh.setColors(new Float32Array(borderColors));
        borderMesh.setIndices(borderIndices);
        borderMesh.update(pc.PRIMITIVE_LINES);

        const dotMesh = new pc.Mesh(this.gd);
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

    getPolygonVertices(target: VoronoiSite, sites: VoronoiSite[]): pc.Vec2[] {
        // start with a large bounding box representing the entire grid
        let polygon: pc.Vec2[] = [
            new pc.Vec2(-this.halfW, -this.halfH),
            new pc.Vec2(this.halfW, -this.halfH),
            new pc.Vec2(this.halfW, this.halfH),
            new pc.Vec2(-this.halfW, this.halfH)
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