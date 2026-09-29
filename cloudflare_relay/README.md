# Cloudflare relay for Kasanie health alerts

The VPS cannot reach Telegram directly in its current network. This Worker
receives a minimal health alert from the VPS and delivers it to Telegram from
Cloudflare. The bot token and destination chat ID stay in Cloudflare Secrets;
the VPS stores only a separate relay secret.

## Deploy

1. In Cloudflare Workers, create a Worker from `worker.js` and deploy it to a
   `workers.dev` address, for example
   `https://kasanie-telegram-alert-relay.<account>.workers.dev`.
2. In **Settings → Variables and Secrets**, create encrypted secrets:
   - `TELEGRAM_BOT_TOKEN` — BotFather token for the Kasanie alert bot;
   - `TELEGRAM_CHAT_ID` — numeric ID of the alert recipient;
   - `RELAY_SHARED_SECRET` — a new random value of at least 32 characters.
3. On the VPS, update `/etc/kasanie-monitor.env` (permissions `600`):

   ```dotenv
   KASANIE_TELEGRAM_RELAY_URL=https://kasanie-telegram-alert-relay.<account>.workers.dev
   KASANIE_TELEGRAM_RELAY_SHARED_SECRET=<RELAY_SHARED_SECRET>
   ```

4. Remove `KASANIE_TELEGRAM_BOT_TOKEN` and `KASANIE_TELEGRAM_CHAT_ID` from
   that VPS file after the relay test succeeds, then run:

   ```bash
   cd /opt/kasanie
   sudo ./scripts/send-monitor-test-alert.sh
   ```

The Worker accepts only `POST /v1/notify` and only with the relay secret. It
does not share the robot Worker secret or expose any command routes.
