# Chatbot Remover
The **Chatbot Remover** solution contains a child flow which allows admins to programmatically delete Copilot Studio bots from Power Automate. It does so by leveraging the [App Users Generator](https://powertricks.io/app-users-generator/) and the Dataverse API.

I share more information about this solution and what it can do on this blog post: https://powertricks.io/delete-copilot-studio-bots-as-admin

![Chatbot Remover Screenshot](/Chatbot%20Remover/Screenshots/ChatbotRemoverFlowCallingFlow.png)

## Pre-requisites
- Access to an environment with Dataverse enabled
- [App Users Generator Installed on that Environment](https://powertricks.io/app-users-generator/) 
- The Power Platform Tenant Administrator role is needed for the account installing the solutions
- A premium license is required for the account running the child flows

## The Solution 
The Solution contains a single child flow. [More information abut how the child flow is built can found  here.](https://powertricks.io/delete-copilot-studio-bots-as-admin)

## Install the solution
Proceed to the steps below to set up the solution:
1. Download the solution package. Both Managed and Unmanaged solutions are added so that you can pick according to your preference
2. Import the solution in an environment matching the requirements from the pre-requisites section
3. Configure the connections when prompted
4. Turn on the child Flows
8. Create another flow to call the child flow and confirm that it can delete a chatbot on an environment.

## Use the solution
Please refer to [the related blog post](https://powertricks.io/delete-copilot-studio-bots-as-admin) for more information about the solution.