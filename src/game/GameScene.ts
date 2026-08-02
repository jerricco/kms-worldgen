import * as pc from 'playcanvas'

import fontJsonUrl from '../assets/font/PatrickHand.json?url';

import { MapGenerator, type GenerationMeta, type GenerationSettings } from '../lib/generation/MapGenerator';

import { OrthoCameraController }     from './scripts/OrthoCamera';
import { DebugUiController }         from './scripts/DebugUi';
import { ChunkManager }              from './scripts/ChunkManager';



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

    // behaviour
    public chunkManager!: pc.Entity;
    public chunker!: ChunkManager;

    // settings & services
    public config!: GenerationSettings

    constructor() {
        // @TODO: Handle more gracefully if webGL is disabled or not around
        console.time('Initialising...')
        this.#preload(); // @TODO: loading screen

        // @TODO: main menu & game creation screen.
        
        // configure the game level
        this.config = this.configureLevel();
        
        // start the currently configured level.
        // @TODO: load this in from disk if needed.
        this.startLevel()     

        console.timeEnd('Initialising...')
    }

    ////////////////
    // PRELOADING //
    ////////////////
    #preload() {
        this.canvas = document.getElementById("canvas") as HTMLCanvasElement
        if (!this.canvas) throw new Error("Canvas element not found");

        this.app = this.#getGameApp();

        // preload relevant assets
        this.font = new pc.Asset('PatrickHandFont', 'font', { url: fontJsonUrl })
        this.font.preload = true;
        this.app.assets.add(this.font);
        this.app.assets.load(this.font);

        // @DEBUG START
        this.app.scene.ambientLight = new pc.Color(0.98, 0.98, 0.98);
        this.app.start();
    }

    #getGameApp(): pc.Application {
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

    ///////////////////
    // CONFIGURATION //
    ///////////////////
    configureLevel(): GenerationSettings {
        return {
            // seed: playerSeed || MapGenerator.DEFAULT_SEED
            //////////////////////////////////////////////////
            // Good seeds (so far):
            seed: 'Donaldo Ronaldo Trumpino',
            // seed: 'poo',
            // seed: 'strange bedfellows by stephen king',
            // seed: 'Pooline Handson',
            // seed: 'Hershey Testereo',
            // seed: 'sanga ranga bangaranga',
            // seed: 'fuck me I wish I were dead, aye',
            // seed: 'helpmeimdrowning',
            // seed: 'aborio rice',
            worldWidth: 1600, // 12800,
            worldHeight: 1600, // 12800,
            cellGridSize: 400,
            oceanClamp: 0.85,
            macroScale: 0.0045,
            squishFactor: 1.0,
            stretchX: 0.7,
            stretchY: 1.3,
            chunkSize: 50,

            abyssalLevel: -1.0,
            trenchLevel: -0.85,
            deepOceanLevel: -0.55,
            oceanLevel: -0.25,
            seaLevel: 0,
            beachLevel: 0.03,
            plainLevel: 0.48,
            hillLevel: 0.68,
            mountainLevel: 0.82,
            peakLevel: 0.95,
        }
    }

    /////////////////
    // START LEVEL //
    /////////////////
    startLevel() {
        this.camera = this.getOrthoCamera();

        this.doLevelGeneration();

        // listen to regeneration requests
        this.app.root.on('world:regenerate', (newConfig: GenerationSettings & GenerationMeta) => {
            let settingHasChanged: boolean = false;
            for (const [key, value] of Object.entries(newConfig)) { // @TODO: satisfy typescript's stupid [Iterator] thing
                if (this.config[key] === undefined) continue; // ignore GenerationMeta
                const comparevalue = !isNaN(Number(value)) ? Number(value) : value;
                if (comparevalue !== this.config[key]) {
                    // update config and flag for regen
                    this.config[key] = comparevalue;
                    settingHasChanged = true;
                }
            }

            if (settingHasChanged) {
                this.doLevelGeneration();
            }
        })


        // finally load the UI
        this.ui = this.getUI();

        // load artefacts into the UI so that they can get rendered
        const debugUIScript = this.ui.script['debug-ui-controller'];
        debugUIScript.voronoi = this.chunker.generator.voronoi;
    }

    doLevelGeneration() {
        this.chunkManager = this.getChunkManager();

        //////////////////////
        // LEVEL GENERATION //
        //////////////////////

        ///// STEP 1: Prepass
        this.chunker.generator.pregenerate();
        ///// STEP 2: Initial Chunk generation at current camera location
        // generate starting chunks at the loaded camera location
        const globalCamPos: pc.Vec3 = this.camera.getPosition();
        this.chunker.updateChunkRadius(globalCamPos.x, globalCamPos.z, 16)
        // @TODO reveal all loaded chunks when save data is present.

        ///// STEP 3: Chunk generation streaming
    }

    getOrthoCamera(): pc.Entity {
        // @TODO: get current width/height (larger of the two) of generated chunks
        // and use that to determine a new max orthoHeight. Will also need to be updated with new chunks.
        // For now, we'll just stick it to being the original 16 chunk radius.
        const orthoHeight = (16 * 50) * 1.1;
        // create and attach camera
        const cam = new pc.Entity('OrthoCamera');
        cam.addComponent('camera', {
            clearColor: new pc.Color(0.8, 0.8, 0.8),
            projection: pc.PROJECTION_ORTHOGRAPHIC,
            orthoHeight,
            nearClip: 0.1,
            farClip: 2000,
        });
        
        this.app.root.addChild(cam);
        cam.setLocalEulerAngles(-90, 0, 0);
        cam.setPosition(0, 100, 0);
        
        // camera control
        cam.addComponent('script');
        
        const camScript = cam.script!.create(OrthoCameraController) as unknown as OrthoCameraController

        camScript.maxOrthoHeight = orthoHeight
        camScript.zoomSpeed = 0.33
        return cam;
    }

    getChunkManager(): pc.Entity {
        if (this.chunker) {
            this.chunkManager.destroy();
        }

        const chunkManager = new pc.Entity('ChunkManagerEntity')
        chunkManager.addComponent('script')

        // @ts-ignore  
        this.chunker = chunkManager.script?.create(ChunkManager) as ChunkManager
        this.chunker.settings = this.config;
        this.chunker.generator = new MapGenerator(this.config, this.app.graphicsDevice);

        this.app.root.addChild(chunkManager)
        return chunkManager
    }

    getUI() {
        const ui = new pc.Entity('UIContainerEntity');
        ui.addComponent('script')
        const debugUIScript = ui.script?.create(DebugUiController) as unknown as DebugUiController;
        debugUIScript.settings = { ...this.chunker.generator.settings }
        debugUIScript.meta = { ...this.chunker.generator.meta }
        
        this.app.root.addChild(ui);
        return ui;
    }
}