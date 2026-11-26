# Identidad corporativa SAREB en Admin, cuadro de costes en el menú y filtro «mes en curso»

Fecha: 2026-10-27
Estado: borrador pendiente de revisión
Work item: pendiente de crear (PBI con cuatro tasks, ver §9)
Proyecto Azure DevOps: AI DocClassExt

## 1. Objetivo

Adaptar el Admin de DocumentIA (Blazor Server, `src/frontend/DocumentIA.Admin`) al manual de identidad y a la guía de estilo de SAREB, y aprovechar la línea para tres cambios funcionales del menú y los filtros:

1. Quitar del menú la entrada «Monitor» (v1) y dejar la de Monitor v2 con el nombre «Monitor».
2. Añadir al menú la entrada «Cuadro de costes», que apunta a la página `/costes` ya existente.
3. Añadir el valor «Mes en curso» al selector de rango de los filtros compartidos por Monitor y Costes. Costes arranca con «Mes en curso»; Monitor arranca con «Últimas 24 horas».

Fuera de alcance: la página Monitor v1 (`/monitor`) y su ruta siguen existiendo sin cambios; no se recompila Bootstrap; no se cambia el contrato HTTP con las Functions; no hay modo oscuro.

## 2. Fuentes normativas

- `SAREB_MANUAL-DE-IDENTIDAD_ActualizacionDic2015.pdf`: 1.3 colores corporativos (p. 7-8), 1.4 la marca (p. 9), 1.5 área de respeto (p. 10), 3.1 tipografía para materiales electrónicos (p. 43), 3.5 tablas (p. 48).
- `Sareb-Guia-de-estilo-.pdf`: 6.6 uso de gráficos (p. 19-21), tabla digital de ejemplo (p. 25).
- Logos: `logos-sareb-services/sareb-logo.png` (positivo, RGBA 2625×1121) y `sareb-logo-negativo.png` (negativo, RGBA 2625×1113), en `C:\Users\ubf0238\source\repos\AgilizaIA\Docs\auxiliares\Recuros corporativos\`.

El manual imprime el color 7 como «GF927C»; es una errata de «6F927C» (RGB 111, 146, 124).

## 3. Enfoque

Tema por tokens sobre Bootstrap 5. Un fichero `wwwroot/sareb.css` declara los tokens en `:root`, sobrescribe las variables de Bootstrap (`--bs-primary`, `--bs-success`, `--bs-warning`, `--bs-danger`, `--bs-info`, `--bs-secondary`, `--bs-body-font-family`, variables de tabla) y añade las reglas de tablas, tarjetas y KPIs. Se carga en `App.razor` después de `bootstrap.min.css` y antes de `app.css`. No se toca `bootstrap.min.css`. Los colores que usan los componentes C# salen de una clase estática `SarebPaleta`.

Descartados: recompilar Bootstrap desde SCSS (mete Node y Sass en un proyecto que no los tiene) y sustituir Bootstrap (reescribir páginas por un cambio de identidad).

## 4. Tokens y paleta

| Token CSS | Valor | Uso |
|---|---|---|
| `--sareb-verde` | `#00858C` | primario (`--bs-primary`), enlaces, foco, título de tablas, barra lateral |
| `--sareb-azul-oscuro` | `#046E97` | segunda serie de gráficos, hover del primario |
| `--sareb-azul` | `#0086AD` | tercera serie, `--bs-info` |
| `--sareb-turquesa` | `#00ABA8` | cuarta serie, `--bs-success` (estado correcto) |
| `--sareb-verde-claro` | `#90CCB7` | cabeceras de tabla, fondos suaves, marcador del ítem activo del menú |
| `--sareb-verde-gris` | `#6F927C` | texto secundario sobre fondo claro, etiquetas de KPI |
| `--sareb-gris` | `#BBBEB4` | bordes, `--bs-secondary`, base neutra de gráficos |
| `--sareb-gris-claro` | `#AFB6AC` | serie neutra adicional |
| `--sareb-naranja` | `#F39E48` | `--bs-warning`, estado en revisión |
| `--sareb-rojo` | `#EA6B43` | `--bs-danger`, estado con error |

Tipografía: `Arial, Helvetica, sans-serif` en `html, body` (manual 3.1). De `app.css` se eliminan los azules de la plantilla de Blazor (`#1b6ec2`, `#1861ac`, `#006bb7`, `#258cfb`) y el foco pasa al verde corporativo. Los colores de validación de formularios (`#26b050`, `#e50000`) pasan a turquesa y rojo de la paleta.

