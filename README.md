# Pexilio

Phase 4 starts with suppliers and purchase drafts on top of company setup, sign-in, categories, products, and the stock ledger. Saving a draft does not change stock. Receiving, sales, returns, invoicing, supplier payments, transfers, a multi-location screen, licensing, and a Windows installer are not included.

Run the commands from the repository root on Arch Linux. French is the default language. The window also offers Arabic, with a right-to-left layout, and English.

## One-time host setup

Confirm the SDK can see ASP.NET:

```bash
dotnet --list-runtimes
```

The list must include `Microsoft.AspNetCore.App` and `Microsoft.NETCore.App` 10.0.x. This repository pins SDK `10.0.113` in `global.json`.

Arch's `dotnet-sdk` package does not include ASP.NET. When `Microsoft.AspNetCore.App` is missing, install the matching packs:

```bash
sudo pacman -S --needed aspnet-runtime aspnet-targeting-pack
```

`scripts/dev/dotnet-env.sh` uses that system SDK when the packs are present. Otherwise it uses a user-local SDK of the same version at `~/.dotnet`.

PostgreSQL 18 is the system service. Start it when it is not already accepting connections:

```bash
sudo systemctl enable --now postgresql
pg_isready -h 127.0.0.1 -p 5432
```

The desktop window uses the system secret service for the refresh token when that service accepts a credential. On this GNOME session that service is `gnome-keyring`.

## Every development session

```bash
./scripts/dev/postgres-up.sh
./scripts/dev/migrate.sh
./scripts/dev/api.sh
```

In a second terminal:

```bash
./scripts/dev/desktop.sh
```

`postgres-up.sh` creates the role `pexilio_dev` and the databases `pexilio_dev` and `pexilio_test` when they are missing. It does not drop or rewrite other databases. It writes `config/dev/local.env`, which is gitignored. If `PEXILIO_JWT_SIGNING_KEY` is missing, the script generates a 32-byte key and stores it in that file. It does not print the key or the database password.

`migrate.sh` applies Entity Framework Core migrations to `pexilio_dev` only. It stops if the database name in `PEXILIO_CONNECTION` is not `pexilio_dev`.

`api.sh` listens on `http://127.0.0.1:5080`. `desktop.sh` runs `npm run tauri dev` and opens a desktop window. It does not build or install a production package.

Health response:

```bash
curl -sS http://127.0.0.1:5080/health
```

A ready stack returns `{"status":"healthy","database":"up"}`.

First-run status, before anyone submits setup:

```bash
curl -sS http://127.0.0.1:5080/api/v1/setup/status
```

That returns `{"complete":false}` until the company and administrator are created. Setup can succeed only once for a database.

## Database

`20261027231938_InitialCreate` is the baseline migration. Its `Up` and `Down` methods are empty because Phase 0 registered no entity types. Leave that migration unchanged after it has been applied.

`20261028143621_CompanyAndAccounts` adds `company`, `tax_rate`, the ASP.NET Identity tables, and `refresh_token` in the `pexilio` schema. The company table allows one row per database. The default tax rate is a `tax_rate` row marked as the default.

`20261028161707_Catalog` adds `category` and `product`. A category may name a parent, and the API rejects a cycle. A product stores its SKU, unit, `numeric(19,4)` unit price, tax-rate reference, active flag, and whether stock is tracked. Prices travel as JSON strings with four decimal places. SKU uniqueness is case-insensitive.

`20261028191901_Stock` adds `stock_location`, `stock_balance`, `stock_movement`, and `stock_idempotency`. It does not insert quantities. The movement table is append-only: database triggers reject update, delete, and truncate, including for the table owner. A normal stock change writes the movement and the balance in one transaction. A deferred trigger rejects a commit whose stored balance does not equal the sum of that product's signed deltas. Quantities are `numeric(19,4)`. The API accepts decimal strings and rejects more than four decimal places instead of rounding them. One default location, `MAIN` / `Principal`, is created on the first stock read or adjustment. A minimum quantity warns when on-hand is strictly below it; equality is reported separately and neither value blocks a stock change.

