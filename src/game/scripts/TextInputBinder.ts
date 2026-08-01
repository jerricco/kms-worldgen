import * as pc from 'playcanvas';

export class TextInputBinder extends pc.Script {
    static scriptName = 'text-input-binder';
    
    private currentText: string = ""
    private isFocused: boolean = false;
    private instanceId!: number;

    private blinkTimer: number = 0;
    private blinkSpeed: number = 0.5; // Blink every 0.5 seconds

    private cursorIndex: number = 0;
    private selectionIndex: number = 0;

    private cursorEntity: pc.Entity | null = null;
    private focusOutlineEntity: pc.Entity | null = null;
    private selectionEntity: pc.Entity | null = null;

    private keydownHandler = this.handleKeyDown.bind(this)
    private blurHandler = this.blurInput.bind(this)

    initialize() {
        if (!this.entity || !this.entity.element)
            throw new Error("TextInputBinder: No target ELEMENT entity was found! Please provide one.");

        this.currentText = this.entity.element.text || this.currentText;
        this.cursorIndex = this.currentText.length;
        this.selectionIndex = this.cursorIndex;

        this.entity.element.useInput = true;

        // @NOTE: Ensure parent entity-component has 'useInput=true' for click detection
        if (this.entity.element) {
            this.entity.element.on('mousedown', this.focusInput, this);
            this.entity.element.on('touchstart', this.focusInput, this);
        }

        window.addEventListener('keydown', this.keydownHandler);
        window.addEventListener('mousedown', this.blurHandler);
        window.addEventListener('touchstart', this.blurHandler);

        // Global instance handling
        if (!window.inputbinder) {
            window.inputbinder = {}; // window level tracking
        }

        // also keep track of the number of inputs onscreen so that we only destroy these when the last is gone.
        if (!window.inputbinder.textInputBinderCount) window.inputbinder.textInputBinderCount = 0;
        window.inputbinder.textInputBinderCount++ // maps to instanceId as index
        this.instanceId = window.inputbinder.textInputBinderCount;

        // store a register of these entities so that others can unfocus them & handle window events.
        if (!window.inputbinder.instances) window.inputbinder.instances = {};
        window.inputbinder.instances[this.instanceId] = this;

        this.createVisuals();
    }

    update(dt: number) {
        if (!this.isFocused || !this.cursorEntity) return;

        // this.blinkTimer += dt;
        // if (this.blinkTimer >= this.blinkSpeed) {
        //     this.blinkTimer = 0;
        //     this.cursorEntity.enabled = !this.cursorEntity.enabled;
        // }
    }

    destroy() {
        window.inputbinder.textInputBinderCount-- // reduce the count immediately
        // disable & remove its instance
        for (const instanceId of Object.entries(window.inputbinder.instances)) {
            if (parseInt(instanceId) === this.instanceId) {
                window.removeEventListener('keydown', this.keydownHandler);
                window.removeEventListener('mousedown', this.blurHandler);
                window.removeEventListener('touchstart', this.blurHandler);
                delete window.inputbinder.instances[instanceId];
            }
        }

        // clear inputbinder entirely if no instances left
        if (window.inputbinder.instances.length === 0) {
            delete window.inputbinder;
        }

        if (this.entity.element) {
            this.entity.element.off('mousedown', this.focusInput, this);
            this.entity.element.off('touchstart', this.focusInput, this);
        }

        if (this.cursorEntity) this.cursorEntity.destroy();
        if (this.focusOutlineEntity) this.focusOutlineEntity.destroy();
        if (this.selectionEntity) this.selectionEntity.destroy();
    }

    // CONSTRUCTION FUNCTIONS
    setInputText(text: string) {
        this.entity.element.text = this.currentText = text;
        this.cursorIndex = text.length;
        this.selectionIndex = this.cursorIndex;
        this.updateVisuals();
    }

    createVisuals() {}

    updateVisuals() {}

    // INPUT LEVEL EVENTS
    private focusInput(e: FocusEvent) {
        e.stopPropagation();
        // scan other focussed textBinders and unfocus them.
        for (const [instanceId, binder] of Object.entries(window.inputbinder.instances)) {
            if (parseInt(instanceId) !== this.instanceId) {
                binder.isFocused = false;
            }
        }

        this.isFocused = true;
        this.entity.fire('ui:focus', this.currentText);
    }

    // WINDOW LEVEL EVENTS
    private handleKeyDown(e: KeyboardEvent) {
        // Only accept typing if user has clicked/focused this UI element
        if (!this.isFocused) return;


        // Prevent browser defaults for tabs, arrows, or space scrolling while typing
        if (e.key === 'Backspace' || e.key === ' ') {
            e.preventDefault();
        }

        if (e.key === 'Backspace') {
            // Remove last character
            this.currentText = this.currentText.slice(0, -1);
        } else if (e.key === 'Enter') {
            this.blurInput()
            this.entity.fire('ui:key:enter', this.currentText)
            return;
        } else if (e.key.length === 1) {
            // Append single character keys (letters, numbers, space)
            this.currentText += e.key;
        }

        // Update the visible PlayCanvas UI Text element mesh
        if (this.entity && this.entity.element) {
            this.entity.element.text = this.currentText;
        }
    }

    private blurInput() {
        if (this.isFocused) {
            this.isFocused = false;
            this.entity.fire('ui:blur', this.currentText);
        }
    }
}