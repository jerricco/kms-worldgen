import * as pc from 'playcanvas';
import type { Tile } from '../../lib/generation/types';
import { RuleGridRenderer } from './RuleGridRenderer';
import { Textbox } from './Textbox';
import { isPrimitive } from '../../lib/utils';
import type { GameSettings } from '../GameScene';

export class DebugUiController extends pc.Script {
    static scriptName=  'debug-ui-controller';

    public font: pc.Asset | null = null;
    private screenEntity!: pc.Entity;
    private ruler!: pc.Entity;
    private settings!: GameSettings;

    // References to UI groups for toggling visibility or pulling data
    private coordinateBox!: pc.Entity;
    private tileInfoBox!: pc.Entity;
    
    // References to internal elements needed for logic updates
    private tileNameTextEl!: pc.Entity;
    private tileTypeTextEl!: pc.Entity;
    private tileCoordTextEl!: pc.Entity;

    private refreshBtn!: pc.Entity;

    private inputs = {}; // track input containers across the screen
    private values = {}; // track input values across the screen

    initialize() {
        if (!this.settings) {
            throw new Error('DebugUI: There needs to be game settings to debug!')
        }

        this.font = this.app.assets.find('PatrickHandFont');
        
        // create debug UI
        this.createUiHierarchy();
        // this.hideTileInfo(); // start hidden, only show when a tile is seleted

        // create debug grid rulers
        this.ruler = new pc.Entity('RuleGridEntity')
        this.entity.addChild(this.ruler)
        this.ruler.addComponent('script')
        this.ruler?.script?.create(RuleGridRenderer);
    }

    update() {
        // fill GameSettings inputs
        if (this.values['seed'] === undefined) {
            this.map = this.app.root.findByName('ChunkManagerEntity')?.script.ChunkManager
            for (const name in this.inputs) {
                let fillValue = this.map?.settings[name];

                // @TODO: number type input & switch or checkbox
                if (typeof fillValue === 'number') fillValue = `${fillValue}`;
                if (typeof fillValue === 'boolean') fillValue = fillValue ? 'true' : 'false';
                if (fillValue === undefined) fillValue = '';

                this.values[name] = fillValue
                this.inputs[name].script['textbox-input'].inputValue = fillValue
            }
        }
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
        this.buildMapSettingsPanel();
        // this.buildHoverCornerBox();
        // this.buildTileInfoBox();
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
        this.inputs['seed'].script?.create(Textbox);

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
        this.refreshBtn.button.on('click', () => {
            // @TODO: rewrite this to account for all other settings
            if (this.values['seed'] === null) return;

            this.values['seed'] = this.inputs['seed'].script['textbox-input'].inputValue
            // this.refreshSeed(this.values['seed']) // @TODO
        });

        // compose elements for display
        group.addChild(this.inputs['seed'])
        this.refreshBtn.addChild(btnText);
        group.addChild(this.refreshBtn);
        this.screenEntity.addChild(group);
    }

    private buildMapSettingsPanel() {
        // initialise group
        const group = new pc.Entity('MapSettingsDebugInputGroup');
        group.setLocalPosition(20, -90, 0);

        // fill with settings
        let settingCount = 0;
        for (const name in this.settings) {
            if (name === 'seed') continue; // we already have seedbox
            const setting = this.settings[name];
            // we'll handle more complex bits later
            if (!isPrimitive(setting)) continue;

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
            this.inputs[name].script?.create(Textbox);

            group.addChild(this.inputs[name])
            settingCount++
        }

        // create based on settings for dynamic height
        group.addComponent('element', {
            type: pc.ELEMENTTYPE_IMAGE,
            anchor: new pc.Vec4(0, 1, 0, 1), // Top center
            pivot: new pc.Vec2(0, 1),
            width: 100,
            height: (settingCount * 38) + 8,
            margin: new pc.Vec4(0, 0, 0, 0),
            color: new pc.Color(0.15, 0.15, 0.15, 0.6),
            useInput: true,
        });

        this.screenEntity.addChild(group);
    }

    private buildHoverCornerBox() {
        this.coordinateBox = new pc.Entity('HoverCornerBox');
        this.coordinateBox.addComponent('element', {
            type: pc.ELEMENTTYPE_IMAGE,
            anchor: new pc.Vec4(1, 0, 1, 0), // Bottom Right Corner
            pivot: new pc.Vec2(1, 0),
            width: 120,
            height: 120,
            margin: new pc.Vec4(0, 30, 30, 0), // Pushed away from borders
            color: new pc.Color(0.6, 0.15, 0.15),
            useInput: true
        });

        const hoverText = new pc.Entity('HoverText');
        hoverText.addComponent('element', {
            type: pc.ELEMENTTYPE_TEXT,
            anchor: new pc.Vec4(0.5, 0.5, 0.5, 0.5),
            pivot: new pc.Vec2(0.5, 0.5),
            text: "No tile\nselected",
            fontSize: 16,
            alignment: new pc.Vec2(0.5, 0.5),
            color: new pc.Color(1, 1, 1),
            fontAsset: this.font
        });

        this.coordinateBox.addChild(hoverText);
        this.screenEntity.addChild(this.coordinateBox);
    }

    private buildTileInfoBox() {
        this.tileInfoBox = new pc.Entity('TileInfoBox');
        this.tileInfoBox.addComponent('element', {
            type: pc.ELEMENTTYPE_IMAGE,
            anchor: new pc.Vec4(0, 0.5, 0, 0.5), // Mid Left Alignment
            pivot: new pc.Vec2(0, 0.5),
            width: 320,
            height: 250,
            margin: new pc.Vec4(40, 0, 0, 0),
            color: new pc.Color(0.1, 0.1, 0.13, 0.95)
        });

        // Shared baseline text configurations
        const createTextRow = (yOffset: number, label: string) => {
            const row = new pc.Entity();
            row.addComponent('element', {
                type: pc.ELEMENTTYPE_TEXT,
                anchor: new pc.Vec4(0, 1, 1, 1),
                pivot: new pc.Vec2(0, 1),
                margin: new pc.Vec4(20, yOffset, 20, 0),
                text: label,
                fontSize: 20,
                color: new pc.Color(0.9, 0.9, 0.9)
            });
            this.tileInfoBox.addChild(row);
            return row.element!;
        };

        this.tileNameTextEl = createTextRow(30, "Name: None");
        this.tileTypeTextEl = createTextRow(80, "Type: None");
        this.tileCoordTextEl = createTextRow(130, "Coords: 0,0");

        this.screenEntity.addChild(this.tileInfoBox);
    }

    public showTileInfo(data: Tile) {
        this.tileInfoBox.enabled = true;
        this.tileNameTextEl.text = `Name: ${data.name}`;
        this.tileTypeTextEl.text = `Type: ${data.type}`;
        this.tileCoordTextEl.text = `Coords: ${data.coordinates}`;
    }

    public hideTileInfo() {
        this.tileInfoBox.enabled = false;
    }
}
