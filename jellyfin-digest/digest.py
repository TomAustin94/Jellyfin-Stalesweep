"""
Jellyfin Forgotten Watchlist Digest
------------------------------------
Queries your Jellyfin library for movies and TV shows that have never been
played and were added more than 30 days ago. Picks 5 at random and sends
a weekly digest email via Gmail SMTP.

Required environment variables:
    JELLYFIN_URL          - Base URL of your Jellyfin server (e.g. https://jellyfin.example.com)
    JELLYFIN_API_KEY      - Jellyfin API key
    JELLYFIN_USER_ID      - Jellyfin user ID (UUID)
    GMAIL_ADDRESS         - Gmail address to send from
    GMAIL_APP_PASSWORD    - Gmail App Password (not your regular password)
    RECIPIENT_EMAIL       - Email address to send the digest to
"""

import os
import random
import smtplib
import sys
from datetime import datetime, timedelta, timezone
from email.mime.multipart import MIMEMultipart
from email.mime.text import MIMEText

import requests


# ---------------------------------------------------------------------------
# Configuration
# ---------------------------------------------------------------------------

JELLYFIN_URL = os.environ.get("JELLYFIN_URL", "").rstrip("/")
JELLYFIN_API_KEY = os.environ.get("JELLYFIN_API_KEY", "")
JELLYFIN_USER_ID = os.environ.get("JELLYFIN_USER_ID", "")
GMAIL_ADDRESS = os.environ.get("GMAIL_ADDRESS", "")
GMAIL_APP_PASSWORD = os.environ.get("GMAIL_APP_PASSWORD", "")
RECIPIENT_EMAIL = os.environ.get("RECIPIENT_EMAIL", "")

# Items must have been in the library for at least this many days to be included
MIN_DAYS_IN_LIBRARY = 30

# Number of items to feature in the digest
DIGEST_COUNT = 5

# Maximum length for the overview/synopsis snippet
OVERVIEW_MAX_LENGTH = 150


# ---------------------------------------------------------------------------
# Jellyfin API helpers
# ---------------------------------------------------------------------------

def fetch_unwatched_items():
    """
    Fetch all unplayed movies and TV series from the Jellyfin library.
    Returns a list of item dicts on success, raises on failure.
    """
    endpoint = f"{JELLYFIN_URL}/Users/{JELLYFIN_USER_ID}/Items"
    params = {
        "Recursive": "true",
        "IncludeItemTypes": "Movie,Series",
        "Filters": "IsUnplayed",
        "Fields": "DateCreated,Name,ProductionYear,Overview",
        "api_key": JELLYFIN_API_KEY,
    }

    print(f"Querying Jellyfin at {JELLYFIN_URL} …")
    try:
        response = requests.get(endpoint, params=params, timeout=30)
        response.raise_for_status()
    except requests.exceptions.ConnectionError:
        print(f"ERROR: Could not connect to Jellyfin at {JELLYFIN_URL}. "
              "Check that JELLYFIN_URL is correct and the server is reachable.")
        sys.exit(1)
    except requests.exceptions.Timeout:
        print("ERROR: Request to Jellyfin timed out. The server may be overloaded.")
        sys.exit(1)
    except requests.exceptions.HTTPError as exc:
        print(f"ERROR: Jellyfin returned an HTTP error: {exc}")
        sys.exit(1)

    data = response.json()
    items = data.get("Items", [])
    print(f"  → {len(items)} unplayed items returned by the API.")
    return items


