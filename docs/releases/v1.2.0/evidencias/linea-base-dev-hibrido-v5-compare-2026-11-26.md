# Comparativa de ejecuciones

- A: `C:\temp\MVP\DocumentIA.Batch\eval\runs\20261119-155707-gpt56-luna-golden`
- B: `C:\temp\MVP\DocumentIA.Batch\eval\runs\20261126-hibrido-v5-gpt56-luna-golden`
- Documentos evaluados en comun (interseccion por rel_path): 467

## TDN1

- Accuracy A: 65.95 % (308/467)
- Accuracy B: 64.67 % (302/467)
- b01 (A acierta, B falla): 25
- b10 (A falla, B acierta): 19
- McNemar exacto, p-valor de dos colas: 0.4514
- Veredicto: **diferencia dentro del ruido**


## TDN2

- Accuracy A: 41.33 % (193/467)
- Accuracy B: 39.61 % (185/467)
- b01 (A acierta, B falla): 29
- b10 (A falla, B acierta): 21
- McNemar exacto, p-valor de dos colas: 0.3222
- Veredicto: **diferencia dentro del ruido**


## Veredicto global

Basado en TDN2 (nivel mas fino): **diferencia dentro del ruido** (p=0.3222, umbral p<0.05). TDN1: **diferencia dentro del ruido** (p=0.4514).
