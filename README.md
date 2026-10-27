# Your Order We Deliver - Delivery Management System

Assignment 3 (Design Implementation) for **SWE30003 - Software Architectures and Design**.
A working prototype of the *Your Order We Deliver* (YOWD) delivery management system for
Swinsoft Consulting, implementing the requirements from Assignment 1 (SRS) and the object
design from Assignment 2.

The application lets customers request deliveries and track them, office staff quote and
allocate work, delivery staff manage pickups and drop-offs, and managers review statistics and
maintain staff records.

## Tech stack

- **.NET 8** / C# 12 (nullable enabled, warnings as errors, latest-recommended analyzers)
- **ASP.NET Core Razor Pages** for the web front end
- **Cookie authentication** with role-based authorisation
- **xUnit** for the test suite
- **JSON file** persistence (no external database required)
- Bootstrap 5 + jQuery for styling and client-side validation

## Architecture

The solution (`src/YourOrderWeDeliver.sln`) is split into three projects so the business logic
has no dependency on the web framework:

| Project | Responsibility |
|---------|----------------|
| `YourOrderWeDeliver.Core` | Pure business logic: domain model, coordination, pricing and persistence contracts. No web dependencies. |
| `YourOrderWeDeliver.Web` | ASP.NET Core Razor Pages UI, authentication/authorisation, configuration and JSON data store. |
| `YourOrderWeDeliver.Tests` | xUnit tests covering the request-to-delivery lifecycle, quoting, roster and statistics/persistence. |

### Key design patterns

- **Singleton** - `DeliveryManagementSystem` is the single top-level coordinator. It runs the
  start-up sequence, owns the one `DeliveryRegister` and `StaffRoster`, and saves state.
- **Observer** - `StaffRoster` observes every `DeliveryStaffAvailability`; when a staff member's
  availability changes it automatically reviews and reassigns affected deliveries.
- **Strategy** - `IPricingPolicy` / `StandardPricingPolicy` lets the quote-costing rules vary
  independently of the quoting workflow.

### Core domain

`Customer`, `DeliveryRequest`, `DeliveryQuote`, `Delivery`, `DeliveryStaff`, `Parcel`, `Payment`,
`Address`, `ServiceArea`, `DeliveryWindow`, and `DeliveryStatistics`, coordinated by
`DeliveryRegister` (requests and deliveries) and `StaffRoster` (delivery staff).

## Features (mapped to Assignment 1 tasks)

| Task | Feature | Where |
|------|---------|-------|
| T1 | Record a delivery request (customer online **and** office staff on behalf of a customer), with a review step before submitting | `Pages/Request`, `Pages/Office/Requests` |
| T2 | Prepare a quote, including suggesting the earliest available delivery window | `Pages/Office/Requests` |
| T3 | Customer accepts or declines a quote; acceptance creates the operational delivery | `Pages/Quote/Respond` |
| T4 | Allocate a delivery to suitable staff; reassign when availability changes | `Pages/Office/Allocation` |
| T5 | Delivery staff manage their availability | `Pages/Availability` |
| T6/T7 | Pickup (parcels + payment) and completion of a delivery | `Pages/Driver` |
| T8 | Customer delivery tracking by reference + contact | `Pages/Track` |
| T9 | Statistics by day / week / month / year / year-to-date, overall and per staff | `Pages/Manager/Statistics` |
| T10 | Register, update, deactivate and reactivate delivery staff records | `Pages/Manager/Staff` |

## Getting started

### Prerequisites

- [.NET SDK 8.0](https://dotnet.microsoft.com/download/dotnet/8.0) or later

### Run the web application

```bash
cd src
dotnet run --project YourOrderWeDeliver.Web
```

Then open the URL shown in the console (for example `https://localhost:5001`). On first run the
system seeds a few demo delivery staff with two weeks of availability so quoting and allocation
can be demonstrated immediately; no customer requests are seeded, so each scenario starts clean.

### Run the tests

```bash
cd src
dotnet test
```

### Build everything

```bash
cd src
dotnet build
```

## Demo accounts

Credentials are for the prototype only and come from `appsettings.json`.

| Role | Username | Password |
|------|----------|----------|
| Office staff | `office` | `office123` |
| Manager | `manager` | `manager123` |
| Delivery staff | the staff ID (e.g. `DS-001`) | `driver123` |

> Customers do **not** sign in - they submit and track requests directly.

## Configuration

Settings live under the `DeliveryManagementSystem` section of
`src/YourOrderWeDeliver.Web/appsettings.json`:

- `DataFile` - path to the JSON data file (default `App_Data/yowd-data.json`).
- `SeedDemoData` - seed demo staff when the data file is empty.
- `ServiceAreaName` / `ServiceAreaPostcodes` - the area within which pickups and destinations are
  accepted, as comma-separated postcode ranges (e.g. `3000-3211,3335-3341`).
- `DeliveryStaffPassword` - shared password delivery staff use with their staff ID.
- `Accounts` - office staff and manager sign-ins.

## Project structure

```
Assignment_3/
├── src/
│   ├── YourOrderWeDeliver.sln
│   ├── YourOrderWeDeliver.Core/      # Domain, Coordination, Pricing, Persistence
│   ├── YourOrderWeDeliver.Web/       # Razor Pages, Infrastructure, configuration
│   └── YourOrderWeDeliver.Tests/     # xUnit tests
└── README.md
```

## Authors

Group 4 - SWE30003, Swinburne University of Technology.
