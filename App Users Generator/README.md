# App Users Generator
The **App Users Generator** solution allows to generate or activate an Application User on an environment just before making a call with the Dataverse Web API on that environment, and disable it after. This is the equivalent of a "Just In Time" API access when needed. The solution simply contains two child flows to create and disbale the App User before and after making the Dataverse API call. 

I share more information about this solution and what it can do on this blog post: https://powertricks.io/app-users-generator

![App Generator Screenshot](/App%20Users%20Generator/Screenshots/AppUsersGeneratorChildFlow1.png)

## Pre-requisites
- Access to an environment with Dataverse enabled
- The Dataverse and Power Platform for Admins connectors need to be enabled on the environment 
- The Power Platform Tenant Administrator role is needed for the account running the child flows
- A premium license is required for the account running the child flows
- An App needs to be registered in Azure to associate with the Application Users generated (more info in the installation steps)
- An Azure Key Vault instance is also required (more info in the installation steps)

## The Solution 
The Solution contains 2 child flows and 1 flow only for demonstration purpose. [More information abut the solution is available here.](https://powertricks.io/app-users-generator)

## Install the solution
Proceed to the steps below to set up the solution:
1. [Create the App Registration in Azure](https://powertricks.io/app-users-generator#app-registration)
2. [Set up an Azure Key Vaut and upload the App Secret](https://powertricks.io/app-users-generator#key-vault)
3. Download the solution package. Both Managed and Unmanaged solutions are added so that you can pick according to your preference
4. Import the solution in an environment matching the requirements from the pre-requisites section
5. Configure the connections when prompted
6. Configure the Environment Variable:
    - **AppUsersGeneratorClientId**: The Client Id of the App created in step 1
    - **AppUsersGeneratorSecret**: The reference to the key vault and secret, using this format: ```/subscriptions/{Subscription ID}/resourceGroups/{Resource Group Name}/providers/Microsoft.KeyVault/vaults/{Key Vault Name}/secrets/{Secret Name}```
7. Turn on all the Cloud Flows
8. Run the Demo Flow called "AppUsersGenerator Parent - Demo" by inputting an dev/test environment Id as parameter to confirm that the solution works as expected

## Use the solution
Please refer to [the related blog post](https://powertricks.io/app-users-generator) for more information about the solution.