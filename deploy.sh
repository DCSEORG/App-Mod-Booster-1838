#!/bin/bash
# deploy.sh
# Deploys Expense Management infrastructure and application to Azure (without GenAI)
# Usage: ./deploy.sh
#
# Prerequisites:
#   - Azure CLI logged in (az login)
#   - jq installed
#   - Python 3 with pip3
#   - ODBC Driver 18 for SQL Server installed
#
# After deployment, view the app at: <APP_URL>/Index

set -e

# ─── Variables ───────────────────────────────────────────────────────────────
RESOURCE_GROUP="rg-expensemgmt-demo"
LOCATION="uksouth"
ADMIN_OBJECT_ID=$(az ad signed-in-user show --query id -o tsv)
ADMIN_LOGIN=$(az ad signed-in-user show --query userPrincipalName -o tsv)

echo "============================================"
echo "  Expense Management Deployment"
echo "============================================"
echo "Resource Group : $RESOURCE_GROUP"
echo "Location       : $LOCATION"
echo "Admin UPN      : $ADMIN_LOGIN"
echo "Admin Object ID: $ADMIN_OBJECT_ID"
echo ""

# ─── 1. Create Resource Group ─────────────────────────────────────────────────
echo "Step 1: Creating resource group..."
az group create \
    --name "$RESOURCE_GROUP" \
    --location "$LOCATION" \
    --output none
echo "  ✓ Resource group created"

# ─── 2. Deploy Infrastructure (App Service + SQL, no GenAI) ───────────────────
echo ""
echo "Step 2: Deploying infrastructure (App Service + Azure SQL)..."
DEPLOYMENT_OUTPUT=$(az deployment group create \
    --resource-group "$RESOURCE_GROUP" \
    --template-file infra/main.bicep \
    --parameters \
        location="$LOCATION" \
        adminObjectId="$ADMIN_OBJECT_ID" \
        adminLogin="$ADMIN_LOGIN" \
        deployGenAI=false \
    --query properties.outputs \
    -o json)

echo "  ✓ Infrastructure deployed"

# Extract outputs
APP_SERVICE_NAME=$(echo $DEPLOYMENT_OUTPUT    | jq -r '.appServiceName.value')
APP_SERVICE_URL=$(echo $DEPLOYMENT_OUTPUT     | jq -r '.appServiceUrl.value')
SQL_SERVER_FQDN=$(echo $DEPLOYMENT_OUTPUT     | jq -r '.sqlServerFqdn.value')
SQL_SERVER_NAME=$(echo $DEPLOYMENT_OUTPUT     | jq -r '.sqlServerName.value')
DATABASE_NAME=$(echo $DEPLOYMENT_OUTPUT       | jq -r '.databaseName.value')
MANAGED_IDENTITY_CLIENT_ID=$(echo $DEPLOYMENT_OUTPUT | jq -r '.managedIdentityClientId.value')
MANAGED_IDENTITY_NAME="mid-AppModAssist-01-01-00"

echo "  App Service  : $APP_SERVICE_NAME"
echo "  App URL      : $APP_SERVICE_URL"
echo "  SQL Server   : $SQL_SERVER_FQDN"
echo "  Database     : $DATABASE_NAME"
echo "  MI Client ID : $MANAGED_IDENTITY_CLIENT_ID"

# ─── 3. Configure App Service Settings ────────────────────────────────────────
echo ""
echo "Step 3: Configuring App Service settings..."
CONNECTION_STRING="Server=tcp:${SQL_SERVER_FQDN};Database=${DATABASE_NAME};Authentication=Active Directory Managed Identity;User Id=${MANAGED_IDENTITY_CLIENT_ID};"

az webapp config appsettings set \
    --name "$APP_SERVICE_NAME" \
    --resource-group "$RESOURCE_GROUP" \
    --settings \
        "ConnectionStrings__DefaultConnection=${CONNECTION_STRING}" \
        "ManagedIdentityClientId=${MANAGED_IDENTITY_CLIENT_ID}" \
        "AZURE_CLIENT_ID=${MANAGED_IDENTITY_CLIENT_ID}" \
        "ASPNETCORE_ENVIRONMENT=Production" \
    --output none
echo "  ✓ App Service settings configured"

# ─── 4. Wait for SQL Server to be Fully Ready ──────────────────────────────────
echo ""
echo "Step 4: Waiting 30 seconds for SQL Server to be fully ready..."
sleep 30
echo "  ✓ Wait complete"

# ─── 5. Add Firewall Rules ─────────────────────────────────────────────────────
echo ""
echo "Step 5: Configuring SQL firewall rules..."

# Allow Azure services (0.0.0.0 to 0.0.0.0)
az sql server firewall-rule create \
    --resource-group "$RESOURCE_GROUP" \
    --server "$SQL_SERVER_NAME" \
    --name "AllowAllAzureIPs" \
    --start-ip-address 0.0.0.0 \
    --end-ip-address 0.0.0.0 \
    --output none
