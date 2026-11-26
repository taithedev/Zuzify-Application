# Zuzify
Native Windows security workspace, independently maintained in C# / WPF. No Electron, browser runtime or Lovable dependency.

## Build and run
Install the .NET 8 SDK on Windows, open Zuzify.csproj in Visual Studio 2022 with the .NET desktop development workload, or run:
```powershell
dotnet build -c Release
dotnet run
dotnet publish -c Release -r win-x64 --self-contained true
```

## Implemented
- Local SHA-256 file verification and checksum comparison
- Cryptographically random password generation
- Read-only device diagnostics
- Session security checklist
- Generic activity log and text export
- Theme accent selection and animated page transitions
- Plan selection preview

## Proposed pricing (USD)
| Plan | Price |
|---|---|
| Monthly | $25/month |
| Standard | $45 one-time |
| Pro | $85 one-time |
| Elite | $125 one-time |
| Ultimate | $200 one-time |

This is a development preview. Prices are informational, feature tiers are not enforced, and there is no live billing, account backend, license validation or updater. Do not sell this preview as a completed product. A production launch requires tested entitlement handling, clear refund and renewal terms, and an eligible payment account.

## Responsible use
Use the application for defensive security and files or devices you are authorized to inspect. No unauthorized account access, exploit automation, credential theft, or interference with other services is supported. A disclaimer does not remove legal obligations. No file contents, passwords or paths are uploaded by this application.
