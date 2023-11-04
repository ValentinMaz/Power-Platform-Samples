# PPAC Reports Extractor
The PPAC Reports Extractor is heavily inspired from [Nicolas Kirmann's github repository to analyze requests consumption](https://github.com/Nicokirr/PPRequestAnalyzer).
The **PPAC Reports Extractor** is a free Solution to automatically download Power Platform tenant consumption reports on a daily basis and save them on SharePoint. Admins can use this to connect to such reports with Power BI and monitor platform usage across the tenant, which can in turn help them to anticipate demand, plan budget, and prevent service interruption if users over consummes their request entitlements. The reports in scope are:
- **AIByUserAndEnvironment**: AI Builder consumption
- **ApiByLicensedUser**: Power Platform Requests consumption for licensed users
- **ApiByNonLicensedUser**: Power Platform Requests consumption for non licensed users
- **ApiByFlow**: Power Platform Requests consumption for licensed flows
- **PowerPagesAnonymous**: Power Pages consumption reports for Anonymous users
- **PowerPagesAuthenticated**: Power Pages consumption reports for Authenticated users

More information about this solution can be found on this blog post: https://powertricks.io/automatically-download-tenant-reports 

## Pre-requisites
- Access to an environment with these connectors enabled: **HTTP with Azure AD**, **Office 365 Outlook** and **SharePoint**
- Environment Maker Access (or more) on that environment for the import of the solution
- An account with the an appropriate Tenant Administrator role (Power Platform Admin or Global Admin) to run the flows
- A Power Automate Premium license for this account as the Flows use a premium connector
- A SharePoint site to store the csv reports

## The solution
The Solution principally contains:
- 1 Child Flow 'Data Load Executor' to generate the reports and store them on SharePoint when prompted
- 1 Cloud Flow 'Daily Data Load Requestor' to request each report every day
- 1 Power Apps Canvas App 'PpaReports - Manual Request' to request a report for specific start and end dates:  

![PPAC Report Extractor App](https://github.com/ValentinMaz/Power-Platform-Samples/blob/b9981210dc43f19661737f318984371a9969eea6/PPAC%20Reports%20Extractor/Screenshots/PPAC%20Reports%20Extractor%20-%20App.png)
- 1 Cloud Flow 'Manual Data Load Requestor', triggered from the App to request the report generation

## Install the solution
1. Download the solution package. Both Managed and Unmanaged solutions are added so that you can pick according to your preference
2. Import the solution in the target environment
3. Configure the connection for the connectors when prompted. For the HTTP with Azure AD connector, you will need to create a connction to connect to https://licensing.powerplatform.microsoft.com/ for the first reference and to https://graph.microsoft.com/ for the second one.  

![Connection image 1](https://github.com/ValentinMaz/Power-Platform-Samples/blob/e60325a5d5918918f2960d131973d9d1fad12bc8/PPAC%20Reports%20Extractor/Screenshots/PPAC%20Reports%20Extractor%20-%20Connections%202.png)
![Connection image 2](https://github.com/ValentinMaz/Power-Platform-Samples/blob/e60325a5d5918918f2960d131973d9d1fad12bc8/PPAC%20Reports%20Extractor/Screenshots/PPAC%20Reports%20Extractor%20-%20Connections%203.png)

4. Configure the Environment Variables:
    - **PpacReportsEmailAdmins**: the email address receiving the failure alerts if a flow fails to extract a report
    - **PpacReportsSPSite**: the SharePoint site to upload the reports
    - **PpacReportsSPFolderPath<reportType>**: these variables correspond to the relative folder path where each report will be uploaded. If the folders are not already created when the flow runs, they will be automatically generated
    - **PpacReportsNumberDelay**: this variable corresponds to the delay in minutes that the flow will wait after requesting the report and prior to trying to download it. Based on the size of the tenant this delay might need to be anything from 1 minute to a lot more. For the daily extractions, between 1 and 45 minutes should be enough, according to the size of the tenant.

## Customize the solution
You can customize the Look & Feel of the App by updating the App Formulas for the colors, font size, logo, etc...

## Troubleshoot
- If you get a 'BadRequest' error when the flow "Data Load Executor" runs, it is likely that your connection references are not mapped properly.
![Bad request screenshot](https://github.com/ValentinMaz/Power-Platform-Samples/blob/e60325a5d5918918f2960d131973d9d1fad12bc8/PPAC%20Reports%20Extractor/Screenshots/PPAC%20Reports%20Extractor%20-%20Bad%20Request.png)
In this case:
    - If you are using the managed solution, go to the default solution and identify the 2 connection references called 'PpacReports - HTTP with Azure AD Graph API' and 'PpacReports - HTTP with Azure AD Licensing API'.
    - If you are using the unmanaged solution, directly identify these connection references in the solution
    - Make sure that each reference is mapped to the connection connecting to the right url (see 1. of the installation instructions above). Unfortunatley there is not an easy way to map the right connection to the right connection reference. The easiest is to use an environment with only the 2 connections for this connector that are needed by these flows and to swap them if it is not working
- If you get an error 'Not ready' when trying to download the report in the flow 'Data Load Executor', it is probably that the waiting delay is too low for the size of the report. Try to increase the amount of minutes in the variable **PpacReportsNumberDelay** (10, 30, 45, 120, ...)
![Not Ready error screenshot](https://github.com/ValentinMaz/Power-Platform-Samples/blob/e60325a5d5918918f2960d131973d9d1fad12bc8/PPAC%20Reports%20Extractor/Screenshots/PPAC%20Reports%20Extractor%20-%20Error%20NotReady.png)
- If you get the error below when trying to download the report in the flow 'Data Load Executor', it is that the report is too big. If you were trying to download the report from the Canvas App, try a shorter period of time to decrease the size of the report.
*'Http request failed as there is an error: 'Cannot write more bytes to the buffer than the configured maximum buffer size: 104857600.'*
![Size error screenshot](https://github.com/ValentinMaz/Power-Platform-Samples/blob/eab6a865ab8f02943fbd2b55596b9dfebf73c048/PPAC%20Reports%20Extractor/Screenshots/PPAC%20Reports%20Extractor%20-%20Error%20Size.png)
