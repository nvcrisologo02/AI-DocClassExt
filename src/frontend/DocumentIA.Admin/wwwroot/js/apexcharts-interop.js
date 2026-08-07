// Interop de ApexCharts para Blazor Server. Guarda las instancias por id de
// elemento para poder destruirlas: en Blazor Server un componente puede
// desaparecer sin recargar la pagina, y sin destruir quedarian colgando.
window.documentIaCharts = {
    instancias: {},

    crear: function (elementId, opciones) {
        const contenedor = document.getElementById(elementId);
        if (!contenedor) {
            console.error(`No existe el contenedor de grafico ${elementId}`);
            return false;
        }
        this.destruir(elementId);
        const grafico = new ApexCharts(contenedor, opciones);
        grafico.render();
        this.instancias[elementId] = grafico;
        return true;
    },

    actualizar: function (elementId, series, categorias) {
        const grafico = this.instancias[elementId];
        if (!grafico) {
            return false;
        }
        grafico.updateOptions({ xaxis: { categories: categorias } }, false, false);
        grafico.updateSeries(series, true);
        return true;
    },

    destruir: function (elementId) {
        const grafico = this.instancias[elementId];
        if (grafico) {
            grafico.destroy();
            delete this.instancias[elementId];
        }
    }
};
