import * as pc from 'playcanvas'
import type { MapGenerator } from '../../lib/generation';
import twoSidedLighting from 'playcanvas/build/playcanvas/src/scene/shader-lib/glsl/chunks/lit/frag/twoSidedLighting.js';

export class RuleGridRenderer extends pc.Script {
    static scriptName = 'rule-grid-renderer';
    
    public fadeMinZoom = 30;
    public fadeMaxZoom = 50;


    // grid LOD lerp fade
    public zoomLODFadeGrid = 48;
    private startGridAlpha!: number;
    private endGridAlpha!: number;
    private lerpGridProgress = 0;
    private isGridLerping = false;

    private startGridAlphaLerp(from: number, to: number) {
        this.startGridAlpha = from;
        this.endGridAlpha = to;
        this.lerpGridProgress = 0
        this.isGridLerping = true;
    }

    // label LOD lerp fade
    public zoomLODFadeLabels = 12;
    private startLabelAlpha!: number;
    private endLabelAlpha!: number;
    private lerpLabelProgress = 0;
    private isLabelLerping = false;

    private startLabelAlphaLerp(from: number, to: number) {
        this.startLabelAlpha = from;
        this.endLabelAlpha = to;
        this.lerpLabelProgress = 0
        this.isLabelLerping = true;
    }

    public gridColor: pc.Color = new pc.Color(0.25, 0.25, 0.25, 0.5);
    public xAxisColor: pc.Color = new pc.Color(0.8, 0.1, 0.1, 1.0); // Red
    public zAxisColor: pc.Color = new pc.Color(0.1, 0.5, 0.8, 1.0); // Blue

    /** @attribute */
    public font: pc.Asset | null = null;
    public gen: MapGenerator | null = null;
    
    private gridMaterial!: pc.ShaderMaterial;
    private axisMaterial!: pc.ShaderMaterial;
    private labelScreen!: pc.Entity;
    
    // @TODO: Make poolRadius 2D and have it calculate the poolRadiusZ to be by the
    // screen device's aspect ratio
    public poolRadius: number = 16
    private textPool: pc.Entity[] = [];
    private _workingColor: pc.Color = new pc.Color();

    initialize(): void {
        // load assets
        this.font = this.app.assets.find('PatrickHandFont');

        const gd: pc.GraphicsDevice = this.app.graphicsDevice;

        // Create a massive flat quad structure to prevent edge visibility limits
        const mesh: pc.Mesh = pc.Mesh.fromGeometry(gd, new pc.PlaneGeometry({
            halfExtents: new pc.Vec2(5000, 5000),
            widthSegments: 1,
            lengthSegments: 1
        }))

        // Define grid shader & attach it to a screen
        const gridScreen = this.createGridScreen(mesh);
        const axisScreen = this.createAxisScreen(mesh)
        this.labelScreen = this.createLabelScreen();
        
        this.app.root.addChild(gridScreen);
        this.app.root.addChild(axisScreen);
        this.app.root.addChild(this.labelScreen);
    }

    update(dt: number): void {
        const cam = this.app.root.findByName('OrthoCamera') as pc.Entity
        if (!cam || !cam.camera) return;
        
        const camera: pc.CameraComponent = cam?.camera;
        
        // ensure the drawn entity stays stationary as the map is traversed.
        const camPos: pc.Vec3 = cam.getPosition();
        this.entity.setPosition(camPos.x, 0, camPos.z);

        // update grid object to ensure it gets any level generation updates.
        const gen = this.app.root.findByName('MapRenderEntity')?.script.MapRenderer.generation;
        if (!this.gen || gen.seed !== this.gen.seed) this.gen = gen;
        
        // update the entitie's elements
        this.updateGridRender(dt, camera);
        this.updateLabelRender(dt, camera, camPos)
    }

