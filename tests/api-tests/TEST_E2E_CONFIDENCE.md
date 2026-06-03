# Test E2E - Confianza Self-Reported GPT (AB#99727)

## Descripción

Test end-to-end para validar que el sistema de confianza dinámica self-reported por GPT funciona correctamente en entorno real.

## Propósito

Validar que:
1. El campo `Confianza` está presente en la respuesta de clasificación
2. Los valores de confianza **varían** entre documentos (no siempre 0.9 hardcoded)
3. Todos los valores están en el rango válido `[0.0, 1.0]`
4. La confianza self-reported se propaga correctamente a través del pipeline completo

## Pre-requisitos

1. **Azure Functions locales en ejecución:**
   ```powershell
   cd src\backend\DocumentIA.Functions
   func host start
   ```

2. **Azurite en ejecución:**
   ```powershell
   azurite --silent --location c:\azurite --debug c:\azurite\debug.log
   ```

3. **Directorio con documentos de prueba:**
   - Mínimo 5 documentos PDF diferentes
   - Recomendado: 10+ documentos para mejor análisis estadístico
   - Los documentos deben ser clasificables por GPT (tipologías SAREB)

## Uso

### Ejecución básica

```powershell
.\test-gpt-self-reported-confidence.ps1 -TestDocumentsPath "C:\temp\test-docs"
```

### Parámetros disponibles

```powershell
-TestDocumentsPath   # Ruta al directorio con PDFs de prueba (requerido)
-MinDocuments        # Número mínimo de documentos (default: 5)
-Endpoint            # Endpoint de la función (default: http://localhost:7071/api/IngestDocument)
```

### Ejemplo con parámetros personalizados

```powershell
.\test-gpt-self-reported-confidence.ps1 `
    -TestDocumentsPath "C:\DocumentosPruebaClasificacion" `
    -MinDocuments 10 `
    -Endpoint "http://localhost:7071/api/IngestDocument"