def filter_old_items(items):
    """
    Keep only items that have been in the library for more than MIN_DAYS_IN_LIBRARY days.
    """
    cutoff = datetime.now(timezone.utc) - timedelta(days=MIN_DAYS_IN_LIBRARY)
    filtered = []

    for item in items:
        date_created_str = item.get("DateCreated")
        if not date_created_str:
            # Skip items with no creation date
            continue

        # Jellyfin returns ISO 8601 timestamps like "2024-01-15T10:30:00.0000000Z"
        try:
            # Python 3.11 fromisoformat handles the trailing Z; for older
            # versions we normalise it to +00:00 first.
            date_created_str = date_created_str.replace("Z", "+00:00")
            date_created = datetime.fromisoformat(date_created_str)
        except ValueError:
            print(f"  WARNING: Could not parse DateCreated '{date_created_str}' "
                  f"for '{item.get('Name')}'. Skipping.")
            continue

        if date_created < cutoff:
            filtered.append(item)

    print(f"  → {len(filtered)} items added more than {MIN_DAYS_IN_LIBRARY} days ago.")
    return filtered


def pick_random_items(items, count=DIGEST_COUNT):
    """Randomly select up to `count` items from the list."""
    return random.sample(items, min(count, len(items)))


# ---------------------------------------------------------------------------
# Email formatting
# ---------------------------------------------------------------------------

def truncate(text, max_length=OVERVIEW_MAX_LENGTH):
    """Truncate text to max_length characters, appending '…' if cut."""
    if not text:
        return "No description available."
    text = text.strip()
    if len(text) <= max_length:
        return text
    return text[:max_length].rstrip() + "…"


def build_html_email(items):
    """
    Build a clean, readable HTML email body for the digest.
    """
    today = datetime.now().strftime("%B %d, %Y")

    # Build one card per item
    cards_html = ""
    for i, item in enumerate(items, start=1):
        name = item.get("Name", "Unknown Title")
        year = item.get("ProductionYear", "")
        year_str = f" ({year})" if year else ""
        item_type = item.get("Type", "")
        type_badge = "🎬 Movie" if item_type == "Movie" else "📺 Series"
        overview = truncate(item.get("Overview", ""))

        cards_html += f"""
        <tr>
          <td style="padding: 0 0 24px 0;">
            <table width="100%" cellpadding="0" cellspacing="0" border="0"
                   style="background:#f9f9f9; border-radius:8px; border:1px solid #e8e8e8;">
              <tr>
                <td style="padding:20px 24px;">
                  <p style="margin:0 0 4px 0; font-size:13px; color:#888;">{type_badge}</p>
                  <h2 style="margin:0 0 6px 0; font-size:18px; color:#1a1a1a;
                             font-weight:700;">{i}. {name}{year_str}</h2>
                  <p style="margin:0; font-size:14px; color:#555; line-height:1.5;">
                    {overview}
                  </p>
                </td>
              </tr>
            </table>
          </td>
        </tr>"""

    html = f"""<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>Your Jellyfin Watchlist Digest</title>
</head>
<body style="margin:0; padding:0; background:#ffffff; font-family: -apple-system,
             BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;">

  <table width="100%" cellpadding="0" cellspacing="0" border="0"
         style="background:#ffffff; padding: 32px 16px;">
    <tr>
      <td align="center">
        <table width="600" cellpadding="0" cellspacing="0" border="0"
               style="max-width:600px; width:100%;">

          <!-- Header -->
          <tr>
            <td style="padding:0 0 32px 0; border-bottom: 2px solid #f0f0f0;">
              <h1 style="margin:0; font-size:26px; color:#1a1a1a; font-weight:800;">
                🎬 Your Forgotten Watchlist
              </h1>
              <p style="margin:8px 0 0 0; font-size:14px; color:#888;">{today}</p>
            </td>
          </tr>

          <!-- Intro -->
          <tr>
            <td style="padding:24px 0 16px 0;">
              <p style="margin:0; font-size:15px; color:#444; line-height:1.6;">
                Hey! Here are <strong>{len(items)} things sitting in your Jellyfin library
                that you've never watched</strong>. They've been waiting patiently for over
                a month — maybe one of them is perfect for tonight?
              </p>
            </td>
          </tr>

          <!-- Item cards -->
          <tr>
            <td style="padding:8px 0 0 0;">
              <table width="100%" cellpadding="0" cellspacing="0" border="0">
                {cards_html}
              </table>
            </td>
          </tr>

          <!-- Footer -->
          <tr>
            <td style="padding:24px 0 0 0; border-top:2px solid #f0f0f0;">
              <p style="margin:0; font-size:12px; color:#aaa; line-height:1.5;">
                This digest is sent automatically every Sunday from your Jellyfin
                Watchlist Digest workflow. Items shown were added more than 30 days ago
                and have never been played. Happy watching! 🍿
              </p>
            </td>
          </tr>

        </table>
      </td>
    </tr>
  </table>

</body>
</html>"""

    return html


