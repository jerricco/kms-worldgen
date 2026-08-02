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

        this.createGlobalState()

        this.createVisuals();
    }

    update(dt: number) {
        if (!this.isFocused || !this.cursorEntity) return;

        this.blinkTimer += dt;
        if (this.blinkTimer >= this.blinkSpeed) {
            this.blinkTimer = 0;
            this.cursorEntity.enabled = !this.cursorEntity.enabled;
        }
    }

    destroy() {
        window[TextInputBinder.scriptName].textInputBinderCount-- // reduce the count immediately
        // disable & remove its instance
        for (const instanceId of Object.entries(window[TextInputBinder.scriptName].instances)) {
            if (parseInt(instanceId) === this.instanceId) {
                window.removeEventListener('keydown', this.keydownHandler);
                window.removeEventListener('mousedown', this.blurHandler);
                window.removeEventListener('touchstart', this.blurHandler);
                delete window[TextInputBinder.scriptName].instances[instanceId];
            }
        }

        // clear inputbinder entirely if no instances left
        if (window[TextInputBinder.scriptName].instances.length === 0) {
            delete window[TextInputBinder.scriptName];
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

    createGlobalState() {
        window.addEventListener('keydown', this.keydownHandler);
        window.addEventListener('mousedown', this.blurHandler);
        window.addEventListener('touchstart', this.blurHandler);

        // Global instance handling
        if (!window[TextInputBinder.scriptName]) {
            window[TextInputBinder.scriptName] = {}; // window level tracking
        }

        // also keep track of the number of inputs onscreen so that we only destroy these when the last is gone.
        if (!window[TextInputBinder.scriptName].textInputBinderCount) window[TextInputBinder.scriptName].textInputBinderCount = 0;
        window[TextInputBinder.scriptName].textInputBinderCount++ // maps to instanceId as index
        this.instanceId = window[TextInputBinder.scriptName].textInputBinderCount;

        // store a register of these entities so that others can unfocus them & handle window events.
        if (!window[TextInputBinder.scriptName].instances) window[TextInputBinder.scriptName].instances = {};
        window[TextInputBinder.scriptName].instances[this.instanceId] = this;
    }

    createVisuals() {
        if (!this.entity || !this.entity.element) return;

        this.focusOutlineEntity = new pc.Entity('InputOutline');
        this.focusOutlineEntity.addComponent('element', {
            type: pc.ELEMENTTYPE_IMAGE,
            anchor: new pc.Vec4(0, 0, 1, 1),
            margin: new pc.Vec4(-4, -4, -4, -4),
            color: new pc.Color(0.2, 0.4, 0.8, 0.3),
            enabled: false
        });
        this.entity.addChild(this.focusOutlineEntity);

        this.selectionEntity = new pc.Entity('TextSelection');
        this.selectionEntity.addComponent('element', {
            type: pc.ELEMENTTYPE_IMAGE,
            anchor: new pc.Vec4(0, 0.5, 0, 0.5),
            pivot: new pc.Vec2(0, 0.5),
            color: new pc.Color(0.2, 0.5, 1.0, 0.4),
            enabled: false
        });
        this.entity.addChild(this.selectionEntity);

        this.cursorEntity = new pc.Entity('InputCursor');
        this.cursorEntity.addComponent('element', {
            type: pc.ELEMENTTYPE_IMAGE,
            anchor: new pc.Vec4(0.5, 0.5, 0.5, 0.5),
            pivot: new pc.Vec2(0.5, 0.5),
            width: 2,
            height: this.entity.element.fontSize || 16,
            color: new pc.Color(0, 0, 0, 1),
            enabled: false
        });
        this.entity.addChild(this.cursorEntity);
    }

    updateVisuals() {
        const el = this.entity.element;
        if (!el || el.type !== 'text') return;

        this.blinkTimer = 0;
        if (this.cursorEntity) this.cursorEntity.enabled = true;

        // Read the current clean string from your script property
        const textStr = this.currentText || "";

        // Helper: Safely measure pixel width of a substring using the engine's built-in calculation
        const getWidthAtLineIndex = (index: number): number => {
            if (index <= 0) return 0;

            // Cache original full text string
            const originalText = el.text;

            // Measure up to our targeted cursor/selection index boundary 
            el.text = textStr.substring(0, index);
            const partialWidth = el.textWidth;

            // Restore original string immediately so no visual flicker occurs
            el.text = originalText;

            return partialWidth;
        };

        // Calculate absolute positions relative to the left starting edge
        let cursorX = getWidthAtLineIndex(this.cursorIndex);
        let selectionX = getWidthAtLineIndex(this.selectionIndex);

        // Apply alignment shifts (Handles Left, Centered, or Right alignment structures)
        let alignmentOffset = 0;
        if (el.alignment.x === 0.5) {
            alignmentOffset = -el.textWidth * 0.5; // Offset for centered text layouts
        } else if (el.alignment.x === 1) {
            alignmentOffset = -el.textWidth;       // Offset for right-aligned text layouts
        }

        cursorX += alignmentOffset;
        selectionX += alignmentOffset;

        // Update Caret Entity Positions
        if (this.cursorEntity) {
            // render it at 1000Y over everything
            this.cursorEntity.setLocalPosition(cursorX, 1000, 0);
        }

        // Update Highlight Selection Bounds
        if (this.selectionEntity) {
            if (this.cursorIndex !== this.selectionIndex) {
                this.selectionEntity.enabled = true;
                const startX = Math.min(cursorX, selectionX);
                const width = Math.abs(cursorX - selectionX);

                this.selectionEntity.setLocalPosition(startX, 999, 0);
                if (this.selectionEntity.element) {
                    this.selectionEntity.element.width = width;
                    this.selectionEntity.element.height = el.fontSize || 16;
                }
            } else {
                this.selectionEntity.enabled = false;
            }
        }
    }

    // INPUT LEVEL EVENTS
    private focusInput(e: FocusEvent) {
        e.stopPropagation();
        // scan other focussed textBinders and unfocus them.
        for (const [instanceId, binder] of Object.entries(window[TextInputBinder.scriptName].instances)) {
            if (parseInt(instanceId) !== this.instanceId) {
                (binder as TextInputBinder).isFocused = false;
            }
        }

        if (this.focusOutlineEntity) this.focusOutlineEntity.enabled = true;
        if (this.cursorEntity) this.cursorEntity.enabled = true;

        this.isFocused = true;
        this.blinkTimer = 0;

        this.updateVisuals();
        this.entity.fire('ui:focus', this.currentText);
    }

    // WINDOW LEVEL EVENTS
    private handleKeyDown(e: KeyboardEvent) {
        // Only accept typing if user has clicked/focused this UI element
        if (!this.isFocused) return;

        // Prevent browser defaults for tabs, arrows, or space scrolling while typing
        const fnKeys = ['Backspace', ' ', 'ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown'];
        if (fnKeys.includes(e.key)) {
            e.preventDefault();
        }

        const hasSelection = this.cursorIndex !== this.selectionIndex;
        const startIdx = Math.min(this.cursorIndex, this.selectionIndex);
        const endIdx = Math.max(this.cursorIndex, this.selectionIndex);

        switch (e.key) {
            case 'ArrowLeft': {
                if (e.shiftKey) {
                    this.cursorIndex = Math.max(0, this.cursorIndex - 1);
                } else {
                    this.cursorIndex = hasSelection ? startIdx : Math.max(0, this.cursorIndex - 1);
                    this.selectionIndex = this.cursorIndex;
                }
                break
            };
            case 'ArrowRight': {
                if (e.shiftKey) {
                    this.cursorIndex = Math.min(this.currentText.length, this.cursorIndex + 1);
                } else {
                    this.cursorIndex = hasSelection ? endIdx : Math.min(this.currentText.length, this.cursorIndex + 1);
                    this.selectionIndex = this.cursorIndex;
                }
                break
            };
            case 'ArrowUp': {
                this.cursorIndex = 0;
                if (!e.shiftKey) {
                    this.selectionIndex = this.cursorIndex;
                }
                break
            };
            case 'ArrowDown': {
                this.cursorIndex = this.currentText.length
                if (!e.shiftKey) {
                    this.selectionIndex = this.cursorIndex;
                }   
                break
            };
            case 'Backspace': {
                if (hasSelection) {
                    this.currentText = this.currentText.slice(0, startIdx) + this.currentText.slice(endIdx);
                    this.cursorIndex = startIdx;
                } else if (this.cursorIndex > 0) {
                    this.currentText = this.currentText.slice(0, this.cursorIndex - 1) + this.currentText.slice(this.cursorIndex);
                    this.cursorIndex--;
                }
                this.selectionIndex = this.cursorIndex;
                break
            };
            case 'Delete': {
                if (hasSelection) {
                    this.currentText = this.currentText.slice(0, startIdx) + this.currentText.slice(endIdx);
                    this.cursorIndex = startIdx;
                } else if (this.cursorIndex < this.currentText.length) {
                    this.currentText = this.currentText.slice(0, this.cursorIndex) + this.currentText.slice(this.cursorIndex + 1);
                    // Note: this.cursorIndex does not change because the next character shifts into its position
                }
                this.selectionIndex = this.cursorIndex;
                break;
            };
            case 'Enter': {
                this.blurInput();
                this.entity.fire('ui:key:enter', this.currentText);
                return;
            };
        }

        // handle multipress
        if (e.key.toLowerCase() === 'a' && (e.ctrlKey || e.metaKey)) {
            e.preventDefault();
            this.selectionIndex = 0;
            this.cursorIndex = this.currentText.length;
        }
        else if (e.key.length === 1 && !e.ctrlKey && !e.metaKey) {
            this.currentText = this.currentText.slice(0, startIdx) + e.key + this.currentText.slice(endIdx);
            this.cursorIndex = startIdx + 1;
            this.selectionIndex = this.cursorIndex;
        }


        // Update the visible PlayCanvas UI Text element mesh
        if (this.entity.element) {
            this.entity.element.text = this.currentText;
            this.entity.element.once('text:update', () => {
                this.updateVisuals();
            });
            this.updateVisuals();
        }
    }

    private blurInput() {
        if (this.isFocused) {
            this.isFocused = false;

            if (this.focusOutlineEntity) this.focusOutlineEntity.enabled = false;
            if (this.cursorEntity) this.cursorEntity.enabled = false;
            if (this.selectionEntity) this.selectionEntity.enabled = false;

            this.entity.fire('ui:blur', this.currentText);
        }
    }
}