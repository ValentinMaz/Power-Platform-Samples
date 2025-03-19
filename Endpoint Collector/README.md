# Endpoint Collector
The **Endpoint Collector** is a free Solution to extend the CoE Starter Kit automatically store the endpoints that the cloud flows from the tenant send HTTP requests to with the HTTP and HTTP With Entra Id connectors. The main features of the solution include:
- Automatically identify the flows using these connectors and store the related endpoints configured in the flow
- Automatically detect whether hard-coded secrets and passwords are used to define these HTTP actions in each Flow
- Automatically detect whether the HTTP protocol is used as opposed to HTTPS 
- Navigate through the collected data with a Model Driven App
- Report on the collected data with a Power BI report template
  
More information about this solution can be found on this blog post: https://powertricks.io/endpoint-collector

![Endpoint Collector MDA](/Endpoint%20Collector/Screenshots/MDA-Endpoints.png)

![Endpoint Collector PBI](/Endpoint%20Collector/Screenshots/PBI-Report-Endpoints-2.png)

## Pre-requisites
- Access to an environment with the CoE Starter Kit CorevComponents instaleld
- A Power Automate Premium license for the account which will will import the solution, as well as the Power Platform Tenant Administrator role for that account
- Power BI Desktop installed to set up the report

## The solution
The Solution principally contains:
- 1 Dataverse table to store the endpoints
- 1 Cloud Flow which browses through the relevant flows from the tenant and maintain the Dataverse table
- 1 Model Driven App to browse through the collected data
- 1 Power BI Report Template to report on the collected data
  
The solution is described in more details in the [related blog post](https://powertricks.io/endpoint-collector).
Follow the steps below to set up the solution.

## Install the solution
1. Download the solution package. Both Managed and Unmanaged solutions are added so that you can pick according to your preference
2. Import the solution in the target environment
3. Configure the connection for the connectors when prompted
4. Configure the Environment Variables:
    - **Endpoint Email To**: The mailbox which will be used to send and receive email alerts if the flow fails. Recommendation to set this to a shared mailbox. Since it is used for the "To" and "From" parameters, is should be a mailbox for which the user setting up the solution has access to send emails from.
    - **Endpoint Full Inventory**: This variable does not need to be edited manually. The default value is "Yes", meaning the flow will browse through all the existing cloud flows using HTTP and HTTP With Entra Id connectors in the tenant. It will then automatically set the variable to "No", and will only loko at the flows modified in the last week. The Flow itself runs every week.
5. Turn on the Flow and run it at least once to confirm the endpoints are being added to the table

## Set up the Power BI Report Template
1. After having installed the solution and ran the flow once, open the .pbit file to create the repot with Power BI desktop
2. Set the parameter to the CoE Kit Environment url when prompted (ex "org123.crm4.dynamics.com") and click "load"
3. Confirm that the data is loaded and publish the report