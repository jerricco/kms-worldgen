import * as pc from 'playcanvas';
import { RuleGridRenderer } from './RuleGridRenderer';
import { Textbox } from './Textbox';
import { isPrimitive } from '../../lib/utils';
import { VoronoiFactory } from '../../lib/generation/VoronoiCluster';
import type { GenerationMeta, GenerationSettings } from '../../lib/generation/MapGenerator';

export class DebugUiController extends pc.Script {
    static scriptName = 'debug-ui-controller';

    public font: pc.Asset | null = null;
    public voronoi!: VoronoiFactory;

    private voronoiSiteEntity!: pc.Entity;
    private screenEntity!: pc.Entity;
    private ruler!: pc.Entity;
    private settings!: GenerationSettings;
    private meta!: GenerationMeta;

    private refreshBtn!: pc.Entity;

    private inputs: { [key: string]: pc.Entity } = {}; // track input containers across the screen
    private values: { [key: string]: any } = {}; // track input values across the screen

    initialize() {
        if (!this.settings) {
            throw new Error('DebugUI: There needs to be game settings to debug!')
        }

        this.font = this.app.assets.find('PatrickHandFont');
        
        // create debug UI
        this.createUiHierarchy();

        // create debug grid rulers
        this.ruler = new pc.Entity('RuleGridEntity')
        this.ruler.addComponent('script')
        const ruleGridScript = this.ruler?.script?.create(RuleGridRenderer) as unknown as RuleGridRenderer;
        ruleGridScript.settings = this.settings;
        this.entity.addChild(this.ruler)
    }

    update() {
        // if the UI receives world generation voronoi cells, render them out
        // @TODO: a rendering toggle UI for generative layers
        if (this.voronoi && this.voronoi.sites.length > 0 && !this.voronoiSiteEntity) {
            const { bodies, borders, dots } = this.voronoi.getDebugMesh();
            this.voronoiSiteEntity = new pc.Entity('VoronoiCellMeshContainer');
            // @TODO: body and dot visualisation is bugged af
            // this.voronoiSiteEntity.addChild(bodies);
            this.voronoiSiteEntity.addChild(borders);
            // this.voronoiSiteEntity.addChild(dots);

            this.voronoiSiteEntity.enabled = false; // @DEBUG turn on/off

            this.app.root.addChild(this.voronoiSiteEntity);
        } // @TODO: restart visual entity if voronoi cells regenerate & destroy them if turned off at the UI
    }

    // build screen canvas heirarchy
    private createUiHierarchy() {
        // Master Screen Entity
        this.screenEntity = new pc.Entity('DebugScreen');
        this.screenEntity.addComponent('screen', {
            screenSpace: true,
            referenceResolution: new pc.Vec2(1920, 1080),
            scaleMode: 'blend',
            priority: 101 // @Note: The debug screenspaces will always be on top and range between 100 -> 126
        });
        this.app.root.addChild(this.screenEntity);

        // Render sections
        this.buildSeedDebugInput();
        this.buildMapSettingsPanel({ ...this.settings, ...this.meta });
    }

