import * as pc from 'playcanvas';
import type { ChunkManager } from './ChunkManager';

export class OrthoCameraController extends pc.Script {
    static scriptName = 'ortho-camera-controller'

    /** @attribute */
    panSpeed: number = 1.0;
    /** @attribute */
    zoomSpeed: number = 0.05;
    /** @attribute */
    smoothFactor: number = 0.15;
    /** @attribute */
    minOrthoHeight: number = 5;
    /** @attribute */
    maxOrthoHeight: number = 150;

    private targetPosition = new pc.Vec3();
    private currentPosition = new pc.Vec3();
    private targetOrthoHeight: number = 25;
    
    private isPanning: boolean = false;
    private lastMousePos = new pc.Vec2();
    private lastTouchPos = new pc.Vec2();
    private lastPosHovered = new pc.Vec2();
    private mouseScreenPos = new pc.Vec2();
    private chunkManager!: ChunkManager;

    private _boundWindowBlur!: () => void;
    private _boundWindowMouseOut!: (e: MouseEvent) => void;

    initialize() {
        // Set initial positions based on where the camera is placed
        this.currentPosition.copy(this.entity.getPosition());
        this.targetPosition.copy(this.currentPosition);
        this.targetOrthoHeight = this.entity.camera!.orthoHeight;

        // mouse events
        this.app.mouse.on(pc.EVENT_MOUSEDOWN, this.onMouseDown, this);
        this.app.mouse.on(pc.EVENT_MOUSEMOVE, this.onMouseMove, this);
        this.app.mouse.on(pc.EVENT_MOUSEUP, this.onMouseUp, this);
        this.app.mouse.on(pc.EVENT_MOUSEWHEEL, this.onMouseWheel, this);
        this.app.mouse.disableContextMenu();

        // touch events
        if (this.app.touch) {
            this.app.touch.on(pc.EVENT_TOUCHSTART, this.onTouchStart, this);
            this.app.touch.on(pc.EVENT_TOUCHMOVE, this.onTouchMove, this);
            this.app.touch.on(pc.EVENT_TOUCHEND, this.onTouchEnd, this);
            this.app.touch.on(pc.EVENT_TOUCHCANCEL, this.onTouchEnd, this);
        }

        // Handle mouse window
        this._boundWindowBlur = () => { this.isPanning = false; };
        // Fired if the mouse crosses past the browser content window edge completely
        this._boundWindowMouseOut = (e: MouseEvent) => {
            if (!e.relatedTarget && (e.clientX <= 0 || e.clientY <= 0 || e.clientX >= window.innerWidth || e.clientY >= window.innerHeight)) {
                this.isPanning = false;
            }
        };

        window.addEventListener('blur', this._boundWindowBlur);
        window.addEventListener('mouseout', this._boundWindowMouseOut);

        this.on('destroy', () => {
            this.app.mouse.off(pc.EVENT_MOUSEDOWN, this.onMouseDown, this);
            this.app.mouse.off(pc.EVENT_MOUSEMOVE, this.onMouseMove, this);
            this.app.mouse.off(pc.EVENT_MOUSEUP, this.onMouseUp, this);
            this.app.mouse.off(pc.EVENT_MOUSEWHEEL, this.onMouseWheel, this);
            this.app.mouse.enableContextMenu();

            if (this.app.touch) {
                this.app.touch.off(pc.EVENT_TOUCHSTART, this.onTouchStart, this);
                this.app.touch.off(pc.EVENT_TOUCHMOVE, this.onTouchMove, this);
                this.app.touch.off(pc.EVENT_TOUCHEND, this.onTouchEnd, this);
                this.app.touch.off(pc.EVENT_TOUCHCANCEL, this.onTouchEnd, this);
            }

            // Clean up global DOM event listener
            window.removeEventListener('blur', this._boundWindowBlur);
            window.removeEventListener('mouseout', this._boundWindowMouseOut);
        });
    }

    update(dt: number) {
        // get when available
        if (!this.chunkManager) {
            this.chunkManager = this.app.root.findByName('ChunkManagerEntity')?.script.ChunkManager;
        }

        const t = 1 - Math.pow(this.smoothFactor, dt * 60);

        this.currentPosition.lerp(this.entity.getPosition(), this.targetPosition, t);
        this.entity.setPosition(this.currentPosition);

        const camera = this.entity.camera!;
        camera.orthoHeight = pc.math.lerp(camera.orthoHeight, this.targetOrthoHeight, t);
    }

    public setPosition(x: number, z: number) {
        this.currentPosition.x = 0
        this.currentPosition.z = 0
        this.targetPosition.x = 0
        this.targetPosition.z = 0
        this.entity.setPosition(x, this.currentPosition.y, z);
    }

    private onMouseDown(event: pc.MouseEvent) {
        if (event.button === pc.MOUSEBUTTON_LEFT || event.button === pc.MOUSEBUTTON_RIGHT) {
            this.isPanning = true;
            this.lastMousePos.set(event.x, event.y);
        }
    }

    private onMouseMove(event: pc.MouseEvent) {
        this.mouseScreenPos.set(event.x, event.y)
        if (this.isPanning) {
            const dx = event.x - this.lastMousePos.x;
            const dy = event.y - this.lastMousePos.y;
    
            this.panByDelta(dx, dy)
            this.lastMousePos.set(event.x, event.y);
        } else {
            this.findTileInformation(event.x, event.y)
        }
    }

