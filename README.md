# Jellyfin Forgotten Watchlist Digest

A scheduled GitHub Actions workflow that sends you a weekly email reminder about movies and TV shows sitting unwatched in your Jellyfin library.

Every Sunday at 18:00 UTC, it queries your Jellyfin server for media that:
- Has **never been played** (play count = 0)
- Was **added more than 30 days ago**

It picks **5 at random** and sends a clean HTML email digest so you can rediscover forgotten gems.

---

## Prerequisites

- A running [Jellyfin](https://jellyfin.org/) server (self-hosted)
- A Gmail account with **2-Step Verification** enabled
- A GitHub repository (this one!) with Actions enabled

---

## Finding Your Jellyfin Credentials

### Jellyfin API Key

1. Open the Jellyfin web interface and sign in as an administrator.
2. Go to **Dashboard → API Keys** (under the "Advanced" section in the left sidebar).
3. Click **+** to create a new key, give it a name (e.g. `watchlist-digest`), and copy the key shown.

### Jellyfin User ID

Your User ID is a UUID that identifies your account in Jellyfin.

1. Go to **Dashboard → Users** and click your user.
2. The URL in your browser will look like:
   ```
   https://jellyfin.example.com/web/index.html#!/useredit.html?userId=abcdef12-3456-7890-abcd-ef1234567890
   ```
3. Copy the value of the `userId` query parameter — that's your User ID.

Alternatively, call the API directly:
```
GET https://your-jellyfin-server/Users
Headers: X-Emby-Token: <your-api-key>
```
The response is a JSON array; find your username and copy the `"Id"` field.

---

## Generating a Gmail App Password

> **Do not use your regular Gmail password.** GitHub Actions would have access to it, and Google may block sign-ins from unknown locations. App Passwords are purpose-built for this.

1. Go to your [Google Account](https://myaccount.google.com/) and ensure **2-Step Verification** is turned on.
2. Visit **[Google App Passwords](https://myaccount.google.com/apppasswords)**.
3. Select **Mail** as the app and **Other (custom name)** as the device; type something like `jellyfin-digest`.
4. Click **Generate** and copy the 16-character password shown (it won't be displayed again).

---

## Adding GitHub Secrets

The workflow reads all sensitive values from [GitHub Actions secrets](https://docs.github.com/en/actions/security-guides/encrypted-secrets) so nothing private is ever stored in the repository.

1. Go to your repository on GitHub.
2. Click **Settings → Secrets and variables → Actions**.
3. Click **New repository secret** for each of the following:

| Secret name | Example value | Description |
|---|---|---|
| `JELLYFIN_URL` | `https://jellyfin.example.com` | Base URL of your Jellyfin server. No trailing slash. |
| `JELLYFIN_API_KEY` | `abc123...` | API key created in the Jellyfin dashboard. |
| `JELLYFIN_USER_ID` | `abcdef12-3456-...` | UUID of the Jellyfin user whose library to query. |
| `GMAIL_ADDRESS` | `you@gmail.com` | Gmail address used to send the email. |
| `GMAIL_APP_PASSWORD` | `abcd efgh ijkl mnop` | 16-character Gmail App Password (not your login password). |
| `RECIPIENT_EMAIL` | `you@example.com` | Where the digest should be delivered. Can be the same as `GMAIL_ADDRESS`. |

---

## Triggering a Manual Run

You don't have to wait until Sunday to test it.

1. Go to your repository on GitHub and click the **Actions** tab.
2. Select **Jellyfin Watchlist Digest** from the left-hand workflow list.
3. Click **Run workflow** → **Run workflow** (confirm on the dropdown).
4. Watch the run complete in real time by clicking into it.

If everything is configured correctly you'll receive the digest email within a minute or two.

---

## How It Works

```
digest.py
  │
  ├─ Reads environment variables (all six secrets above)
  │
  ├─ Calls GET /Users/{userId}/Items on your Jellyfin server
  │    Filters: Recursive, Movie+Series, IsUnplayed
  │    Fields:  DateCreated, Name, ProductionYear, Overview
  │
  ├─ Keeps only items added more than 30 days ago
  │
  ├─ Randomly picks up to 5
  │
  └─ Sends an HTML email via Gmail SMTP (port 587, STARTTLS)
```

The script uses only the Python standard library for email (`smtplib`, `email.mime`) and a single third-party dependency (`requests`) for the HTTP call.

---

## Customisation

| What to change | Where |
|---|---|
| Number of items in digest | `DIGEST_COUNT` constant in `digest.py` |
| Minimum age (days) | `MIN_DAYS_IN_LIBRARY` constant in `digest.py` |
| Overview snippet length | `OVERVIEW_MAX_LENGTH` constant in `digest.py` |
| Schedule | `cron` expression in `.github/workflows/digest.yml` |
| Recipient address | `RECIPIENT_EMAIL` secret |

---

## Troubleshooting

**`Could not connect to Jellyfin`** — Check that `JELLYFIN_URL` is reachable from the internet (GitHub Actions runners have no access to private networks). If your Jellyfin is behind a VPN or LAN-only, you'll need to expose it or run the workflow on a self-hosted runner.

**`Gmail authentication failed`** — Ensure you are using an App Password, not your regular Gmail password, and that 2-Step Verification is enabled on your Google account.

**No email received** — Run the workflow manually and inspect the logs. If the script prints "Nothing to report" it means all your unplayed items were added less than 30 days ago — try lowering `MIN_DAYS_IN_LIBRARY` temporarily to verify end-to-end.
