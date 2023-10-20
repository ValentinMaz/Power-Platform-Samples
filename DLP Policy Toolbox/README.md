# DLP Policy Toolbox
The **DLP Policy Toolbox** is a solution built to facilitate the management of DLP Policies. It is mainly composed of a Power Apps Canvas App which offers two main functionalities:
- Duplicate an existing DLP Policy in a few clicks with the **DLP Duplicator**
- Identify all the policies that apply to a specific environment with the **DLP Finder**

More information about this tool can be found on this blog post: https://powertricks.io/dlp-policy-toolbox

![https://github.com/ValentinMaz/Power-Platform-Samples/blob/9294576d904ff814b3f185c6ab5fc2adad19218b/DLP%20Policy%20Toolbox/Screenshots/DLP%20Policy%20Toolbox%20Screenshot.png](https://github.com/ValentinMaz/Power-Platform-Samples/blob/9294576d904ff814b3f185c6ab5fc2adad19218b/DLP%20Policy%20Toolbox/Screenshots/DLP%20Policy%20Toolbox%20Screenshot.png)

## Pre-requisites
- Access to an environment with Dataverse enabled (to be able to import the Solution)
- Environment Maker Access (or more) on that environment
- The Power Platform for Admins connector needs to be enabled on that Environment
- Users wanting to use the App will need an appropriate Tenant Administrator role (Power Platform Admin, D365 Admin, or Global Admin)

## The solution
The Solution contains:
- 1 Power Apps Canvas App
- 3 Automate Cloud flows triggered from the App
- 1 Connection reference for the Power Platform for Admins connector

No data is stored by the solution, no premium license is required.

## Install the solution
The installation is quick and easy.
1. Download the solution package. Both Managed and Unmanaged solutions are added so that you can pick according to your preference.
2. Import the solution in the target environment
3. Configure the connection for the Power Platform for Admins connector when prompted
4. Confirm that the 3 cloud flows are turned on

## Customize the solution
You can customize the Look & Feel of the App by updating the App Formulas for the colors, font size, logo, etc...

## Use the solution
The App should be intuitive, but feel free to refer to [the blog post](https://powertricks.io/dlp-policy-toolbox) for more information about how to use the App.
