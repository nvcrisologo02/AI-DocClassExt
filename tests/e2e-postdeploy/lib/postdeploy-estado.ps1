Set-StrictMode -Version Latest

# Invoke-DocumentIAE2ECase (tests/api-tests/documentia-e2e-common.ps1) solo
# devuelve PASS, FAIL o SKIP. Bajo ese FAIL hay dos cosas distintas: una
# asercion incumplida (invariante violada de verdad) y un fallo de
# infraestructura (timeout con la orquestacion aun en Running/Pending, la
# orquestacion terminando en un estado que no es Completed, o cualquier
# excepcion HTTP/red atrapada por su catch generico). El runner de validacion
# necesita distinguirlas: FAIL es invariante violada, ERROR es que el caso no
# se pudo ejecutar.
#
# No se toca documentia-e2e-common.ps1 (es compartido con
# run-e2e-postdeploy.ps1 y anadir un estado nuevo le cambiaria los totales y
# el exit code). La clasificacion se hace aqui, en el runner, leyendo el
# prefijo del Reason que esa libreria ya produce:
#   - "runtimeStatus=X tras N intentos" (cualquier X: Running, Pending,
#     Failed, o cualquier otro estado terminal) -> ERROR. La spec define
#     ERROR como "la orquestacion no llego a Completed", sin distinguir por
#     que estado terminal fue: una orquestacion Failed no evaluo ninguna
#     invariante igual que una que quedo en Running.
#   - "runtimeStatus 'X' no observado en polling" (verificacion de historial
#     de polling, empieza por "runtimeStatus '", no por "runtimeStatus=") ->
#     FAIL: la orquestacion si completo; esto es una asercion de contenido
#     sobre su historial, no un fallo de infraestructura.
#   - "Excepcion: ..." (catch generico: red, HTTP, parseo, etc.) -> ERROR
#   - cualquier otro Reason (asercion de contenido incumplida) -> FAIL
function Get-EstadoDePasada {
    param([pscustomobject]$Resultado)

    if ($Resultado.Status -eq "PASS") { return "PASS" }
    if ($Resultado.Status -eq "SKIP") { return "ERROR" }

    $razon = [string]$Resultado.Reason
    if ($razon -like "Excepcion:*") { return "ERROR" }
    if ($razon -match '^runtimeStatus=') { return "ERROR" }
    return "FAIL"
}
