# RC1 Pilot Rollout Checklist (30 Days)

This checklist tracks the deployment of Version 1.0 (RC1) to the first pilot supermarket location.

## Day -7 to Day 0: Pre-Flight Preparation

### Infrastructure Verification
- [ ] Local Server hardware meets Minimum Specs (Intel i5, 32GB RAM, SSD).
- [ ] UPS installed with >30 min backup capacity.
- [ ] Docker Compose deployed and running.
- [ ] Local SSL configured (if applicable) for secure tablet access.
- [ ] Database Backup cron job active and tested.
- [ ] Database Restore script tested on recovery DB.
- [ ] Prometheus, Grafana, and Health endpoints verified.

### POS Hardware Verification
- [ ] Barcode Scanners (USB HID) configured and tested (1D/2D).
- [ ] Receipt Printers (ESC/POS or USB) test pages successfully printed via system.
- [ ] Cash Drawer kick-out tested via receipt printer trigger.

### Business Data Seeding
- [ ] Product Master data fully imported from CSV/Legacy System.
- [ ] Opening Stock levels imported and verified.
- [ ] Customer Master data imported.
- [ ] Employee Users created with strict roles (Cashier vs Manager).

---

## Day 1: Go-Live
- [ ] Staff trained on Login, Scanning, Holding Carts, and Payment modes.
- [ ] Shadow Cashiers for the first 4 hours to catch immediate UI/UX bottlenecks.
- [ ] Verify End-of-Day (EOD) Settlement matches physical cash register.

---

## Daily Pilot Review (Day 2 to Day 30)
The System Administrator must produce the **Daily Health Report** covering:

### Performance & Usage Metrics
- [ ] Total Invoices generated.
- [ ] Average Billing Time per transaction.
- [ ] POS Downtime (if any, cross-referenced with Grafana metrics).
- [ ] Failed background jobs (Hangfire Dashboard).

### Business Integrity
- [ ] EOD Cash matches System Cash exactly.
- [ ] Sample 10 physical inventory items against system inventory (Verify Stock Integrity).

### Incident Tracking
- [ ] Log any Critical/High/Medium/Low incidents reported by staff.
- [ ] Ensure any CRITICAL bugs are patched via hotfix deployments.

---

## Day 30: Go-Live Decision
- [ ] Evaluate Pilot Success Metrics.
- [ ] Goal: 0 Critical Bugs active, 100% daily backup success rate, stable operations.
- [ ] **Sign-off for Version 1.0 General Release.**
