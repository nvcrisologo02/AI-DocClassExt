# DocumentIA — Guion de Presentación (30 minutos)
> Audiencia: equipo con conocimientos no técnicos  
> Fecha: abril 2026 | Proyecto: AI DocClassExt — SAREB

---

## Estructura general

| Bloque | Contenido | Tiempo |
|--------|-----------|--------|
| 1 | Introducción: el problema que resolvemos | 4 min |
| 2 | Qué hace el sistema (funcionalidades clave) | 8 min |
| 3 | Cómo está construido (visión de alto nivel) | 5 min |
| 4 | Puntos fuertes | 5 min |
| 5 | Capacidades de expansión | 4 min |
| 6 | Resumen y conclusión | 4 min |

---

## Bloque 1 — Introducción: el problema que resolvemos *(4 min)*

### Mensaje clave
> "Antes del sistema, procesar un documento era lento, manual y propenso a errores. Ahora ocurre automáticamente en segundos."

### Guion

Gestionamos miles de documentos inmobiliarios cada año: notas simples del Registro de la Propiedad, tasaciones, escrituras, certificados energéticos. Cada uno de esos documentos contiene datos clave para la gestión de los activos: quién es el titular de la finca, qué cargas tiene, cuál es su valor de tasación, su referencia catastral…

Hasta ahora, el proceso era completamente manual:
- Abrimos el documento.
- Identificamos a ojo: "esto es una nota simple", "esto es una tasación".
- Buscamos manualmente los datos relevantes dentro del documento.
- Se archivaba en el gestor documental (GDC), si es necesario.

Esto tiene tres problemas evidentes: **lentitud**, **errores humanos** y **falta de trazabilidad** — si alguien archivaba mal un documento, no había registro de quién lo hizo ni por qué.

DocumentIA es un sistema que recibe un documento PDF, lo procesa de forma completamente automática, y en cuestión de segundos devuelve los datos estructurados, validados y archivados — sin intervención humana en el caso general.

---

## Bloque 2 — Qué hace el sistema (funcionalidades clave) *(8 min)*

### Mensaje clave
> "El sistema hace por sí solo lo que una persona haría en varios minutos, con mayor precisión y dejando rastro completo de cada decisión."

### El pipeline de procesamiento

Cuando llega un documento, el sistema lo somete a un proceso secuencial de pasos. Conviene imaginarlos como una cadena de montaje inteligente:

---

**Paso 1 — Normalización y verificación de duplicados**

Antes de hacer nada, el sistema calcula una "huella digital" única del documento (similar a un código de barras). Si ese documento ya fue procesado antes, el sistema lo detecta y devuelve el resultado anterior en milisegundos, sin necesidad de volver a procesar. Esto evita trabajo redundante y consumo innecesario de recursos.

---

**Paso 2 — Clasificación automática**

El sistema identifica qué tipo de documento es. Para ello usa modelos de Inteligencia Artificial entrenados específicamente con documentos de SAREB. Si el modelo principal no tiene suficiente certeza, activa automáticamente un segundo modelo de respaldo basado en GPT-4 (el mismo motor que usa ChatGPT) para confirmar la clasificación.

---

**Paso 3 — Extracción de datos**

Una vez clasificado el documento, el sistema extrae automáticamente los campos relevantes para ese tipo: número de finca, titulares, cargas, fechas, referencias catastrales, valores de tasación… Los campos a extraer dependen del tipo de documento, y son completamente configurables.

Si el modelo principal no consigue extraer algún campo con suficiente fiabilidad, activa de nuevo el modelo GPT como refuerzo para completar los datos que faltan.

---

**Paso 4 — Validación de datos**

Los datos extraídos se verifican contra un conjunto de reglas de negocio configurables:
- ¿El NIF tiene el formato correcto?
- ¿La fecha de otorgamiento es coherente con la fecha del documento?
- ¿La referencia catastral tiene la longitud correcta?
- ¿Están presentes todos los campos obligatorios?

Si hay errores de validación, quedan registrados en el resultado para que un operador pueda revisarlos.

---

**Paso 5 — Enriquecimiento con datos externos**

El sistema puede consultar fuentes externas de información para completar o verificar los datos extraídos. Por ejemplo:
- Cruzar la referencia catastral con la base de datos de activos de SAREB para resolver a qué activo pertenece ese documento.
- Consultar sistemas adicionales como Atlas u otras fuentes de referencia.

Este enriquecimiento es opcional y configurable por tipo de documento.

---

**Paso 6 — Archivo en el Gestor Documental (GDC)**

Una vez procesado, el documento se archiva automáticamente en el Gestor Documental Corporativo (GDC) con sus metadatos correctamente asignados.

