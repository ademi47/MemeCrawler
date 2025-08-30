# MemeCrawler Project

## 📖 Project Overview

The **Meme Report System** is a full-stack application that automatically collects the **top memes from Reddit (/r/memes) in the last 24 hours**, stores and serves them via a **.NET 8 Web API**, displays them in a **React/Next.js frontend**, and delivers a **daily PDF report to Telegram** using **n8n automation**.

This project was designed and developed as a **technical assignment** (showcasing backend, frontend, and workflow automation).

Key highlights:

---

- **Backend**: ASP.NET 8 Web API with Reddit OAuth integration.
- **Frontend**: React/Next.js UI to browse and trigger reports.
- **Automation**: n8n cron workflow that generates & sends Telegram reports.
- **Deployment**: Runs locally with Docker Compose or on AWS (EC2, ECS, Amplify).

---

## Project Objectives

1. Create a webservice that crawls https://www.reddit.com/r/memes/ and returns top 20 voted posts for the past 24 hours. Sorted by top voted post first, descending order.

2. Stores the crawled data into a database for historical tracking and future data visualization.

3. Present and generate a report file for past 24 hrs top 20 trending memes that can be sent as a file via a Telegram Chatbot.

4. Create a presentation deck to showcase live demo and explain both frontend and backend designs.

5. Suggest 3 alternative use cases or actionable insights from the generated report.

---

## Tech Stack

**Client:** React, Next, TailwindCSS

**Server:** Node, Express

**Automation:** n8n, Telegram

**Code Versioning:** Github

**Production Deployment:** AWS Amplify, AWS E2

**Prompt Engineering:** Chat GPT 5

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

### 1) Get Top Voted Posts (24h)

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

### 2) Trigger Telegram Report and send to Telegram Bot

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

🤖 How We Fetch Top 20 Memes (Past 24h)

🔗 API Endpoint Used

```bash
https://www.reddit.com/r/memes/top/.json?t=day&limit=20
```

- /r/memes → the subreddit we’re tracking

- /top → fetches posts sorted by upvotes

- .json → requests a JSON response instead of HTML

- t=day → restricts results to posts from the past 24 hours

- limit=20 → fetch only the top 20 posts

🛡️ Authentication & Headers

For production usage, I integrated with Reddit’s OAuth2 API at:

```bash
https://oauth.reddit.com/r/memes/top?t=day&limit=20
```

This requires:

- Client ID + Client Secret from a Reddit “Script App”

- Username + Password (for server-side password grant)

- A proper User-Agent string (Reddit requires this)

e.g.:

```bash
MemeCrawler/1.0 (by u:Ademi47)
```

📥 Example JSON Response (trimmed)

```bash
{
  "data": {
    "children": [
      {
        "data": {
          "id": "abc123",
          "title": "Did you know my Internet name is Ademi47",
          "url": "https://i.redd.it/ademi.png",
          "ups": 6743,
          "created_utc": 1725021123,
          "permalink": "/r/memes/comments/abc123/Did_you_know_my_Internet_name_is_Ademi47/"
        }
      }
    ]
  }
}
```

🗄️ What I Store

**From each Reddit post, I extract and persist:**

- id → stable Reddit post ID (used as unique key in DB)

- title → post title

- url → direct media or link

- ups → upvote count

- created_utc → original Reddit timestamp

- permalink → Reddit link to the post

**This data is written into:**

- memes → canonical table (latest state of each post)

- memesnapshots → time-stamped snapshot (who was top at each 10-minute run)

---

## 🛠️ MemeCrawler Service (Back end)

A lightweight background job _MemeCrawlWorker_ runs on a schedule (e.g., every 20 minutes) and performs these steps:

- Fetch Top Posts (24h window)

- Calls Reddit API for /r/memes top posts (t=day, limit=20).

- Normalizes each record (title, media URL, upvotes, created_utc, author, permalink, etc.).

- De-duplication & Idempotency

- Computes a stable external key per post (e.g., reddit_post_id from Reddit’s id).

- Checks if that reddit_post_id already exists then insert snapshot accordingly.

