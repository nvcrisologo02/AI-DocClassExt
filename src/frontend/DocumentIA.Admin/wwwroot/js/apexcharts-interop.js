// Interop de ApexCharts para Blazor Server. Guarda las instancias por id de
// elemento para poder destruirlas: en Blazor Server un componente puede
// desaparecer sin recargar la pagina, y sin destruir quedarian colgando.

// Valores por defecto segun la guia de estilo de SAREB (6.6): Arial, sin
// animaciones ni degradados y leyenda abajo a la izquierda. Ningun color se
// fija aqui, ni el de la rejilla: llegan desde C# (SarebPaleta) en cada grafico,
// para no mantener la paleta en dos sitios.
window.Apex = {
    chart: { fontFamily: 'Arial, Helvetica, sans-serif', animations: { enabled: false } },
    fill: { type: 'solid' },
    legend: { position: 'bottom', horizontalAlign: 'left' }
};

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