---

**Paso 7 — Puntuación de confianza**

Al final del proceso, el sistema asigna una **puntuación de confianza global** (de 0 a 100%) que mide la fiabilidad del resultado:

| Resultado | Confianza | Significado |
|-----------|-----------|-------------|
| **OK** | ≥ 85% | El sistema confía en el resultado. Procesamiento automático. |
| **REVISIÓN** | 70–85% | Resultado probablemente correcto pero conviene que alguien lo revise. |
| **ERROR** | < 70% | Baja fiabilidad. Requiere intervención humana. |

Esta puntuación se calcula combinando la certeza de la clasificación, la completitud de la extracción y el resultado de las validaciones. Es transparente y auditable.

---

**Todo queda registrado**

Cada ejecución deja un registro completo en base de datos: qué documento se procesó, qué pasos se ejecutaron, cuánto tardó cada uno, qué resultado se obtuvo y qué confianza tiene. Esto permite auditar cualquier decisión del sistema en cualquier momento.

---

## Bloque 3 — Cómo está construido (visión de alto nivel) *(5 min)*

### Mensaje clave
> "El sistema está construido sobre infraestructura cloud de Azure, es escalable por diseño."

### Visión de alto nivel (sin entrar en código)

El sistema se apoya en tres tipos de componentes:

**1. El motor de procesamiento (backend)**  
Es el corazón del sistema. Recibe documentos, ejecuta todos los pasos descritos y devuelve resultados. Funciona como un servicio en la nube.

**2. Los servicios de Inteligencia Artificial (Azure AI)**  
El sistema no tiene IA propia. En su lugar, aprovecha servicios de Microsoft Azure especializados:
- **Azure Document Intelligence**: modelos entrenados para reconocer y extraer datos de documentos específicos de SAREB.
- **Azure Content Understanding**: extracción avanzada de campos complejos.
- **Azure OpenAI (GPT-4)**: modelo de lenguaje para casos donde los modelos específicos tienen baja certeza.

*Analogía:* el motor de procesamiento es como un director de operaciones que coordina a diferentes especialistas (los servicios de IA) según lo que necesite cada caso.

**3. Las interfaces de COMPLETAR_GDC_HTTP_BASIC_USERNAMEsitracion**  
Hay una herramientas para interactuar con el sistema:
- **Portal de Administración (web)**: panel para que el equipo configure el sistema — tipos de documentos reconocidos, reglas de validación, modelos de IA activos, plugins de integración — sin necesidad de programar nada.

**Infraestructura**  
Todo está alojado en Azure (la nube de Microsoft), usando los mismos entornos que ya utiliza SAREB. Los secretos y credenciales están gestionados de forma segura a través de Azure Key Vault. El sistema cuenta con monitorización automática (Application Insights) que alerta ante cualquier error o anomalía.
La infomracion nunca sale del ecosistema de Sareb.

---

## Bloque 4 — Puntos fuertes *(5 min)*

### Mensaje clave
> "El sistema no es sólo rápido — es fiable, transparente y seguro por diseño."

---

**1. Degradación segura ("no se cae aunque falle una parte")**  
Si un servicio de IA no responde o devuelve un resultado poco fiable, el sistema activa automáticamente un mecanismo alternativo (fallback). Si el alternativo tampoco funciona, el proceso continúa de forma parcial y deja registrado qué ocurrió. Nunca se pierde el documento.

---

**2. Confianza transparente**  
El sistema no da un resultado sin explicar cuánto confía en él. Cada dato extraído lleva asociada su puntuación de certeza. Se puede ver de un vistazo si el resultado es fiable o requiere revisión, sin necesidad de releer el documento original.

---

**3. Deduplicación inteligente**  
Si se envía el mismo documento dos veces, el sistema lo detecta y devuelve el resultado anterior sin reprocesar. Esto evita trabajo duplicado y garantiza consistencia en los datos.

---

**4. Configuración sin programar**  
Los tipos de documentos reconocidos, los campos que se extraen de cada uno, las reglas de validación y los sistemas externos consultados son completamente configurables desde el portal de COMPLETAR_GDC_HTTP_BASIC_USERNAMEistración, sin necesidad de modificar código. El equipo de negocio puede adaptar el sistema a nuevas necesidades de forma autónoma.

---

**5. Trazabilidad total**  
Cada documento procesado tiene un identificador único. En cualquier momento se puede consultar: qué ocurrió con ese documento, qué datos se extrajeron, cuánto tiempo tardó cada paso, qué modelo de IA se usó y con qué confianza. Esto es fundamental para auditorías regulatorias y para detectar problemas a tiempo.

---

