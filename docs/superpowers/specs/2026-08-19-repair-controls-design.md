# Repair Controls Design

## Goal

Restore automatic building repair and make manual repair mode easy to understand and exit on mouse and touch input.

## Behavior

- The player actor owns `AutoRepairManager`, so the existing button order has a resolver.
- Auto repair scans damaged owned buildings and starts one eligible repair per scan using the existing manager behavior.
- Manual repair mode uses the existing gold wrench cursor and blocked-wrench variant.
- Clicking a valid damaged repair target issues repair and keeps repair mode active for consecutive repairs.
- Clicking another selectable actor when no repair order can be issued cancels repair mode and selects that actor in the same click.
- Clicking empty terrain cancels repair mode and follows normal selection clearing behavior.
- Right click and pressing the repair button again retain their current cancellation behavior.

## Scope

The change is limited to the RA2 player trait registration and `RepairOrderGenerator`. Repair cost, rate, eligibility, AI behavior, and other order-generator modes are unchanged.

## Tests

Repository policy tests verify manager registration, cancel-and-select fallback wiring, and wrench cursor names. The RA2 mod and iOS Release application must build successfully.
