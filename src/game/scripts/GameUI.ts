import * as pc from 'playcanvas';
import type { Tile } from '../../lib/generation';

export class GameUiController extends pc.Script {
    static scriptName=  'game-ui-controller';

    public font!: pc.Asset;
    private screenEntity!: pc.Entity;

    // References to UI groups for toggling visibility or pulling data
    private seedModifierInput!: pc.Entity;
    private coordinateBox!: pc.Entity;
    private tileInfoBox!: pc.Entity;
    
    // References to internal elements needed for logic updates
    private textInputElement!: pc.Entity;
    private tileNameTextEl!: pc.Entity;
    private tileTypeTextEl!: pc.Entity;
    private tileCoordTextEl!: pc.Entity;

    private fontLoaded = false;

    // Track mock value for the input box
    private currentInputValue: string = "Input a seed...";

    update() {
        if (!this.font || this.fontLoaded) return;

        this.fontLoaded = true;
        this.createUiHierarchy();
        this.setupEventHandlers();
        this.hideTileInfo(); // start hidden, only show when a tile is seleted
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
        this.buildTextInputAndButton();
        this.buildHoverCornerBox();
        this.buildTileInfoBox();
    }

    private buildTextInputAndButton() {
        const map = this.app.root.findByName('MapRenderEntity')?.script.MapRenderer;
        this.currentInputValue = map.generation.seed;

        this.seedModifierInput = new pc.Entity('TextInputGroup');
        this.seedModifierInput.setLocalPosition(20, -20, 0)
        this.seedModifierInput.addComponent('element', {
            type: pc.ELEMENTTYPE_IMAGE,
            anchor: new pc.Vec4(0, 1, 0, 1), // Top center
            pivot: new pc.Vec2(0, 1),
            width: 500,
            height: 60,
            color: new pc.Color(0.15, 0.15, 0.15, 0.8),
            useInput: true
        });

        // The editable text container
        const textBox = new pc.Entity('TextBoxElement');
        textBox.addComponent('element', {
            type: pc.ELEMENTTYPE_TEXT,
            anchor: new pc.Vec4(0, 0, 0.7, 1), // Takes left 70% of group width
            pivot: new pc.Vec2(0, 0.5),
            margin: new pc.Vec4(15, 0, 0, 0),
            text: this.currentInputValue,
            fontSize: 24,
            color: new pc.Color(1, 1, 1),
            enableOutline: true,
            outlineColor: new pc.Color(0, 0, 0),
            useInput: true,
            fontAsset: this.font
        });

        this.seedModifierInput.addChild(textBox);
        this.textInputElement = textBox.element!;

        // Action / Refresh Button Next To Textbox
        const refreshBtn = new pc.Entity('RefreshButton');
        refreshBtn.addComponent('element', {
            type: pc.ELEMENTTYPE_IMAGE,
            anchor: new pc.Vec4(0.75, 0, 1, 1), // Takes right 25% of group width
            pivot: new pc.Vec2(1, 0.5),
            margin: new pc.Vec4(0, 5, 5, 5),
            color: new pc.Color(0.2, 0.6, 0.26),
            useInput: true
        });
        refreshBtn.addComponent('button', {
            active: true,
            fadeDuration: 0.1,
            hoverColor: new pc.Color(0.25, 0.75, 0.33),
            pressedColor: new pc.Color(0.15, 0.5, 0.2)
        });

        const btnText = new pc.Entity('BtnText');
        btnText.addComponent('element', {
            type: pc.ELEMENTTYPE_TEXT,
            anchor: new pc.Vec4(0.5, 0.5, 0.5, 0.5),
            pivot: new pc.Vec2(0.5, 0.5),
            text: "REFRESH",
            fontSize: 18,
            color: new pc.Color(1, 1, 1),
            fontAsset: this.font
        });

        refreshBtn.addChild(btnText);
        this.seedModifierInput.addChild(refreshBtn);

        this.screenEntity.addChild(this.seedModifierInput);
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

    private setupEventHandlers() {
        // Text box focus execution setup 
        this.textInputElement.entity.element!.on('click', () => {
            const userInput = prompt("Modify field text value:", this.currentInputValue);
            if (userInput !== null) {
                this.currentInputValue = userInput;
                this.textInputElement.text = userInput;
            }
        });

        // Trigger action via refresh button
        const refreshBtnEntity = this.seedModifierInput.findByName('RefreshButton');
        refreshBtnEntity?.element!.on('click', () => {
            this.refreshSeed(this.currentInputValue);
        });

        // Hover Corner Interactions 
        // @TODO: Make this appear/disappear when the OrthoCamera has a tile in its sights.
    }

    private refreshSeed(textValue: string) {
        console.log(`[UI Action] Refresh button clicked! Processing text payload: "${textValue}"`);
        // Append engine state manipulation actions here
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