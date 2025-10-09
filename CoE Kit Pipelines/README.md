# CoE Kit Pipelines
This repository provides reusable Azure DevOps pipelines specifically designed to accelerate and simplify the upgrade and deployment process for the Microsoft Center of Excellence (CoE) Starter Kit in Power Platform environments.

While these pipelines are tailored for the CoE Kit core components, they can easily be extended to support additional solutions as needed.

Microsoft recommends updating the CoE Kit at least every 3 months to benefit from the latest features, improvements, and security updates. These pipelines help Power Platform admins automate and standardize the upgrade process, making it faster, more reliable, and less error-prone.

For more information about the use case, upgrade strategies, and best practices, visit [PowerTricks.io](https://powertricks.io/coe-kit-pipelines).

## Pre-requisites — CoE Starter Kit ALM with Azure DevOps

**Purpose:** This README lists everything you need **before** running the below pipelines in this repo:
- **sync.yml**
- **generate-sampleSettings.yml**
- **deploy.yml**

---

## 1) Power Platform setup

### 1.1 Environments (Dataverse with database)
Provision **three** environments (each **with Dataverse** and **PCF components** enabled):

- **CoE Source** — CoE Starter Kit solution “as is” (managed), used for reference & settings generation.  
- **CoE Dev** — CoE + **your customizations** (develop unmanaged here).  
- **CoE Prod** — **Managed** only (what runs in production).
(A fourth environment "CoE Test" can help test the deployment from Dev before reaching Production)

**Tips**
- Use clear environment names/URLs (e.g., `coe-source`, `coe-dev`, `coe-prod`).
- Make sure **Audit/Telemetry** environment settings match your governance requirements.

### 1.2 Creator Kit in each environment
Install the **Creator Kit** in **Source**, **Dev**, and **Prod** (some CoE components rely on its PCF/controls, you can [download it here](https://aka.ms/creatorkitdownload)).

**Notes**
- Ensure **code components** are enabled in each environment before installing.
- Install the [**latest managed**](https://aka.ms/creatorkitdownload) Creator Kit packages to all environments.

---

## 2) Azure DevOps setup

### 2.1 Organization, Project & Repo
- [Azure DevOps **Organization**](https://learn.microsoft.com/en-us/azure/devops/organizations/accounts/create-organization?view=azure-devops) created.
- A **Project** with an **Azure Repos Git** repository, preferably an empty repository.

### 2.2 Hosted parallel jobs (free grant if needed)
- Ensure the org has at least **1 Microsoft-hosted parallel job**.  
  You can request the [**free grant** from Microsoft](https://aka.ms/azpipelines-parallelism-request).

### 2.3 Install **Microsoft Power Platform Build Tools**
Install the official [**Microsoft Power Platform Build Tools**](https://learn.microsoft.com/en-us/power-platform/alm/devops-build-tools#get-microsoft-power-platform-build-tools) extension in your ADO org.  

### 2.4 Service connections (one per environment)
Create **one Service Connection per environment** of type **Power Platform**:

- **App registration (Entra ID):**  
  - Create **one app per environment** (recommended) → `coe-source-spn`, `coe-dev-spn`, `coe-prod-spn`.  
  - Add a **client secret** for each; store securely.

- **Application User (Dataverse):**  
  - In **each environment**, create an **Application User** that points to the respective app registration.  
  - Assign the app user the **System Administrator** role to avoids import failures (can eventually tighten if all the permissions are not required).

- **Create the Service Connection** in ADO: 
  - Type: **Power Platform**  
  - Point it to the environment URL, application (client) ID, tenant ID, and client secret.  
  - Name it clearly, e.g.: `CoE_SOURCE`, `CoE_DEV`, `CoE_PROD`.

### 2.5 Repository permissions for pipelines (branch & PR)
Pipelines push branches and open PRs using `System.AccessToken`.  
Grant the **Build Service** identity access on your repo:

1. Go to **Project settings → Repositories → (your repo) → Security**.  
2. Select **`<Project Name> Build Service (<Org Name>)`**.  
3. **Allow** at least:
   - **Read**
   - **Contribute**
   - **Create branch**
   - **Contribute to pull requests**
   - (Optional) **Create tag** if you tag releases
4. If you enforce branch policies on `main`, ensure the Build Service can **push branches** and **create PRs** (not necessarily bypass policies).

---


## 3) Setting up the pipelines in your repository

1. **Create a `pipelines` folder** in your own repository (e.g., `/pipelines`).
2. For each pipeline you want to use, **create a new `.yml` file** in your `pipelines` folder (e.g., `pipelines/sync-coe.yml`).
3. **Copy and paste the content** of the corresponding YAML file from this repository into your new file.
4. **Before creating the pipeline**, review the comments at the top of each YAML script you copied. Make any necessary adjustments (such as service connection names, environment variables, or file paths) to match your setup.
5. In Azure DevOps, **create a new pipeline** for each YAML file:
   - Select "Existing Azure Pipelines YAML file" when prompted.
   - Point to the relevant YAML file in your `pipelines` folder (e.g., `pipelines/sync-coe.yml`).
   - Repeat for each pipeline you want to use (e.g., Sync CoE, Generate Settings, Pack & Deploy).

---

## 4) Variables to set in the pipelines

**Sync**
- `solutionName` — GitHub release asset prefix to download (e.g., `CenterofExcellenceCoreComponents`).
- `outputFolder` — where to unpack the solution.
- `settingsFolder` — location of settings files.
- `targetBranch` — your repository’s default branch (e.g., `main`).

**Generate Sample-Settings`**
- `parameters.serviceConnection` — environment to read live values from (typically **Dev**).
- `parameters.settingsFile` — template used to shape the output fields.
- `parameters.outputFile` — destination path in the repo.
- `parameters.targetBranch` — branch to PR into.

**Deploy**
- `parameters.target` — `source | dev | prod`.
- Mapping: **service connection** & **valuesFile** for each environment.
- Paths: `solutionSourceFolder`, `outputDir`, `managedZip`, `settingsTempFile`.

---

## 5) OPTIONAL: Quick validation checklist (do these once)

1. **Build Tools installed**  
   - Add `PowerPlatformToolInstaller@2` at the top of a test pipeline and confirm `pac` runs (`pac -v`).

2. **Service connection works**  
   - Add `PowerPlatformWhoAmI@2` with each environment’s service connection; verify it succeeds.

3. **OAuth token available**  
   - In a test pipeline, echo PRs via REST with `$(System.AccessToken)` (or just run one of these pipelines).  
   - If empty/unauthorized, enable **“Allow scripts to access the OAuth token.”**

4. **Repo permissions correct**  
   - From a pipeline run, create a **test branch** and PR.  
   - If permission denied, grant the **Build Service** the rights listed in §2.5.

5. **Hosted parallel jobs**  
   - If runs remain queued with “no hosted parallelism,” request the free grant.

---

## 6) Summary - Order of operations (first-time setup)

1. Create environments (Source, Dev, Prod) **with Dataverse**.  
2. Enable code components & install **Creator Kit** in all three.  
3. Create App Registrations + client secrets (one per environment).  
4. Create **Application Users** in each environment; assign **System Administrator**.  
5. Create **Power Platform (SPN)** service connections in ADO (one per environment).  
6. Install **Microsoft Power Platform Build Tools** in the ADO org.  
7. Grant repo permissions to **Build Service** identity (§2.5).  
8. Run **Sync** pipeline → review PR.  
9. Run **Generate-SampleSettings** → review PR.  
10. Run **Deploy** (target `source`, then `dev` then `prod`).
