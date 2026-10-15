# Comparativa de ejecuciones

- A: `C:\temp\MVP\DocumentIA.Batch\eval\runs\BASELINE-GPT4OMINI-DEV`
- B: `C:\temp\MVP\DocumentIA.Batch\eval\runs\20261015-080441-v1.1.0-pre`
- Documentos evaluados en comun (interseccion por rel_path): 467

## TDN1

- Accuracy A: 55.89 % (261/467)
- Accuracy B: 57.39 % (268/467)
- b01 (A acierta, B falla): 2
- b10 (A falla, B acierta): 9
- McNemar exacto, p-valor de dos colas: 0.0654
- Veredicto: **diferencia dentro del ruido**


## TDN2

- Accuracy A: 33.40 % (156/467)
- Accuracy B: 33.83 % (158/467)
- b01 (A acierta, B falla): 4
- b10 (A falla, B acierta): 6
- McNemar exacto, p-valor de dos colas: 0.7539
- Veredicto: **diferencia dentro del ruido**


## Veredicto global

Basado en TDN2 (nivel mas fino): **diferencia dentro del ruido** (p=0.7539, umbral p<0.05). TDN1: **diferencia dentro del ruido** (p=0.0654).