    // init rendering
    private getAxisShaderMaterial(uniqueName?: string): pc.ShaderMaterial {
        const vertexShader: string = `
            attribute vec3 vertex_position;
            varying vec3 vWorldPos;
            uniform mat4 matrix_model;
            uniform mat4 matrix_viewProjection;

            void main(void) {
                vec4 worldPos = matrix_model * vec4(vertex_position, 1.0);
                vWorldPos = worldPos.xyz;
                gl_Position = matrix_viewProjection * worldPos;
            }
        `;

        const fragmentShader: string = `
            precision highp float;
            varying vec3 vWorldPos;

            uniform vec4 uGridColor;
            uniform vec4 uXAxisColor;
            uniform vec4 uZAxisColor;
            uniform float uFade;
            uniform float uIsAxisPass;

            float getLineFactor(float coord, float lineWidth) {
                float gridSpace = fract(coord - 0.5);
                float dist = abs(gridSpace - 0.5) / fwidth(coord);
                return 1.0 - min(dist / lineWidth, 1.0);
            }

            void main(void) {
                float lineWidth = 1.0;
                float xLines = getLineFactor(vWorldPos.x, lineWidth);
                float zLines = getLineFactor(vWorldPos.z, lineWidth);
                float gridMask = max(xLines, zLines);

                vec4 finalColor = vec4(0.0);
                if (uGridColor.a > 0.0) {
                    finalColor = uGridColor * gridMask;
                }

                // 2. Dynamic Axis Thickness Rules
                float distToZAxis = abs(vWorldPos.x);
                float distToXAxis = abs(vWorldPos.z);
                
                float axisWidth = 0.05; // Fallback world-space width for grid layer
                
                if (uIsAxisPass > 0.5) {
                    float unitsPerPixelX = fwidth(vWorldPos.x);
                    float unitsPerPixelZ = fwidth(vWorldPos.z);
                    float desiredPixelWidth = 1.0; 
                    
                    if (distToZAxis < (unitsPerPixelX * desiredPixelWidth) && uZAxisColor.a > 0.0) {
                        finalColor = uZAxisColor;
                    } else if (distToXAxis < (unitsPerPixelZ * desiredPixelWidth) && uXAxisColor.a > 0.0) {
                        finalColor = uXAxisColor;
                    }
                } else {
                    if (distToZAxis < axisWidth && uZAxisColor.a > 0.0) {
                        finalColor = uZAxisColor;
                    } else if (distToXAxis < axisWidth && uXAxisColor.a > 0.0) {
                        finalColor = uXAxisColor;
                    }
                }

                float finalAlpha = finalColor.a * uFade;
                if (finalAlpha < 0.01) {
                    discard;
                }

                gl_FragColor = vec4(finalColor.rgb, finalAlpha);
            }
        `;

        return new pc.ShaderMaterial({
            uniqueName: uniqueName || "",
            attributes: { vertex_position: pc.SEMANTIC_POSITION },
            vertexGLSL: vertexShader,
            fragmentGLSL: fragmentShader
        });
    }

    private createGridScreen(mesh: pc.Mesh): pc.Entity {
        const cam = this.app.root.findByName('OrthoCamera') as pc.Entity
        const camera: pc.CameraComponent = cam?.camera;
        
        const zeroColor = new Float32Array([0, 0, 0, 0]);
        const initAlpha = camera.orthoHeight >= this.zoomLODFadeGrid ? 0 : 1;

        this.gridMaterial = this.getAxisShaderMaterial('grid_instance');
        this.gridMaterial.setParameter('uGridColor', new Float32Array([this.gridColor.r, this.gridColor.g, this.gridColor.b, this.gridColor.a]));
        this.gridMaterial.setParameter('uXAxisColor', zeroColor); // Ignore axis rendering
        this.gridMaterial.setParameter('uZAxisColor', zeroColor);
        this.gridMaterial.setParameter('uFade', initAlpha);
        this.gridMaterial.setParameter('uIsAxisPass', 0.0);
        // since grid lines are always alpha, blend them
        this.gridMaterial.blendType = pc.BLEND_NORMAL;
        this.gridMaterial.depthWrite = false;

        this.gridMaterial.update();

        const gridMesh: pc.MeshInstance = new pc.MeshInstance(mesh, this.gridMaterial);
        const gridScreen = new pc.Entity('GridSquaresScreen');
        gridScreen.addComponent('screen', { screenSpace: false, priority: 1 });
        gridScreen.addComponent('render', {
            type: 'asset',
            meshInstances: [gridMesh]
        });

        return gridScreen;
    }

    private createAxisScreen(mesh: pc.Mesh): pc.Entity {
        const zeroColor = new Float32Array([0, 0, 0, 0]);

        this.axisMaterial = this.getAxisShaderMaterial('axis_instance')
        this.axisMaterial.setParameter('uGridColor', zeroColor);
        this.axisMaterial.setParameter('uXAxisColor', new Float32Array([this.xAxisColor.r, this.xAxisColor.g, this.xAxisColor.b, this.xAxisColor.a]));
        this.axisMaterial.setParameter('uZAxisColor', new Float32Array([this.zAxisColor.r, this.zAxisColor.g, this.zAxisColor.b, this.zAxisColor.a]));
        this.axisMaterial.setParameter('uFade', 1.0);
        this.axisMaterial.setParameter('uIsAxisPass', 1.0);
        this.axisMaterial.update();

        const axisMesh: pc.MeshInstance = new pc.MeshInstance(mesh, this.axisMaterial);
        const axisScreen = new pc.Entity('GridAxisScreen');
        axisScreen.addComponent('screen', { screenSpace: false, priority: 3 });
        axisScreen.addComponent('render', {
            type: 'asset',
            meshInstances: [axisMesh]
        });

        return axisScreen;
    }

