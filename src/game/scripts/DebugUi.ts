import * as pc from 'playcanvas';
import { MapGenerator, type Tile } from '../../lib/generation';
import { RuleGridRenderer } from './RuleGridRenderer';
import { TextInputBinder } from './TextInputBinder';
import { TextboxEntity } from '../entities/TextboxEntity';
import type { MapRenderer } from './MapRenderer';

export class DebugUiController extends pc.Script {
    static scriptName=  'debug-ui-controller';

    public font: pc.Asset | null = null;
    private screenEntity!: pc.Entity;
    private map: MapRenderer;

    // References to UI groups for toggling visibility or pulling data
    private seedModifierInput!: pc.Entity;
    private coordinateBox!: pc.Entity;
    private tileInfoBox!: pc.Entity;
    
    // References to internal elements needed for logic updates
    private textInputElement!: pc.Entity;
    private tileNameTextEl!: pc.Entity;
    private tileTypeTextEl!: pc.Entity;
    private tileCoordTextEl!: pc.Entity;

    // Track mock value for the input box
    private currentInputValue: string = "Input a seed...";

    initialize() {
        this.font = this.app.assets.find('PatrickHandFont');
        this.map = this.app.root.findByName('MapRenderEntity')?.script.MapRenderer
        
        // create debug UI
        this.createUiHierarchy();
        this.hideTileInfo(); // start hidden, only show when a tile is seleted

        // create debug grid rulers
        const ruler = new pc.Entity('RuleGridEntity')
        this.entity.addChild(ruler)
        ruler.addComponent('script')
        ruler.script.create(RuleGridRenderer)

        // @TODO: activate normal game scene UI elements
    }

    // build screen canvas heirarchy
    private createUiHierarchy() {
        // Master Screen Entity
        this.screenEntity = new pc.Entity('MainScreen');
        this.screenEntity.addComponent('screen', {
            screenSpace: true,
            referenceResolution: new pc.Vec2(1920, 1080),
            scaleMode: 'blend'
        });
        this.app.root.addChild(this.screenEntity);

        // Render sections
        this.buildSeedDebugInput();
        this.buildHoverCornerBox();
        this.buildTileInfoBox();
    }

    private buildSeedDebugInput() {
        this.currentInputValue = this.map.generation?.seed || this.currentInputValue;
        
        // BUILD ELEMENTS
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

        const inputContainer = new pc.Entity('SeedDebugInputContainer')
        inputContainer.addComponent('element', {
            type: pc.ELEMENTTYPE_IMAGE,
            anchor: new pc.Vec4(0, 0, 0.73, 1),
            pivot: new pc.Vec2(0, 0.5),
            margin: new pc.Vec4(8, 8, 0, 8),
        });

        group.addChild(inputContainer)
        const seedTextElement = TextboxEntity.create(this.app, inputContainer, this.currentInputValue);
        seedTextElement.on('ui:blur', (text) => {
            if (text !== this.currentInputValue) this.refreshSeed(text);
        })

        // Action / Refresh Button Next To Textbox
        const refreshBtn = new pc.Entity('RefreshButton');
        refreshBtn.addComponent('element', {
            type: pc.ELEMENTTYPE_IMAGE,
            anchor: new pc.Vec4(0.75, 0, 1, 1), // Takes right 25% of group width
            pivot: new pc.Vec2(1, 0.5),
            margin: new pc.Vec4(0, 8, 8, 8),
            color: new pc.Color(0.2, 0.6, 0.26),
            useInput: true
        });
        // refreshBtn.addComponent('button', {
        //     active: true,
        //     fadeDuration: 0.1,
        //     hoverColor: new pc.Color(0.25, 0.75, 0.33),
        //     pressedColor: new pc.Color(0.15, 0.5, 0.2)
        // });
        // refreshBtn.on('click', () => {
        //     this.refreshSeed(this.currentInputValue);
        // });

        // const btnText = new pc.Entity('BtnText');
        // btnText.addComponent('element', {
        //     type: pc.ELEMENTTYPE_TEXT,
        //     anchor: new pc.Vec4(0.5, 0.5, 0.5, 0.5),
        //     pivot: new pc.Vec2(0.5, 0.5),
        //     text: "REFRESH",
        //     fontSize: 18,
        //     color: new pc.Color(1, 1, 1),
        //     fontAsset: this.font,
        //     useInput: true
        // });


        // compose elements for display
        // refreshBtn.addChild(btnText);
        group.addChild(refreshBtn);
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

    private refreshSeed(textValue: string) {
        const { width, height, settings: config } = this.map.generation as MapGenerator;
        this.map.generation = new MapGenerator(textValue, width, height, config)
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