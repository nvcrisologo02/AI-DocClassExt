# Comparativa de ejecuciones

- A: `C:\temp\MVP\DocumentIA.Batch\eval\runs\BASELINE-GPT4OMINI-DEV`
- B: `C:\temp\MVP\DocumentIA.Batch\eval\runs\20261007-202810-v1.0.0-pre`
- Documentos evaluados en comun (interseccion por rel_path): 465

## TDN1

- Accuracy A: 56.13 % (261/465)
- Accuracy B: 56.77 % (264/465)
- b01 (A acierta, B falla): 7
- b10 (A falla, B acierta): 10
- McNemar exacto, p-valor de dos colas: 0.6291
- Veredicto: **diferencia dentro del ruido**


## TDN2

- Accuracy A: 33.55 % (156/465)
- Accuracy B: 33.98 % (158/465)
- b01 (A acierta, B falla): 4
- b10 (A falla, B acierta): 6
- McNemar exacto, p-valor de dos colas: 0.7539
- Veredicto: **diferencia dentro del ruido**


## Veredicto global

Basado en TDN2 (nivel mas fino): **diferencia dentro del ruido** (p=0.7539, umbral p<0.05). TDN1: **diferencia dentro del ruido** (p=0.6291).
