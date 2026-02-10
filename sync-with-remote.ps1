# sync-with-remote.ps1
Write-Host "=== Sincronizando con Repositorio Remoto ===" -ForegroundColor Cyan

cd C:\temp\MVP\documento-ia-clasificacion-mvp

# 1. VERIFICAR RAMA ACTUAL
Write-Host "`n[1/5] Verificando rama actual..." -ForegroundColor Yellow
$currentBranch = git branch --show-current
Write-Host "Rama actual: $currentBranch" -ForegroundColor Cyan

# 2. VERIFICAR SI HAY REMOTO CONFIGURADO
Write-Host "`n[2/5] Verificando remoto..." -ForegroundColor Yellow
$remoteUrl = git remote get-url origin 2>$null

if ($remoteUrl) {
    Write-Host "Remoto configurado: $remoteUrl" -ForegroundColor Green
} else {
    Write-Host "ERROR: No hay remoto configurado" -ForegroundColor Red
    Write-Host "Necesitas configurar el remoto primero:" -ForegroundColor Yellow
    Write-Host "  git remote add origin <URL-DEL-REPOSITORIO>" -ForegroundColor White
    exit 1
}

# 3. FETCH PARA VER CAMBIOS REMOTOS
Write-Host "`n[3/5] Obteniendo cambios del remoto..." -ForegroundColor Yellow
git fetch origin

# Verificar si la rama existe en el remoto
$remoteBranchExists = git ls-remote --heads origin $currentBranch 2>$null

if ($remoteBranchExists) {
    Write-Host "La rama '$currentBranch' existe en el remoto" -ForegroundColor Green
    
    # Ver diferencias entre local y remoto
    $localCommits = git rev-list --count origin/$currentBranch..$currentBranch 2>$null
    $remoteCommits = git rev-list --count $currentBranch..origin/$currentBranch 2>$null
    
    Write-Host "Commits locales por subir: $localCommits" -ForegroundColor Cyan
    Write-Host "Commits remotos por bajar: $remoteCommits" -ForegroundColor Cyan
    
    if ($remoteCommits -gt 0) {
        Write-Host "`nWARNING: Hay commits en el remoto que no tienes localmente" -ForegroundColor Yellow
        Write-Host "Necesitas hacer pull primero para integrar cambios" -ForegroundColor Yellow
        Write-Host "`nOpciones:" -ForegroundColor Cyan
        Write-Host "  1. Pull con rebase (recomendado): git pull --rebase origin $currentBranch" -ForegroundColor White
        Write-Host "  2. Pull con merge: git pull origin $currentBranch" -ForegroundColor White
        Write-Host "  3. Ver diferencias primero: git log $currentBranch..origin/$currentBranch" -ForegroundColor White
        
        $opcion = Read-Host "`nSelecciona opcion (1/2/3/Cancelar)"
        
        switch ($opcion) {
            "1" {
                Write-Host "`nHaciendo pull con rebase..." -ForegroundColor Yellow
                git pull --rebase origin $currentBranch
                
                if ($LASTEXITCODE -eq 0) {
                    Write-Host "OK: Rebase completado" -ForegroundColor Green
                } else {
                    Write-Host "ERROR: Conflictos durante rebase" -ForegroundColor Red
                    Write-Host "Resuelve los conflictos y ejecuta: git rebase --continue" -ForegroundColor Yellow
                    exit 1
                }
            }
            "2" {
                Write-Host "`nHaciendo pull con merge..." -ForegroundColor Yellow
                git pull origin $currentBranch
                
                if ($LASTEXITCODE -eq 0) {
                    Write-Host "OK: Merge completado" -ForegroundColor Green
                } else {
                    Write-Host "ERROR: Conflictos durante merge" -ForegroundColor Red
                    Write-Host "Resuelve los conflictos y ejecuta: git commit" -ForegroundColor Yellow
                    exit 1
                }
            }
            "3" {
                Write-Host "`nDiferencias con el remoto:" -ForegroundColor Cyan
                git log --oneline --graph $currentBranch..origin/$currentBranch
                Write-Host "`nEjecuta el script de nuevo despues de revisar" -ForegroundColor Yellow
                exit 0
            }
            default {
                Write-Host "Cancelado" -ForegroundColor Red
                exit 0
            }
        }
    }
} else {
    Write-Host "La rama '$currentBranch' NO existe en el remoto (primera vez)" -ForegroundColor Yellow
    Write-Host "Se creara en el remoto durante el push" -ForegroundColor Cyan
}

