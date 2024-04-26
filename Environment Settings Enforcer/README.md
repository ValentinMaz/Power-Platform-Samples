# Environment Settings Enforcer
The **Environment Settings Enforcer** is a solution built to allow Power Platform Admins to enforce Environment Settings across environments. This fulfills a gap in the platform governance capabilities, where Environment Admins are free to manage their environment settings. It can be beneficial for Tenant Admins to define such environment configurations at tenant level instead. The main functionalities of the solution are:
- Deine the setting configurations which have to be enforced on environments
- Manage policies by associating the setting configurations and the environments which should be configured as such
- Manually deploy policies
- Define the policies which should be automatically deployed every day to ensure the Environment Admins are not reverting back to other configurations

More information about this solution and what it can do are available on this blog post: https://powertricks.io/settings-enforcer

![Setting Enforcer Screenshot](/Environment%20Settings%20Enforcer/Screenshots/SettingsEnforcerApp.png)

## Pre-requisites
- Access to an environment with Dataverse enabled
- The Environment should also have the CoE Starter Kit installed
- The Dataverse connector needs to be enabled on the environment 
- The Power Platform Tenant Administrator role is needed for the account owning the solution flows and using the Model Driven App
- A premium license is required for the Admins using the App

## The Solution 
The Solution contains 16 components in total, principally composed of:
- 1 Model Driven App to manage the settings and policies
- 4 Cloud flows and 1 connection reference to perform the setting deployments
- 1 Javascript Web Resource to customize the behaviour of a main form
- 1 Command Library to leverage PowerFx from a custom command
- 1 Enviornment Variable
- 5 Custom Dataverse Tables as well as a simple reference to the Environment Table from the CoE Kit

## Install the solution
Proceed to the steps below to set up the solution:
1. Download the solution package. Both Managed and Unmanaged solutions are added so that you can pick according to your preference
2. Import the solution in an environment matching the requirements from the pre-requisites section
3. Configure the connection when prompted
4. Configure the Environment Variable:
    - **EnvSetEnforcerRandomDelayOn**: If set to true, the policies flagged to be automatically applied every day will be applied at a different hour each day. This can prevent Environment Administrator from implementing workarounds.
5. Turn on all the Cloud Flows

## Use the solution
Please refer to [the related blog post](https://powertricks.io/settings-enforcer) for more information about how to use the App.