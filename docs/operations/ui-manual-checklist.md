# ApexPost MVC UI manual checklist

Run this checklist against a seeded local deployment with test accounts for each listed role. Use synthetic names and references only. Confirm that page totals match the JSON records visible through the application services.

## Shared behavior

- [ ] Sign in as each role and confirm navigation only shows permitted sections.
- [ ] Confirm page headings, responsive navigation, validation summary, field errors, success/error alerts, and empty states at desktop and narrow viewport widths.
- [ ] Submit a state-changing form without its antiforgery token and confirm it is rejected.
- [ ] Submit a valid form and refresh the resulting page; confirm Post-Redirect-Get does not repeat the operation.
- [ ] Sign out and confirm protected pages redirect to sign in.

## Administrator

- [ ] Dashboard shows the signed-in staff name, Administrator role, scoped totals, and next actions.
- [ ] Create, edit, close, and reopen a branch. Confirm a closed branch cannot accept operational writes.
- [ ] Create and edit staff; confirm role and active-branch choices are accurate.
- [ ] Create and edit catalogue services and verify the current service price appears in shipment booking.
- [ ] Review inventory across authorized branches and create an item at an active branch.
- [ ] Create a service basket, add lines, start payment, confirm payment, view receipt, and inspect the resulting transaction.
- [ ] Book a shipment, inspect its label preview, confirm booking, then advance its status.
- [ ] Start and complete identity verification, then issue a money order above and below the configured threshold.
- [ ] Open Reports and compare available branch counts with stored records; confirm unsupported daily sales and shipment zone/service-level reports are labelled unavailable.

## Branch Manager

- [ ] Confirm dashboard totals and lists remain within the assigned branch.
- [ ] Create a transaction and shipment; attempt to submit another branch ID and confirm it is ignored or rejected.
- [ ] Void an eligible transaction with a reason and refund a paid transaction with a reason.
- [ ] Attempt an empty/short void or refund reason and confirm no operation succeeds.
- [ ] Attempt branch and staff administration URLs and confirm access is denied.

## Counter clerk

- [ ] Confirm counter transaction, shipment, and money-order pages are available only for the assigned branch.
- [ ] Add mixed service lines, verify calculated totals, and complete payment. Current model supports service lines only; verify no product/tax total is represented as available.
- [ ] Attempt to void/refund and access inventory, reports, branch, staff, and catalogue administration; confirm access is denied.
- [ ] Book a shipment above the configured weight limit and confirm validation rejects it.
- [ ] Issue a high-value money order without successful identity verification and confirm it is rejected; complete successful verification and retry.

## Inventory clerk

- [ ] Confirm dashboard and inventory pages show the assigned branch only.
- [ ] Receive and issue stock with a reason; confirm updated quantities and low-stock state.
- [ ] Attempt to issue more stock than available and confirm no negative balance is created.
- [ ] Attempt counter, shipment, money-order, report, and administration routes; confirm access is denied.

## Auditor

- [ ] Confirm Reports is available and shows current supported branch-level money-order, verification, and low-stock values.
- [ ] Confirm transaction, inventory mutation, shipment, money-order, branch/staff/catalogue write routes are unavailable or denied.
- [ ] Confirm full identity-document data and payment secrets are absent from all rendered pages.

## Known model limits

- Retail transaction records currently model catalogue services only; product baskets and tax calculation are not represented.
- The shipment model does not contain zone or service-level dimensions, so that shipment volume report cannot be calculated from current data.
- The MVC shipment label is an ephemeral, printable preview; it is not a persisted carrier label.
- Money-order listing and identity-verification listing are limited by the current application use-case APIs; workflow records are opened through their detail or completion links.
