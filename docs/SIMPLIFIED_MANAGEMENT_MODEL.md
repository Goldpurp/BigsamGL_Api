# Simplified BigsamGL management model

## Confirmed product scope

The dashboard is intentionally focused on documents, money, stock and operational schedules. It is not a general CRM or task-management product.

### Sales

Sales contains only:

- quotations;
- sales invoices and credit notes;
- receipts and payment methods;
- freight rate cards;
- additional item and service prices;
- weekly and historical document/revenue performance.

The active API is `/api/sales/summary`, `/api/sales/invoices`, `/api/sales/quotations`, `/api/sales/receipts`, and `/api/sales/shipping-prices`. Quotation and invoice creation remain in their existing focused Operations and Finance APIs so pricing and accounting rules are not duplicated.

Lead enquiries, prospect pipelines, sales next-actions, sales activities and lead targets are retired from the API and dashboard. Their existing PostgreSQL data is retained temporarily as dormant historical data; it is not used by reports.

### Purchases

Every company purchase is separated as:

- `GoodsInput` — physical items entering company inventory;
- `Service` — maintenance, logistics, rent, regulatory and professional services;
- `Asset` — equipment or other capital items;
- `Other` — controlled exceptional purchases.

The purchase view shows supplier/vendor, purchase lines, currency, total, amount paid, outstanding amount, receiving state and payments by Cash, Card, Bank Transfer or another configured method. The dashboard should use `/api/purchasing/summary` for an unambiguous overview.

Approval, audit and accounting posting remain behind the simple interface. Removing them would make money-out transactions difficult to control and reconcile.

### Inventory

The daily inventory screen contains only:

- item name, SKU/barcode and category;
- location;
- quantity on hand;
- stock in;
- stock out;
- transfer when necessary;
- reorder level and low-stock warning.

Use `/api/inventory/basic-stock`, `/api/inventory/stock-in`, `/api/inventory/stock-out`, `/api/inventory/locations`, and `/api/inventory/summary`. The immutable transaction ledger and concurrency controls remain internal because they protect the basic quantities from double posting.

### Contacts

The directory is divided into separate pages and endpoints:

- `/api/contacts/clients`;
- `/api/contacts/suppliers`;
- `/api/contacts/directors`;
- `/api/contacts/shareholders`;
- `/api/contacts/staff`;
- `/api/contacts/service-providers`;
- partner agencies, carriers, government agencies and other contacts through the typed directory.

Directors and shareholders require administrator access. Contact history records communication that already happened; it does not create follow-up tasks or next actions.

### Schedule

The schedule supports:

- pickups and deliveries;
- executive/boss meetings;
- client, supplier and staff meetings;
- maintenance timing;
- warehouse inspections;
- customs appointments;
- training;
- compliance deadlines;
- other controlled appointments.

Appointments support attendees, staff conflict checks, reminders and completion/cancellation records. They do not link to enquiries or sales next-actions.

### Reports

Reports are separated by purpose:

- `/api/reports/sales` — quotations, invoices, credit notes and receipts, always separated by currency;
- `/api/reports/purchases` — goods, services, assets, payment method and outstanding amounts;
- `/api/reports/inventory` — quantities, categories and low-stock items only;
- `/api/reports/contacts` — counts by contact category/status/country;
- `/api/reports/schedule` — appointment category, state and completion rate;
- `/api/reports/executive-summary` — a clearly labelled top-level summary without mixing currencies.

## Suggestions to reconsider before final removal

1. **Keep credit notes.** They are required to correct an issued invoice without editing financial history.
2. **Keep purchase approval internally.** Even if the page is simple, a manager should still control large or unusual money-out transactions.
3. **Keep the inventory transaction ledger internally.** A simple quantity field without movement history is vulnerable to staff mistakes and cannot be reconciled.
4. **Keep contact interaction history, but not follow-up tasks.** Past calls and emails are useful during disputes; future-action reminders are outside the chosen scope.
5. **Keep quotation statuses.** Draft, sent, accepted, rejected and expired states are necessary for document accuracy even without a lead pipeline.
6. **Consider recurring schedules later.** Maintenance and repeated pickups will eventually benefit from daily, weekly or monthly recurrence, but one-off schedules are simpler for the first release.
7. **Retain dormant lead data for one release.** After verifying that no legacy import or dashboard needs it, export it and remove the tables in a later controlled migration if desired.
8. **Keep company contacts separate from client accounts.** A client account owns cargo and invoices; an individual contact represents a person at that company.

## Dashboard navigation

1. Overview
2. Sales
   - Quotations
   - Invoices and credit notes
   - Receipts
   - Shipping and item prices
3. Purchases
   - Goods input
   - Services
   - Assets/other purchases
   - Payments
4. Basic Inventory
   - Stock list
   - Stock in/out
   - Locations
5. Contacts
   - Clients
   - Suppliers
   - Directors
   - Shareholders
   - Staff
   - Service providers and partners
6. Schedule
7. Reports
8. Freight Operations, Workforce, Audit and Settings
