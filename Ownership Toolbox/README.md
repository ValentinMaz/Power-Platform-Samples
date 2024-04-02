# Ownership Toolbox
The **Ownership Toolbox** is a free tool to help Power Platform admins to efficiently manage apps and flows ownership. It is a Solution with just one Canvas Apps which allows to:
- Identify the Flows and Apps owned by a user in an environment
- Add co-owners to selected Flows and Apps, individually or in bulk
- Change the main Owner of selected Apps or Flows, individually or in bulk
- Remove the users with access to an App or Flow

More information about this tool can be found on this blog post: https://powertricks.io/ownership-toolbox

![My Image](/Ownership%20Toolbox/Screenshots/ownership-toolbox-2.png)

## Pre-requisites
- Access to an environment where the [CoE Starter Kit](https://learn.microsoft.com/en-us/power-platform/guidance/coe/starter-kit) is installed
- The [Ownership Updater](https://github.com/ValentinMaz/Power-Platform-Samples/tree/main/Ownership%20Updater) solution needs to be installed on that Environment. The child flow from that solution is needed to update the main owner of Flows
- Environment Maker Access (or more) on that environment for the import of the solution
- The Dataverse, Office365Users, PowerAppsforAdmins and PowerAutomateforAdmins connectors need to be enabled on that environment
- Users wanting to use the App will need an appropriate Tenant Administrator role (Power Platform Admin or Global Admin)

## The solution
The Solution contains:
- 1 Power Apps Canvas App
- 1 Flow triggered from the App only when the Admin needs to change the main owner of a flow - this Flow then calls the [Ownership Updater](https://github.com/ValentinMaz/Power-Platform-Samples/tree/main/Ownership%20Updater) child flow to perform the change

No data is stored by the solution, a premium license is required for the users to connect to the environment Dataverse tables (Makers, PowerApps Apps, Environments, Flows and AAD Users).

## Install the solution
The installation is quick and easy.
1. Download and Import the [Ownership Updater](https://github.com/ValentinMaz/Power-Platform-Samples/tree/main/Ownership%20Updater), and set the connections upon import
2. Download the **Ownership Toolbox** solution package. Both Managed and Unmanaged solutions are added so that you can pick according to your preference.
3. Import the solution in the target environment, the same as the one where you installed the Ownership Updater
4. Configure the connections for the connectors when prompted

## Customize the solution
You can customize the Look & Feel of the App by updating the App Formulas for the colors, font size, logo, etc... [More info here.](https://powertricks.io/ownership-toolbox#additional-comments)

## Use the solution
The App should be intuitive, but feel free to refer to [the blog post](https://powertricks.io/ownership-toolbox) for more information about how to use the App.

## Troubleshoot
- If your Power Apps version does not have [the Host object](https://learn.microsoft.com/en-us/power-platform/power-fx/reference/object-hos) available, you might experience the errors below in the canvas app:

  ![https://user-images.githubusercontent.com/70452834/277759211-3cdf78d9-afd6-4a40-ae55-4652878f34e5.png](https://user-images.githubusercontent.com/70452834/277759211-3cdf78d9-afd6-4a40-ae55-4652878f34e5.png)

  You can fix this issue by updating the App.Formulas property and replace "Host.TenantID" by your actual tenantId