- New post → insert.

- Existing post → upsert only fields that can change (e.g., upvotes, thumbnail, removed flag).

---

## 💻 Front End Guide

### 🎨 UI Tech Stack & Features

The frontend is built with **Next.js** and **Tailwind CSS**, designed to provide a clean, responsive, and interactive user interface for exploring and managing meme reports.

---

### 🛠️ Tech Stack

#### **Framework: Next.js (React)**

- Provides **server-side rendering (SSR)** and **static site generation (SSG)** for fast loads.
- Simplifies **API integration** via `fetch` in client/server components.
- Supports environment variables (e.g. `NEXT_PUBLIC_API_BASE`) to easily switch between **local** and **production APIs**.

#### **Styling: Tailwind CSS**

- Utility-first CSS framework for **fast styling**.
- Consistent, responsive layouts without writing custom CSS.
- Easy to apply hover/active states for buttons and interactive components.

#### **HTTP Requests: Axios / fetch**

- Used to call backend APIs (`/reports/top-24h` and `/reports/send-telegram-now`).
- Error handling and loading states included for better UX.

### **UI Components**

- Built with **React functional components**.
- Structured into reusable components: _ReportCard_, _ReportsPage_, _SendReportButton_.

---

### 🌟 Features

#### 1. **Memes Page**

- Displays the **Top 20 Memes (24h)** retrieved from the backend API.
- Each report card shows:
  - Meme **title**
  - Meme **upvotes** count
  - **Image preview** (from Reddit post URL)
- Responsive grid layout for desktop and mobile.

---

#### 2. **Send Report Button**

- A **call-to-action button** triggers the backend API `/reports/send-telegram-now`.
- When clicked, the system generates the **PDF report** and delivers it to Telegram.
- Button styled with Tailwind for **active/hover states** (blue → lighter on hover).

---

#### 3. **CORS & API Integration**

- Configured `NEXT_PUBLIC_API_BASE` in `.env` to point to backend API.
- Added backend **CORS policy** (`AllowAnyOrigin`, `AllowAnyHeader`, `AllowAnyMethod`) to allow frontend requests.
- Ensures smooth communication between frontend and backend in dev and prod.

---

#### 4. **Error Handling & Debugging**

- UI gracefully handles:
  - **Empty data** (e.g., if API fails).
  - **CORS errors** → logs helpful messages in the console.
  - **API errors** → displays fallback states.

---

#### 5. **Deployment-Ready**

- Supports **local dev** (`npm run dev`) with `.env.local`.
- Easy deployment to **Vercel** or **AWS Amplify**.
- Uses environment variables for production-ready configuration.

---

### 🚀 Future Enhancements

- Add **search and filter** (e.g., search by keyword in meme titles).
- Add **sorting controls** (sort by upvotes, createdAt).
- Paginate historical reports pulled from `memesnapshots` table.
- Dark/light mode toggle with Tailwind’s theme support.

---

👉 In short, the **UI** provides a simple but powerful way to:

- Browse **top memes from Reddit**.
- Trigger automated **report generation**.
- Seamlessly integrate with the backend and n8n workflows.

---

## 📔 GEN AI Prompts and Usage

Below are some of the key GEN-AI prompts that I used in this project.

**1. Frontend with Next.js + Tailwind + Troubleshooting**

- “I’m building a frontend with Next.js and Tailwind CSS. Show me how to structure a Reports page that fetches from my API and displays data in cards. Include a Send Report button that calls a POST endpoint. Make it responsive.”

- “I’m getting a CORS error in my Next.js frontend when calling my backend API. Explain why this happens and how to fix it (server-side CORS policy vs client-side fetch).”

- “My Tailwind buttons look greyed out. Show me how to style them with hover, active, and focus states so they look clickable.”

**2. Reddit API & Auth Token Retrieval**

- “Explain step-by-step how to create a Reddit script app, get a client ID and secret, and use them to generate an OAuth2 access token with the password grant type. Show example curl commands.”

- “Why am I getting invalid_grant when requesting a Reddit access token? Walk me through the common causes and fixes.”

