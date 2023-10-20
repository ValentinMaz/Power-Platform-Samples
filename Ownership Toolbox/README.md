# Ownership Toolbox
The **Ownership Toolbox** is a free tool to help Power Platform admins to efficiently manage apps and flows ownership. It is a Solution with just one Canvas Apps which allows to:
- Identify the Flows and Apps owned by a user in an environment
- Add co-owners to selected Flows and Apps, individually or in bulk
- Change the main Owner of selected Apps, individually or in bulk
- Remove the users with access to an App or Flow

More information about this tool can be found on this blog post: https://powertricks.io/ownership-toolbox

![https://github.com/ValentinMaz/Power-Platform-Samples/blob/ca14d14c03a17f23a8bf8c925ef1125ee8812403/Ownership%20Toolbox/Screenshots/ownership-toolbox.png](https://github.com/ValentinMaz/Power-Platform-Samples/blob/ca14d14c03a17f23a8bf8c925ef1125ee8812403/Ownership%20Toolbox/Screenshots/ownership-toolbox.png)

## Pre-requisites
- Access to an environment where the [CoE Starter Kit](https://learn.microsoft.com/en-us/power-platform/guidance/coe/starter-kit) is installed
- Environment Maker Access (or more) on that environment for the import of the solution
- The Dataverse, Office365Users, PowerAppsforAdmins and PowerAutomateforAdmins connectors need to be enabled on that environment
- Users wanting to use the App will need an appropriate Tenant Administrator role (Power Platform Admin or Global Admin)

## The solution
The Solution contains:
- 1 Power Apps Canvas App

No data is stored by the solution, a premium license is required for the users to connect to the environment Dataverse tables (Makers, PowerApps Apps, Environments, Flows and AAD Users).

## Install the solution
The installation is quick and easy.
1. Download the solution package. Both Managed and Unmanaged solutions are added so that you can pick according to your preference.
2. Import the solution in the target environment
3. Configure the connection for the connectors when prompted

## Customize the solution
You can customize the Look & Feel of the App by updating the App Formulas for the colors, font size, logo, etc... [More info here.](https://powertricks.io/ownership-toolbox#additional-comments)

## Use the solution
The App should be intuitive, but feel free to refer to [the blog post](https://powertricks.io/ownership-toolbox) for more information about how to use the App.