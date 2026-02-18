# SMTP Email System — MobileOpsConnect

## Overview

**Yes**, MobileOpsConnect has a built-in SMTP email service. It uses the standard .NET `System.Net.Mail.SmtpClient` to send HTML-formatted emails over a secure (SSL/TLS) connection. The default provider is **Gmail SMTP**, but any SMTP host can be configured.

---

## Architecture

The email system follows a clean **Interface → Implementation → Dependency Injection** pattern:

```
IEmailService (interface)
    └── SmtpEmailService (concrete implementation)
            └── Registered in Program.cs via DI
                    └── Injected into Controllers that need to send emails
```

### Key Files

| File | Purpose |
|------|---------|
| `Services/IEmailService.cs` | Defines the email service contract |
| `Services/SmtpEmailService.cs` | Implements the contract using `System.Net.Mail.SmtpClient` |
| `Program.cs` (line 47) | Registers the service: `builder.Services.AddScoped<IEmailService, SmtpEmailService>()` |
| `appsettings.json` | Holds SMTP host, port, credentials, and sender info |

---

## Configuration (`appsettings.json`)

The SMTP settings live under the `"Smtp"` section:

```json
"Smtp": {
    "Host": "smtp.gmail.com",
    "Port": 587,
    "Username": "",        // ← Your Gmail address (e.g., myapp@gmail.com)
    "Password": "",        // ← Gmail App Password (NOT your normal password)
    "FromEmail": "",       // ← Sender address (defaults to Username if blank)
    "FromName": "MobileOps Connect"  // ← Display name in the "From" field
}
```

### Configuration Fields

| Field | Default | Description |
|-------|---------|-------------|
| `Host` | `smtp.gmail.com` | SMTP server hostname |
| `Port` | `587` | SMTP port (587 = STARTTLS, 465 = SSL) |
| `Username` | *(empty)* | Login username / email for authentication |
| `Password` | *(empty)* | Login password (use App Password for Gmail) |
| `FromEmail` | same as `Username` | The "From" email address shown to recipients |
| `FromName` | `MobileOps Connect` | The "From" display name shown to recipients |

> **Important:** If `Username` or `Password` is empty, the service will **skip sending** and log a warning instead of crashing.

---

## How It Works (Step-by-Step)

### 1. Service Registration (Startup)

In `Program.cs`, the service is registered as a **scoped** dependency:

```csharp
builder.Services.AddScoped<IEmailService, SmtpEmailService>();
```

This means every HTTP request gets its own instance of `SmtpEmailService`.

### 2. The Interface — `IEmailService`

```csharp
public interface IEmailService
{
    Task SendEmailAsync(string toEmail, string subject, string htmlBody);
}
```

A single method: send an email with a recipient, subject, and HTML body.

### 3. The Implementation — `SmtpEmailService`

When `SendEmailAsync` is called, it:

1. **Reads configuration** — Pulls `Host`, `Port`, `Username`, `Password`, `FromEmail`, and `FromName` from `appsettings.json`.
2. **Validates credentials** — If username or password is empty, it logs a warning and returns without sending.
3. **Creates an SMTP client** — Connects to the configured host:port with SSL enabled.
4. **Builds the email** — Sets `From`, `To`, `Subject`, and the HTML `Body`.
5. **Sends the email** — Calls `SmtpClient.SendMailAsync()`.
6. **Logs the result** — Logs success or catches/logs any exception (the app does NOT crash on email failure).

### 4. Controller Injection

Controllers that need email simply inject `IEmailService` via their constructor:

```csharp
public WarehouseController(..., IEmailService emailService, ...)
{
    _emailService = emailService;
}
```

Then call it:

```csharp
await _emailService.SendEmailAsync(recipientEmail, "Subject Line", "<h2>HTML Body</h2>");
```

---

## Where Emails Are Sent (Current Usage)

### 1. Low Stock Alert — `WarehouseController.StockOut()`

