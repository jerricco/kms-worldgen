import * as pc from 'playcanvas'

export class RuleGridRenderer extends pc.Script {
    static scriptName = 'rule-grid-renderer';
    
    public fadeMinZoom = 30;
    public fadeMaxZoom = 50;

    public gridColor: pc.Color = new pc.Color(0.25, 0.25, 0.25, 1.0);
    public xAxisColor: pc.Color = new pc.Color(0.8, 0.1, 0.1, 1.0); // Red
    public zAxisColor: pc.Color = new pc.Color(0.1, 0.5, 0.8, 1.0); // Blue

    /** @attribute */
    public font?: pc.Asset
    public poolRadius: number = 12
    
    private gridMaterial!: pc.StandardMaterial;
    private axisMaterial!: pc.StandardMaterial;
    private textPool: pc.Entity[] = [];
    private screenEntity!: pc.Entity;
    private labelsCreated: boolean = false;

    initialize(): void {
        // grid frame rendering
        const gd: pc.GraphicsDevice = this.app.graphicsDevice;
        const zeroColor = new Float32Array([0, 0, 0, 0]);

        this.gridMaterial = this.getAxisShaderMaterial('grid_instance');
        this.gridMaterial.setParameter('uGridColor', new Float32Array([this.gridColor.r, this.gridColor.g, this.gridColor.b, this.gridColor.a]));
        this.gridMaterial.setParameter('uXAxisColor', zeroColor); // Ignore axis rendering
        this.gridMaterial.setParameter('uZAxisColor', zeroColor);
        this.gridMaterial.setParameter('uFade', 1.0);
        this.gridMaterial.setParameter('uIsAxisPass', 0.0);
        this.gridMaterial.update();
        
        this.axisMaterial = this.getAxisShaderMaterial('axis_instance')
        this.axisMaterial.setParameter('uGridColor', zeroColor);
        this.axisMaterial.setParameter('uXAxisColor', new Float32Array([this.xAxisColor.r, this.xAxisColor.g, this.xAxisColor.b, this.xAxisColor.a]));
        this.axisMaterial.setParameter('uZAxisColor', new Float32Array([this.zAxisColor.r, this.zAxisColor.g, this.zAxisColor.b, this.zAxisColor.a]));
        this.axisMaterial.setParameter('uFade', 1.0);
        this.axisMaterial.setParameter('uIsAxisPass', 1.0);
        this.axisMaterial.update();

        // 3. Create a massive flat quad structure to prevent edge visibility limits
        const mesh: pc.Mesh = pc.createPlane(gd, {
            halfExtents: new pc.Vec2(5000, 5000),
            widthSegments: 1,
            lengthSegments: 1
        });

        const gridMesh: pc.MeshInstance = new pc.MeshInstance(mesh, this.gridMaterial);
        const axisMesh: pc.MeshInstance = new pc.MeshInstance(mesh, this.axisMaterial);

        // 4. Register the component safely via standard entity API methods
        this.entity.addComponent('render', {
            type: 'asset',
            meshInstances: [axisMesh, gridMesh]
        });

        // co-ordinate rendering labels
        this.screenEntity = new pc.Entity('GridLabelsScreen');
        this.screenEntity.addComponent('screen', { screenSpace: false });
        this.app.root.addChild(this.screenEntity);

    }

    public update(dt: number): void {
        this.createLabelEntitiesIfEmpty();
        
        const cam = this.app.root.findByName('OrthoCamera')
        if (!cam || !cam.camera) return;

        const camPos: pc.Vec3 = cam.getPosition();
        const camera: pc.CameraComponent = cam?.camera;

        // Position tracking: Keeps the plane perfectly centered under the screen viewport coordinates
        this.entity.setPosition(camPos.x, 0, camPos.z);

        // Zoom-dependent fading implementation
        if (camera.projection === pc.PROJECTION_ORTHOGRAPHIC) {
            const zoom: number = camera.orthoHeight;
            let alphaFade: number = 1.0;
            let charFade: number = 1.0;
            
            if (zoom >= this.fadeMaxZoom) {
                alphaFade = 0.0;
            } else if (zoom > this.fadeMinZoom) {
                alphaFade = 1.0 - (zoom - this.fadeMinZoom) / (this.fadeMaxZoom - this.fadeMinZoom);
            }

            // grid rendering - fade out regular grid lines
            this.gridMaterial.setParameter('uFade', alphaFade);
            if (alphaFade < 1.0) {
                this.gridMaterial.blendType = pc.BLEND_NORMAL;
                this.gridMaterial.depthWrite = false; // Disable depth write only when transparent to prevent artifacts
            } else {
                this.gridMaterial.blendType = pc.BLEND_NONE;
                this.gridMaterial.depthWrite = true;  // Enable full depth write when opaque
            }
            this.gridMaterial.update();

            // text rendering
            const centerGridX = Math.round(camPos.x);
            const centerGridZ = Math.round(camPos.z);
            let poolIndex = 0;
            for (let xOffset = -this.poolRadius; xOffset <= this.poolRadius; xOffset++) {
                for (let zOffset = -this.poolRadius; zOffset <= this.poolRadius; zOffset++) {
                    const currentLabel = this.textPool[poolIndex];
                    if (!currentLabel || !currentLabel.element) continue;

                    if (alphaFade <= 0.0) {
                        poolIndex++;
                        continue;
                    }

                    const cellWorldX = (centerGridX + xOffset + 0.035), cellWorldZ = (centerGridZ + zOffset + 0.15);
                    currentLabel.setPosition(cellWorldX, 0.01, cellWorldZ);
                    
                    // @NOTE: this is the display value, which won't be zero indexed
                    const labelText = `X:${Math.round(cellWorldX) + 1}, Z:${Math.round(cellWorldZ) + 1}`; 
                    if (currentLabel.element.text !== labelText) currentLabel.element.text = labelText;

                    charFade = zoom >= (this.fadeMaxZoom / 5) ? 0.0 : 1.0;
                    currentLabel.enabled = !(zoom >= this.fadeMaxZoom / 5);

                    const currentColor = currentLabel.element.color;
                    currentLabel.element.color = new pc.Color(currentColor.r, currentColor.g, currentColor.b, charFade);

                    poolIndex++;
                }
            } 
        }
    }

    private getAxisShaderMaterial(uniqueName?: string) {
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

    private createLabelEntitiesIfEmpty() {
        if (this.font === undefined || this.labelsCreated) return;

        const sideLength = (this.poolRadius * 2) + 1;
        const totalElements = sideLength * sideLength;

        for (let i = 0; i < totalElements; i++) {
            const labelEntity = new pc.Entity(`GridLabel_${i}`);

            labelEntity.addComponent('element', {
                type: pc.ELEMENTTYPE_TEXT,
                anchor: new pc.Vec4(0.5, 0.5, 0.5, 0.5),
                pivot: new pc.Vec2(0, 0.5),
                text: '0, 0',
                fontSize: 0.18, // Clean reading size for small grid spaces
                color: new pc.Color(0.1, 0.1, 0.1, 1.0),
                alignment: new pc.Vec2(0, 0.5),
                fontAsset: this.font
            });

            // Adjust orientation flat onto the XZ ground plane
            labelEntity.setLocalEulerAngles(-90, 0, 0);

            this.screenEntity.addChild(labelEntity);
            this.textPool.push(labelEntity);
        }

        this.labelsCreated = true;
    }
}
