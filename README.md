# 🔥 LeetGhost - LeetCode Streak Keeper

LeetGhost is an automated service that helps you maintain your LeetCode daily submission streak. If you forget to submit a solution, LeetGhost will automatically submit one of your previously solved problems to keep your streak alive.

## Features

- 🤖 **Telegram Bot Control** - Manage everything via chat commands
- 🔄 **Scheduled Monitoring** - Periodically checks your LeetCode submission status
- 🚀 **Auto-Submit** - Automatically submits a stored solution when the deadline approaches
- 📊 **Smart Selection** - Prefers solutions that haven't been recently used
- 🔔 **Push Notifications** - Get notified when your streak is saved
- 💾 **SQLite Database** - Lightweight, single-file data storage
- ☁️ **Deploy Ready** - Single container, easy to deploy

## Quick Start

### 1. Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- A Telegram account
- A LeetCode account with some solved problems

### 2. Create a Telegram Bot

1. Open Telegram and start a chat with [@BotFather](https://t.me/botfather)
2. Send `/newbot` and follow the prompts
3. Copy the bot token (looks like `123456:ABC-DEF1234...`)

### 3. Configure the Application

Edit `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=leetghost.db"
  },
  "TelegramBot": {
    "BotToken": "YOUR_BOT_TOKEN_FROM_BOTFATHER"
  },
  "Schedule": {
    "CheckCronExpression": "*/30 * * * *",
    "AutomationStartHour": 23,
    "AutomationStartMinute": 0,
    "TimeZone": "UTC"
  }
}
```

Or use User Secrets (recommended):

```powershell
cd src/LeetGhost
dotnet user-secrets set "TelegramBot:BotToken" "your-bot-token"
```

### 4. Run the Service

```powershell
cd src/LeetGhost
dotnet run
```

### 5. Connect Your LeetCode Account

1. Start a chat with your bot on Telegram
2. Send `/bind`
3. Follow the instructions to enter your LeetCode session cookies
4. Add solutions via the API or Swagger UI

## Telegram Bot Commands

| Command | Description |
|---------|-------------|
| `/start`, `/help` | Show available commands |
| `/bind` | Connect your LeetCode account |
| `/status` | Check your current streak status |
| `/submit` | Manually trigger a submission |
| `/solutions` | List your stored solutions |
| `/toggle [id]` | Enable/disable a solution |
| `/stats` | View submission statistics |
| `/pause` | Pause auto-submissions |
| `/resume` | Resume auto-submissions |

## Configuration Reference

### Schedule Settings

| Setting | Description | Default |
|---------|-------------|---------|
| `CheckCronExpression` | Cron expression for checks | `*/30 * * * *` |
| `AutomationStartHour` | Hour to start automation (0-23) | `23` |
| `AutomationStartMinute` | Minute to start automation | `0` |
| `TimeZone` | Default timezone | `UTC` |

### Telegram Bot Settings

| Setting | Description |
|---------|-------------|
| `BotToken` | Bot token from @BotFather |
| `AllowedChatIds` | Optional: Restrict to specific chat IDs |

## Adding Solutions

Solutions can be added via the REST API:

```bash
POST /api/solutions
{
  "userId": 1,
  "problemSlug": "two-sum",
  "problemTitle": "Two Sum",
  "language": "python3",
  "code": "class Solution:\n    def twoSum(self, nums, target):\n        ..."
}
```

Or use Swagger UI at `http://localhost:5000` when running in development mode.

## Deployment

### Docker

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY publish/ .
ENV ASPNETCORE_URLS=http://+:5000
ENTRYPOINT ["dotnet", "LeetGhost.dll"]
```

Build and run:
```bash
dotnet publish -c Release -o publish
docker build -t leetghost .
docker run -d -p 5000:5000 \
  -e TelegramBot__BotToken=your-token \
  -v leetghost-data:/app \
  leetghost
```

## License

MIT
| `TimeZone` | Timezone for scheduling | `UTC` |
| `MaxRetryAttempts` | Max submission retries | `3` |
| `RetryDelaySeconds` | Delay between retries | `10` |

### Common Timezones

| Region | TimeZone ID |
|--------|-------------|
| US Eastern | `Eastern Standard Time` |
| US Pacific | `Pacific Standard Time` |
| UK | `GMT Standard Time` |
| Central Europe | `Central European Standard Time` |
| Japan | `Tokyo Standard Time` |
| India | `India Standard Time` |

### Notification Settings

#### Console (Default)
```json
{
  "Notifications": {
    "EnableConsole": true
  }
}
```

#### Telegram
```json
{
  "Notifications": {
    "Telegram": {
      "Enabled": true,
      "BotToken": "123456789:ABCdefGHIjklMNOpqrsTUVwxyz",
      "ChatId": "your-chat-id"
    }
  }
}
```

To get Telegram credentials:
1. Create a bot via [@BotFather](https://t.me/botfather)
2. Get your chat ID by messaging [@userinfobot](https://t.me/userinfobot)

#### Discord
```json
{
  "Notifications": {
    "Discord": {
      "Enabled": true,
      "WebhookUrl": "https://discord.com/api/webhooks/..."
    }
  }
}
```

#### Email
```json
{
  "Notifications": {
    "Email": {
      "Enabled": true,
      "SmtpHost": "smtp.gmail.com",
      "SmtpPort": 587,
      "Username": "your-email@gmail.com",
      "Password": "your-app-password",
      "FromAddress": "your-email@gmail.com",
      "ToAddress": "your-email@gmail.com",
      "UseSsl": true
    }
  }
}
```

## Solution Format

Each solution in `solutions.json` has the following fields:

| Field | Type | Description |
|-------|------|-------------|
| `id` | string | Unique identifier |
| `problemSlug` | string | LeetCode problem URL slug (e.g., "two-sum") |
| `problemTitle` | string | Human-readable title |
| `language` | string | Programming language |
| `code` | string | Your solution code |
| `isEnabled` | bool | Whether to include in auto-submission |
| `notes` | string | Optional notes |

### Supported Languages

- `csharp` / `c#`
- `python` / `python3`
- `java`
- `javascript` / `js`
- `typescript` / `ts`
- `cpp` / `c++`
- `c`
- `go` / `golang`
- `rust`
- `kotlin`
- `swift`
- `ruby`
- `scala`
- `php`

## Running as a Service

### Windows (Task Scheduler)

1. Build the project: `dotnet publish -c Release`
2. Create a Task Scheduler task to run the executable

### Linux (systemd)

Create `/etc/systemd/system/leetghost.service`:

```ini
[Unit]
Description=LeetGhost - LeetCode Streak Keeper
After=network.target

[Service]
Type=simple
User=your-user
WorkingDirectory=/path/to/LeetGhost
ExecStart=/usr/bin/dotnet /path/to/LeetGhost.dll
Restart=always

[Install]
WantedBy=multi-user.target
```

Then:
```bash
sudo systemctl enable leetghost
sudo systemctl start leetghost
```

### Docker

Create a `Dockerfile`:

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "LeetGhost.dll"]
```

Build and run:
```bash
docker build -t leetghost .
docker run -d --name leetghost \
  -v /path/to/appsettings.json:/app/appsettings.json \
  -v /path/to/solutions:/app/solutions \
  leetghost
