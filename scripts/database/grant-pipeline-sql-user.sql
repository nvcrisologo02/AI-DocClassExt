-- Alta one-time del SPN del service connection de Azure DevOps como usuario
-- de la BD DocumentIA, con permisos para aplicar migrations EF Core.
-- (AB#100018 - pipeline dedicado de migrations)
--
-- COMO EJECUTAR:
--   - Conectado a la BD DocumentIA del entorno correspondiente (NO a master),
--     con un usuario administrador de Entra del servidor SQL.
--   - Ejecutar SOLO el bloque del entorno que corresponda.
--   - Idempotente: si el usuario ya existe, el bloque lo deja como esta.
--
-- Roles concedidos (mismo trio que el usuario docaisql):
--   db_ddladmin  -> DDL de las migrations (CREATE/ALTER TABLE, INDEX, PROCEDURE)
--   db_datareader/db_datawriter -> seeds (INSERT/UPDATE) y __EFMigrationsHistory

-- ============================================================================
-- DEV  (servidor srbsqldevdocai, service connection "AI DocClassExt DEV",
--       appId efff077f-57d1-41c5-b66f-7b0d2e00fc0e)
-- ============================================================================
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = 'sareb-AI DocClassExt-04d2ac3a-de4c-4357-b4c7-b5b445c1b918')
BEGIN
    CREATE USER [sareb-AI DocClassExt-04d2ac3a-de4c-4357-b4c7-b5b445c1b918] FROM EXTERNAL PROVIDER;
END
ALTER ROLE db_ddladmin  ADD MEMBER [sareb-AI DocClassExt-04d2ac3a-de4c-4357-b4c7-b5b445c1b918];
ALTER ROLE db_datareader ADD MEMBER [sareb-AI DocClassExt-04d2ac3a-de4c-4357-b4c7-b5b445c1b918];
ALTER ROLE db_datawriter ADD MEMBER [sareb-AI DocClassExt-04d2ac3a-de4c-4357-b4c7-b5b445c1b918];
GO

-- ============================================================================
-- PRE  (servidor srbsqlpredocai, service connection "AI DocClassExt PRE",
--       appId 1d2b166d-e608-487e-9711-5c5fba9b157a)
-- ============================================================================
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = 'sareb-AI DocClassExt-a2694897-36aa-4f2e-9878-7de073674a6e')
BEGIN
    CREATE USER [sareb-AI DocClassExt-a2694897-36aa-4f2e-9878-7de073674a6e] FROM EXTERNAL PROVIDER;
END
ALTER ROLE db_ddladmin  ADD MEMBER [sareb-AI DocClassExt-a2694897-36aa-4f2e-9878-7de073674a6e];
ALTER ROLE db_datareader ADD MEMBER [sareb-AI DocClassExt-a2694897-36aa-4f2e-9878-7de073674a6e];
ALTER ROLE db_datawriter ADD MEMBER [sareb-AI DocClassExt-a2694897-36aa-4f2e-9878-7de073674a6e];
GO

-- ============================================================================
-- PROD (servidor srbsqlprodocai, service connection "AI DocClassExt PRO",
--       appId 96ab6d96-57a6-425b-a668-7666ac96b5c5)
-- ============================================================================
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = 'sareb-AI DocClassExt-1ebfe36f-0538-46de-be19-1db6b14c5be3')
BEGIN
    CREATE USER [sareb-AI DocClassExt-1ebfe36f-0538-46de-be19-1db6b14c5be3] FROM EXTERNAL PROVIDER;
END
ALTER ROLE db_ddladmin  ADD MEMBER [sareb-AI DocClassExt-1ebfe36f-0538-46de-be19-1db6b14c5be3];
ALTER ROLE db_datareader ADD MEMBER [sareb-AI DocClassExt-1ebfe36f-0538-46de-be19-1db6b14c5be3];
ALTER ROLE db_datawriter ADD MEMBER [sareb-AI DocClassExt-1ebfe36f-0538-46de-be19-1db6b14c5be3];
GO

-- Verificacion (ejecutar tras el bloque):
-- SELECT dp.name, dp.type_desc, r.name AS role_name
-- FROM sys.database_principals dp
-- LEFT JOIN sys.database_role_members rm ON rm.member_principal_id = dp.principal_id
-- LEFT JOIN sys.database_principals r ON r.principal_id = rm.role_principal_id
-- WHERE dp.name LIKE 'sareb-AI DocClassExt-%';
