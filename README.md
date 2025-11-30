# TfoHelper

TfoHelper is a WPF application designed to automate the process of authenticating via a web portal, retrieving credentials from CyberArk using SAML, and launching "Toad for Oracle" with those credentials.

## Overview

The application streamlines the workflow for database administrators and developers by:
1.  Hosting a WebView to capture metadata and authentication tokens from a corporate portal.
2.  Intercepting SAML responses to authenticate with the CyberArk API.
3.  Securely retrieving database passwords from CyberArk.
4.  Automating the launch and login process of "Toad for Oracle".
5.  Reporting success or failure to a central monitoring service.

## Project Structure

*   **TfoHelper.App**: The main WPF application entry point and UI orchestration.
*   **TfoHelper.Configuration**: Manages loading and binding of configuration files (`appsettings.json`, `cyberark.json`, etc.).
*   **TfoHelper.CyberArk**: Handles communication with the CyberArk REST API (Logon, GetPassword).
*   **TfoHelper.Logging**: Provides a custom file-based logging service with sensitive data redaction.
*   **TfoHelper.Reporting**: Sends automation results (success/failure) to a backend reporting service.
*   **TfoHelper.ToadAutomation**: Uses UI Automation (UIA) to control the Toad for Oracle application.
*   **TfoHelper.WebViewHost**: Encapsulates the WebView2 control for web interaction and traffic interception.

## Prerequisites

*   .NET 6.0 SDK or later.
*   "Toad for Oracle" installed on the machine.
*   WebView2 Runtime (usually included with Windows 10/11).
*   Access to the corporate network for CyberArk and Reporting APIs.

## Installation

1.  Clone the repository:
    ```bash
    git clone https://github.com/your-repo/TfoHelper.git
    ```
2.  Restore dependencies:
    ```bash
    dotnet restore
    ```
3.  Build the solution:
    ```bash
    dotnet build --configuration Release
    ```

## Configuration

The application relies on several JSON configuration files which should be placed in the execution directory (e.g., `bin/Release/net6.0-windows/`).

### `appsettings.json`
Contains general application settings.
```json
{
  "InitialUrl": "https://portal.example.com/login",
  "WebViewUserDataFolder": "C:\\Temp\\TfoHelperWebView",
  "Logging": {
    "LogDirectory": "C:\\Logs\\TfoHelper",
    "MinimumLevel": "INFO"
  },
  "Timeouts": {
    "WebViewTimeoutSeconds": 120,
    "ToadLaunchTimeoutSeconds": 60,
    "ToadLoginTimeoutSeconds": 30
  },
  "Toad": {
    "ToadExecutablePath": "C:\\Program Files\\Quest Software\\Toad for Oracle\\Toad.exe",
    "Arguments": ""
  }
}
```

### `cyberark.json`
Configuration for CyberArk API behavior.
```json
{
  "DefaultApiUse": "legacy",
  "DefaultConcurrentSession": "yes"
}
```

### `vaultmap.json`
Maps vault names (captured from the portal) to specific CyberArk URLs.
```json
{
  "VAULT_A": {
    "IdpInitiatedUrl": "https://idp.example.com/init?vault=A",
    "CyberArkSamlLogonUrl": "https://cyberark-a.example.com/PasswordVault/API/auth/SAML/Logon",
    "CyberArkGetPasswordValueUrl": "https://cyberark-a.example.com/PasswordVault/API/Accounts/GetPassword"
  }
}
```

### `reporting.json`
Configuration for the reporting service.
```json
{
  "ReportingBaseUrl": "https://monitor.example.com",
  "ReportingPath": "/api/report",
  "RequireHttps": true,
  "RetryCount": 3,
  "RetryDelaySeconds": 2
}
```

## Usage

1.  Ensure all configuration files are correctly set up.
2.  Run the application (`TfoHelper.App.exe`).
3.  The application will open a window displaying the initial login page.
4.  Perform the necessary actions on the web page. The application will automatically:
    *   Capture metadata (Username, TNS, Vault Name).
    *   Redirect to the IDP.
    *   Intercept the SAML response.
    *   Authenticate with CyberArk in the background.
    *   Retrieve the password.
    *   Launch Toad and log in.
5.  Check the logs in the configured `LogDirectory` for detailed execution traces.

## Logging

Logs are written to the configured directory in JSON format. Sensitive information like passwords and SAML tokens are automatically redacted.

## License

[License Name] - See the LICENSE file for details.
