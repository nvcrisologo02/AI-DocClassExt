# 21. Plan de Entrenamiento y Optimización de Prompts para Clasificación (TDN1/TDN2)

Fecha: 2026-06-09  
Ámbito: Documento IA Clasificación MVP  
Objetivo principal: Mejorar resultados de clasificación priorizando calidad de tipologías, reducción de ambigüedades y robustez de prompts antes de ejecutar fine-tuning.

---

## 1. Objetivos y Resultados Esperados

Objetivos de negocio y calidad:

- Subir la precisión Top-1 en TDN1.
- Subir la precisión Top-1 en TDN2 (condicionada a TDN1 correcto).
- Reducir la tasa de Desconocido y clasificación parcial por ambigüedad.
- Reducir el fallback innecesario a GPT cuando reglas/DI ya son suficientes.
- Establecer un criterio claro de cuándo sí/no realizar fine-tuning.

Resultados esperados al finalizar este plan:

- Catálogo TDN1/TDN2 con descripciones y prioridades depuradas.
- Prompting de fase 1 y fase 2 estandarizado y auditable.
- Matriz de confusión priorizada con acciones por cada confusión crítica.
- Dataset de casos difíciles listo para entrenamiento/evaluación.
- Decisión formal Go/No-Go de fine-tuning con evidencia.

---

## 2. Principios de Ejecución

- Primero calidad de etiquetas y catálogo; después prompts; finalmente modelo.
- Evitar fine-tuning prematuro con datos ruidosos o ambiguos.
- Cada cambio debe medirse sobre baseline reproducible.
- Proteger clases minoritarias: no optimizar solo por volumen.

---

## 3. Cronograma Operativo (10 días)

### D1: Baseline y segmentación de errores

Entregables:

- Baseline de métricas actual:
  - Top-1 TDN1
  - Top-1 TDN2 condicional
  - Tasa Desconocido
  - Parse error fase 1/fase 2
  - Fallback GPT rate
- Segmentación por familia TDN1 y por tipología más frecuente.

### D2: Matriz de confusión priorizada

Entregables:

- Top 10 confusiones por impacto (frecuencia x severidad).
- Hipótesis de causa por confusión:
  - ambigüedad de catálogo,
  - prompt insuficiente,
  - falta de ejemplos,
  - solape semántico.

### D3: Ficha de tipología (versión 1)

Entregables:

- Ficha completada para las 20 tipologías de mayor volumen.
- Revisión de prioridad y descripciones GPT por tipología.

### D4: Reglas de desambiguación y negativos

Entregables:

- Reglas de desempate por pareja conflictiva (A vs B).
- Lista de señales negativas (qué NO es cada tipología).
- Catálogo de confusores por tipología.

### D5: Refactor de prompts fase 1 (TDN1)

Entregables:

- Prompt fase 1 con:
  - criterios de inclusión/exclusión,
  - salida JSON estricta,
  - obligación de justificar selección.
- Checklist de validación de prompt fase 1.

### D6: Refactor de prompts fase 2 (TDN2 por familia)

Entregables:

- Prompt fase 2 por familia con catálogo restringido.
- Instrucciones explícitas para desempate entre subtipos cercanos.
- Manejo explícito de caso no resoluble (propuesta controlada).

### D7: Evaluación offline y ajuste fino de prompts

Entregables:

- Comparativa baseline vs prompts v2.
- Ajustes en descripciones y prioridad de tipologías conflictivas.

### D8: Curación dataset hard-cases

Entregables:

- Dataset curado (casos ambiguos, fronteras, clases raras).
- Etiquetado doble en muestra crítica (control de calidad).

### D9: Simulación de entrenamiento y criterio Go/No-Go

Entregables:

- Simulación de ganancia esperada con/ sin fine-tuning.
- Evaluación de riesgos:
  - overfitting,
  - degradación en clases minoritarias,
  - coste operacional.

### D10: Decisión ejecutiva y plan de despliegue

Entregables:

- Decisión formal:
  - Go fine-tuning, o
  - No-Go (continuar iteración de prompts/catálogo).
