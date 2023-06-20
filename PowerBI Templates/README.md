# Power BI Templates
This folder contains the Power BI templates that I share with the community. Follow the instructions for the set up. If you face some challenges during the set up please create an issue in github directly.

## AI Builder Consumption Template.pbit
![ai-builder-consumption-report-screenshot](https://github.com/ValentinMaz/Power-Platform-Samples/blob/3b5bbe7fe7e6cfa057c6f6595ca1a66a1cde51a9/PowerBI%20Templates/Screenshots/ai-builder-consumption-report-screenshot.png)

- Blog article with all details: https://powertricks.io/manage-ai-builder-consumption/
- Prerequisites:
   - Access to an Environment where the CoE Kit is installed with permissions over the admin_environment table (https://aka.ms/CoEStarterKit)
   - Access to an Environment that has not been assigned a Security Group (can be the CoE Kit environmnet or the default environment)
   - Access to some extracted AI Builder consumption reports (as explained in the blog article referenced above, they can be extracted from the Power Platform Admin Center by any one who is the System Admin of at least one environment - or tenant admin)
   - Access to a SharePoint site
   - Power BI Desktop installed
- Parameters to configure:
   - SharePointSite: the url of the site where the reports will be stored
   - SharePointFolder: the name of the folder where the reports will be stored in the site
   - DefaultEnvironmentUrl: the org Url of the default environment, or any environment without a security group assigned
   - CoEKitEnvironmentUrl: the org Url of the environment where the CoE Kit is installed
- Other steps: once the parameters are configured, the report will be refreshed pulling all the required data. The visuals are pretty self-explanatory, feel free to adjust as needed!

## API Calls Consumption Template.pbit
![power-platform-requests-consumption-report-screenshot](https://github.com/ValentinMaz/Power-Platform-Samples/blob/e9704da4aae55308a94bdc31946c5ba961c7f468/PowerBI%20Templates/Screenshots/power-platform-requests-consumption-report-screenshot.png)

- Blog article with all details: https://powertricks.io/monitor-power-platform-requests-consumption/
- Prerequisites:
   - Access to an Environment that has not been assigned a Security Group (such as the default environment if relevant)
   - Access to some extracted Power Platform consumption csv reports (as explained in the blog article referenced above, they can be extracted from the Power Platform Admin Center by any one who is the System Admin of at least one environment - or tenant admin)
   - Access to a SharePoint site
   - Power BI Desktop installed
- Parameters to configure:
   - SharePointSite: the url of the site where the reports will be stored
   - SharePointFolder: the name of the folder where the reports will be stored in the site
   - DefaultEnvironmentUrl: the org Url of the default environment, or any environment without a security group assigned
- Other steps: once the parameters are configured, the report will be refreshed pulling all the required data. The visuals are pretty self-explanatory, feel free to adjust as needed!

## My Flows & Apps Template.pbit
![my-flows-and-apps-report](https://github.com/ValentinMaz/Power-Platform-Samples/blob/59173da489d2ba313e6b56f4ba599a3a0f8540e8/PowerBI%20Templates/Screenshots/my-flows-and-apps-report.png)
![my-flows-report](https://github.com/ValentinMaz/Power-Platform-Samples/blob/59173da489d2ba313e6b56f4ba599a3a0f8540e8/PowerBI%20Templates/Screenshots/my-flows-report.png)
![myapps-report](https://github.com/ValentinMaz/Power-Platform-Samples/blob/59173da489d2ba313e6b56f4ba599a3a0f8540e8/PowerBI%20Templates/Screenshots/myapps-report.png)

- Blog article with all details: https://powertricks.io/enable-makers-to-follow-best-practices/
- Prerequisites:
   - Access to an Environment where the CoE Starter Kit is installed (https://aka.ms/CoEStarterKit)
   - Power BI Desktop installed
- Parameters to configure:
   - GuidelinesPage: the url of a page explaining the best practices to the users (SharePoint or other)
   - CoEKitEnvironmentUrl: the org Url of the environment where the CoE Kit is installed
- Other steps: once the parameters are configured, the report will be refreshed pulling all the required data. The visuals are pretty self-explanatory, feel free to adjust as needed!
