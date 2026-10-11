# 2. Provision with Azure CLI

To provision the infrastructure outside GitHub Actions, sign in to the intended subscription and run:

```bash
az login
az account set --subscription "<subscription-id>"
az deployment sub create \
	--name release-notes-local \
	--location "<azure-region>" \
	--template-file infra/main.bicep \
	--parameters location="<azure-region>" resourceNameSuffix="<lowercase-alphanumeric-suffix>"
```

This command creates or updates the resource group and workload without requiring a pre-existing resource group. [infra/main.bicepparam](../infra/main.bicepparam) shows the parameter contract; replace its example suffix before using it.