- “How can I safely store Reddit API credentials (client id, secret, username, password) in environment variables for my .NET backend?”

**3. LINQ Queries & Code Optimization**

- "I have a list of memes with fields (Title, Upvotes, CreatedAt). Show me a LINQ query in C# to get the top 20 memes from the last 24 hours ordered by Upvotes.”

- “Explain how to optimize EF Core LINQ queries so they don’t fetch unnecessary data. Show examples of Select, AsNoTracking, and filtering before projecting.”

- “What’s the best way to profile slow LINQ queries in .NET and rewrite them for performance?”

**4. Why I Picked n8n for Automation**

- “I want to automatically fetch data from my API every hour, generate a PDF, and send it to Telegram. What workflow automation tool should I use? Compare Zapier, Airflow, and n8n.”

- “Show me how to build an n8n workflow with Cron → HTTP Request → HTML to PDF → Telegram Bot. Include JSON structure for the workflow.”

- “How can I test my local API with a remote n8n instance? Explain tunneling options (ngrok, localtunnel) and how to configure n8n HTTP nodes.”

---

## 🔍 Production Deployment

### 🚀 Frontend: Production Deployment with AWS Amplify (Next.js + Tailwind)

This section describes how the **UI** is deployed to **AWS Amplify Hosting** using a connected GitHub repo. It supports both **SPA** and **Next.js (SSR/CSR)** builds.

---

### ✅ Prerequisites

- GitHub repo with our frontend (Next.js)
- Environment variable: `NEXT_PUBLIC_API_BASE=https://memecrawler.duckdns.org`
- Backend already reachable over HTTPS (for CORS)

---

#### 1) Connect Repository

1. Open **AWS Amplify → Host a web app → GitHub**.
2. Select the repo/branch (e.g., `main`).
3. Framework auto-detected as **Next.js**.

---

#### 2) Build Settings

Amplify usually generates this automatically:

```yaml
# amplify.yml
version: 1
frontend:
  phases:
    preBuild:
      commands:
        - npm ci
    build:
      commands:
        - npm run build
  artifacts:
    baseDirectory: .next
    files:
      - "**/*"
  cache:
    paths:
      - node_modules/**/*
```

#### 3) Environment Variables

**_In Amplify → App settings → Environment variables, I added:_**

- `NEXT_PUBLIC_API_BASE = https://memecrawler.duckdns.org`

- Note: Avoid server-side fetches at build time for external APIs (can cause fetch failed / ENOTFOUND). Use client-side fetch or runtime SSR with try/catch.

#### 4. Custom Domain & HTTPS

- 1. Amplify → Domain management → Add domain. --> This is optional. I left this blank as I don't have an extra domain registered.

- 2. Select branch mapping → Amplify provisions ACM cert + Route 53 records automatically.

- 3. Wait for propagation; test website.

#### 5. CORS Considerations

- Backend must allow our UI origin: `https://main.duj0n3y16kfpp.amplifyapp.com/`

- CORS policy (server): allow GET, POST, necessary headers, credentials.

#### 6. Troubleshooting (WIP)

- CORS errors: Verify API’s allowed origins and HTTPS (mismatched protocols cause mixed content).

- Next.js build fails (fetch): Move external fetch to client-side or a server route that runs at request-time.

- 404 on deep links (SPA): Enable rewrites to index.html.

---

### 🖥️ Backend: Production Deployment on AWS EC2 (Docker Compose + NGINX + TLS)

This section explains deploying the .NET 8 API on EC2 (Amazon Linux 2023) behind NGINX with Let’s Encrypt TLS. It pairs with the Amplify-hosted UI.

✅ Prerequisites

- Domain/DNS control (Route 53 or registrar)

- Repo containing src/Memes.Api and /deploy/docker-compose.yml

- Environment file for API (src/Memes.Api/.env):

```bash
ASPNETCORE_URLS=http://0.0.0.0:8080
Telegram__BotToken=...
Telegram__ChatId=...
Reddit__UserAgent=MemeCrawler/1.0 (by u:Ademi47)
Reddit__ClientId=...
Reddit__ClientSecret=...
Reddit__Username=...
Reddit__Password=...
```