    private onMouseUp() {
        this.isPanning = false;
    }

    private onMouseWheel(event: pc.MouseEvent) {
        const oldTargetHeight = this.targetOrthoHeight;
        const zoomAmount = event.wheel * this.zoomSpeed;
        const scaleFactor = this.targetOrthoHeight * 0.1
        this.targetOrthoHeight -= zoomAmount * scaleFactor
        this.targetOrthoHeight = pc.math.clamp(this.targetOrthoHeight, this.minOrthoHeight, this.maxOrthoHeight);

        const mouseWorldPos = new pc.Vec3();
        this.entity!.camera?.screenToWorld(event.x, event.y, this.entity.camera!.nearClip, mouseWorldPos)

        const zoomRatioChange = (oldTargetHeight - this.targetOrthoHeight) / oldTargetHeight;
        const toMouseX = mouseWorldPos.x - this.targetPosition.x;
        const toMouseZ = mouseWorldPos.z - this.targetPosition.z;

        this.targetPosition.x += toMouseX * zoomRatioChange;
        this.targetPosition.z += toMouseZ * zoomRatioChange;
        this.clampCameraToWorld();
        this.lastMousePos.set(event.x, event.y)
    }

    // --- MOBILE TOUCH EVENT HANDLING ---
    private onTouchStart(event: pc.TouchEvent) {
        if (event.touches.length === 1) {
            this.isPanning = true;
            this.lastTouchPos.set(event.touches[0].x, event.touches[0].y);
        }
    }

    private onTouchMove(event: pc.TouchEvent) {
        if (!this.isPanning || event.touches.length !== 1) return;

        const touch = event.touches[0];
        const dx = touch.x - this.lastTouchPos.x;
        const dy = touch.y - this.lastTouchPos.y;

        // Touch systems operate on inverse drag interactions natively
        this.panByDelta(dx, dy);
        this.lastTouchPos.set(touch.x, touch.y);
    }

    private onTouchEnd() {
        this.isPanning = false;
    }

    private findTileInformation(x: number, y: number): void {
        if (!this.chunkManager) return; // don't bother getting tile information if we haven't generated any yet

        const rayStart = this.entity!.camera.screenToWorld(x, y, this.entity!.camera.nearClip);
        const gridX = Math.floor(rayStart.x / this.chunkManager.tileSize);
        const gridY = Math.floor(rayStart.z / this.chunkManager.tileSize);
        
        const worldW = this.chunkManager.settings.worldWidth;
        const worldH = this.chunkManager.settings.worldHeight
        const xRadius = worldW / 2;
        const yRadius = worldH / 2;
        const isInsideGrid = gridX > -xRadius && gridX < xRadius && gridY > -yRadius && gridY < yRadius;
        const isDifferentTile = gridX !== this.lastPosHovered.x || gridY !== this.lastPosHovered.y
        
        if (!isInsideGrid || !isDifferentTile) return;
        this.lastPosHovered.x = gridX;
        this.lastPosHovered.y = gridY;

        // @TODO: uncomment when I can use this later
        // const chunkSize = this.chunkManager.settings.chunkSize;
        // const chunkX = Math.floor(gridX / chunkSize);
        // const chunkY = Math.floor(gridY / chunkSize);
        // const tileX = gridX - (chunkX * chunkSize);
        // const tileY = gridY - (chunkY * chunkSize);
        // const chunk = this.chunkManager.chunks.get(`${chunkX},${chunkY}`);
        // const tileIndex = Chunk.getLocalIndex(Math.floor(tileX), Math.floor(tileY));

        // const region = RegionName[chunk?.regionIds[tileIndex]];
        // const elevation = chunk?.elevations[tileIndex];
        // console.log(`${region} Tile (@${gridX},${gridY}) - elevation: ${elevation?.toFixed(2)}`);
    }

    private panByDelta(dx: number, dy: number) {
        const currentHeight = this.entity.camera!.orthoHeight
        const screenScale = (currentHeight * 2) / window.innerHeight;
        const worldDx = -dx * screenScale * this.panSpeed;
        const worldDz = -dy * screenScale * this.panSpeed;

        this.targetPosition.x += worldDx;
        this.targetPosition.z += worldDz;

        this.clampCameraToWorld();
    }

    // @TODO: account for screen aspect ratios so that the map always can pan into VOID.
    private clampCameraToWorld() {
        const chunkManager = this.app.root.findByName('ChunkManagerEntity')?.script.ChunkManager;
        if (!chunkManager) {
            this.targetPosition.x = 0;
            this.targetPosition.z = 0;
            return;
        }

        // horizontal clamp
        const targetX = this.targetPosition.x
        const leftBound = chunkManager.chunkExtentMinX - 100;
        const rightBound = chunkManager.chunkExtentMaxX + 100;
        if (targetX < leftBound ||targetX > rightBound) {
            this.targetPosition.x = pc.math.clamp(this.targetPosition.x, leftBound, rightBound);
        }

        // vertical clamp
        const targetZ = this.targetPosition.z
        const topBound = chunkManager.chunkExtentMinY - 100;
        const bottomBound = chunkManager.chunkExtentMaxY + 100;
        if (targetZ < topBound || targetZ > bottomBound) {
            this.targetPosition.z = pc.math.clamp(this.targetPosition.z, topBound, bottomBound);
        }
    }
}