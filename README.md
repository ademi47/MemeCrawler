---

## 📡 API Guide — Top Voted Posts & Report Trigger

This service exposes a minimal, clear API for:

- Retrieving Top (24h) Reddit posts (JSON).

- Triggering a report send to Telegram (and/or returning a quick status).

- (Optional) Downloading a PDF version of the 24h report.

**All endpoints return structured JSON unless otherwise stated**

🔁 Conventions

- Base URL (Prod): https://memecrawler.duckdns.org
- Swagger URL (Prod): https://memecrawler.duckdns.org/swagger/index.html

- Content-Type: application/json; charset=utf-8

- Auth: gateway/auth required in production.

User-Agent: Backend uses a custom User-Agent when calling Reddit.

**_1) Get Top Voted Posts (24h)_**

Retrieve the top posts from /r/memes for the last 24 hours (sorted by upvotes, desc).

Endpoint

```bash
GET /memes/top-24h
```

Query Parameters
| Name | Type | Default | Description |
| ------ | ------- | ------: | --------------------------------------------- |
| `take` | integer | `20` | Number of posts to return. |

_Example Requests_

```bash
# cURL (default 20)
curl -s https://memecrawler.duckdns.org/memes/top-24h | jq

# cURL (explicit size)
curl -s "https://memecrawler.duckdns.org/memes/top-24h?take=10" | jq

# HTTPie
http GET https://memecrawler.duckdns.org/memes/top-24h take==25

```

_Success Response — 200 OK_

```bash
[
  {
    "id": "1n2790j",
    "title": "She’s not ready yet",
    "author": "No-Basis-144",
    "permalink": "https://www.reddit.com/r/memes/comments/1n2790j/shes_not_ready_yet/",
    "contentUrl": "https://i.redd.it/qhkzkdpm7qlf1.jpeg",
    "upvotes": 66983,
    "numComments": 478,
    "createdUtc": "2025-08-28T09:06:16+00:00",
    "thumbnail": "https://b.thumbs.redditmedia.com/ioONiHiX6NPJa2F9vWaEiZfPN7Icu72dSH6oCKowfkE.jpg"
  },
  {
    "id": "1n2h6n7",
    "title": "The consistency",
    "author": "Luget717",
    "permalink": "https://www.reddit.com/r/memes/comments/1n2h6n7/the_consistency/",
    "contentUrl": "https://i.redd.it/h9jq9gqvfslf1.jpeg",
    "upvotes": 20749,
    "numComments": 81,
    "createdUtc": "2025-08-28T16:36:09+00:00",
    "thumbnail": "https://b.thumbs.redditmedia.com/jUck-7O6nGDL-8NOQKNGIiHadcJLnfnlnOLursw6kLU.jpg"
  }
]
```

_Error Responses_

```bash
400 Bad Request — Invalid take (e.g., negative or non-numeric).

{ "error": "Invalid 'take' value. Must be a positive integer." }
```

```bash
429 Too Many Requests — Upstream Reddit rate-limited and local retries exhausted.

{ "error": "Rate limited. Please retry shortly." }
```

```bash
502 Bad Gateway — Upstream Reddit error or parse failure.

{ "error": "Upstream fetch failed." }
```

```bash
500 Internal Server Error — Unexpected failure.

{ "error": "Unexpected server error." }
```

_Notes_

- Sorting is descending by upvotes.

- Data is retrieved live from Reddit (or our cache layer, if added).

- server-side caching can be added (e.g., 60–120s) to reduce API churn.

**_2) Trigger Telegram Report and send to Telegram Bot_**

This process the current top-24h report and send it to the configured Telegram chat. This is used by the UI button and/or n8n.

Endpoint

```bash
POST /reports/send-telegram-now
```

Request Body

```bash
No body required.
```

Example Requests

```bash
# cURL
curl -X POST https://memecrawler.duckdns.org/reports/send-telegram-now

# HTTP
http POST https://memecrawler.duckdns.org/reports/send-telegram-now
```

_Success Response — 200 OK_

```bash
{
  "status": "ok",
  "message": "Report sent to Telegram."
}
```

_Error Responses_

```bash
{
503 Service Unavailable — Telegram disabled or not configured.

{
  "error": "Telegram disabled: BotToken/ChatId not configured."
}
```

```bash
502 Bad Gateway — Telegram API failure (non-2xx from Bot API).

{ "error": "Telegram send failed." }
```

```bash
500 Internal Server Error — Any unexpected server-side exception.

{ "error": "Unexpected server error." }
```

_Notes_

This endpoint does not return the PDF; it triggers delivery to Telegram.

For automation, this can be called from n8n on a Cron schedule or a manual “Execute Workflow”.

## 🛠️ How the Background Service Stores Data in Database

A lightweight background job _MemeCrawlWorker_ runs on a schedule (e.g., every 20 minutes) and performs these steps:

- Fetch Top Posts (24h window)

- Calls Reddit API for /r/memes top posts (t=day, limit=20).

- Normalizes each record (title, media URL, upvotes, created_utc, author, permalink, etc.).

- De-duplication & Idempotency

- Computes a stable external key per post (e.g., reddit_post_id from Reddit’s id).

- Checks if that reddit_post_id already exists then insert snapshot accordingly.

- New post → insert.

- Existing post → upsert only fields that can change (e.g., upvotes, thumbnail, removed flag).

## 🔍 Live Demo — What You’ll See When “Clicking API”

**A) In the Browser (or Postman)**

```bash
1. Open: https://memecrawler.duckdns.org/reports/top-24h, Alteratively you may go to -> https://memecrawler.duckdns.org/swagger/index.html. then trigger the API via swagger.

2. You’ll see a JSON array of meme posts (title, url, upvotes, createdAt).

3. In the UI, this populates the Reports page list (with images and vote counts).
```

B) “Send Report” Button

```bash
1. Click Send Report on the Reports page.

2. The UI fires: POST /reports/send-telegram-now.

3. The API fetches current top-24h posts and formats a summary (or builds PDF).

4. Telegram Bot sends the message/document into your configured chat.

5. You see a success toast in the UI (and the new report in PDF format in Telegram).
```

_Screenshots_

![plot](./Screenshots/APIs/1-API.jpg)

![plot](./Screenshots/APIs/2-API.jpg)