# ---------------------------------------------------------------------------
# Email sending
# ---------------------------------------------------------------------------

def send_email(html_body, item_count):
    """
    Send the digest email via Gmail SMTP using TLS (port 587).
    """
    subject = f"🎬 Your Jellyfin watchlist — {item_count} things you've forgotten"

    # Build the MIME message
    msg = MIMEMultipart("alternative")
    msg["Subject"] = subject
    msg["From"] = GMAIL_ADDRESS
    msg["To"] = RECIPIENT_EMAIL

    # Attach HTML content (clients that support HTML will use this)
    msg.attach(MIMEText(html_body, "html", "utf-8"))

    print(f"Sending digest email to {RECIPIENT_EMAIL} …")
    try:
        with smtplib.SMTP("smtp.gmail.com", 587) as server:
            server.ehlo()
            server.starttls()
            server.ehlo()
            server.login(GMAIL_ADDRESS, GMAIL_APP_PASSWORD)
            server.sendmail(GMAIL_ADDRESS, RECIPIENT_EMAIL, msg.as_string())
    except smtplib.SMTPAuthenticationError:
        print("ERROR: Gmail authentication failed. "
              "Make sure you're using an App Password (not your regular Gmail password) "
              "and that 2-Step Verification is enabled on your Google account.")
        sys.exit(1)
    except smtplib.SMTPException as exc:
        print(f"ERROR: SMTP error while sending email: {exc}")
        sys.exit(1)

    print("  → Email sent successfully!")


# ---------------------------------------------------------------------------
# Validation
# ---------------------------------------------------------------------------

def check_env():
    """Ensure all required environment variables are set before doing anything."""
    required = {
        "JELLYFIN_URL": JELLYFIN_URL,
        "JELLYFIN_API_KEY": JELLYFIN_API_KEY,
        "JELLYFIN_USER_ID": JELLYFIN_USER_ID,
        "GMAIL_ADDRESS": GMAIL_ADDRESS,
        "GMAIL_APP_PASSWORD": GMAIL_APP_PASSWORD,
        "RECIPIENT_EMAIL": RECIPIENT_EMAIL,
    }
    missing = [name for name, value in required.items() if not value]
    if missing:
        print(f"ERROR: The following required environment variables are not set: "
              f"{', '.join(missing)}")
        sys.exit(1)


# ---------------------------------------------------------------------------
# Entry point
# ---------------------------------------------------------------------------

def main():
    print("=== Jellyfin Forgotten Watchlist Digest ===\n")

    # 1. Validate config
    check_env()

    # 2. Fetch unplayed items from Jellyfin
    all_items = fetch_unwatched_items()

    # 3. Filter to items added more than 30 days ago
    old_items = filter_old_items(all_items)

    if not old_items:
        print("\nNothing to report — your library has no items that are both unplayed "
              "and more than 30 days old. No email will be sent.")
        return

    # 4. Pick a random selection
    selected = pick_random_items(old_items)
    print(f"\nSelected {len(selected)} item(s) for the digest:")
    for item in selected:
        year = item.get("ProductionYear", "?")
        print(f"  • {item.get('Name')} ({year})")

    # 5. Build and send the email
    html_body = build_html_email(selected)
    send_email(html_body, len(selected))

    print("\nDone! 🍿")


if __name__ == "__main__":
    main()
