# Ownership Updater
The **Ownership Updater** is a simple solution containing a Child Flow which can be called by admins to change the main owner of a Canvas App or a solution-aware Cloud Flow. It allows to:
- Change the main owner of the App or Flow by inputting the email address of the user who should be the new Owner
- Define whether the current Owner should be a co-owner or not have any edit rights over the App or Flow

More information about this solution and what it can do are available on this blog post: https://powertricks.io/ownership-updater

![My Image](/Ownership%20Updater/Screenshots/OwnershipUpdaterMainFlowScreenshot.png)

## Pre-requisites
- Access to an environment with Dataverse enabled
- The following connectors need to be enabled on the environment: Dataverse, Power Platform for Admins, Power Apps for Admins. 
- The Power Platform Tenant Administrator role is needed for the account owning the solution flows
- A premium license is required for the Admins owning the flows

## The Solution 
The Solution contains 3 child flows:
- 1 Main Child flow to request the ownership change
- 1 Child Flow called by the Main one if it is a request to change an App Owner
- 1 Child Flow called by the Main one if it is a request to change a Flow Owner
More information about how the flow were design can be found in [the related blog post](https://powertricks.io/ownership-updater)

## Install the solution
Proceed to the steps below to set up the solution:
1. Download the solution package. Both Managed and Unmanaged solutions are added so that you can pick according to your preference
2. Import the solution in an environment matching the requirements from the pre-requisites section
3. Configure the connections when prompted
5. Turn on all the Cloud Flows

## Use the solution
Simply create a new Solution in the same information where you installed the Ownership Updater. Create a Cloud Flow which calls the main child flow, called "Ownership Updater Child - Main".

![My Image](/Ownership%20Updater/Screenshots/OwnershipUpdaterMainFlowInputs.png)