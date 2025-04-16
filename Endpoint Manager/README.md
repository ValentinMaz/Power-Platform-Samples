# Endpoint Manager
The **Endpoint Manager** is a free Solution to allow Power Platform CoEs and Admins to manage Endpoint Rule Filtering and Custom Connector Pattern Rules more efficiently. With this solution, Admins can define global whitelists of endpoints applied to multiple DLP Policies, as opposed to needing to configure them for each DLP and each connector separately via the PPAC.
  
More information about this solution can be found on this blog post: https://powertricks.io/endpoint-manager

![Endpoint Manager Bulk Rule Creation](/Endpoint%20Manager/Screenshots/epm-bulk-create-rules2.png)
  
## Pre-requisites
- Access to an environment with the HTTP connector (endpoints `https://api.bap.microsoft.com/providers/PowerPlatform.Governance/*`), Office 365 Outlook, and the Dataverse connectors enabled
- A Power Automate Premium license for the account which will import the solution
- An App Registered in Azure to use the Business Application Platform API (api.bap.microsoft.com) (more information in a dedicated section below)
- An Azure Key Vault instance set up to store the secrets of the Azure App (more information in a dedicated section below)

## The solution
The Solution principally contains:
- 5 Dataverse tables to store the endpoint configurations and DLP Policies
- 3 Cloud Flow to maintain metadata tables and manage the deployments of endpoint filtering rules
- 1 Model Driven App to configure and deloy the endpoint rules
  
The solution is described in more details in the [related blog post](https://powertricks.io/endpoint-manager).
Follow the steps below to set up the solution.

## Prepare the Azure App for the Business Application Platform API
1. Register an App in Azure
2. Follow [these instructions](https://learn.microsoft.com/en-us/power-platform/admin/powershell-create-service-principal) to register this app as an Admin Management Application.

## Configure the Azure Key Vault
1. Set up an Azure Key Vault in a subscription if not done already. Make sure that it is configured as mentioned in [this Microsoft doc](https://learn.microsoft.com/en-us/power-apps/maker/data-platform/environmentvariables-azure-key-vault-secrets)
2. Generate the Client secret for the above App and add the secret in the vault

## Install the solution
1. Download the solution package. Both Managed and Unmanaged solutions are added so that you can pick according to your preference
2. Import the solution in the target environment
3. Configure the connection for the connectors when prompted
4. Configure the Environment Variables:
    - **EpmEmailFailure**: The mailbox which will be used to send and receive email alerts if a flow fails. Recommendation to set this to a shared mailbox. Since it is used for the "To" and "From" parameters, is should be a mailbox for which the user setting up the solution has access to send emails from.
    - **EpmClientId**: The Client Id of the App registered in Azure.
    - **EpmTenantId**: The Tenant Id.
    - **EpmSecret**: The identifier of the key vault secret for the Azure App for the Power Platform Licensing API. It has to be set to `/subscriptions/{Subscription ID}/resourceGroups/{Resource Group Name}/providers/Microsoft.KeyVault/vaults/{Key Vault Name}/secrets/{Secret Name}`
5. Turn on the Flows and run the two below flows:
    - "Epm - Populate Supported Connectors"
    - "Epm - Populate Policies"
6. Finally, open the Model Driven App and use it as described in the [related blog post](https://powertricks.io/endpoint-manager).