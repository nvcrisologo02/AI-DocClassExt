// JSONEditor JavaScript Interop Library
window.jsonEditorInterop = {
    editors: {},
    
    // Initialize a new JSONEditor instance
    initEditor: function(elementId, mode, readOnly) {
        const container = document.getElementById(elementId);
        if (!container) {
            console.error(`Container with id ${elementId} not found`);
            return false;
        }
        
        const options = {
            mode: mode || 'tree',
            modes: ['tree', 'form', 'code', 'text', 'preview'],
            indentation: 2,
            search: true,
            navigationBar: true,
            statusBar: true,
            onEditable: readOnly ? function() { return false; } : undefined,
            onChangeText: function(jsonString) {
                // Trigger change event that can be listened from Blazor
                const element = document.getElementById(elementId);
                if (element) {
                    element.dispatchEvent(new CustomEvent('editorchange', {
                        detail: { json: jsonString, elementId: elementId }
                    }));
                }
            }
        };
        
        try {
            this.editors[elementId] = new JSONEditor(container, options);
            return true;
        } catch (error) {
            console.error(`Error initializing JSONEditor for ${elementId}:`, error);
            return false;
        }
    },
    
    // Set JSON content in editor
    setJson: function(elementId, jsonString) {
        if (!this.editors[elementId]) {
            console.error(`Editor with id ${elementId} not found`);
            return false;
        }
        
        try {
            const json = typeof jsonString === 'string' ? JSON.parse(jsonString) : jsonString;
            this.editors[elementId].set(json);
            return true;
        } catch (error) {
            console.error(`Error setting JSON for ${elementId}:`, error);
            return false;
        }
    },
    
    // Get JSON content from editor
    getJson: function(elementId) {
        if (!this.editors[elementId]) {
            console.error(`Editor with id ${elementId} not found`);
            return null;
        }
        
        try {
            return JSON.stringify(this.editors[elementId].get());
        } catch (error) {
            console.error(`Error getting JSON from ${elementId}:`, error);
            return null;
        }
    },
    
    // Get text content from editor (as string, not JSON)
    getText: function(elementId) {
        if (!this.editors[elementId]) {
            console.error(`Editor with id ${elementId} not found`);
            return null;
        }
        
        try {
            return this.editors[elementId].getText();
        } catch (error) {
            console.error(`Error getting text from ${elementId}:`, error);
            return null;
        }
    },
    
    // Change editor mode
    setMode: function(elementId, mode) {
        if (!this.editors[elementId]) {
            console.error(`Editor with id ${elementId} not found`);
            return false;
        }
        
        try {
            this.editors[elementId].setMode(mode);
            return true;
        } catch (error) {
            console.error(`Error setting mode for ${elementId}:`, error);
            return false;
        }
    },
    
    // Get current mode
    getMode: function(elementId) {
        if (!this.editors[elementId]) {
            console.error(`Editor with id ${elementId} not found`);
            return null;
        }
        
        try {
            return this.editors[elementId].getMode();
        } catch (error) {
            console.error(`Error getting mode for ${elementId}:`, error);
            return null;
        }
    },
    
    // Validate JSON
    validateJson: function(elementId) {
        if (!this.editors[elementId]) {
            console.error(`Editor with id ${elementId} not found`);
            return { valid: false, error: 'Editor not found' };
        }
        
        try {
            this.editors[elementId].get(); // This will throw if JSON is invalid
            return { valid: true };
        } catch (error) {
            return { valid: false, error: error.message };
        }
    },
    
    // Destroy editor instance
    destroyEditor: function(elementId) {
        if (this.editors[elementId]) {
            this.editors[elementId].destroy();
            delete this.editors[elementId];
            return true;
        }
        return false;
    },

    // Refresh editor layout after container resize
    refreshEditor: function(elementId) {
        const editor = this.editors[elementId];
        if (!editor) {
            return false;
        }

        try {
            if (typeof editor.refresh === 'function') {
                editor.refresh();
            }
            window.dispatchEvent(new Event('resize'));
            return true;
        } catch (error) {
            console.error(`Error refreshing editor ${elementId}:`, error);
            return false;
        }
    },
    
    // Check if editor exists
    hasEditor: function(elementId) {
        return elementId in this.editors;
    }
};

window.fileInterop = {
    downloadBase64File: function(contentType, fileName, base64Content) {
        if (!base64Content || !fileName) {
            return;
        }

        const anchor = document.createElement('a');
        anchor.href = `data:${contentType || 'application/octet-stream'};base64,${base64Content}`;
        anchor.download = fileName;
        anchor.style.display = 'none';
        document.body.appendChild(anchor);
        anchor.click();
        document.body.removeChild(anchor);
    }
};

window.wizardDraftInterop = {
    set: function(key, value) {
        if (!key) {
            return;
        }

        localStorage.setItem(key, value || '');
    },

    get: function(key) {
        if (!key) {
            return null;
        }

        return localStorage.getItem(key);
    },

    remove: function(key) {
        if (!key) {
            return;
        }

        localStorage.removeItem(key);
    }
};