**6. Integración nativa con GDC**  
El archivo sube automáticamente al final del procesamiento, con los metadatos correctamente asignados según el tipo de documento y si asi esta indicado, en caso de existir en GDC se devuelve el ObjectID.

---

## Bloque 5 — Capacidades de expansión *(4 min)*

### Mensaje clave
> "El sistema está diseñado para crecer. Añadir nuevos tipos de documentos o nuevas integraciones no requiere rediseñar nada."

---

**Nuevos tipos de documentos**  
El sistema puede reconocer y procesar cualquier tipo de documento para el que se entrene un modelo o se defina una configuración. Añadir soporte para un nuevo tipo de documento — por ejemplo, escrituras de compraventa o contratos de arrendamiento — es una tarea de configuración y entrenamiento, no de desarrollo desde cero.

---

**Nuevas fuentes de enriquecimiento (plugins)**  
El sistema tiene una arquitectura de plugins: cada integración con un sistema externo es un componente independiente que se puede activar, desactivar o sustituir sin afectar al resto. Si mañana SAREB quiere consultar un nuevo sistema de referencia o un registro externo, basta con añadir un plugin nuevo.

---

**Mejora continua de los modelos**  
Los modelos de IA utilizados para clasificar y extraer datos se pueden reentrenar con nuevos ejemplos a medida que el sistema acumule más casos reales. Esto significa que el sistema mejora su precisión con el tiempo sin necesidad de intervención manual en cada documento.

---

**Hoja de ruta prevista**

| Capacidad | Estado |
|-----------|--------|
| Soporte a nuevas tipologías documentales | Disponible (configuración) |
| Reglas de validación cruzadas entre campos | En desarrollo |
| Protección de datos personales / GDPR (cifrado, enmascaramiento en logs) | StandBy |
| Gestión automática del ciclo de vida de documentos en Storage | Planificado |
| Dashboards operativos en tiempo real (Application Insights) | En desarrollo |

---

## Bloque 6 — Resumen y conclusión *(4 min)*

### Mensaje clave
> "Hemos construido una solución que transforma un proceso manual, lento y frágil en un pipeline automático, auditable y preparado para crecer."

---

### Resumen ejecutivo

**Lo que hemos construido:**
- Un motor de procesamiento con 13 pasos secuenciales, totalmente orquestados.
- Integración con 3 servicios de IA de Azure (Document Intelligence, Content Understanding, GPT-4).
- Un sistema de confianza que señala automáticamente qué resultados son seguros y cuáles requieren revisión humana.
- Un portal de COMPLETAR_GDC_HTTP_BASIC_USERNAMEistración para gestionar el sistema sin programar.
- Integración automática con el Gestor Documental Corporativo.
- Registro completo de auditoría para cada documento procesado.

**El estado actual:** el sistema está en producción. Los bloques fundamentales están completos y operativos. Esta en fase de consolidación de calidad y pruebas, con las funcionalidades avanzadas de expansión planificadas para los próximos meses.

---

### Mensaje final

DocumentIA no es sólo una herramienta de automatización. Es una plataforma que puede evolucionar con las necesidades de SAREB: nuevos tipos de documentos, nuevas integraciones, mayor volumen, mejores modelos. La base está construida para durar y para escalar.

El tiempo que antes dedicaban los operadores a clasificar y transcribir datos puede ahora dedicarse a tareas de mayor valor: revisar los casos complejos, mejorar la configuración del sistema, y tomar decisiones sobre los datos — en lugar de extraerlos.

---

## Notas para el presentador

- **Apoyo visual recomendado:** diagrama de flujo simplificado del pipeline (7 pasos con iconos), tabla de estados de confianza (OK/REVISIÓN/ERROR) y tabla de estado de la hoja de ruta.
- **Posibles preguntas del público y respuestas breves:**
  - *¿Qué pasa si la IA se equivoca?* → El sistema lo señala con una puntuación baja y lo marca para revisión humana. Nunca descarta silenciosamente un error.
  - *¿Es seguro enviar documentos con datos personales?* → Los documentos se procesan en los entornos de Azure de SAREB. Los secretos y credenciales están en Azure Key Vault. El cifrado en reposo está planificado en la próxima fase.
  - *¿Puede procesar documentos que no sean PDFs?* → Actualmente el sistema está optimizado para PDF. La arquitectura permite añadir soporte a otros formatos con trabajo adicional de configuración.
  - *¿Cuánto cuesta por documento?* → El coste es proporcional al uso de los servicios de IA de Azure. La deduplicación garantiza que no se pague dos veces por el mismo documento.
  - *¿Necesita conexión a internet constante?* → Sí, depende de los servicios de Azure. En un entorno de producción cloud esto es transparente; no hay instalación local de IA.
