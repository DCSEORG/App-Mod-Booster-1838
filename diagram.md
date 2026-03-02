# Azure Services Architecture Diagram

## Expense Management System — Azure Resources

```
┌──────────────────────────────────────────────────────────────────────────┐
│  Resource Group: rg-expensemgmt-demo  (UK South)                         │
│                                                                           │
│  ┌─────────────────────────────────────────────────────────────────────┐  │
│  │  App Service Plan (Standard S1)  [uksouth]                          │  │
│  │  ┌──────────────────────────────────────────────────────────────┐   │  │
│  │  │  App Service: app-expensemgmt-<suffix>                       │   │  │
│  │  │  ASP.NET Core 8 Razor Pages + REST API + Swagger             │   │  │
│  │  │                                                              │   │  │
│  │  │  ┌──────────────────────────────────────────────────────┐   │   │  │
│  │  │  │  User-Assigned Managed Identity                      │   │   │  │
│  │  │  │  mid-AppModAssist-01-01-00                           │   │   │  │
│  │  │  └───────────────┬──────────────────────────────────────┘   │   │  │
│  │  └──────────────────┼───────────────────────────────────────────┘   │  │
│  └─────────────────────┼───────────────────────────────────────────────┘  │
│                        │                                                   │
│           ┌────────────┼──────────────────┐                               │
│           │            │                  │                               │
│           ▼            ▼                  ▼                               │
│  ┌────────────────┐  ┌──────────────────────────────────────────────────┐ │
│  │  Azure SQL     │  │  Azure OpenAI  [Sweden Central]                  │ │
│  │  [uksouth]     │  │  aoai-expensemgmt-<suffix>                       │ │
│  │                │  │  Model: gpt-4o  (Standard, capacity 8)           │ │
│  │  Server:       │  └──────────────────────────────────────────────────┘ │
│  │  sql-expense-  │                                                        │
│  │  mgmt-<suffix> │  ┌──────────────────────────────────────────────────┐ │
│  │                │  │  AI Search  [uksouth]                             │ │
│  │  Database:     │  │  srch-expensemgmt-<suffix>  (Basic SKU)           │ │
│  │  Northwind     │  └──────────────────────────────────────────────────┘ │
│  │                │                                                        │
│  │  Auth: Azure   │                                                        │
│  │  AD Only       │                                                        │
│  └────────────────┘                                                        │
└──────────────────────────────────────────────────────────────────────────┘
```

## Connection Flow

```
  User Browser
       │
       │ HTTPS
       ▼
  App Service (Razor Pages / REST API)
       │
       │ Managed Identity Auth
       │ (Active Directory Managed Identity)
       ▼
  Azure SQL Database (Northwind)
  [Stored Procedures only - no direct table access]

  Chat UI (chatui/index.html)
       │
       │ REST API calls
       ▼
  App Service (ChatController)
       │
       │ ManagedIdentityCredential (client ID from config)
       ▼
  Azure OpenAI (gpt-4o) — Function Calling Loop
       │
       │ Tool Calls → App Service APIs
       ▼
  Azure SQL (via Stored Procedures)
```

## Authentication Pattern

| Service        | Auth Method                             |
|----------------|-----------------------------------------|
| App → SQL      | Active Directory Managed Identity       |
| App → OpenAI   | ManagedIdentityCredential (client ID)   |
| App → Search   | ManagedIdentityCredential (client ID)   |
| Local Dev → SQL | Active Directory Default (az login)    |

## Deployment Scripts

| Script                | What it deploys                                    |
|-----------------------|----------------------------------------------------|
| `./deploy.sh`         | App Service + SQL + App (no GenAI)                 |
| `./deploy-with-chat.sh` | App Service + SQL + GenAI + App (full stack)     |

> **Note:** View the app at `<APP_URL>/Index` — the root URL `/` does not redirect.