echo "  ✓ Azure services firewall rule created"

# Add current deployment machine IP
MY_IP=$(curl -s https://api.ipify.org)
az sql server firewall-rule create \
    --resource-group "$RESOURCE_GROUP" \
    --server "$SQL_SERVER_NAME" \
    --name "AllowDeploymentIP" \
    --start-ip-address "$MY_IP" \
    --end-ip-address "$MY_IP" \
    --output none
echo "  ✓ Deployment IP ($MY_IP) added to firewall"

echo "Waiting additional 15 seconds for firewall rules to propagate..."
sleep 15

# ─── 6. Install Python Dependencies ───────────────────────────────────────────
echo ""
echo "Step 6: Installing Python dependencies..."
pip3 install --quiet pyodbc azure-identity
echo "  ✓ Python dependencies installed"

# ─── 7. Update Python Scripts with Connection Details ─────────────────────────
echo ""
echo "Step 7: Updating Python scripts with connection details..."
sed -i.bak "s|SERVER = \"example.database.windows.net\"|SERVER = \"${SQL_SERVER_FQDN}\"|g" run-sql.py && rm -f run-sql.py.bak
sed -i.bak "s|DATABASE = \"database_name\"|DATABASE = \"${DATABASE_NAME}\"|g" run-sql.py && rm -f run-sql.py.bak
sed -i.bak "s|DATABASE = \"Northwind\"|DATABASE = \"${DATABASE_NAME}\"|g" run-sql.py && rm -f run-sql.py.bak

sed -i.bak "s|SERVER = \"example.database.windows.net\"|SERVER = \"${SQL_SERVER_FQDN}\"|g" run-sql-dbrole.py && rm -f run-sql-dbrole.py.bak
sed -i.bak "s|DATABASE = \"database_name\"|DATABASE = \"${DATABASE_NAME}\"|g" run-sql-dbrole.py && rm -f run-sql-dbrole.py.bak
sed -i.bak "s|DATABASE = \"Northwind\"|DATABASE = \"${DATABASE_NAME}\"|g" run-sql-dbrole.py && rm -f run-sql-dbrole.py.bak

sed -i.bak "s|SERVER = \"example.database.windows.net\"|SERVER = \"${SQL_SERVER_FQDN}\"|g" run-sql-stored-procs.py && rm -f run-sql-stored-procs.py.bak
sed -i.bak "s|DATABASE = \"database_name\"|DATABASE = \"${DATABASE_NAME}\"|g" run-sql-stored-procs.py && rm -f run-sql-stored-procs.py.bak
sed -i.bak "s|DATABASE = \"Northwind\"|DATABASE = \"${DATABASE_NAME}\"|g" run-sql-stored-procs.py && rm -f run-sql-stored-procs.py.bak

echo "  ✓ Python scripts updated"

# ─── 8. Run Database Schema ────────────────────────────────────────────────────
echo ""
echo "Step 8: Running database schema..."
python3 run-sql.py
echo "  ✓ Database schema applied"

# ─── 9. Configure Managed Identity DB Role ────────────────────────────────────
echo ""
echo "Step 9: Configuring managed identity database role..."
# Replace placeholder in script.sql (cross-platform Mac compatible)
sed -i.bak "s/MANAGED-IDENTITY-NAME/${MANAGED_IDENTITY_NAME}/g" script.sql && rm -f script.sql.bak
python3 run-sql-dbrole.py
# Restore the placeholder for future runs
sed -i.bak "s/${MANAGED_IDENTITY_NAME}/MANAGED-IDENTITY-NAME/g" script.sql && rm -f script.sql.bak
echo "  ✓ Managed identity database role configured"

# ─── 10. Deploy Stored Procedures ──────────────────────────────────────────────
echo ""
echo "Step 10: Deploying stored procedures..."
python3 run-sql-stored-procs.py
echo "  ✓ Stored procedures deployed"

# ─── 11. Build and Deploy Application ─────────────────────────────────────────
echo ""
echo "Step 11: Building and deploying application..."

# Build the app
cd app
dotnet publish -c Release -o ./publish --nologo -q
cd publish
zip -r ../../app.zip . -x "*.pdb"
cd ../..

echo "  ✓ Application built and zipped"

az webapp deploy \
    --resource-group "$RESOURCE_GROUP" \
    --name "$APP_SERVICE_NAME" \
    --src-path ./app.zip \
    --type zip \
    --output none

echo "  ✓ Application deployed"

# ─── Done ──────────────────────────────────────────────────────────────────────
echo ""
echo "============================================"
echo "  ✓ Deployment Complete!"
echo "============================================"
echo ""
echo "  App URL  : ${APP_SERVICE_URL}/Index"
echo ""
echo "  NOTE: Navigate to ${APP_SERVICE_URL}/Index to view the app"
echo "  (The root URL / does not redirect automatically)"
echo ""
echo "  Swagger UI: ${APP_SERVICE_URL}/swagger"
echo ""