    private buildSeedDebugInput() {
        // BUILD ELEMENTS
        // group element
        const group = new pc.Entity('SeedDebugInputGroup');
        group.setLocalPosition(20, -20, 0);
        group.addComponent('element', {
            type: pc.ELEMENTTYPE_IMAGE,
            anchor: new pc.Vec4(0, 1, 0, 1), // Top center
            pivot: new pc.Vec2(0, 1),
            width: 500,
            height: 60,
            margin: new pc.Vec4(0, 0, 0, 0),
            color: new pc.Color(0.15, 0.15, 0.15, 0.6),
        });

        // input container - wraps Textbox entity
        this.inputs['seed'] = new pc.Entity('SeedDebugInputContainer')
        this.inputs['seed'].addComponent('element', {
            type: pc.ELEMENTTYPE_IMAGE,
            anchor: new pc.Vec4(0, 0, 0.73, 1),
            pivot: new pc.Vec2(0, 0.5),
            margin: new pc.Vec4(8, 8, 0, 8),
            color: new pc.Color(1, 0, 0, 0)
        });

        this.inputs['seed'].addComponent('script');
        const seedbox = this.inputs['seed'].script?.create(Textbox) as unknown as Textbox;
        seedbox.initValue = this.values['seed'] = this.settings['seed']

        // seed refresh button
        this.refreshBtn = new pc.Entity('RefreshButton');
        this.refreshBtn.addComponent('element', {
            type: pc.ELEMENTTYPE_IMAGE,
            anchor: new pc.Vec4(0.75, 0, 1, 1), // Takes right 25% of group width
            pivot: new pc.Vec2(1, 0.5),
            margin: new pc.Vec4(0, 8, 8, 8),
            color: new pc.Color(0.2, 0.6, 0.26),
            useInput: true
        });

        this.refreshBtn.addComponent('button', {
            active: true, 
            fadeDuration: 0.1, 
            transitionMode: pc.BUTTON_TRANSITION_MODE_TINT,
            imageEntity: this.refreshBtn,
            hoverTint: new pc.Color(0.2, 0.6, 0),
            pressedTint: new pc.Color(0.2, 0.6, 0.9),
            inactiveTint: new pc.Color(0.3, 0.3, 0.3, 1.0),
        });

        const btnText = new pc.Entity('BtnText');
        btnText.addComponent('element', {
            type: pc.ELEMENTTYPE_TEXT,
            anchor: new pc.Vec4(0.5, 0.5, 0.5, 0.5),
            pivot: new pc.Vec2(0.5, 0.5),
            text: "Regenerate",
            fontSize: 18,
            color: new pc.Color(1, 1, 1),
            margin: new pc.Vec4(0, 0, 0, 0),
            fontAsset: this.font,
            useInput: true
        });

        // events
        // @TODO: handle reference and cleanup in destroy()
        this.inputs['seed'].on('ui:key:enter', () => this.refreshBtn.button?.fire('click'))
        this.refreshBtn.button?.on('click', () => {
            // @TODO: rewrite this to account for all other settings
            if (this.values['seed'] === null) return;

            // @TODO - refresh generation
        });

        // compose elements for display
        group.addChild(this.inputs['seed'])
        this.refreshBtn.addChild(btnText);
        group.addChild(this.refreshBtn);
        this.screenEntity.addChild(group);
    }

    private buildMapSettingsPanel(settings: GenerationSettings | GenerationMeta) {
        // initialise group
        const group = new pc.Entity('MapSettingsDebugInputGroup');
        group.setLocalPosition(20, -90, 0);

        // fill with settings
        let settingCount = 0;
        for (const name in settings) {
            if (name === 'seed') continue; // we already have seedbox
            // @ts-ignore
            const setting = settings[name];
            // we'll handle more complex bits later
            if (!isPrimitive(setting)) continue;

            const isMetaValue = !this.settings[name] && !!this.meta[name];
            this.inputs[name] = new pc.Entity('SeedDebugInputContainer')
            const localPositionZ = (settingCount * 38) + 8
            this.inputs[name].setLocalPosition(0, -localPositionZ, 0);

            this.inputs[name].addComponent('element', {
                type: pc.ELEMENTTYPE_IMAGE,
                anchor: new pc.Vec4(0, 1, 1, 1),
                height: 30,
                pivot: new pc.Vec2(0, 1),
                margin: new pc.Vec4(8, 8, 8, 8),
                color: new pc.Color(1, 0, 0, 0)
            });
    
            this.inputs[name].addComponent('script');
            const textbox = this.inputs[name].script?.create(Textbox) as unknown as Textbox;
            let inputValue = setting === undefined || setting === null ? "" : setting;
            inputValue = typeof inputValue === "number" && inputValue % 1 === 0 ? inputValue : inputValue.toFixed(); // typescript is retarded.
            textbox.initValue = this.values[name] = Number.isNaN(inputValue) ? `${String(inputValue)}` : inputValue;
            textbox.label = name;
            if (isMetaValue) textbox.readonly = true;

            group.addChild(this.inputs[name])
            settingCount++
        }

        // create based on settings for dynamic height
        group.addComponent('element', {
            type: pc.ELEMENTTYPE_IMAGE,
            anchor: new pc.Vec4(0, 1, 0, 1), // Top center
            pivot: new pc.Vec2(0, 1),
            width: 250,
            height: (settingCount * 38) + 8,
            margin: new pc.Vec4(0, 0, 0, 0),
            color: new pc.Color(0.15, 0.15, 0.15, 0.6),
            useInput: true,
        });

        this.screenEntity.addChild(group);
    }
}