- Plan de despliegue controlado (shadow/A-B) y rollback.

---

## 4. Plantilla de Ficha de Tipología

Usar esta plantilla para cada tipología prioritaria:

- Código tipología:
- Nombre tipología:
- Familia TDN1:
- Subtipo TDN2:
- Prioridad actual:
- Prioridad propuesta:
- Definición positiva (qué sí es):
- Definición negativa (qué no es):
- Confusores frecuentes (3-5):
- Regla de desempate principal:
- Frases/señales fuertes:
- Frases/señales de exclusión:
- Ejemplos límite (difíciles):
- Riesgo de ambigüedad (alto/medio/bajo):
- Acción recomendada:

---

## 5. Checklist de Revisión (Prioridad, Descripción, Ambigüedad)

Checklist mínimo por tipología:

- La descripción GPT diferencia claramente esta tipología de sus 2-3 vecinas más parecidas.
- Existe al menos una regla negativa explícita.
- Existe al menos una regla de desempate explícita.
- La prioridad refleja criticidad de negocio y frecuencia real.
- El catálogo no contiene descripciones redundantes o casi idénticas.
- Las señales fuertes no son genéricas (evitar términos excesivamente comunes).
- La tipología no depende únicamente de un único término ambiguo.

Checklist mínimo por familia TDN1:

- El catálogo TDN2 está completo y sin duplicidades semánticas.
- Cada TDN2 tiene justificación distintiva.
- Se han definido confusores intrafamilia.

---

## 6. Criterio Go/No-Go para Fine-Tuning GPT

### Requisitos mínimos para considerar Go

- Calidad de etiqueta validada en muestra crítica.
- Ambigüedades principales mitigadas en catálogo y prompts.
- Mejoras por prompting estabilizadas (sin regresión grave en minoritarias).
- Dataset hard-cases suficiente y balanceado para objetivo de entrenamiento.
- Evidencia de error residual sistemático que prompts no resuelven.

### Condiciones de No-Go

- Etiquetas inconsistentes o ambiguas en clases clave.
- Ganancia de prompts aún inestable (alta varianza entre corridas).
- Falta de volumen/calidad por clase para entrenar sin sobreajuste.
- Riesgo alto de degradación en clases minoritarias.

---

## 7. Métricas de Seguimiento

Métricas core:

- Accuracy Top-1 TDN1.
- Accuracy Top-1 TDN2 condicional a TDN1 correcto.
- Tasa Desconocido.
- Tasa clasificación parcial.
- Parse error rate fase 1 y fase 2.
- Fallback GPT rate.

Métricas de robustez:

- Ambigüedad top1-top2 media por familia.
- Precisión por clase minoritaria.
- Delta de regresión por tipología crítica.

---

## 8. Riesgos y Mitigaciones

Riesgo: Sobreoptimización para clases frecuentes.  
Mitigación: Reportar siempre métricas por clase y macro-promedio.

Riesgo: Fine-tuning con datos ruidosos.  
Mitigación: Doble validación humana para hard-cases antes de entrenamiento.

Riesgo: Catálogo semánticamente solapado.  
Mitigación: Forzar reglas de exclusión y desempate por pareja conflictiva.

Riesgo: Mejoras aparentes por ajuste de umbral, no por calidad real.  
Mitigación: Evaluar curvas de precisión/recall y no solo métricas puntuales.

---

## 9. Definición de Hecho (DoD)

Se considera completado cuando:

- Existe baseline reproducible y comparativa posterior.
- Top confusiones tienen acciones implementadas o planificadas.
- Fichas de tipología prioritaria están completas.
- Prompts fase 1 y fase 2 están normalizados y validados.
- Se emite decisión Go/No-Go de fine-tuning con evidencia cuantitativa.
- Se documenta plan de despliegue seguro con rollback.

---

## 10. Próximos pasos inmediatos

- Ejecutar D1 y D2 en la próxima iteración.
- Completar fichas de tipologías críticas (D3-D4).
- Entrar en refactor de prompts fase 1/fase 2 (D5-D6).
- Re-evaluar con benchmark antes de decidir entrenamiento (D9-D10).
