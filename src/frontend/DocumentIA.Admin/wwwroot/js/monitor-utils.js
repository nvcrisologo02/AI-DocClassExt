// Utilidades JS para el modal de detalle del Monitor: copiar al portapapeles
// y descargar el JSON de una ejecucion. Independiente de json-editor-interop.js
// porque no tiene relacion con el editor, solo con el modal que lo envuelve.
window.monitorUtils = {
    // navigator.clipboard requiere contexto seguro (HTTPS o localhost); en su
    // ausencia se recurre a un textarea oculto + document.execCommand('copy'),
    // soportado por navegadores antiguos y por HTTP sin TLS.
    copyToClipboard: async function (text) {
        if (navigator.clipboard && window.isSecureContext) {
            try {
                await navigator.clipboard.writeText(text);
                return true;
            } catch (error) {
                console.error('Error al copiar al portapapeles:', error);
            }
        }

        try {
            const textarea = document.createElement('textarea');
            textarea.value = text;
            textarea.style.position = 'fixed';
            textarea.style.opacity = '0';
            document.body.appendChild(textarea);
            textarea.focus();
            textarea.select();
            const copiado = document.execCommand('copy');
            document.body.removeChild(textarea);
            return copiado;
        } catch (error) {
            console.error('Error al copiar al portapapeles (fallback):', error);
            return false;
        }
    },

    downloadJson: function (fileName, content) {
        try {
            const blob = new Blob([content], { type: 'application/json' });
            const url = URL.createObjectURL(blob);
            const anchor = document.createElement('a');
            anchor.href = url;
            anchor.download = fileName;
            anchor.style.display = 'none';
            document.body.appendChild(anchor);
            anchor.click();
            document.body.removeChild(anchor);
            URL.revokeObjectURL(url);
            return true;
        } catch (error) {
            console.error('Error al descargar el JSON:', error);
            return false;
        }
    }
};
