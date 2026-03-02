-- script.sql
-- Sets up the managed identity user in the Northwind database
-- MANAGED-IDENTITY-NAME is replaced by sed in deploy.sh before execution

-- Drop and recreate the managed identity user with correct SID
IF EXISTS (SELECT * FROM sys.database_principals WHERE name = 'MANAGED-IDENTITY-NAME')
BEGIN
    DROP USER [MANAGED-IDENTITY-NAME];
END

CREATE USER [MANAGED-IDENTITY-NAME] FROM EXTERNAL PROVIDER;
ALTER ROLE db_datareader ADD MEMBER [MANAGED-IDENTITY-NAME];
ALTER ROLE db_datawriter ADD MEMBER [MANAGED-IDENTITY-NAME];
GRANT EXECUTE TO [MANAGED-IDENTITY-NAME];
