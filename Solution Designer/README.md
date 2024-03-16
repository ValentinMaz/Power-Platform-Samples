# Solution Deployer
The **Solution Deployer** is a solution built to allow Power Platform CoEs to centrally maintain reusable components and deploy them widely in the organization, without the need for Manage Environments and a premium licenses for all Enviornment Users. The main functionalities of the solution are:
- Configure the Target Environments to deploy the solutions to
- Request the deployment of a solution to a single environment
- Manage Solution Subscriptions, mapping a solution with multiple subscribed environments to trigger the solution deployment to all related environments
- Automatically share the Solution Components with the pre-configured Target Owner for the environment, and notify them accordingly
- Handle the case when Environment Variable Definitions need to be mapped with a value or Connection References with a connection

More information about this solution and what it can do are available on this blog post: https://powertricks.io/solution-deployer

![Solution Deployer](/Solution%20Deployer/Screenshots/NavigationSolutionSubscription.png)

## Pre-requisites
- Access to an environment with Dataverse enabled
- The following connectors need to be enabled on the enviornment: Dataverse, Dataverse Legacy, Office 365 for Outlook, Power Apps for Admins, Power Automate for Admins, Power Platform for Admins. 
- The Power Platform Tenant Administrator role is needed for the account owning the solution flows and using the Model Driven App
- A premium license is required for the Admins using the App

## The Solution 
The Solution contains 29 components in total, principally composed of:
- 1 Model Driven App to manage the deployments and related configuration
- 2 Custom Pages to create Deployments and Solution Subscriptions
- 1 Javascript Web Resource to open the Custom Pages on the side of the screen
- A Command Library to leverage PowerFx from a custom command
- An XML file and a setting value to customize the theme of the App
- 8 Cloud Flows and 5 connection references
- 3 Enviornment Variables
- 4 Custom Dataverse Tables as well as a simple reference to the Users table

## Install the solution
Proceed to the steps below to set up the solution:
1. Download the solution package. Both Managed and Unmanaged solutions are added so that you can pick according to your preference
2. Import the solution in an environment matching the requirements from the pre-requisites section
3. Configure the connections when prompted
4. Configure the Environment Variables:
    - **SolDeployerDVSourceEnvironment**: The Power Platform Environment used as the Source. Only the solutions from this environment will be available for deployment
    - **SolDeployerSourcePublishers**: Admins can configure this variable with a list of publisher prefixes, separated with ";". Only the Source Enviornment solutions from these publishers will be available for deployment. If blank, all solutions of the Source environment will show.
    - **SolDeployerEmailNotifyFrom**: this variable has to be configured with the mailbox from which the notifications will be sent. It can be the email address of the user importing the solution or the email address of a shared mailbox they have permissions over.
5. Turn on all the Cloud Flows
6. Open the Model Driven App in Play Mode and create and some Target Environments - If you are leveraging the CoE Kit in the same or in another environment, you can import the "Solution Deployer Dataflow Addon" Solution which includes a Dataflow to automatically maintain the list of target environments. Mroe information about this Addon below.

## Use the solution
Please refer to [the related blog post](https://powertricks.io/solution-deployer) for more information about how to use the App.

# Solution Deployer Dataflow Addon
This Solution is only composed of one Dataflow. Its purpose it to pull the environments from the environment table of the CoE Starter Kit to maintain the list of Target Environments for the Solution Deployer. Unfortunately although Dataflows are now solution aware, it is not possible to use Environment Variable within the Power Query. Therefore you will need to follow the steps below to set up the Dataflow:
1. Import the Solution Deployer (see above steps)
2. Import the Dataflow Addon solution in the same environment as where you imported the Solution Deployer. Both Managed and Unmanaged solutions are added so that you can pick according to your preference.
3. Find the imported Dataflow "Pull Environments From CoE Kit" by opening this page, replacing {EnvironmentId} by the Id of the environment where you imported the solution: https://make.powerapps.com/environments/{EnvironmentId}/dataintegration
4. Click on the 3 of the Dataflow and click "Edit"
5. On the left on the queries pane, click the Parameter "CoEKitOrgUrl", and replace the value by by the Instance Url of the environment where the CoE Kit is installed
6. Once replaced, open the the "admin_enviornment" query and set up the connection by clicking "Configure connection"
7. Feel free to adjust the Query steps (optional), for instance you might want to add a filter to not import all environments as target environments
8. Click Next and Publish. The Dataflow will run once and should already be configured to run every day which you can adjust as needed