# Week 2 business rule model gaps

The current Domain and Application model does not contain enough persisted information to calculate these assignment reports or transaction totals without inventing data:

- Daily branch sales split between retail products and services: `RetailTransaction` has no creation/business date and `TransactionLineItem` has only a service identifier. There is no product-versus-service classification or tax policy/rate. The existing total is the sum of line totals, so mixed retail/service baskets and tax totals cannot be derived faithfully.
- Shipment volume by zone and service level: `Shipment` stores a service identifier, package, charge, status and originating branch. It has no zone or service-level value. Recipient postal address is not a safe substitute for a configured shipping zone.

The current report service now provides low-stock counts and money-order plus identity-verification outcome counts by branch using persisted branch IDs. High-value money orders retain the ID of the verified record; identity-document data is not retained.

Implementing the unavailable report dimensions requires explicit domain fields, persistence migration/compatibility handling, and seed/catalogue updates. No placeholder classifications, dates, tax rates, zones or service levels are inferred.