- Security Group allowing 80/443 (and temporary 8080/5678 if needed for testing)

#### 1) Launch EC2

AMI: Amazon Linux 2023

Instance: t3.small or above

Assign Elastic IP (optional)

SSH into instance as ec2-user

#### 2) Install Docker & Compose Plugin

```bash
sudo dnf update -y
sudo dnf install -y docker docker-compose-plugin git
sudo systemctl enable --now docker
sudo usermod -aG docker ec2-user
```

#### 3) Clone & Run Stack

```bash
git clone https://github.com/ademi47/MemeCrawler.git
cd <repo>/deploy
docker compose up -d --build
docker compose ps
```

Typical `docker-compose.yml` includes:

- api (builds from src/Memes.Api)

- postgres

#### 4) NGINX Reverse Proxy

```bash
sudo dnf install -y nginx
sudo systemctl enable --now nginx
```

Create `/etc/nginx/conf.d/api.conf`:

```bash
server {
  listen 80;
  server_name memecrawler.duckdns.org;

  location / {
    proxy_pass http://127.0.0.1:8080/;
    proxy_http_version 1.1;
    proxy_set_header Host $host;
    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    proxy_set_header X-Forwarded-Proto $scheme;
  }
}
```

Test & reload:

```bash
sudo nginx -t && sudo systemctl reload nginx
```

Point DNS: https://memecrawler.duckdns.org → <EC2 My public IP>

#### 5) TLS (Used Let’s Encrypt via Certbot)

```bash
sudo dnf install -y certbot python3-certbot-nginx
sudo certbot --nginx -d https://memecrawler.duckdns.org
```

auto-renew is installed by default; verify with:

```bash
systemctl status certbot-renew.timer
```

#### 6) Harden CORS for Production

In API CORS policy, whitelist only our UI origin:

Allow methods/headers actually used:

GET, POST

Content-Type, Authorization (if needed)

#### 7) Optional: Systemd for Auto-Start

```bash
sudo tee /etc/systemd/system/memes.service <<'EOF'
[Unit]
Description=Meme Stack
After=docker.service
Requires=docker.service

[Service]
Type=oneshot
WorkingDirectory=/home/ec2-user/<repo>/deploy
ExecStart=/usr/bin/docker compose up -d
ExecStop=/usr/bin/docker compose down
RemainAfterExit=yes

[Install]
WantedBy=multi-user.target
EOF
sudo systemctl enable memes && sudo systemctl start memes
```

#### 8) Logs & Monitoring

```bash
docker compose logs -f api
journalctl -u nginx -f
```

#### Summary

- Frontend: AWS Amplify → fast CI/CD, custom domain, HTTPS, env vars.

- Backend: EC2 + Docker Compose → NGINX reverse proxy → TLS via Certbot.

- Integration: CORS locked to UI domain, NEXT_PUBLIC_API_BASE points to HTTPS API.

---

## 🔍 Live Demo

### Video Guide

Please Click below link to watch the project Demo.

```bash
https://www.youtube.com/watch?v=upHuJ_AFY1s
```

**Note:**

```bash
This video is unlisted hence it's not searchable. Kindly do not share the link.
The project has been successfully deployed to the production environment for functionality testing.
However, I encountered some CORS issues on the frontend, which are preventing it from running fully at the moment.
I’m actively working on resolving these and will have the frontend up and running as soon as possible.
```

### Production Test using API calls via Browser (or Postman)

**Memes Route**

```bash
1. Open: https://memecrawler.duckdns.org/memes/top-24h, Alteratively you may go to -> https://memecrawler.duckdns.org/swagger/index.html. then trigger the API via swagger.

Note: You’ll see a JSON array of meme posts.

Sample:

{
"id":"1n3fwwb",
"title":"Monster.com",
"author":"Own_Touch9354",
"permalink":"https://www.reddit.com/r/memes/comments/1n3fwwb/monstercom/",
"contentUrl":"https://i.redd.it/wuaox6tb90mf1.jpeg",
"upvotes":19788,"numComments":72,
"createdUtc":"2025-08-29T18:53:35+00:00",
"thumbnail":"https://b.thumbs.redditmedia.com/13uoRnFJFR9ASqdkVBIX67PpI4ycBv8-t_HB3tcpyEg.jpg"
}

3. In the UI, when /memes route refreshes, this calls the above API and shows the Memes posts list (with images, vote counts and post links).

4. There are two views in the UI. /memes and /reports
```

