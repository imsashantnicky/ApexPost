# Development JSON data

Set `ApexPost:JsonData:DataDirectory` to a development-only directory and set `ApexPost:JsonSeed:Enabled` to `true` to create deterministic demonstration branches, staff records, catalogue services, and prices. Seed data contains no accounts, passwords, card data, or provider credentials.

Initialization seeds only when the configured directory contains no JSON files. With existing valid JSON it does nothing; with seeding disabled it does nothing; with malformed JSON it stops and reports an error without changing files. Never enable seeding against a production data directory.

To reset disposable local data, first confirm the configured directory is a development-only path, then delete only that explicitly configured directory while the app is stopped. Do not use a broad workspace or user-data path as the reset target. Restart with seeding enabled to recreate the demonstration data.