```

## How It Works

1. **Scheduled Check** - The service runs on a cron schedule (default: every 30 minutes)
2. **Status Check** - It queries LeetCode's GraphQL API to check if you've submitted today
3. **Automation Trigger** - If no submission exists and the automation threshold time is reached (default: 11 PM), it starts the automation process
4. **Solution Selection** - Picks a random enabled solution from your repository, preferring less recently used ones
5. **Submission** - Submits the solution to LeetCode and waits for the result
6. **Retry Logic** - If the submission fails, it tries another solution (up to max retries)
7. **Notification** - Sends notifications about the status through enabled channels

## Security Notes

- **Never commit your credentials** to version control
- Use User Secrets or environment variables for sensitive configuration
- LeetCode session cookies expire periodically - you'll need to refresh them
- Consider running the service on a secure private machine

## Troubleshooting

### Session Expired
If you see authentication errors, your LeetCode session has likely expired. Get new credentials from your browser.

### Timezone Issues
Make sure your `TimeZone` setting matches a valid Windows/IANA timezone identifier. Check the table above for common values.

### Submission Failures
- Ensure your stored solutions are actually correct and accepted
- Check that the `language` matches LeetCode's expected format
- Verify the `problemSlug` matches the URL path on LeetCode

## License

MIT License - feel free to use and modify as needed.

---

**Disclaimer**: This tool is for personal use to maintain your coding habits. Use responsibly and in accordance with LeetCode's terms of service.
