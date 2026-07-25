import * as pc from 'playcanvas';
import { MapGenerator, type Tile } from '../../lib/generation';

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
    /** @attribute */
    minXBoundary: number = 20 - MapGenerator.DEFAULT_WIDTH;
    /** @attribute */
    minZBoundary: number = 20 - MapGenerator.DEFAULT_HEIGHT;
    /** @attribute */
    maxXBoundary: number = 20 + MapGenerator.DEFAULT_WIDTH;
    /** @attribute */
    maxZBoundary: number = 20 + MapGenerator.DEFAULT_HEIGHT;

    private targetPosition = new pc.Vec3();
    private currentPosition = new pc.Vec3();
    private targetOrthoHeight: number = 25;
    
    private isPanning: boolean = false;
    private lastMousePos = new pc.Vec2();
    private lastTouchPos = new pc.Vec2();
    private lastPosHovered = new pc.Vec2();
    private mouseScreenPos = new pc.Vec2();

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
        this._boundWindowMouseOut = (e: MouseEvent) => {

            // Fired if the mouse crosses past the browser content window edge completely
            if (!e.relatedTarget && (e.clientX <= 0 || e.clientY <= 0 || e.clientX >= window.innerWidth || e.clientY >= window.innerHeight)) {
                this.isPanning = false;
            }
        };

        window.addEventListener('blur', this._boundWindowBlur);
        window.addEventListener('mouseout', this._boundWindowMouseOut);

        this.on('destroy', () => {
            this.app.mouse.off(pc.EVENT_MOUSEDOWN, this.onMouseDown, this);
            // @TODO: ensure this only fires when it crosses over the drawn map, not when it crosses UI.
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
        const t = 1 - Math.pow(this.smoothFactor, dt * 60);

        this.currentPosition.lerp(this.entity.getPosition(), this.targetPosition, t);
        this.entity.setPosition(this.currentPosition);

        const camera = this.entity.camera!;
        camera.orthoHeight = pc.math.lerp(camera.orthoHeight, this.targetOrthoHeight, t);
    }

    private onMouseDown(event: pc.MouseEvent) {
        // @TODO: select tiles on click
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
        // @TODO: implement click-drag selection 
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
        this.clampCameraToMap();
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

    private findTileInformation(x: number, y: number): Tile | null {
        const map = this.app.root.findByName('MapRenderEntity')?.script.MapRenderer;
        if (!map) return null;

        const rayStart = this.entity!.camera.screenToWorld(x, y, this.entity!.camera.nearClip);
        const intersectX = rayStart.x;
        const intersectZ = rayStart.z;
        const gridX = Math.floor(intersectX / map.tileSize);
        const gridY = Math.floor(intersectZ / map.tileSize);

        const isInsideGrid = gridX >= 0 && gridX < map.generation.width && gridY >= 0 && gridY < map.generation.height;
        const isDifferentTile = gridX !== this.lastPosHovered.x || gridY !== this.lastPosHovered.y

        if (!isInsideGrid || !isDifferentTile) return null;

        this.lastPosHovered.x = gridX;
        this.lastPosHovered.y = gridY;

        const tile: Tile = map.generation.grid[gridX][gridY] || null;
        if (tile) {
            console.log(`${tile.region.name} Tile (@${gridX},${gridY}) - elevation: ${tile.elevation.toFixed(2)}`);
        } else {
            console.log(`[Tile Hovered] Empty space or boundary edge at: (${gridX}, ${gridY})`);
        }

        return tile;
    }

    private panByDelta(dx: number, dy: number) {
        const currentHeight = this.entity.camera!.orthoHeight;
        const screenScale = (currentHeight * 2) / this.app.graphicsDevice.height;
        const worldDx = -dx * screenScale * this.panSpeed;
        const worldDz = -dy * screenScale * this.panSpeed;

        this.targetPosition.x += worldDx;
        this.targetPosition.z += worldDz;

        this.clampCameraToMap();
    }

    private clampCameraToMap() {
        const map = this.app.root.findByName('MapRenderEntity')?.script.MapRenderer;
        const targetX = this.targetPosition.x
        const targetZ = this.targetPosition.z
        const minX = -(map.generation.width / 2) + 100;
        const maxX = (map.generation.width * 1.5) - 100;
        const minZ = -(map.generation.height / 2) + 100;
        const maxZ = (map.generation.height * 1.5) - 100;

        if (targetX > maxX || targetX < minX) 
            this.targetPosition.x = pc.math.clamp(this.targetPosition.x, minX, maxX);
        
        if (targetZ > maxZ || targetZ < minZ) 
            this.targetPosition.z = pc.math.clamp(this.targetPosition.z, minZ, maxZ);
    }
}