# BigsamGL freight-forwarding and consolidation operating model

## Business position

Bigsam Global Logistics is modeled as a freight forwarder, consolidator, warehouse and fulfilment operator. It serves individual senders, families, SMEs, online retailers, importers, larger business accounts, and smaller partner logistics agencies. BigsamGL receives or collects cargo, stores and prepares it, combines compatible consignments into scheduled dispatches, books capacity with external airlines, ocean carriers and delivery providers, coordinates clearance, and keeps clients informed.

BigsamGL is not modeled as the owner or operator of the main ocean vessel or airline. A carrier's vessel, flight, voyage, container, master AWB, bill of lading and carrier tracking events are external transport facts arranged and monitored by BigsamGL.

This matches the public service flow: quote request; collection or drop-off; packaging, weighing and processing; customer updates; and delivery. Current public services include weekly air and sea freight, pickup, storage, customs-clearance support, door-to-door delivery, UK same/next-day delivery, B2B fulfilment and restocking, container sharing, and China sourcing support. See [BigsamGL services](https://bigsamgl.com/services/), [B2B logistics](https://bigsamgl.com/b2b-logistics/), and [company overview](https://bigsamgl.com/about/).

## Operational hierarchy

1. **Client account** — an `Individual`, `Business`, or `PartnerAgency`. A partner agency is a client of BigsamGL, not the main-line carrier.
2. **Booking/consignment** — the customer's instruction, sender/receiver details, route, collection or hub-pickup choice, cargo description and documents.
3. **Cargo item/pieces** — the physical goods BigsamGL receives, labels, weighs, stores, reconciles and tracks.
4. **Consolidation batch** — BigsamGL's internal grouping of compatible cargo for one planned dispatch. Types are `Groupage`, `DedicatedClientLoad`, `PartnerAgencyLoad`, and `MixedDirectAndAgency`.
5. **External transport movement** — the booked airline, ocean carrier or partner-forwarder leg, including flight/vessel, schedule, gateways and carrier-issued master reference.
6. **Container** — an external physical container used on a sea movement. It may contain cargo from several BigsamGL clients.
7. **Milestones** — carrier updates and audited BigsamGL operational updates normalized into the client-facing tracking timeline.

## API boundaries

- `/api/clients` manages direct and partner-agency client accounts and supports `accountType` filtering.
- `/api/operations/consolidation-batches` manages BigsamGL's internal groupage/dispatch batches. The historical `/api/cargo/batches` route remains as a compatibility alias.
- `/api/operations/shipment-movements` manages external main-carrier or partner-forwarder legs.
- `/api/operations/shipment-allocations` records when cargo is assigned or reassigned to a movement/container after invoicing.
- `/api/operations/containers` stores carrier container references and normalized MSC tracking events.
- `/api/operations/cargo/{cargoId}/milestones` stores auditable BigsamGL updates; carrier adapters use the same tracking pipeline.

An air or sea movement cannot be classified as `BigsamLocalDelivery`; it must identify a `ThirdPartyCarrier` or `PartnerForwarder`. Local collection and delivery remain in the booking/pickup/delivery workflow. Dispatch never invents an AWB or bill of lading: staff must record the external provider's master transport reference first.

## Dashboard language

Use **Consolidation Batch**, **External Carrier Movement**, **Carrier Reference**, and **BigsamGL Tracking Update**. Do not use language such as “our vessel”, “our airline fleet”, or “BigsamGL sailing” unless the company later becomes the licensed operator of that asset.

The operations board should follow this sequence:

`Quote → Booking → Collect/Drop-off → Receive & Weigh → Warehouse → Consolidate → Book External Carrier → Dispatch → Carrier/Manual Updates → Clearance → Hub Pickup/Delivery`

Display the BigsamGL internal batch code separately from the external master AWB, bill of lading, carrier booking reference, flight, vessel, voyage and container number. Client pages should show only the tracking information relevant to that client's cargo, never the other clients sharing a batch or container.

## Scaling rules

- Do not add fleet maintenance, vessel crew, aircraft rotation or ship-capacity planning to the core product unless the operating model changes.
- Scale around warehouse throughput, consolidation capacity, partner capacity bookings, carrier performance, dispatch frequency, exception handling, client communication, route profitability and branch controls.
- Keep carrier adapters replaceable. MSC, airline, courier and future partner feeds should normalize into the same movement/container/milestone contracts.
- Report direct-customer and partner-agency revenue separately while preserving a consolidated company view.
- Preserve allocation and tracking history when a carrier, container, flight or voyage changes.