# 4. VERIFICAR ESTADO ANTES DEL PUSH
Write-Host "`n[4/5] Verificando estado local..." -ForegroundColor Yellow

$uncommittedChanges = git status --short
if ($uncommittedChanges) {
    Write-Host "WARNING: Hay cambios sin commitear:" -ForegroundColor Yellow
    git status --short
    Write-Host "`nHaz commit de estos cambios antes de push? (S/N)" -ForegroundColor Yellow
    $commitNow = Read-Host
    
    if ($commitNow -eq "S" -or $commitNow -eq "s") {
        Write-Host "Ejecuta primero: .\commit-motor-validacion.ps1" -ForegroundColor Cyan
        exit 0
    }
}

# 5. PUSH
Write-Host "`n[5/5] Haciendo push..." -ForegroundColor Yellow

Write-Host "`nResumen del push:" -ForegroundColor Cyan
Write-Host "  Rama local: $currentBranch" -ForegroundColor White
Write-Host "  Remoto: origin" -ForegroundColor White
Write-Host "  URL: $remoteUrl" -ForegroundColor White

# Verificar si tiene upstream configurado
$upstream = git rev-parse --abbrev-ref --symbolic-full-name "@{u}" 2>$null

if (-not $upstream) {
    Write-Host "`nConfigurar '$currentBranch' para hacer tracking de 'origin/$currentBranch'" -ForegroundColor Yellow
    git push --set-upstream origin $currentBranch
} else {
    git push
}

if ($LASTEXITCODE -eq 0) {
    Write-Host "`n╔══════════════════════════════════════════════════════╗" -ForegroundColor Green
    Write-Host "║              PUSH EXITOSO                            ║" -ForegroundColor Green
    Write-Host "╚══════════════════════════════════════════════════════╝" -ForegroundColor Green
    
    Write-Host "`nCodigo subido correctamente a:" -ForegroundColor Cyan
    Write-Host "  $remoteUrl" -ForegroundColor White
    Write-Host "  Rama: $currentBranch" -ForegroundColor White
    
    # Mostrar URL del PR si es GitHub/GitLab/Azure DevOps
    if ($remoteUrl -match "github.com") {
        $repoPath = $remoteUrl -replace '.*github.com[:/](.+?)(\.git)?$','$1'
        Write-Host "`nCrear Pull Request:" -ForegroundColor Yellow
        Write-Host "  https://github.com/$repoPath/compare/$currentBranch" -ForegroundColor Cyan
    } elseif ($remoteUrl -match "dev.azure.com") {
        Write-Host "`nCrear Pull Request en Azure DevOps:" -ForegroundColor Yellow
        Write-Host "  $remoteUrl" -ForegroundColor Cyan
    } elseif ($remoteUrl -match "gitlab.com") {
        $repoPath = $remoteUrl -replace '.*gitlab.com[:/](.+?)(\.git)?$','$1'
        Write-Host "`nCrear Merge Request:" -ForegroundColor Yellow
        Write-Host "  https://gitlab.com/$repoPath/-/merge_requests/new?merge_request[source_branch]=$currentBranch" -ForegroundColor Cyan
    }
    
} else {
    Write-Host "`n╔══════════════════════════════════════════════════════╗" -ForegroundColor Red
    Write-Host "║              ERROR EN PUSH                           ║" -ForegroundColor Red
    Write-Host "╚══════════════════════════════════════════════════════╝" -ForegroundColor Red
    
    Write-Host "`nPosibles causas:" -ForegroundColor Yellow
    Write-Host "  - No tienes permisos en el repositorio remoto" -ForegroundColor White
    Write-Host "  - La rama esta protegida" -ForegroundColor White
    Write-Host "  - Problemas de autenticacion" -ForegroundColor White
    
    Write-Host "`nVerifica tu autenticacion:" -ForegroundColor Cyan
    Write-Host "  git config --global user.name" -ForegroundColor White
    Write-Host "  git config --global user.email" -ForegroundColor White
    
    exit 1
}
