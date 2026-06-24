# AISO-Teams SAP Module — Development Setup

This guide explains how to set up the SAP ABAP development environment and synchronize code using abapGit.

## Prerequisites

- Access to SAP S/4HANA System (development client).
- SAP GUI installed on your local machine.
- Developer key and authorization to create Z-objects.

## 1. Install abapGit (If not already installed)

abapGit is an open-source Git client for ABAP.

1. Download the standalone `zabapgit.abap` program from the [official abapGit repository](https://abapgit.org).
2. Open SAP GUI and go to transaction `SE38`.
3. Create a new program named `ZABAPGIT`, paste the downloaded code, and activate it.

## 2. Connect the Repository

1. Execute transaction `ZABAPGIT`.
2. Click on **New Online** to clone the repository.
3. Provide the GitHub repository URL: `https://github.com/LongTran2003/aiso-teams.git`.
4. Specify the package (e.g., `$AISO` or a transportable Z-package) where the code will be deployed.
5. In the **Folder Logic** section, specify the starting folder as `/sap/abap/`.
6. Enter your GitHub credentials (use a Personal Access Token as the password).

## 3. Pulling Code

1. Once the repository is linked, abapGit will show a diff between the GitHub code and your SAP system.
2. Click **Pull** to import the ABAP artifacts (CDS Views, AMDP, RAP Services, Tables) into your SAP system.
3. Activate all imported objects via transaction `SE80` or Eclipse ADT.

## 4. Pushing Changes

When you make changes to the ABAP code in SAP:

1. Open `ZABAPGIT`.
2. Review the detected changes in the repository.
3. Click **Stage**, select the objects you want to commit.
4. Enter a commit message following the team's convention (e.g., `feat(sap): update Sales Order CDS view`).
5. Click **Commit** to push the changes back to GitHub.

## 5. Postman OData Testing

To test the OData services generated via RAP:
1. Import the Postman collections located in `sap/postman/`.
2. Set up your environment variables in Postman (Base URL, SAP Username, SAP Password).
3. Ensure you fetch the `x-csrf-token` before making POST/PUT requests.
