# Migraciones de base de datos — DocumentIA

Guía de desarrollo para crear y probar migraciones de EF Core en local. La aplicación en DEV,
PRE y PRO no se hace desde local ni desde los pipelines de aplicación: la hace el pipeline
Migrations-BD (`azure-pipelines-migrations.yml`) siguiendo la Fase 2.1 y la Fase 4.2 de
[RELEASE_MANAGEMENT.md](RELEASE_MANAGEMENT.md).

## 1. Contexto

- Entity Framework Core 8 sobre SQL Server. Herramienta `dotnet-ef` 8.0.x.
- Migraciones en `src/backend/DocumentIA.Data/Migrations/`; proyecto y proyecto de arranque:
  `src/backend/DocumentIA.Data/DocumentIA.Data.csproj` (es lo que usa el pipeline).
- Versión de esquema por entorno: tabla `__EFMigrationsHistory`. El pipeline Migrations-BD
  publica en cada run el conjunto aplicado y el del repo.
- Prerrequisito one-time por entorno: `scripts/database/grant-pipeline-sql-user.sql` (alta del
  SPN de la service connection en la BD).

## 2. Crear una migración

```powershell
dotnet ef migrations add "AddNewColumn_YourFeature" `
  --project src/backend/DocumentIA.Data/DocumentIA.Data.csproj `
  --startup-project src/backend/DocumentIA.Data/DocumentIA.Data.csproj `
  --context DocumentIADbContext
```

Genera `Migrations/YYYYMMDDHHMMSS_AddNewColumn_YourFeature.cs` (métodos `Up` y `Down`) y
actualiza `Migrations/DocumentIADbContextModelSnapshot.cs`.

```csharp
public partial class AddNewColumn_YourFeature : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "NewColumn",
            table: "Documentos",
            type: "nvarchar(max)",
            nullable: true);
            
        // Añadir índice si es necesario
        migrationBuilder.CreateIndex(
            name: "IX_Documentos_NewColumn",
            table: "Documentos",
            column: "NewColumn");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex("IX_Documentos_NewColumn", "Documentos");
        migrationBuilder.DropColumn("NewColumn", "Documentos");
    }
}
```

Buenas prácticas:

1. Un cambio lógico por migración.
2. `Down()` siempre implementado.
3. Índices para las columnas que se filtran con frecuencia; en tablas grandes, crear el índice
   en un script aparte y medirlo primero en una copia (ver el índice del Monitor en
   `scripts/database/indice-monitor-costes-pro.sql` como referencia).
4. `NOT NULL` solo con valor por defecto para las filas existentes.
5. Compatibilidad hacia atrás: el código desplegado antes de la migración debe seguir
   funcionando con el esquema nuevo (regla "esquema antes que código" del runbook). Añadir
   columnas y tablas es seguro; renombrar o borrar exige dos releases.
6. Probar en local antes de hacer commit (sección 3).

## 3. Probar una migración en local

1. BD local limpia: borrar la BD de desarrollo y ejecutar
   `dotnet ef database update --project src/backend/DocumentIA.Data/DocumentIA.Data.csproj --startup-project src/backend/DocumentIA.Data/DocumentIA.Data.csproj`.
2. Comprobar la columna o tabla nueva con una consulta directa.
3. Ejecutar los tests que tocan la entidad: `dotnet test src/backend/DocumentIA.Tests.Unit --filter "FullyQualifiedName~<NombreTest>"`.
4. Probar la vuelta atrás: `dotnet ef migrations list` y `dotnet ef database update <migración-anterior>` con los mismos `--project` y `--startup-project`; comprobar que la columna desaparece.
5. Generar el script idempotente que usará el pipeline y revisarlo:
   `dotnet ef migrations script --idempotent --context DocumentIADbContext --project src/backend/DocumentIA.Data/DocumentIA.Data.csproj --startup-project src/backend/DocumentIA.Data/DocumentIA.Data.csproj -o artifacts/migrations.sql`.

## 4. Aplicación en entornos y vuelta atrás

Ver [RELEASE_MANAGEMENT.md](RELEASE_MANAGEMENT.md): Fase 2.1 (PRE), Fase 4.1 y 4.2 (copia
`prerel` y PRO) y Anexo B, capa 4 (restauración). Este documento no describe despliegues.