    private createLabelScreen() {
        const labelScreen = new pc.Entity('GridLabelsScreen');
        labelScreen.addComponent('screen', { screenSpace: false, priority: 2 });
        labelScreen['uFade'] = 0; // store the uFade arbitrarily so we can track it for lerping

        const sideLength = (this.poolRadius * 2) + 1;
        const totalElements = sideLength * sideLength;

        for (let i = 0; i < totalElements; i++) {
            const labelEntity = new pc.Entity(`GridLabel_${i}`);

            labelEntity.addComponent('element', {
                type: pc.ELEMENTTYPE_TEXT,
                anchor: new pc.Vec4(0.5, 0.5, 0.5, 0.5),
                pivot: new pc.Vec2(0, 1),
                text: '0, 0',
                fontSize: 0.18, // Clean reading size for small grid spaces
                color: new pc.Color(0.1, 0.1, 0.1, 1.0),
                alignment: new pc.Vec2(0, 0.5),
                fontAsset: this.font
            });

            // Adjust orientation flat onto the XZ ground plane
            labelEntity.setLocalEulerAngles(-90, 0, 0);

            labelScreen.addChild(labelEntity);
            this.textPool.push(labelEntity);
        }

        return labelScreen
    }

    // update rendering
    private updateGridRender(dt: number, camera: pc.CameraComponent) {
        const uFade = this.gridMaterial.getParameter('uFade');
        const currentAlpha = Number.isNaN(uFade.data) ? 0 : uFade.data

        // start lerping if necessary
        if (!this.isGridLerping) {
            const shouldFadeOut = camera.orthoHeight >= this.zoomLODFadeGrid;
            const shouldFadeIn = camera.orthoHeight < this.zoomLODFadeGrid;
            if (shouldFadeOut && currentAlpha > 0) {
                this.startGridAlphaLerp(1, 0);
            } else if (shouldFadeIn && currentAlpha < 1) {
                this.startGridAlphaLerp(0, 1);
            }
        } else {
            this.lerpGridProgress += dt;
            const alphaFactor = pc.math.clamp(this.lerpGridProgress / 0.32, 0, 1);
            const lerpAlpha = pc.math.lerp(this.startGridAlpha, this.endGridAlpha, alphaFactor);

            this.gridMaterial.setParameter('uFade', lerpAlpha);
            this.gridMaterial.update();

            // cancel lerp once done.
            if (alphaFactor >= 1) this.isGridLerping = false;
        }
    }

    private updateLabelRender(dt: number, camera: pc.CameraComponent, camPos: pc.Vec3) {
        // retrieve arbitrary prop to track lerp
        const uFade = this.labelScreen['uFade'];
        const currentAlpha = Number.isNaN(uFade) ? 0 : uFade
        
        const shouldFadeOut = camera.orthoHeight >= this.zoomLODFadeLabels;
        const shouldFadeIn = camera.orthoHeight < this.zoomLODFadeLabels;
        
        // start lerping if necessary
        if (!this.isLabelLerping) {
            if (shouldFadeOut && currentAlpha > 0) {
                this.startLabelAlphaLerp(currentAlpha, 0);
            } else if (shouldFadeIn && currentAlpha < 1) {
                this.startLabelAlphaLerp(currentAlpha, 1);
            }
        }
        
        let lerpAlpha = currentAlpha;
        if (this.isLabelLerping) {
            this.lerpLabelProgress += dt;
            const alphaFactor = pc.math.clamp(this.lerpLabelProgress / 0.32, 0, 1);
            lerpAlpha = pc.math.lerp(this.startLabelAlpha, this.endLabelAlpha, alphaFactor);
            this.labelScreen['uFade'] = lerpAlpha; // update screen alpha reference
            
            if (alphaFactor >= 1) {
                this.isLabelLerping = false;
            }
        }

        // lerp render labels
        const centerGridX = Math.round(camPos.x);
        const centerGridZ = Math.round(camPos.z);
        let poolIndex = 0

        for (let xOffset = -this.poolRadius; xOffset <= this.poolRadius; xOffset++) {
            for (let zOffset = -this.poolRadius; zOffset <= this.poolRadius; zOffset++) {
                const currentLabel = this.textPool[poolIndex];

                if (!currentLabel || !currentLabel.element) continue;
                poolIndex++;

                if (lerpAlpha <= 0) {
                    if (currentLabel.enabled) currentLabel.enabled = false;
                    continue;
                }

                // Ensure element is visible
                if (!currentLabel.enabled) currentLabel.enabled = true;

                const element = currentLabel.element;
                this._workingColor.copy(element.color);
                this._workingColor.a = lerpAlpha;
                element.color = this._workingColor;

                const cellWorldX = (centerGridX + xOffset + 0.035);
                const cellWorldZ = (centerGridZ + zOffset + 0.15);
                currentLabel.setPosition(cellWorldX, 0.01, cellWorldZ);
                    
                let labelText = this.getLabelText(cellWorldX, cellWorldZ)
                if (currentLabel.element.text !== labelText) {
                    currentLabel.element.text = labelText;
                }
            }
        }
    }

    private getLabelText(cellWorldX: number, cellWorldZ: number): string {
        const gridX = Math.round(cellWorldX), gridY = Math.round(cellWorldZ); // @NOTE: this is the display value, which won't be zero indexed
        const tile = this.gen?.grid?.[gridX]?.[gridY];
        return `X:${gridX + 1}, Z:${gridY + 1}`
            + `\n${tile?.region.name || 'VOID'}`
            + `\nY:${tile?.elevation.toFixed(2) || 0.00}`;
    }
}