**Trigger:** When a Stock Out operation causes a product's quantity to drop to or below the low-stock threshold.

**Recipient:** The support/admin email configured in `SystemSettings.SupportEmail` (falls back to `support@mobileops.com`).

**Email Content:**
```
Subject: ⚠️ Low Stock Alert: {Product Name}

Body:
  Low Stock Alert
  {Product Name} (SKU: {SKU}) is down to {quantity} units
  after stock-out — below the {threshold}-unit threshold.
  Please reorder this item as soon as possible.
  ---
  MobileOps Connect ERP — Automated Alert
```

### 2. Leave Approved — `LeaveRequestsController.Approve()`

**Trigger:** When a manager/admin approves an employee's leave request.

**Recipient:** The employee who filed the leave request.

**Email Content:**
```
Subject: ✅ Leave Approved

Body:
  Leave Approved
  Your {LeaveType} leave ({StartDate} – {EndDate}) has been approved.
  ---
  MobileOps Connect ERP
```

### 3. Leave Rejected — `LeaveRequestsController.Reject()`

**Trigger:** When a manager/admin rejects an employee's leave request.

**Recipient:** The employee who filed the leave request.

**Email Content:**
```
Subject: ❌ Leave Rejected

Body:
  Leave Rejected
  Your {LeaveType} leave ({StartDate} – {EndDate}) has been rejected.
  ---
  MobileOps Connect ERP
```

---

## How to Set Up (Gmail Example)

### Step 1: Enable 2-Step Verification on your Google Account
- Go to https://myaccount.google.com/security
- Turn on **2-Step Verification**

### Step 2: Generate an App Password
- Go to https://myaccount.google.com/apppasswords
- Select **Mail** and **Windows Computer**
- Click **Generate** — you'll get a 16-character password (e.g. `abcd efgh ijkl mnop`)

### Step 3: Update `appsettings.json`
```json
"Smtp": {
    "Host": "smtp.gmail.com",
    "Port": 587,
    "Username": "yourapp@gmail.com",
    "Password": "abcdefghijklmnop",
    "FromEmail": "yourapp@gmail.com",
    "FromName": "MobileOps Connect"
}
```

### Step 4: Run the app
Emails will now be sent automatically when the triggers above occur.

---

## Error Handling & Logging

- **No crash on failure** — The `SendEmailAsync` method wraps everything in a `try/catch`. If the email fails, it logs the error and the app continues normally.
- **Missing credentials** — If `Username` or `Password` is blank, the email is silently skipped with a warning log: `"SMTP credentials not configured. Email not sent to {ToEmail}"`.
- **Success log** — On successful send: `"Email sent to {ToEmail}: {Subject}"`.
- **Failure log** — On exception: `"Failed to send email to {ToEmail}"` with the full exception details.

---

## Flow Diagram

```
   User Action (e.g., Stock Out / Approve Leave)
           │
           ▼
   Controller calls _emailService.SendEmailAsync(to, subject, body)
           │
           ▼
   SmtpEmailService reads config from appsettings.json
           │
           ├── Credentials empty? → Log warning, return (no email sent)
           │
           ▼
   Creates SmtpClient → Connects to smtp.gmail.com:587 (SSL)
           │
           ▼
   Builds MailMessage (From, To, Subject, HTML Body)
           │
           ▼
   Sends via SmtpClient.SendMailAsync()
           │
           ├── Success → Log: "Email sent to {email}"
           └── Failure → Log error, app continues running
```

---

## Quick Reference

| What | Details |
|------|---------|
| **Protocol** | SMTP over TLS (port 587) |
| **Default Provider** | Gmail (`smtp.gmail.com`) |
| **.NET Class Used** | `System.Net.Mail.SmtpClient` |
| **Email Format** | HTML (`IsBodyHtml = true`) |
| **DI Lifetime** | Scoped (per-request) |
| **Crash on failure?** | No — errors are caught and logged |
| **Triggers** | Low stock alert, leave approved, leave rejected |
