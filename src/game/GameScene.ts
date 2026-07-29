import * as pc from 'playcanvas'

import { type GlobalGenerationMeta } from '../lib/generation/generator';
import { OrthoCameraController } from './scripts/OrthoCamera';
import { DebugUiController } from './scripts/DebugUi';
import fontJsonUrl from '../assets/font/PatrickHand.json?url';
import type { ChunkSettings } from '../lib/generation/chunk';

// Merges all the settings which will later split 
export type GameSettings = 
    Pick<GlobalGenerationMeta, 'stretchX' | 'stretchY' | 'oceanClamp'> & 
    ChunkSettings & 
    { seed: string }; 

export class GameScene {
    // playcanvas orchestrators
    public canvas!: HTMLCanvasElement;
    public app!: pc.Application;

    // cameras
    public camera!: pc.Entity;
    
    // game resources
    public font!: pc.Asset;

    // interface screens
    public ui!: pc.Entity;

    // settings
    public config!: GameSettings

    constructor() {
        console.time('Initialising...')
        this.preload(); // @TODO: loading screen

        // @TODO: replace this with a main menu & game creation screen.
        // That will later provide the game settings to startLevel.
        // This function is trash and will need to instead properly assign these props
        this.config = this.configureLevel();
        
        // start the currently configured level.
        // @TODO: load this in from disk if needed.
        this.startLevel()     

        console.timeEnd('Initialising...')
    }

    preload() {
        this.canvas = document.getElementById("canvas") as HTMLCanvasElement
        if (!this.canvas) throw new Error("Canvas element not found");

        this.app = this.getGameApp();

        // preload relevant assets
        this.font = new pc.Asset('PatrickHandFont', 'font', { url: fontJsonUrl })
        this.font.preload = true;
        this.app.assets.add(this.font);
        this.app.assets.load(this.font);

        // @DEBUG START
        this.app.scene.ambientLight = new pc.Color(0.98, 0.98, 0.98);
        this.app.start();
    }

    configureLevel(): GameSettings {
        return {
            // seed: playerSeed || MapGenerator.DEFAULT_SEED
            //////////////////////////////////////////////////
            // Good seeds (so far):
            // seed: 'Donaldo Ronaldo Trumpino',
            // seed: 'poo',
            // seed: 'strange bedfellows by stephen king',
            // seed: 'Pooline Handson',
            // seed: 'Hershey Testereo',
            // seed: 'sanga ranga bangaranga',
            // seed: 'fuck me I wish I were dead, aye',
            seed: 'helpmeimdrowning',
            maxX: 800, // 12800
            maxY: 800, // 12800
            stretchX: 0.7,
            stretchY: 1.3,
            oceanClamp: 0.85,
            chunkSize: 50,
            seaLevel: 0.32,
            abyssalLevel: 0.1,
            trenchLevel: 0.16,
            beachLevel: 0.34,
            plainLevel: 0.48,
            hillLevel: 0.60,
            peakLevel: 0.60,
        }
    }

    startLevel() {
        this.camera = this.getOrthoCamera();
        this.ui = this.getUI()

        // get current camera location
        // generate starting chunks at that location for a new game
        // @TODO reveal all loaded chunks when save data is present.
    }

    getGameApp(): pc.Application {
        const elementInput = new pc.ElementInput(this.canvas, { useMouse: true, useTouch: true });

        const app = new pc.Application(this.canvas, {
            elementInput,
            mouse: new pc.Mouse(this.canvas),
            keyboard: new pc.Keyboard(this.canvas),
            touch: new pc.TouchDevice(this.canvas),
        });

        app.setCanvasFillMode(pc.FILLMODE_FILL_WINDOW);
        app.setCanvasResolution(pc.RESOLUTION_AUTO);

        window.addEventListener('resize', () => app.resizeCanvas());

        return app;
    }

    getOrthoCamera(): pc.Entity {
        const orthoHeight = (this.config.maxY / 2) * 1.1;
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
        cam.setPosition(this.config.maxX / 2, 100, this.config.maxY / 2);
        
        // camera control
        cam.addComponent('script')
        const camScript = cam.script!.create(OrthoCameraController) as unknown as OrthoCameraController
        camScript.maxOrthoHeight = orthoHeight
        camScript.zoomSpeed = 0.33
        return cam;
    }

    getUI() {
        const ui = new pc.Entity('UIContainerEntity');
        this.app.root.addChild(ui);

        ui.addComponent('script')
        ui.script?.create(DebugUiController);
        return ui;
    }
}