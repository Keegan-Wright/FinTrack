# The API

The browser talks to the server through a JSON API under `/api`. Every endpoint needs a signed in user, acts for that
user only, and expects an `X-FinTrack-Request` header.

The OpenAPI document is at `/openapi/v1.json` once you're signed in, written as OpenAPI 3.2. Its
descriptions come from the doc comments in `FinanceTracker.Contracts`, so keep those up to date.

Routes are in `ApiRoutes`, and the endpoints in `Src/FinanceTracker/Api`.

## Answers

An endpoint returns the service's answer and lets a filter turn it into the response. Add `.ConflictWhenNone()` or
`.NotFoundWhenNone()` to the route. A null or false answer is then a 409 or 404, true is a 204, and anything else is
a 200. The OpenAPI document shows the same.

An answer with several outcomes, such as a change that's refused or made, is a `closed` record with
`[JsonPolymorphic(InferClosedTypePolymorphism = true)]` and a sealed record for each outcome, e.g. `PasskeyChange`.
The JSON carries a `$type` naming the outcome, and the client switches on the record it gets back. The service has to
answer with the base type, which `$type` comes from.
