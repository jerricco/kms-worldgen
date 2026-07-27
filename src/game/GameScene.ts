import * as pc from 'playcanvas'

import { MapGenerator, type MapSettings } from '../lib/generation/generator';
import { MapRenderer } from './scripts/MapRenderer';
import { OrthoCameraController } from './scripts/OrthoCamera';
import { DebugUiController } from './scripts/DebugUi';
import fontJsonUrl from '../assets/font/PatrickHand.json?url';

export class GameScene {
    public app: pc.Application;
    public camera!: pc.Entity;
    public map!: pc.Entity;
    public ui!: pc.Entity;

    public font!: pc.Asset;

    constructor() {
        console.time('Initialising...')
        const canvas = document.getElementById("canvas") as HTMLCanvasElement
        if (!canvas) throw new Error("Canvas element not found");

        this.app = GameScene.getGameApp(canvas);

        // preload relevant assets
        this.font = new pc.Asset('PatrickHandFont', 'font', { url: fontJsonUrl })
        this.font.preload = true;
        this.app.assets.add(this.font);
        this.app.assets.load(this.font);

        // @DEBUG START
        this.app.scene.ambientLight = new pc.Color(0.98, 0.98, 0.98);
        this.app.start();

        
        // @DEBUG - this will need a lot more orchestration logic once we have menu/ui
        this.startLevel({ 
            // Good seeds (so far):
            // - 'Donaldo Ronaldo Trumpino'
            // - 'poo'
            // - 'strange bedfellows by stephen king'
            // - 'Pooline Handson'
            // - 'Hershey Testereo'
            // - 'sanga ranga bangaranga'
            // - 'fuck me I wish I were dead, aye'
            seed: 'Donaldo Ronaldo Trumpino',
            width: MapGenerator.DEFAULT_WIDTH, 
            height: MapGenerator.DEFAULT_HEIGHT,
        })     

        console.timeEnd('Initialising...')
    }

    startLevel(game_settings: any = {
        seed: 'Donaldo Ronaldo Trumpino',
        width: MapGenerator.DEFAULT_WIDTH,
        height: MapGenerator.DEFAULT_HEIGHT,
        config: {}
    }) {
        const { seed, width, height, config } = game_settings
        this.camera = this.getOrthoCamera(width, height);
        this.ui = this.getUI()
        this.map = this.getMapRenderer(seed, width, height, config);
    }

    static getGameApp(canvas: HTMLCanvasElement): pc.Application {
        const elementInput = new pc.ElementInput(canvas, {
            useMouse: true,
            useTouch: true,
        });

        const app = new pc.Application(canvas, {
            elementInput,
            mouse: new pc.Mouse(canvas),
            keyboard: new pc.Keyboard(window),
            touch: new pc.TouchDevice(canvas),
        });

        app.setCanvasFillMode(pc.FILLMODE_FILL_WINDOW);
        app.setCanvasResolution(pc.RESOLUTION_AUTO);

        window.addEventListener('resize', () => app.resizeCanvas());

        return app;
    }

    getOrthoCamera(width: number, height: number): pc.Entity {
        const orthoHeight = (height / 2) * 1.1;
        // create and attach camera
        const cam = new pc.Entity('OrthoCamera');
        cam.addComponent('camera', {
            clearColor: new pc.Color(0.8, 0.8, 0.8),
            projection: pc.PROJECTION_ORTHOGRAPHIC,
            orthoHeight: orthoHeight,
            nearClip: 0.1,
            farClip: 2000,
        });
        
        this.app.root.addChild(cam)
        cam.setLocalEulerAngles(-90, 0, 0);
        cam.setPosition(width / 2, 100, height / 2);
        
        // camera control
        cam.addComponent('script')
        cam.script!.create(OrthoCameraController)
        cam.script!.get('ortho-camera-controller')!.maxOrthoHeight = orthoHeight
        cam.script!.get('ortho-camera-controller')!.zoomSpeed = 0.33
        return cam;
    }

    getMapRenderer(seed:string , width: number, height: number, config: MapSettings): pc.Entity {
        // create script to use
        const map = new pc.Entity('MapRenderEntity')
        
        map.addComponent('script')        
        const script = map.script?.create(MapRenderer)
        script.seed = seed;
        script.width = width;
        script.height = height;
        script.config = config;        
        
        this.app.root.addChild(map)
        return map
    }

    getUI() {
        const ui = new pc.Entity('UIContainerEntity');
        this.app.root.addChild(ui);

        ui.addComponent('script')
        ui.script.create(DebugUiController);
        return ui;
    }
}