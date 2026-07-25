import * as pc from 'playcanvas'


import { MapGenerator, type MapSettings } from '../lib/generation';
import { MapRenderer } from './scripts/MapRenderer';
import { OrthoCameraController } from './scripts/OrthoCamera';
import { RuleGridRenderer } from './scripts/RuleGridRenderer';
import { GameUiController } from './scripts/GameUI';
import fontJsonUrl from '../assets/font/PatrickHand.json?url';

// @TODO: generate an runtime lightmap for the output grid - 
// this is so lighting data can stay prebaked for the topology
// @TODO: Create a Directional Disk sun/moon to track day night cycles.
// @TODO: shadows handling for objects on a tile
// @TODO: I mean, the font doesn't display but it loads, HELP.
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

        // @DEBUG START
        this.app.scene.ambientLight = new pc.Color(0.98, 0.98, 0.98);
        this.app.start();

        console.timeEnd('Initialising...')

        // @DEBUG - this will need a lot more orchestration logic once we have menu/ui
        this.startLevel(
            { 
                // Good seeds (so far):
                // - 'Donaldo Ronaldo Trumpino'
                // - 'poo'
                // - 'strange bedfellows by stephen king'
                // - 'Pooline Handson'
                // - 'Hershey Testereo'
                // - 'sanga ranga bangaranga'
                seed: 'Donaldo Ronaldo Trumpino',
                // @NOTE: this commented value generates a portrait window but the perlin noise treats x,y as 
                // equivalent and truncates it. The generation is still different, but does not work in this config
                width: MapGenerator.DEFAULT_WIDTH, // 300, 
                height: MapGenerator.DEFAULT_HEIGHT,
            }
        )        
    }

    // @TODO: game_settings should eventually be the saved data of a save file. If it's not present, start a new game.
    startLevel(game_settings: any = {
        seed: 'Donaldo Ronaldo Trumpino',
        width: MapGenerator.DEFAULT_WIDTH,
        height: MapGenerator.DEFAULT_HEIGHT,
        config: {}
    }) {
        const { seed, width, height, config } = game_settings
        console.log(seed)


        // @TODO: asset loading
        this.font = new pc.Asset('PatrickHandFont', 'font', { url: fontJsonUrl })
        this.app.assets.add(this.font);
        this.app.assets.load(this.font);

        this.font.ready((asset) => {
            const ruler = new pc.Entity('RuleGridEntity')
            this.app.root.addChild(ruler)
            ruler.addComponent('script')
            const rulegrid = ruler.script.create(RuleGridRenderer)
            rulegrid.font = asset
    
            this.map = this.getMapRenderer(seed, width, height, config);
            this.camera = this.getOrthoCamera(width, height);
            this.ui = this.getUI(asset)
        })
        
    }

    static getGameApp(canvas: HTMLCanvasElement): pc.Application {
        const app = new pc.Application(canvas, {
            mouse: new pc.Mouse(canvas),
            keyboard: new pc.Keyboard(window)
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
        this.app.root.addChild(map)

        
        map.addComponent('script')
        if (!map.script) throw new Error('Whoops, MapRender couldn\'t add a script!');
        
        const script = map.script.create(MapRenderer)
        script.generation = new MapGenerator(seed, width, height, config)

        
        return map
    }

    getUI(font: pc.Asset) {
        const ui = new pc.Entity('UIContainerEntity');
        this.app.root.addChild(ui);

        ui.addComponent('script')
        const script = ui.script.create(GameUiController);
        script.font = font;
        return ui;
    }
}