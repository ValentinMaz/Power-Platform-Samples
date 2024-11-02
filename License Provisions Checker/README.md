# License Provisions Checker
The **License Provisions Checker** is a free Solution to automatically extract information regarding purchased Power Platform license skus and addons in the tenant and store this information in Dataverse. This information can be used to:
- Identify and receive alert about expiring licenses
- Identify opportunities to optimize costs by decommissionning lower license packs and purchasing bigger and cheapker packs instead for the same quantity of licenses
- Centralize information about all licenses, purchased sku units as well as actual license quantities in the tenant that are provisioned, assigned and consumed 
More information about this solution can be found on this blog post: https://powertricks.io/license-provisions-checker

![License Provisions Checker](/License%20Provisions%20Checker/Screenshots/lpc-home.png)

## Pre-requisites
- Access to an environment with these connectors enabled: **HTTP**, **Office 365 Outlook** and **Dataverse**
- [Dataverse Accelerator solution](https://learn.microsoft.com/en-us/power-apps/maker/data-platform/dataverse-accelerator/dataverse-accelerator#install-the-dataverse-accelerator) installed on the environment 
- App registered in Azure to use the Graph API List subscribedSkus request (more information in a dedicated section below)
- App Registered in Azure to use the Power Platform Licensing API (more information in a dedicated section below)
- An Azure Key Vault instance set up to store the secrets of the two Azure Apps
- A Power Automate Premium license for the account which will will import the solution

## The solution
The Solution principally contains:
- 3 Dataverse tables to store information about purchased skus, licenses and to keep a history of previous license quantities
- 3 Cloud Flows: 1 to configure the license and sku tables, 1 to automatically update the license and purchased sku quantities every day, and 1 to save a snapshot of license quantities regularly for history logging 
The solution is described in more details in the [related blog post](https://powertricks.io/license-provisions-checker).
Follow the steps below to set up the solution.

## Prepare the Azure App for the Graph API
1. Register an App in Azure
2. Assign the API Application Permissions "LicenseAssignment.Read.All", or "Organization.Read.All"
3. Grant Admin Consent for the permissions

## Prepare the Azure App for the Power Platform Licensing API
1. Register an App in Azure (it can technically be the same as the one used for the Graph API, but would recommend to use two different ones)
2. Follow [these instructions](https://learn.microsoft.com/en-us/power-platform/admin/powershell-create-service-principal) to register this app as an Admin Management Application.

## Configure the Azure Key Vault
1. Set up an Azure Key Vault in a subscription if not done already. Make sure that it is configured as mentioned in [this Microsoft doc](https://learn.microsoft.com/en-us/power-apps/maker/data-platform/environmentvariables-azure-key-vault-secrets)
2. Generate the Client secret for the two above Apps and add these secrets in the vault

## Install the solution
1. Download the solution package. Both Managed and Unmanaged solutions are added so that you can pick according to your preference
2. Import the solution in the target environment
3. Configure the connection for the connectors when prompted.
4. Configure the Environment Variables:
    - **LpcBoolIsDemo**: Set it to "No". If set to "Yes", fake license quantities data will be loaded into the App. The purpose of this vairable is only to do some tests if no licenses are actually provisioned in teh tenant. 
    - **LpcTextEmail**: The mailbox which will be used to send and receive email alerts when license quantities change in the tenant and/or when a flow fails. Recommendation to set this to a shared mailbox. Since it is used for the "To" and "From" parameters, is should be a mailbox for which the user setting up the solution has access to send emails from.
    - **LpcTextTenantId**: The tenant id
    - **LpcTextGraphApiAuthClientId**: The client id of the Azure App created for the Graph API in the "Prepare the Azure App for the Graph API" section.
    - **LpcSecretGraphApiAuth**: The identifier of the key vault secret for the Azure App created for the Graph API. It has to be set to `/subscriptions/{Subscription ID}/resourceGroups/{Resource Group Name}/providers/Microsoft.KeyVault/vaults/{Key Vault Name}/secrets/{Secret Name}`
    - **LpcTextPpApiAuthClientId**: The client id of the Azure App created for the Power Platform Licensing API
    - **LpcSecretPpApiAuth**: the identified of the key vault secret for the Azure App for the Power Platform Licensing API. It has to be set to `/subscriptions/{Subscription ID}/resourceGroups/{Resource Group Name}/providers/Microsoft.KeyVault/vaults/{Key Vault Name}/secrets/{Secret Name}`
    - **LpcTextSnapshotFrequency**: text value which can be "Day", "Week" or "Month". It is the frequency in which a snapshot will be taken of the license table to keep a log of the former license quantities history. I recommend "Month" to avoid consuming too much storage.
    - **LpcNumberSnapshotInterval**: integer representing the number of Day, Week, or Month for which the snpashot should be taken.
5. Confirm that the flows "Lpc - Daily Provision Checker" and "Lpc - Take License Snapshot" are turned off, turn them off otherwise
6. Run the flow "Lpc - Prepulate Licenses" once. Then, open the Model Driven App and confirm that the license and purchased skus have been correctly populated
7. Turn on the flow "Lpc - Daily Provision Checker". Run it manually if it does not run automatically when turned on, and confirm that the purchased sku units quantities are updated, as well as the total quantities from the license table
8. Turn on the flow "Lpc - Take License Snapshot". 