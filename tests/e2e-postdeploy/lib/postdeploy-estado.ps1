Set-StrictMode -Version Latest

# Invoke-DocumentIAE2ECase (tests/api-tests/documentia-e2e-common.ps1) solo
# devuelve PASS, FAIL o SKIP. Bajo ese FAIL hay dos cosas distintas: una
# asercion incumplida (invariante violada de verdad) y un fallo de
# infraestructura (timeout con la orquestacion aun en Running/Pending, o
# cualquier excepcion HTTP/red atrapada por su catch generico). El runner de
# validacion necesita distinguirlas: FAIL es invariante violada, ERROR es que
# el caso no se pudo ejecutar.
#
# No se toca documentia-e2e-common.ps1 (es compartido con
# run-e2e-postdeploy.ps1 y anadir un estado nuevo le cambiaria los totales y
# el exit code). La clasificacion se hace aqui, en el runner, leyendo el
# prefijo del Reason que esa libreria ya produce:
#   - "runtimeStatus=Running tras N intentos" / "...Pending..."  -> ERROR
#     (la orquestacion no llego a terminar; no sabemos que habria pasado)
#   - "runtimeStatus=Failed tras N intentos" (o cualquier otro estado
#     terminal distinto de Running/Pending) -> FAIL (la orquestacion corrio y
#     produjo un resultado, aunque ese resultado sea un fallo)
#   - "Excepcion: ..." (catch generico: red, HTTP, parseo, etc.) -> ERROR
#   - cualquier otro Reason (asercion de contenido incumplida) -> FAIL
function Get-EstadoDePasada {
    param([pscustomobject]$Resultado)

    if ($Resultado.Status -eq "PASS") { return "PASS" }
    if ($Resultado.Status -eq "SKIP") { return "ERROR" }

    $razon = [string]$Resultado.Reason
    if ($razon -like "Excepcion:*") { return "ERROR" }
    if ($razon -match '^runtimeStatus=(Running|Pending) tras') { return "ERROR" }
    return "FAIL"
}