`SarebPaleta` (C#, `Services/SarebPaleta.cs`):

```csharp
public static class SarebPaleta
{
    public const string Verde = "#00858C";
    public const string AzulOscuro = "#046E97";
    public const string Azul = "#0086AD";
    public const string Turquesa = "#00ABA8";
    public const string VerdeClaro = "#90CCB7";
    public const string VerdeGris = "#6F927C";
    public const string Gris = "#BBBEB4";
    public const string GrisClaro = "#AFB6AC";
    public const string Naranja = "#F39E48";
    public const string Rojo = "#EA6B43";

    // De menos a más intensidad, como pide la guía para comparativas.
    public static readonly string[] Serie = [GrisClaro, Gris, VerdeClaro, Turquesa, Azul, AzulOscuro, Verde];

    public const string Ok = Turquesa;
    public const string Revision = Naranja;
    public const string Error = Rojo;
    public const string Neutro = Gris;
}
```

El banner de entorno (`EnvironmentBannerState`) no cambia de código: usa `bg-danger`, `bg-warning` y `bg-info`, que ahora toman los colores de la paleta.

## 5. Layout y menú

### 5.1 Barra lateral (`MainLayout.razor.css`, `NavMenu.razor`, `NavMenu.razor.css`)

- Fondo plano `var(--sareb-verde)`; desaparece el degradado azul-morado.
- Cabecera de la barra: `sareb-logo-negativo.png` a 40 px de alto, alineado a la izquierda, con margen igual al área de respeto (manual 1.5: 1x = distancia entre símbolo y logotipo; en pantalla se puede prescindir del margen izquierdo si la marca va alineada al margen). Debajo, en Arial regular 0,8 rem y blanco al 80 %, el texto «DocumentIA · Administración». Se retira el texto «DocumentIA.Admin» del `navbar-brand`.
- Ítems: texto blanco; activo con fondo blanco al 18 % y borde izquierdo de 3 px en `var(--sareb-verde-claro)`; hover blanco al 10 %. Los iconos SVG embebidos en `NavMenu.razor.css` ya son blancos y se mantienen.
- Los PNG se copian a `wwwroot/img/sareb-logo.png` y `wwwroot/img/sareb-logo-negativo.png` reescalados a 600 px de ancho (los originales son 2625 px y 146 KB cada uno). El positivo queda disponible para la página de Inicio y futuros usos sobre blanco.

### 5.2 Orden del menú

Inicio · Tipologías · Catálogo TDN1 · Catálogo TDN2 · Modelos · Prompts · Plugins Tipologías · separador · **Monitor** (`/monitor-v2`) · **Cuadro de costes** (`/costes`) · separador · Configuración.

Desaparece la entrada «Monitor» que apuntaba a `/monitor`. La página `Monitor.razor` y sus rutas `/monitor` y `/monitor/{Guid}` se conservan sin cambios. «Cuadro de costes» reutiliza el icono de gráfico de barras ya embebido.

### 5.3 Cabecera principal

`top-row` en blanco con borde inferior de 1 px `var(--sareb-gris)`. El banner de entorno sigue ocupando la cabecera.

### 5.4 Inicio (`Home.razor`)

Título «DocumentIA» en verde corporativo, una frase de descripción («Administración de la clasificación documental: configuración, seguimiento y costes.») y cuatro tarjetas de acceso: Tipologías (`/tipologias`), Monitor (`/monitor-v2`), Cuadro de costes (`/costes`) y Configuración (`/configuracion-consulta`). Cada tarjeta: borde `var(--sareb-gris)`, título en verde y una línea de descripción. Sin datos en vivo.

## 6. Gráficos, tablas y KPIs

### 6.1 Gráficos (guía 6.6)

- Opciones por defecto de ApexCharts en `wwwroot/js/apexcharts-interop.js` mediante el objeto global `Apex`: `chart.fontFamily` Arial, animaciones desactivadas, sin degradados (`fill.type = "solid"`), rejilla en gris claro, leyenda abajo a la izquierda, `colors` igual a la serie de `SarebPaleta` (se pasa desde C# en `crear`, para no duplicar los valores en JS).
- Los componentes `MonitorGraficos.razor` y `CostesGrafico.razor` dejan de usar literales de color y toman `SarebPaleta`:
  - Evolución del Monitor: «Total» en `SarebPaleta.Neutro` y «Con error» en `SarebPaleta.Error` (base neutra y un color intenso para destacar).
  - Evolución de costes: una sola serie en `SarebPaleta.Verde`.
- Cada tarjeta de gráfico tiene: título breve en minúscula y negrita con el periodo («ejecuciones por día · últimas 24 horas», «coste de IA por día · mes en curso»), unidad arriba a la derecha en regular («nº», «€») y nota al pie en gris pequeño («Fuente: ejecuciones registradas en DocumentIA; horas en hora peninsular»). El texto del periodo lo genera un método compartido a partir del filtro (`MonitorFiltroDto.DescripcionPeriodo()`), para que los dos gráficos lo escriban igual.
- Los rótulos de datos siguen ocultos, por lo que el eje de valores se mantiene visible.

### 6.2 Tablas (manual 3.5, guía p. 25)

Reglas en `sareb.css` sobre las clases que las páginas ya usan:

- `card-header` de tarjetas que contienen tabla: Arial negrita, blanco sobre `var(--sareb-verde)`.
- `thead th` y `.table-light th`: Arial negrita, negro sobre `var(--sareb-verde-claro)`.
- Datos: Arial regular; filas alternas (`table-striped`) blanco y `var(--sareb-verde-claro)` al 12 %; hover al 25 %.
- `table-dark` pasa a verde corporativo con texto blanco.

Los bloques `<style>` de `MonitorV2.razor` y `Costes.razor` (idénticos, acotados bajo `.mv2`) se mueven a `sareb.css` y desaparecen de las páginas. Los bloques `<style>` de `PromptEditor.razor`, `EjecucionDetalle.razor` y `JsonEditorComponent.razor` solo cambian los literales de color por tokens.

### 6.3 KPIs y estados

Valor en `var(--sareb-verde)` y etiqueta en `var(--sareb-verde-gris)`. Correctas, en revisión y con error usan `text-success`, `text-warning` y `text-danger`, que ahora son turquesa, naranja y rojo de la paleta; badges, alertas y semáforos de salud cambian por la misma vía.

## 7. Filtro «mes en curso» y valores por defecto

### 7.1 Modelo (`MonitorFiltroDto`, en `Services/MonitorService.cs`)

- Nueva propiedad `public bool MesEnCurso { get; set; }`.
- `VentanaUtc()`: si hay rango fijo (`Desde` y `Hasta`), manda el rango fijo como hasta ahora. Si no, y `MesEnCurso` es verdadero, la ventana es `(HoraEspana.InicioDelDiaUtc(primer día del mes en hora peninsular), DateTime.UtcNow)`. En otro caso, `RangoDias` como hasta ahora.
- `EsRangoFijo` no cambia: con `MesEnCurso` sigue siendo falso, así que el auto-refresco de Monitor no se pausa y la ventana se recalcula en cada consulta.
- `Clonar()` copia la propiedad sin cambios (es `MemberwiseClone`).
- `DescripcionPeriodo()` devuelve el texto del periodo para los títulos de los gráficos: «últimas 24 horas», «últimos N días», «mes en curso» o «del d/m/aaaa al d/m/aaaa».

### 7.2 Componente de filtros (`Components/Monitor/MonitorFiltros.razor`)

- El selector de rango añade `<option value="mes">Mes en curso</option>` entre «Últimas 24 horas» y «Últimos 7 días».
- `ValorRango` devuelve `"custom"` con rango fijo, `"mes"` con `MesEnCurso` y `RangoDias` en el resto.
- Elegir «Mes en curso»: `MesEnCurso = true`, `Desde = Hasta = null`, `_personalizado = false`. Elegir un preset numérico: `MesEnCurso = false` además de lo que ya hace. Elegir «Personalizado»: `MesEnCurso = false` y sigue precargando el mes en curso como fechas fijas (comportamiento actual, útil para meses cerrados).

### 7.3 Valores por defecto

- `Costes.razor`: `_filtro = new() { MesEnCurso = true }`.
- `MonitorV2.razor`: `_filtro = new() { RangoDias = 1 }`.
- `Monitor.razor` (v1): sin cambios (7 días).

### 7.4 Contrato con el backend

Sin cambios: la query sigue enviando `desde` y `hasta` absolutos en ISO 8601. `CostesResumenDto.PeriodoDias` lo calcula el backend a partir de esas fechas.

## 8. Pruebas

Proyecto `src/backend/DocumentIA.Tests.Admin` (bUnit 1.38, xunit, FluentAssertions). Se escriben antes que el código (TDD).

- `Services/MonitorFiltroDtoTests.cs` (nuevo o ampliado): con `MesEnCurso` la ventana empieza a las 00:00 del día 1 en hora peninsular y termina en ahora; un rango fijo manda sobre `MesEnCurso`; `EsRangoFijo` es falso con `MesEnCurso`; `DescripcionPeriodo()` para cada caso.
- `Components/MonitorFiltrosTests.cs`: el selector ofrece «Mes en curso»; elegirlo emite `MesEnCurso` con fechas vacías; elegir un preset numérico lo desactiva; con `MesEnCurso` el selector muestra «mes» seleccionado y no muestra los inputs de fecha.
- `Components/CostesPaginaTests.cs`: la página arranca con `MesEnCurso`.
- `Components/MonitorV2Tests.cs`: la página arranca con 24 horas; con «Mes en curso» no aparece el aviso de auto-refresco pausado.
- `Components/NavMenuTests.cs` (nuevo): hay un enlace «Monitor» a `monitor-v2`; hay «Cuadro de costes» a `costes`; no hay enlace a `monitor`.
- `Components/MonitorGraficosTests.cs` y tests de `CostesGrafico`: las opciones enviadas al interop llevan los colores de `SarebPaleta`; la tarjeta muestra título con periodo, unidad y nota de fuente.
- El CSS no se prueba por unidad. Verificación visual en local con captura en Chrome headless de Inicio, Monitor y Cuadro de costes (ver memoria de trampas de render del Admin: Bootstrap 5.1 y decimales con coma).
- Cierre: `dotnet build src/backend/DocumentIA.sln` sin warnings nuevos, `dotnet test src/backend/DocumentIA.Tests.Admin` y `dotnet test src/backend/DocumentIA.Tests.Unit`, `dotnet format src/backend/DocumentIA.sln --verify-no-changes`.

## 9. Work items y rama

PBI: «Identidad corporativa SAREB en Admin, cuadro de costes en el menú y filtro mes en curso». Tasks:

1. Tokens, `sareb.css`, `SarebPaleta`, layout y barra lateral con logo.
2. Gráficos, tablas y KPIs según manual 3.5 y guía 6.6; retirada de los bloques `<style>` duplicados.
3. Menú (quitar Monitor v1, renombrar v2, añadir Cuadro de costes) y página de Inicio.
4. Filtro «mes en curso», valores por defecto de Costes y Monitor, y `DescripcionPeriodo()`.

Se crean en Azure DevOps solo con un sí explícito. Rama: `feature/<AB#>-identidad-sareb-admin` desde `develop`, una vez exista el PBI. Antes de crearla se decide qué hacer con lo pendiente de `feature/101075-configseed-alta-clave-natural` (dos commits sin integrar, tres docs modificados sin confirmar y una spec sin seguimiento).

## 10. Impacto documental

- Técnica: `docs/03_DISENO_TECNICO_DETALLADO.md`, nueva sección de identidad visual del Admin (tokens, `sareb.css`, `SarebPaleta`, logos en `wwwroot/img/`).
- Funcional: `docs/02_ANALISIS_FUNCIONAL.md` RF21 deja de decir «sin entrada de menú»; `docs/05_MANUAL_USO_CONFIGURACION.md` (§ de `/costes`) y `docs/manuales/MANUAL_COSTES_IA.md` retiran el aviso de que `/costes` no está en el menú y describen «Mes en curso» y los nuevos valores por defecto.
- Arquitectura: sin ADR; decisión visual y de implementación local, fuera de los disparadores.
- Explotación: ninguno; no cambian despliegue, configuración ni entornos.

## 11. Riesgos

- Sobrescribir variables de Bootstrap cambia también componentes no revisados (modales, paginación, formularios de edición). Mitigación: la revisión visual de cierre recorre todas las páginas del menú, no solo las tres de la línea.
- Contraste: negro sobre `#90CCB7` da 11,5:1 (AA en cualquier tamaño). Blanco sobre `#00858C` da 4,4:1, justo por debajo del 4,5:1 de AA para texto normal y por encima del 3:1 de texto grande o negrita; la barra lateral en negativo es el uso canónico de la marca y se acepta esa limitación, con los ítems del menú en peso 600 para ganar legibilidad. `#F39E48` con texto blanco no llega a AA, por eso el banner de aviso sigue con `text-dark`.
- Monitor v1 queda sin entrada de menú pero accesible por URL; si alguien lo tiene en favoritos no se rompe.
