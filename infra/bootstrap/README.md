# Bootstrap — One-Time Prerequisites

> [!CAUTION]
> These steps are documented for **reference only**. They must be executed
> manually by an administrator — never run autonomously by a pipeline.

This document describes the one-time setup required before the FinanceTracker CI/CD
pipeline can validate and deploy Azure infrastructure.

---

## 1. Azure Subscription Setup

Ensure you have an active Azure subscription and the Azure CLI installed:

```bash
az login
az account set --subscription "<subscription-id>"
```

## 2. Resource Group Creation

Create resource groups for each environment:

```bash
# Dev
az group create \
  --name rg-FinanceTracker-dev \
  --location "<azure-region>"

# Prod
az group create \
  --name rg-FinanceTracker-prod \
  --location "<azure-region>"
```

## 3. GitHub OIDC Federation

OIDC federation allows GitHub Actions to authenticate with Azure **without
storing secrets**. This requires an Azure AD app registration with a federated
credential.

### 3a. Create an App Registration

```bash
az ad app create --display-name "FinanceTracker-github-oidc"
```

Note the `appId` from the output — this is your **Client ID**.

### 3b. Create a Service Principal

```bash
az ad sp create --id <app-id>
```

### 3c. Add Federated Credential for the Repository

```bash
az ad app federated-credential create \
  --id <app-object-id> \
  --parameters '{
    "name": "FinanceTracker-main-branch",
    "issuer": "https://token.actions.githubusercontent.com",
    "subject": "repo:<github-org>/<repo-name>:ref:refs/heads/main",
    "audiences": ["api://AzureADTokenExchange"]
  }'
```

For pull request triggers, add a second credential:

```bash
az ad app federated-credential create \
  --id <app-object-id> \
  --parameters '{
    "name": "FinanceTracker-pull-request",
    "issuer": "https://token.actions.githubusercontent.com",
    "subject": "repo:<github-org>/<repo-name>:pull_request",
    "audiences": ["api://AzureADTokenExchange"]
  }'
```

### 3d. Grant Contributor Role on Resource Groups

```bash
# Dev
az role assignment create \
  --assignee <app-id> \
  --role Contributor \
  --scope /subscriptions/<subscription-id>/resourceGroups/rg-FinanceTracker-dev

# Prod
az role assignment create \
  --assignee <app-id> \
  --role Contributor \
  --scope /subscriptions/<subscription-id>/resourceGroups/rg-FinanceTracker-prod
```

### 3e. Configure GitHub Repository Secrets

Add these secrets in **Settings → Secrets and variables → Actions**:

| Secret Name               | Value                         |
| ------------------------- | ----------------------------- |
| `AZURE_CLIENT_ID`         | `<app-id>`                    |
| `AZURE_TENANT_ID`         | `<tenant-id>`                 |
| `AZURE_SUBSCRIPTION_ID`   | `<subscription-id>`           |

## 4. Azure AD Admin for SQL

Identify the Azure AD user or group that will act as the SQL administrator:

```bash
# Find your user's object ID
az ad user show --id "<admin-email>" --query id --output tsv
```

Use this object ID as the `sqlAdminObjectId` parameter in your
`dev.bicepparam` and `prod.bicepparam` files.

## 5. Summary — Execution Checklist

| #   | Step                              | Status |
| --- | --------------------------------- | ------ |
| 1   | Azure subscription active         | ☐      |
| 2   | Resource groups created            | ☐      |
| 3a  | App registration created           | ☐      |
| 3b  | Service principal created          | ☐      |
| 3c  | Federated credentials added        | ☐      |
| 3d  | Role assignments granted           | ☐      |
| 3e  | GitHub secrets configured          | ☐      |
| 4   | SQL admin object ID recorded       | ☐      |

> [!IMPORTANT]
> After completing these steps, update the parameter files in
> `infra/parameters/` with the real `sqlAdminObjectId` value before
> running any deployments.
