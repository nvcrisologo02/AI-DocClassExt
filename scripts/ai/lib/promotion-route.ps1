<#
  Sentido de la promocion de artefactos de IA (ADR-001): los analyzers de CU,
  los clasificadores de DI y sus datasets se entrenan en DEV y se promocionan
  DEV -> PRE -> PRO, un salto cada vez. Compartida por copy-cu-analyzers.ps1,
  copy-di-artifacts.ps1 y copy-labeling-dataset.ps1.

  La copia desde PRO (prod -> dev|pre) fue la carga inicial; solo se admite
  para recuperar ese estado y exige -SourceEnvironment prod -FromProd.
#>

# Salto anterior de cada destino: es el origen por defecto.
$script:PromotionPreviousHop = @{ pre = 'dev'; prod = 'pre' }

function Resolve-PromotionRoute {
    param(
        [Parameter(Mandatory)][ValidateSet('dev', 'pre', 'prod')][string]$Environment,
        # Sin ValidateSet: los scripts reenvian su -SourceEnvironment tal cual y
        # llega vacio cuando no se indico (el origen se deduce).
        [string]$SourceEnvironment,
        [switch]$FromProd
    )
    if ($SourceEnvironment -and $SourceEnvironment -notin 'dev', 'pre', 'prod') {
        throw "entorno origen desconocido '$SourceEnvironment' (dev, pre o prod)"
    }
    if (-not $SourceEnvironment) {
        if (-not $script:PromotionPreviousHop.ContainsKey($Environment)) {
            throw "dev no tiene salto anterior: la promocion va DEV -> PRE -> PRO. Para recuperar dev desde PRO usa -SourceEnvironment prod -FromProd"
        }
        $SourceEnvironment = $script:PromotionPreviousHop[$Environment]
    }
    $hop = "$SourceEnvironment -> $Environment"
    if ($FromProd -and $SourceEnvironment -ne 'prod') {
        throw "-FromProd solo vale con -SourceEnvironment prod (salto pedido: $hop)"
    }
    if ($SourceEnvironment -eq 'prod' -and $Environment -in 'dev', 'pre') {
        if (-not $FromProd) {
            throw "salto $hop : copiar desde PRO solo recupera el estado inicial y exige -FromProd"
        }
    } elseif ($script:PromotionPreviousHop[$Environment] -ne $SourceEnvironment) {
        throw "salto $hop no permitido: la promocion va DEV -> PRE -> PRO, un salto cada vez"
    }
    return [pscustomobject]@{ Source = $SourceEnvironment; Target = $Environment }
}

function Get-LabelingStorageAccount {
    # Cuenta de storage con los datasets de etiquetado de CU de cada entorno
    # (la de PRO, de la solicitud del SC "AI DocClassExt Promocion IA").
    param([Parameter(Mandatory)][ValidateSet('dev', 'pre', 'prod')][string]$Environment)
    $accounts = @{ dev = 'srbstgdevdocai'; pre = 'srbstgpredocai'; prod = 'srbstgprodocai' }
    return $accounts[$Environment]
}
