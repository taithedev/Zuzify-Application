# Zuzify 0.2.0 — development preview
A native Windows WPF host with a bundled React UI using Microsoft's WebView2 runtime, not Electron or Lovable. All application source is in this repository.

## Run on Windows
Install .NET 8 SDK, Node.js 22.13+ and Microsoft Edge WebView2 Runtime.
In one terminal:
```powershell
cd server
npm install
npm start
```
In another, from the repo root:
```powershell
cd ui
npm install
npm run build
cd ..
dotnet run
```
Publish with `dotnet publish -c Release -r win-x64 --self-contained true`. The CI workflow builds and uploads a Windows artifact.

## Zuzify licenses
Register an account in the application. In another terminal in server, with the same database and working directory as the service:
```powershell
npm run issue -- create standard
```
Redeem the printed ZUZIFY code in Account. Codes are random, stored hashed, single-use and account-bound; monthly manual codes expire 30 days from issuance. To revoke a manual code: `npm run issue -- revoke KEY`. Licensed tools check the server before every action. This is not tamper-proof DRM; locally running code can be modified.

## Plans (USD)
Monthly $25/month, Standard $45, Pro $85, Elite $125, Ultimate $200 one-time. All currently grant identical access.
Optional Stripe **test mode only**: configure STRIPE_SECRET_KEY (sk_test_), STRIPE_WEBHOOK_SECRET and CHECKOUT_RETURN_URL through environment variables. Send Stripe test events to /webhook, including checkout.session.completed and customer.subscription.created/updated/deleted. Only signed test events grant entitlements. Live keys are rejected. No real payments are enabled.
For production, an eligible adult account holder must configure the payment account and terms, deploy the API behind HTTPS, and complete security and billing lifecycle testing. Use ZUZIFY_API_URL to select the trusted service endpoint.

## Tools
SHA-256 file verification, local password generator, device information, session checklist, activity report and accent themes. No account takeover, exploit automation or unauthorized access tools.

## Limitations
Development SQLite storage; no password recovery, MFA, updater, feature-tier differentiation, durable session login, production deployment or live billing yet. Tests cover core license behavior, not real Stripe webhooks or Windows UI. Do not sell this preview as a complete product.
