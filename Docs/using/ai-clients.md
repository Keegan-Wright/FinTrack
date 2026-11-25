# Using FinTrack from AI clients

Where FinTrack runs with AI, an AI client you already use can answer questions about your money, e.g. what you spent on
takeaways this year. Clients that support MCP (the Model Context Protocol) connect to FinTrack and read your data
through its tools.

The tools **only read**. A client can never change anything, and it only sees your own data. It sends what it reads to
whichever model the client uses, so only connect clients you trust with your money data.

## Setting it up

1. On **Your profile**, turn on **Use AI**. The section then shows FinTrack's MCP address and the settings to copy.
2. Open **API tokens**, press **New token**, and choose **AI clients (MCP)** under **For**. Copy the token.
3. Add the settings to your client, with your token in place of `<your token>`:

```json
{
  "mcpServers": {
    "fintrack": {
      "type": "http",
      "url": "https://fintrack.example.com/mcp",
      "headers": { "Authorization": "Bearer <your token>" }
    }
  }
}
```

Clients name these settings differently, but most take an address and a header.

A token for AI clients only works through MCP, and an API token doesn't work there. It also only works while AI is
turned on for you. Turn it off on **Your profile** and your AI clients are refused until you turn it back on. Expiry, revoking and the emails
when one's made work the same as for [API tokens](api-tokens.md).

## What it can read

Your accounts and tags, transactions and searching them, spending totals, breakdowns and comparisons between periods,
budgets, bills and upcoming payments, debts, savings goals and net worth. Clients are also told today's date and the
currency your amounts are in, so questions like "this week" use the right days and money is shown in your currency.

Answers link to the FinTrack page each figure comes from, so you can check it.