```

## Salida del Test

El script genera salida estructurada con:

### Durante la ejecución

```
================================================================
  Test E2E - GPT Self-Reported Confidence (AB#99727)
================================================================

[INFO] Documentos encontrados: 10
[INFO] Endpoint: http://localhost:7071/api/IngestDocument

----------------------------------------
Procesando: NotaSimple_1234.pdf
----------------------------------------
  [✓] Instance ID: abc123...
  [1/30] Estado: Running | Actual: ClasificarActivity
  [2/30] Estado: Running | Actual: NormalizarActivity
  ...
  [✓] Completado
    Tipologia: NS_1.1
    Modelo   : GPT4oMini
    Confianza: 0.875
    [✓] Validación: OK
```

### Resumen final

```
=================================================================
  RESUMEN DE VALIDACIÓN
=================================================================

Total procesados       : 10
Total válidos          : 10
Total inválidos        : 0

Análisis de confianza:
  Mínima    : 0.750
  Máxima    : 0.950
  Promedio  : 0.857
  Valores únicos: 8 de 10

  [✓] VALIDACIÓN VARIABILIDAD: Detectada variabilidad en confianzas (no siempre 0.9)

=================================================================
  ✓ TEST PASADO: Confianza self-reported funcionando correctamente
=================================================================
```

## Criterios de Éxito

El test **PASA** si se cumplen todas estas condiciones:

1. ✅ **Todos los documentos procesan correctamente** (`runtimeStatus = Completed`)
2. ✅ **Campo Confianza presente** en todas las respuestas
3. ✅ **Confianza en rango válido** `[0.0, 1.0]` para todos los casos
4. ✅ **Variabilidad detectada**: al menos 2 valores distintos de confianza
   - Esto valida que GPT está reportando confianza dinámica, no hardcoded 0.9

## Criterios de Fallo

El test **FALLA** si:

- ❌ Algún documento no completa procesamiento (`Failed`, `Terminated`, etc.)
- ❌ Campo `Confianza` es `null` o no existe
- ❌ Algún valor de confianza está fuera de rango `[0.0, 1.0]`
- ❌ **Todos los valores de confianza son iguales** (ej: todos 0.9)
  - Esto indica que GPT no está reportando confianza dinámica
  - Posible causa: prompts no actualizados o parser fallando

## Interpretación de Resultados

### Escenario ideal (✓ Test pasado)

```
Análisis de confianza:
  Mínima    : 0.650
  Máxima    : 0.975
  Promedio  : 0.845
  Valores únicos: 9 de 10

[✓] VALIDACIÓN VARIABILIDAD: Detectada variabilidad
```

**Interpretación**: GPT está reportando confianza dinámica basada en la certeza real de cada clasificación.

### Escenario problemático (✗ Test fallido)

```
Análisis de confianza:
  Mínima    : 0.900
  Máxima    : 0.900
  Promedio  : 0.900
  Valores únicos: 1 de 10

[✗] VALIDACIÓN VARIABILIDAD: Todos los valores son iguales (0.900)
⚠ POSIBLE PROBLEMA: GPT no está reportando confianza dinámica
```

**Interpretación**: Indica uno de estos problemas:
- Prompts no solicitan campo `confianza`
- Parser no extrae el campo correctamente
- Fallback a 0.9 se está activando siempre
- GPT no responde con el campo `confianza`

## Troubleshooting

### Problema: Todos los valores son 0.9

**Diagnóstico:**

1. Verificar logs de Function App durante clasificación:
   ```
   Clasificación GPT Fase X completada. 
   ConfianzaSelfReported=<no reportada>
   ConfianzaFinal=0.900
   ```

   Si `ConfianzaSelfReported=<no reportada>`, GPT no está enviando el campo.

2. Verificar que el deployment de `GptClasificarDataProvider.cs` incluye los prompts actualizados:
   - `Phase1ResponseFormatInstruction` debe incluir `"confianza": 0.0-1.0`
   - `Phase2ResponseFormatInstruction` debe incluir `"confianza": 0.0-1.0`

3. Verificar respuesta raw de GPT en logs (si debug habilitado):
   - Buscar campo `"confianza"` en JSON response
   - Si no existe, los prompts no se aplicaron correctamente

**Solución:**

```powershell
# Rebuild y redeploy
cd src\backend\DocumentIA.Functions
dotnet clean
dotnet build
func host start
```

### Problema: Confianza fuera de rango

**Diagnóstico:**

Si aparecen valores como `1.5`, `-0.1`, etc., el clamping de `Math.Clamp` no se está aplicando.

**Solución:**

Verificar que `GptHierarchicalClassificationParser.cs` tiene:

```csharp
if (confianzaElement.ValueKind == JsonValueKind.Number)
{
    confianza = Math.Clamp(confianzaElement.GetDouble(), 0.0, 1.0);
}
```

### Problema: Campo Confianza es NULL

**Diagnóstico:**

Verificar en logs si hay warnings de parsing:
```
[Warning] No se pudo parsear campo 'confianza' del JSON de GPT
```

**Solución:**

- Verificar formato JSON en respuesta de GPT
- Confirmar que el campo se llama exactamente `"confianza"` (case-sensitive en algunos parsers)

## Integración con CI/CD

Este test puede integrarse en pipeline de Azure DevOps:

```yaml
- task: PowerShell@2
  displayName: 'Test E2E - GPT Self-Reported Confidence'
  inputs:
    filePath: 'tests/api-tests/test-gpt-self-reported-confidence.ps1'
    arguments: '-TestDocumentsPath "$(Pipeline.Workspace)/test-documents"'
    failOnStderr: true
  condition: succeededOrFailed()
```

## Relación con Work Items

- **Feature**: AB#99725 - [GPT] Implementar confianza dinámica self-reported
- **Task**: AB#99727 - Tests E2E con respuestas reales de GPT
- **Commit**: Ver `git log --grep="AB#99727"`

## Documentos de Referencia

- [02_ANALISIS_FUNCIONAL.md](../../docs/02_ANALISIS_FUNCIONAL.md) - Flujo de clasificación
- [03_DISENO_TECNICO_DETALLADO.md](../../docs/03_DISENO_TECNICO_DETALLADO.md) - Arquitectura del sistema
- [gpt-prompts-two-phase-classification.md](/memories/repo/gpt-prompts-two-phase-classification.md) - Detalles de prompts GPT

## Fecha de Creación

2026-06-03 - AB#99727
