/**
 * Telegram alert relay for Kasanie.
 *
 * Configure TELEGRAM_BOT_TOKEN, TELEGRAM_CHAT_ID and RELAY_SHARED_SECRET as
 * encrypted Cloudflare Worker secrets. This Worker deliberately exposes only
 * notification delivery; it has no command, storage or administrative API.
 */

function json(value, status = 200) {
  return new Response(JSON.stringify(value), {
    status,
    headers: {
      "content-type": "application/json; charset=utf-8",
      "cache-control": "no-store",
    },
  });
}

function authorized(request, env) {
  return request.headers.get("authorization") === `Bearer ${env.RELAY_SHARED_SECRET}`;
}

export default {
  async fetch(request, env) {
    const url = new URL(request.url);
    if (request.method !== "POST" || url.pathname !== "/v1/notify") {
      return json({ error: "not found" }, 404);
    }
    if (!authorized(request, env)) return json({ error: "forbidden" }, 403);

    let body;
    try {
      body = await request.json();
    } catch {
      return json({ error: "invalid JSON" }, 400);
    }
    const text = String(body?.text ?? "").trim();
    if (!text) return json({ error: "text is required" }, 400);

    const telegram = await fetch(`https://api.telegram.org/bot${env.TELEGRAM_BOT_TOKEN}/sendMessage`, {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({
        chat_id: env.TELEGRAM_CHAT_ID,
        text: text.slice(0, 4000),
      }),
    });
    if (!telegram.ok) return json({ error: "telegram delivery failed" }, 502);
    return json({ ok: true });
  },
};
