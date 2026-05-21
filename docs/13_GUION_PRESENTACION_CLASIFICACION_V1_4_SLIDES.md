# Guion Presentacion Ejecutiva - Clasificacion Documental v1 (4 slides)

Objetivo: explicar a direccion que vamos a hacer, que impacto tendra en DocumentIA y como se controla el riesgo de ejecucion.
Duracion sugerida: 7-10 minutos

## Slide 1 - Decision estrategica y valor para DocumentIA

Titulo sugerido:
Clasificacion Documental v1: de capacidad tecnica a ventaja operativa

Mensaje para direccion:
- Vamos a industrializar la clasificacion documental en el flujo actual de DocumentIA, sin rehacer la plataforma.
- El objetivo no es solo clasificar mejor, sino reducir tiempos de ciclo, aumentar trazabilidad y escalar con control de coste.

Que vamos a hacer:
- Activar clasificacion jerarquica TDN1 -> TDN2 -> Matricula en modo classificationOnly
- Gobernar catalogo en Admin/BBDD (Draft -> Published)
- Formalizar estados de negocio para operacion: UNKNOWN, OUT_OF_SCOPE, NEEDS_REVIEW

Impacto esperado en DocumentIA:
- Menor dependencia de criterio manual
- Mejor previsibilidad de operacion y auditoria
- Base comun para crecer en tipologias sin friccion

## Slide 2 - Como se implementa sin interrumpir servicio

Titulo sugerido:
Implementacion por extension del pipeline existente

Mensaje para direccion:
- Se minimiza riesgo porque no se crea un sistema paralelo; se extiende la orquestacion actual.
- Se aplica un enfoque por capas para proteger calidad desde el primer dia.

Que vamos a hacer tecnicamente:
- Capa 1: reglas/OCR + DI Classification
- Capa 2 (rescate): GPT-4o mini solo en casos ambiguos o de baja confianza
- Persistencia completa de la decision y evidencia para auditoria

Reglas de calidad aprobadas:
- Umbral DI: 0,85
- Delta top1-top2: 0,15
- Fallback: timeout 8s + 1 retry + 500ms backoff
- Paginas: 3 por defecto, escalado a 5

Impacto esperado en DocumentIA:
- Mejora de precision en casos complejos sin disparar coste de forma indiscriminada
- Explicabilidad de resultado para negocio, operaciones y auditoria

## Slide 3 - Impacto de negocio y control financiero

Titulo sugerido:
KPIs de direccion: calidad, coste y tiempo bajo control

Mensaje para direccion:
- El plan nace con guardrails economicos y operativos, no solo con objetivos tecnicos.

Compromisos operativos:
- Coste maximo: 0,10 EUR/documento
- Fallback rate objetivo: <= 25%
- Latencia P95 objetivo: <= 90s
- NEEDS_REVIEW inicial: 20-35%, objetivo de estabilizacion: 12-25%

Impacto esperado en DocumentIA:
- Control del coste unitario desde el arranque
- Mayor capacidad de planificacion de SLA
- Reduccion progresiva de volumen manual revisado

## Slide 4 - Plan de ejecucion y decision de seguimiento

Titulo sugerido:
Roadmap de implantacion y gobierno de avance

Mensaje para direccion:
- El plan esta estructurado en ADO y permite seguimiento semanal con criterios objetivos.
- Se avanza por hitos con camino critico explicitado.

Que vamos a hacer:
- Ejecutar Epica 99361 (5 features, 32 historias/tasks)
- Camino critico: F1 Catalogo (bloqueante) -> F2 Motor -> F3 Fallback -> F4 Observabilidad/Persistencia -> F5 Piloto
- Cerrar 5 bloqueos previos al arranque de Dia 1

Como mediremos exito para decision ejecutiva:
- Piloto por tipologia: TDN1 >= 95%, TDN2 >= 85%, precision >= 95%
- Gate final Go/No-Go con calidad + coste + SLA

Solicitud de decision a direccion:
- Mantener GO condicionado aprobado
- Validar prioridad de cierre de bloqueos hoy
- Confirmar cadencia de seguimiento ejecutivo semanal

## Cierre (20-30 segundos)

Esta iniciativa convierte la clasificacion en una capacidad de negocio gobernada: medible, auditable y escalable. El valor para DocumentIA es operar con mayor fiabilidad, menor friccion y control real de coste por documento.