**Reports Route**

```bash
1. Open: https://memecrawler.duckdns.org/reports/top-24h, Alteratively you may go to -> https://memecrawler.duckdns.org/swagger/index.html. then trigger the API via swagger.

2. Click Send Report on the Reports page.

3. The UI fires: POST /reports/send-telegram-now.

4. The API fetches current top-24h posts and formats a summary (or builds PDF).

5. Telegram Bot sends the message/document into our configured chat.

6. You see a success toast in the UI (and the new report in PDF format in Telegram).
```

_Screenshots_

---

_APIs_
![plot](/Screenshots/APIs/mainAPIs.jpg)
![plot](/Screenshots/APIs/TG_send.jpg)

_UI memes route:_
![plot](/Screenshots/Front%20End/memes_route.jpg)

_UI reports route:_
![plot](/Screenshots/Front%20End/reports_route.jpg)

_n8n dashboard:_
![plot](/Screenshots/n8%20flow.jpg)

---

## 💡 Alternative Use Cases for MemeCrawler

Although this project showcases memes, the same pipeline (crawler → database → reports → automation) can be applied to many other domains.

---

### 📰 1. News Aggregation Reports

- Crawl news websites or RSS feeds.
- Generate **daily/weekly reports** of trending news.
- Deliver via Telegram, Slack, Discord or email. (n8n Automation flow can be used across multiple platforms)

---

### 📊 2. Social Media & Influencer Marketing

- The Same program can be extended to fetch records from platforms (Provided that we can use their public APIs)
- Track trending **hashtags, mentions, or influencer posts from tiktok, youtube and other platforms**.
- Measure engagement (likes, shares, comments, upvotes).
- Provide **automated performance reports** to brands and agencies.

---

### 👨‍💻 3. Team Productivity Snapshots

- Pull activity data from tools like **Jira, GitHub, or Trello**.
- Create **daily progress reports** for teams.
- Automate distribution via chat apps or internal portals.

---

### 📢 4. Content Creation & Media Marketing

- Monitor what’s trending across **Reddit, Twitter/X, Instagram, or TikTok**.
- Use insights to plan **content calendars** or viral campaigns.
- Automatically generate **creative briefs** or marketing reports for teams.

---

### 📊 5. Social Media Analytics

- Monitor hashtags or subreddit communities.
- Generate **engagement reports** (likes, shares, comments).
- Provide clients with automated **weekly performance dashboards**.

---

## 🔄 Continuous Improvements & Maintenance

This project is actively evolving. The following areas are under continuous improvement to ensure better stability, security, and usability:

---

### 🐞 Bug Fixes

- Fixing API response inconsistencies.
- Handling edge cases in Reddit API integration.
- Improving error handling for failed Telegram sends.

---

### 🌐 CORS & Frontend Integration

- Refining **CORS policies** to avoid frontend errors.
- Adding stricter **origin/domain whitelists** in production.
- Testing FE <-> BE communication across environments (local, staging, production).

---

### 🧹 Codebase Cleanup

- Removing unused parameters and legacy test code.
- Refactoring **LINQ queries** for performance and readability.
- Organizing project structure for better maintainability.
- Enhancing logs and monitoring for background jobs.

---

### 🚀 Future Enhancements

- Add better **UI/UX polish** (search, filters, dark mode). Make it more nicer.
- Improve **database schema** for analytics and reports.
- Expand automation with **n8n** (multi-channel delivery: Slack, Email).
- Strengthen deployment scripts (CI/CD pipelines, infra-as-code).
- Add more technical guide in the video and instructions to ReadMe with the screenshots.

---