`20261029140146_PurchasingDrafts` adds `supplier`, `purchase`, `purchase_line`, `purchase_idempotency`, and `purchase_number_seq` in schema `pexilio`. It does not insert suppliers or purchases, and it does not update `stock_movement` or `stock_balance`. Purchase quantities and unit costs are `numeric(19,4)`. The purchase number sequence is not rolled back, so a failed create can leave a gap. This migration is applied to `pexilio_test` only. `pexilio_dev` has not been migrated. Do not run `scripts/dev/migrate.sh` for this migration until applying it to `pexilio_dev` has been approved.

`PEXILIO_CONNECTION` selects `pexilio_dev`. `PEXILIO_TEST_CONNECTION` selects `pexilio_test`. Both use the role `pexilio_dev` on `127.0.0.1:5432`. The API and `migrate.sh` use the development database. Integration tests use the test database. They refuse a connection whose database is not `pexilio_test` or whose host is not loopback, and they read `current_database()` before migrating or deleting rows.

Integration tests apply pending migrations on `pexilio_test`. After that check, they delete application rows in `pexilio_test` so setup can be repeated. Deleting historical movements is possible only in that cleanup, and only after `current_database()` is `pexilio_test`: the test disables the append-only triggers, deletes the rows, and enables the triggers again before committing. They do not drop the database, delete `__EFMigrationsHistory`, or open `pexilio_dev`. The unreachable-database test connects to port 1 and does not open either Pexilio database.

## Authentication

There is no public registration. The first `POST /api/v1/setup` creates the company and the only administrator. A later `POST /api/v1/users` is limited to an administrator and always creates a cashier. Passwords use ASP.NET Core Identity hashing and must be 12 to 128 characters. The API enforces roles; hiding a button in the window is not the authorization check.

An administrator can create and change categories and products. A cashier can read them. Changing a price requires the administrator role.

An administrator can record opening stock, receipts, and adjustments. Each of those requests needs a reason and an `Idempotency-Key`. The same key and the same body return the original movement. The same key with a different body is rejected and does not write a second movement. A cashier can read locations, balances, and movements, and cannot change them. The window hides those forms for a cashier; the API still enforces the rule.

Access tokens last 15 minutes and stay in the window's memory. Refresh tokens last 7 days. The API stores only their SHA-256 hashes. Each refresh replaces the previous token. Presenting a replaced token revokes the user's active refresh tokens. Logout revokes the presented token. The desktop asks the operating system credential store to keep the refresh token. When that store cannot be used, the token stays in a process variable and the window says so. It is not written to `localStorage`.

## Tests

```bash
./scripts/dev/test.sh
```

That runs `dotnet test` and `npm test`.

Frontend tests alone:

```bash
npm test --prefix apps/desktop
```

Backend tests alone, after `postgres-up.sh`:

```bash
set -a
source config/dev/local.env
set +a
dotnet test pexilio.slnx
```

## Security limitations

These choices are for this development phase. They are not a production deployment design.

- `config/dev/local.env` holds the database password and the JWT signing key on disk. It is gitignored. A local environment file is not a production secret store, and this phase does not add one.
- The API is HTTP on `127.0.0.1`. Production HTTPS is not part of this phase.
- The signing key is a development key generated on this machine. Access tokens are HMAC-signed with it. Logs must not contain passwords, access tokens, refresh tokens, or that key.
- On Linux the desktop uses the Secret Service. A Rust test stores, reads, and deletes one probe credential through that service. If the service rejects a call, the refresh token remains in the desktop process until logout or the process exits. The development window was not used to complete setup or sign in against `pexilio_dev`.
- Setup is one-time for the database. It does not create a second administrator.
- Account lockout, token expiry, and authorization were exercised by the automated tests against `pexilio_test`, not by completing setup in `pexilio_dev`.

## Stop

`./scripts/dev/postgres-down.sh` does not stop PostgreSQL. This machine's server is shared. Stop it yourself only when nothing else needs it:

```bash
sudo systemctl stop postgresql
```
