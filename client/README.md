# client

The Angular SPA for Bookable. Everything worth reading is in the repo root:
[`../README.md`](../README.md) for the product and architecture,
[`../CLAUDE.md`](../CLAUDE.md) for the frontend rules (standalone components,
signals, strict TS, Tailwind-first), and [`../docs/contracts.md`](../docs/contracts.md)
for the API contract mirrored in `src/app/core/models.ts`.

```bash
npx ng serve   # http://localhost:4200, proxies /api to the API on :5214
npx ng test
npx ng lint
npx ng build
```
