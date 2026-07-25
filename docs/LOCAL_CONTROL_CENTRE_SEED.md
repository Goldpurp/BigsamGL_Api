# BiGSAM local control-centre seed

Last verified: 24 July 2026

## Purpose and safety

The local seed provides one coherent BiGSAM company scenario for dashboard,
API, PDF, Microsoft Office, and workflow testing. It is enabled only when
`Seed:LocalOperationalData=true`. Production startup validation rejects this
setting, so these credentials and records cannot be created by a production
process.

Seeding is transactional and idempotent:

- the complete operational scenario is committed together;
- a failure rolls the scenario back instead of leaving partial records;
- the marker customer is `amina.bello@bigsamgl.test`;
- startup skips the scenario when that marker already exists.

## Staff test accounts

All local staff accounts use:

`BiGSAM-Local-Staff-2026!`

| Hub | Role | Email | Employee number |
| --- | --- | --- | --- |
| Manchester, UK | Super Admin | `superadmin.uk@bigsamgl.test` | `BGL-STF-101` |
| Lagos, Nigeria | Operations Manager | `operator.lagos@bigsamgl.test` | `BGL-STF-102` |
| Guangzhou, China | Operations Manager | `operator.guangzhou@bigsamgl.test` | `BGL-STF-103` |
| Manchester, UK | Sales Manager | `sales.uk@bigsamgl.test` | `BGL-STF-104` |
| Lagos, Nigeria | Finance Manager | `finance.lagos@bigsamgl.test` | `BGL-STF-105` |
| Guangzhou, China | Warehouse Officer | `warehouse.china@bigsamgl.test` | `BGL-STF-106` |
| Lagos, Nigeria | Delivery Coordinator | `delivery.lagos@bigsamgl.test` | `BGL-STF-107` |
| Manchester, UK | Administrator | `admin.uk@bigsamgl.test` | `BGL-STF-108` |

The UK Super Admin has access to all three branches. Each operator has a
primary membership for their own hub.

## Customer login

The marker customer is:

- email: `amina.bello@bigsamgl.test`
- password: `BiGSAM-Local-Customer-2026!` in the supplied local workspace

Do not publish either local password or copy it into production configuration.

## Connected seed coverage

The scenario contains at least five useful records for each normal company
area. Three branch/station records are intentional because BiGSAM currently
operates exactly three hubs.

| Area | Seed coverage |
| --- | --- |
| Company | 3 hubs, branch controls, cross-branch Super Admin membership |
| Staff | 8 staff profiles, 5 attendance sessions, 10 attendance records, 5 leave requests |
| Clients | 8 unique customers, addresses, notification preferences, loyalty accounts |
| Contacts | 8 clients, 5 suppliers, 5 directors, 5 shareholders |
| Sales | 8 leads, 8 quotations, 8 invoices, 5 payments, 5 receipts, 5 credit notes, 5 targets |
| Operations | 8 bookings, 8 one-invoice/one-shipment records, 8 manifests, entries and charge lines |
| Tracking | 8 provider connections and 16 provider/BiGSAM tracking events |
| Exceptions | 5 cargo mismatch or misplacement issues |
| Purchases | 5 company purchases, lines, payouts, payment vouchers and expenses |
| Finance | 10 in/out financial transactions, journals, lines and 5 bank accounts |
| Inventory | 8 items, 8 opening transactions and hub locations |
| Pricing | Published route catalogue plus 8 additional service/item prices |
| Schedule | 8 appointments and 5 pickup schedules |
| Communications | 5 broadcasts and 25 recipients |
| Governance | 5 staff activities, 5 location pings, explicit audit entries plus automatic audit history |

The records share customers, immutable invoice numbers, shipment IDs,
manifest entries, cargo, branch currency, payments, receipts, journals, and
tracking connections. They are not isolated placeholders.

## Document routes

Invoice, receipt, and credit-note documents support:

- `pdf`
- `xlsx`
- `docx`
- `csv`

Examples:

```text
GET /api/documents/invoices/{invoiceId}?format=pdf
GET /api/documents/receipts/{receiptId}?format=xlsx
GET /api/documents/credit-notes/{creditNoteId}?format=docx
```

Finance-number routes remain available:

```text
GET /api/finance/invoices/{invoiceNumber}/document?format=pdf
GET /api/finance/receipts/{receiptNumber}/document?format=xlsx
```

PDF output is paginated and line-wrapped. Excel workbooks contain typed number
and date cells, styled headers, frozen headings, filters, and print setup.

## Local reset

The normal local database is PostgreSQL only. To intentionally rebuild it:

```bash
dotnet ef database drop --project Infrastructure/Infrastructure.csproj \
  --startup-project Api/Api.csproj --force
dotnet ef database update --project Infrastructure/Infrastructure.csproj \
  --startup-project Api/Api.csproj
dotnet run --project Api/Api.csproj
```

Run these commands only against the local connection string. Never enable
`LocalSeed:Enabled` in a production environment.
